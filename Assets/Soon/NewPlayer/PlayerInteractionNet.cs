using Fusion;
using UnityEngine;
using System.Collections;

public class PlayerInteractionNet : NetworkBehaviour
{
    [SerializeField] private float playerReach = 3.5f;
    [SerializeField] private float sphereRadius = 0.5f;

    private const string TAG = "Interactable";

    private Interactable _current;
    public NewPlayerController _player;
    private EquipManagerNet _equip;
    private Camera _cam;
    [SerializeField] private Animator _animator;

    [Header("Cooldown Settings")]
    [SerializeField] private float interactCooldown = 1.0f;
    [SerializeField] private float cleanCooldown = 1.5f;
    [SerializeField] private float spawnCooldown = 2.0f;

    private float _nextInteractTime;
    private float _nextCleanTime;
    private float _nextSpawnTime;

    [Header("Trash (Prototype)")]
    [SerializeField] private GameObject[] trashPrefabs;
    // [변경] 기존 단순 오프셋 대신 레이캐스트 거리 사용
    [SerializeField] private float spawnRayDistance = 5.0f;
    [SerializeField] private LayerMask trashGroundMask = ~0;

    [Header("Game Core")]
    [SerializeField, Range(0f, 1f)] private float gameCoreSpawnChance = 0.2f;
    [SerializeField] private float gameCoreShowSeconds = 2.5f;
    [SerializeField] private GameObject gameCoreVisualPrefab;
    [SerializeField] private float gameCoreSpinSpeedY = 180f;
    [SerializeField] private float gameCoreHeightOffset = 0.15f;

    [Header("Debug Gizmos")]
    [SerializeField] private bool showGizmos = true; // 인스펙터에서 켜고 끌 수 있게
    private Vector3 _debugRayStart;
    private Vector3 _debugRayDir;
    private bool _debugHit;
    private Vector3 _debugPos;
    private Quaternion _debugRot;
    private Vector3 _debugHitPoint;
    public override void Spawned()
    {
        _player = GetComponent<NewPlayerController>();
        _equip = GetComponent<EquipManagerNet>();
        _cam = GetComponentInChildren<Camera>(true);
        _animator = GetComponentInChildren<Animator>(true);
        if (_cam) _cam.gameObject.SetActive(HasInputAuthority);
    }

    void Update()
    {
        if (!HasInputAuthority) return;

        UpdateCooldownUI();
        CheckInteractionLocal();

        // ★ [추가] 매 프레임 기즈모 데이터를 갱신합니다 (미리보기 기능)
        if (showGizmos)
        {
            UpdateGizmoPreview();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            HandleInput();
        }


    }

    private void UpdateGizmoPreview()
    {
        if (_cam == null) return;

        // 1. 실제 생성 로직과 똑같은 레이 정보 사용
        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        _debugRayStart = ray.origin;
        _debugRayDir = ray.direction;

        // 2. 레이캐스트 (Player 레이어 마스크 꼭 확인!)
        if (Physics.Raycast(ray, out RaycastHit hit, spawnRayDistance, trashGroundMask))
        {
            // 닿으면 그 위치
            _debugHit = true;
            _debugHitPoint = hit.point;
            _debugPos = hit.point; // 바닥에 딱 붙은 위치
        }
        else
        {
            // 안 닿으면 허공 (2m 앞)
            _debugHit = false;
            _debugPos = ray.GetPoint(2.0f);
        }

        // 3. 회전 미리보기 (카메라 Y축)
        float targetY = _cam.transform.eulerAngles.y;
        _debugRot = Quaternion.Euler(0f, targetY, 0f);
    }

    private void UpdateCooldownUI()
    {
        if (PlayerHudUI.Instance == null) return;

        var equipped = _equip ? _equip.Equipped : EquipmentId.Hand;
        float remainingTime = 0f;
        float maxTime = 1f;

        switch (equipped)
        {
            case EquipmentId.Hand:
                remainingTime = _nextInteractTime - Time.time;
                maxTime = interactCooldown;
                break;
            case EquipmentId.Sponge:
                remainingTime = _nextCleanTime - Time.time;
                maxTime = cleanCooldown;
                break;
            case EquipmentId.TrashThrow:
                remainingTime = _nextSpawnTime - Time.time;
                maxTime = spawnCooldown;
                break;
            default:
                remainingTime = 0;
                break;
        }

        PlayerHudUI.Instance.UpdateCooldown(Mathf.Max(0f, remainingTime), maxTime);
    }

    private void HandleInput()
    {
        var equipped = _equip ? _equip.Equipped : EquipmentId.Hand;

        if (_current != null)
        {
            var no = _current.GetComponent<NetworkObject>();
            if (no == null) no = _current.GetComponentInParent<NetworkObject>();

            if (no == null)
            {
                Debug.LogWarning("Interactable has no NetworkObject.");
                return;
            }

            // --- Sponge ---
            if (equipped == EquipmentId.Sponge)
            {
                if (_current.interactableType != InteractableType.Dirty)
                {
                    ShowWarning("닦기 상태에서는 오물만 치울 수 있습니다!");
                    return;
                }
                if (Time.time < _nextCleanTime) return;

                RPC_RequestDirtyInteract(no.Id);
                _nextCleanTime = Time.time + cleanCooldown;
                return;
            }

            // --- Hand ---
            if (equipped == EquipmentId.Hand)
            {
                if (_current.interactableType != InteractableType.Trash)
                {
                    ShowWarning("줍기 상태에서는 오물을 닦을 수 없습니다!");
                    return;
                }
                if (Time.time < _nextInteractTime) return;

                // ★ 쓰레기 위치로 플레이어 회전 (클라이언트에서 즉시)
                if (_player != null && _current != null)
                {
                    _player.RotateToPosition(_current.transform.position);
                }

                // 카메라 연출
                if (_player != null)
                {
                    _player.PlayPickUpCameraMove(new Vector3(0, -0.5f, 0.2f), 1.0f);
                }

                RPC_RequestInteract(no.Id);
                _nextInteractTime = Time.time + interactCooldown;
                return;
            }

            ShowWarning("줍기 상태에서만 줍기가 가능합니다!");
        }
        else
        {
            // --- TrashThrow ---
            if (equipped != EquipmentId.TrashThrow)
            {
                ShowWarning("쓰레기 생성 상태일 때만 생성이 가능합니다!");
                return;
            }
            if (Time.time < _nextSpawnTime) return;

            // ★ [중요] 플레이어 회전 전에 카메라 정보를 먼저 저장!
            // 카메라가 플레이어의 자식이라면, 플레이어 회전 후에는 카메라의 월드 방향이 바뀔 수 있습니다.
            Vector3 camPos = _cam != null ? _cam.transform.position : Vector3.zero;
            Vector3 camForward = _cam != null ? _cam.transform.forward : Vector3.forward;
            float camY = _cam != null ? _cam.transform.eulerAngles.y : 0f;

            // 저장해둔 회전 전 카메라 정보로 쓰레기 생성 위치 계산
            Vector3 spawnPos = CalculateSpawnPosition(camPos, camForward);
            
            // ★ 생성 위치로 플레이어 회전 (클라이언트에서 즉시)
            if (_player != null)
            {
                _player.RotateToPosition(spawnPos);
            }

            // 저장해둔 회전 전 카메라 정보로 쓰레기 생성
            TrySpawnTrash(camPos, camForward, camY);
            _nextSpawnTime = Time.time + spawnCooldown;
        }
    }

    private void ShowWarning(string msg)
    {
        if (GameRuleManager.Instance) GameRuleManager.Instance.ShowEquipStatus(msg, 1.2f);
    }

    void CheckInteractionLocal()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (Physics.SphereCast(ray, sphereRadius, out var hit, playerReach, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag(TAG))
            {
                DisableCurrentInteractable();
                return;
            }

            var newInteractable = hit.collider.GetComponentInParent<Interactable>();
            if (newInteractable != null && newInteractable.enabled)
            {
                if (_current && newInteractable != _current)
                    _current.DisableOutline();

                SetNewCurrentInteractable(newInteractable);
            }
            else
            {
                DisableCurrentInteractable();
            }
        }
        else
        {
            DisableCurrentInteractable();
        }
    }

    void SetNewCurrentInteractable(Interactable it)
    {
        _current = it;
        _current.EnableOutline();
        if (HUDController.instance) HUDController.instance.EnableInteractionText(_current.message);
    }

    void DisableCurrentInteractable()
    {
        if (HUDController.instance) HUDController.instance.DisableInteractionText();
        if (_current)
        {
            _current.DisableOutline();
            _current = null;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestInteract(NetworkId targetId, RpcInfo info = default)
    {
        if (_equip != null && _equip.Equipped != EquipmentId.Hand) return;

        var obj = Runner.FindObject(targetId);
        if (obj == null) return;

        var interactable = obj.GetComponent<Interactable>();
        if (interactable == null || !interactable.enabled) return;

        var anyCol = interactable.GetComponentInChildren<Collider>();
        if (anyCol == null || !anyCol.CompareTag(TAG)) return;

        float max = playerReach + 1.0f; // 약간의 오차 허용
        if ((Object.transform.position - interactable.transform.position).sqrMagnitude > max * max) return;

        if (_player != null)
            _player.LockMovementForPickup(1.8f);

        _animator.SetTrigger("pickTrigger");

        Vector3 interactedPos = interactable.transform.position;
        Quaternion interactedRot = interactable.transform.rotation;

        // ★ 쓰레기 위치로 플레이어 회전 (서버에서도 회전 - 클라이언트는 이미 회전함)
        if (_player != null)
        {
            _player.RotateToPosition(interactedPos);
        }

        TrashItem trashItem = obj.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            trashItem.OnPickedUp();
        }

        interactable.Interact();

        bool isDefaultTrash = interactable.SpawnedByPlayer == PlayerRef.None;
        TryAwardGameCore_Server(interactedPos, interactedRot, info, isDefaultTrash);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDirtyInteract(NetworkId targetId, RpcInfo info = default)
    {
        if (_equip != null && _equip.Equipped != EquipmentId.Sponge) return;

        var obj = Runner.FindObject(targetId);
        if (obj == null) return;

        var interactable = obj.GetComponent<Interactable>();
        if (interactable == null || !interactable.enabled) return;

        if (_player != null)
            _player.LockMovementForPickup(2f);

        _animator.SetTrigger("cleanTrigger");

        interactable.RPC_PlayFX(targetId);
        interactable.RPC_PlayFade(targetId, 0.1f);

        try
        {
            foreach (var col in obj.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }
        catch { }

        interactable.StartCoroutine(interactable.DespawnAfterDelay(targetId, 0.3f));
    }

    // 스폰 위치 계산 (회전 전 카메라 정보 사용)
    private Vector3 CalculateSpawnPosition(Vector3 camPos, Vector3 camForward)
    {
        Ray ray = new Ray(camPos, camForward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, spawnRayDistance, trashGroundMask))
        {
            return hit.point;
        }
        else
        {
            return ray.GetPoint(2.0f);
        }
    }

    // ★ [수정됨] 카메라가 바라보는 바닥 지점을 계산하여 생성 요청
    // 회전 전 카메라 정보를 파라미터로 받아서 사용
    private void TrySpawnTrash(Vector3 camPos, Vector3 camForward, float camY)
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;

        int pick = Random.Range(0, trashPrefabs.Length);

        // ★ [수정] 오프셋 제거! 카메라 위치(눈)에서 정확히 시작합니다.
        // 이제 화면 중앙(크로스헤어)과 레이저가 100% 일치합니다.
        Ray ray = new Ray(camPos, camForward);

        Vector3 spawnPos;

        // 디버그 업데이트
        _debugRayStart = ray.origin;
        _debugRayDir = ray.direction;

        // ★ [수정] UpdateGizmoPreview()와 동일한 로직 사용
        if (Physics.Raycast(ray, out RaycastHit hit, spawnRayDistance, trashGroundMask))
        {
            spawnPos = hit.point;
            _debugHit = true;
            _debugHitPoint = hit.point;
        }
        else
        {
            // 허공일 때
            spawnPos = ray.GetPoint(2.0f);
            _debugHit = false;
        }

        // 회전: 저장해둔 회전 전 카메라 Y축 방향 사용
        Quaternion spawnRot = Quaternion.Euler(0f, camY, 0f);

        RPC_RequestSpawnTrash(pick, spawnPos, spawnRot);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        if (_cam == null) return; // 카메라 없으면 패스

        // 1. 레이저 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(_debugRayStart, _debugRayDir * spawnRayDistance);

        // 2. 예상 생성 지점
        if (_debugHit)
        {
            // 바닥에 닿았을 때: 초록색 구
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_debugPos, 0.2f);
        }
        else
        {
            // 허공일 때: 빨간색 와이어 구
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_debugPos, 0.2f);
        }

        // 3. 예상 회전 방향 (파란색 선 = 앞)
        Gizmos.color = Color.blue;
        Vector3 forwardDir = _debugRot * Vector3.forward;
        Gizmos.DrawLine(_debugPos, _debugPos + forwardDir * 1.0f);

        // 화살표 머리 살짝 표시 (선택)
        Gizmos.DrawRay(_debugPos + forwardDir * 1.0f, (_debugRot * new Vector3(-0.2f, 0, -0.2f)));
        Gizmos.DrawRay(_debugPos + forwardDir * 1.0f, (_debugRot * new Vector3(0.2f, 0, -0.2f)));
    }

    // ★ [수정됨] 위치를 서버에서 계산하지 않고 클라이언트가 준 정확한 위치 사용
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnTrash(int prefabIndex, Vector3 finalPos, Quaternion finalRot, RpcInfo _ = default)
    {
        if (_equip != null && _equip.Equipped != EquipmentId.TrashThrow) return;
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (prefabIndex < 0 || prefabIndex >= trashPrefabs.Length) return;

        var prefab = trashPrefabs[prefabIndex];
        if (prefab == null) return;

        if (_player != null)
            _player.LockMovementForPickup(1.5f);

        _animator.SetTrigger("pickTrigger");

        // 클라이언트가 계산해준 위치(finalPos)와 회전(finalRot) 그대로 사용
        var spawnedObj = Runner.Spawn(prefab, finalPos, finalRot, Object.InputAuthority);

        if (spawnedObj != null)
        {
            // ★ 생성된 쓰레기 위치로 플레이어 회전 (서버에서도 회전 - 클라이언트는 이미 회전함)
            if (_player != null)
            {
                _player.RotateToPosition(finalPos);
            }

            var interactable = spawnedObj.GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.SetSpawnedByPlayer(Object.InputAuthority);
            }
        }
    }

    private void TryAwardGameCore_Server(Vector3 worldPos, Quaternion worldRot, RpcInfo info, bool isDefaultTrash)
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (_player == null || _player.Role != PlayerRole.Imposter) return;
        if (!isDefaultTrash) return;

        if (UnityEngine.Random.value > gameCoreSpawnChance) return;

        RPC_ShowGameCoreToOwner(gameCoreShowSeconds);
        RPC_ShowGameCoreVisualAtAll(worldPos, worldRot, gameCoreShowSeconds);

        if (GameRuleManager.Instance != null)
        {
            GameRuleManager.Instance.AddGameCore_Server();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ShowGameCoreToOwner(float seconds, RpcInfo _ = default)
    {
        if (GameRuleManager.Instance)
            GameRuleManager.Instance.ShowLocalStatus("게임코어 획득!", Mathf.Max(0.5f, seconds));
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowGameCoreVisualAtAll(Vector3 worldPos, Quaternion worldRot, float seconds, RpcInfo _ = default)
    {
        float life = Mathf.Max(0.5f, seconds);
        Vector3 spawnPos = worldPos + Vector3.up * Mathf.Max(0f, gameCoreHeightOffset);

        GameObject go = null;
        if (gameCoreVisualPrefab != null)
        {
            go = Instantiate(gameCoreVisualPrefab, spawnPos, worldRot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetPositionAndRotation(spawnPos, worldRot);
            go.transform.localScale = Vector3.one * 0.3f;
        }

        if (go != null)
        {
            StartCoroutine(SpinAndDestroy(go, life, gameCoreSpinSpeedY));
        }
    }

    private IEnumerator SpinAndDestroy(GameObject target, float lifeSeconds, float spinSpeedDegPerSec)
    {
        float t = 0f;
        while (t < lifeSeconds && target != null)
        {
            target.transform.Rotate(0f, spinSpeedDegPerSec * Time.deltaTime, 0f, Space.World);
            t += Time.deltaTime;
            yield return null;
        }
        if (target != null)
        {
            Destroy(target);
        }
    }
}