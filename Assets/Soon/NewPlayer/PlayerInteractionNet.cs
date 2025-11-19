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
    [SerializeField] private float interactCooldown = 1.0f; // 줍기(Hand) 쿨타임
    [SerializeField] private float cleanCooldown = 1.5f;    // 닦기(Sponge) 쿨타임
    [SerializeField] private float spawnCooldown = 2.0f;    // 버리기(TrashThrow) 쿨타임

    // 내부 쿨타임 계산용 변수 (다음 사용 가능 시간)
    private float _nextInteractTime;
    private float _nextCleanTime;
    private float _nextSpawnTime;

    [Header("Trash (Prototype)")]
    [SerializeField] private GameObject[] trashPrefabs;
    [SerializeField] private float trashForwardOffset = 1.6f;
    [SerializeField] private float trashDropHeight = 1.0f;
    [SerializeField] private LayerMask trashGroundMask = ~0;

    [Header("Game Core")]
    [SerializeField, Range(0f, 1f)] private float gameCoreSpawnChance = 0.2f;
    [SerializeField] private float gameCoreShowSeconds = 2.5f;
    [SerializeField] private GameObject gameCoreVisualPrefab;
    [SerializeField] private float gameCoreSpinSpeedY = 180f;
    [SerializeField] private float gameCoreHeightOffset = 0.15f;

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

        // [추가] 매 프레임 UI에 쿨타임 정보 업데이트
        UpdateCooldownUI();

        CheckInteractionLocal();

        if (Input.GetKeyDown(KeyCode.R))
        {
            HandleInput();
        }
    }

    // [추가] 현재 장비 상태에 따라 UI에 쿨타임 정보를 전달하는 함수
    private void UpdateCooldownUI()
    {
        if (PlayerHudUI.Instance == null) return;

        var equipped = _equip ? _equip.Equipped : EquipmentId.Hand;
        float remainingTime = 0f;
        float maxTime = 1f;

        // 현재 장비에 따라 어떤 쿨타임을 보여줄지 결정
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

        // 남은 시간이 음수가 되지 않도록 처리 후 UI 전달
        PlayerHudUI.Instance.UpdateCooldown(Mathf.Max(0f, remainingTime), maxTime);
    }

    // 입력 처리 로직 분리 (가독성을 위해)
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

            // --- Sponge (닦기) ---
            if (equipped == EquipmentId.Sponge)
            {
                if (_current.interactableType != InteractableType.Dirty)
                {
                    ShowWarning("닦기 상태에서는 오물만 치울 수 있습니다!");
                    return;
                }

                // 쿨타임 체크
                if (Time.time < _nextCleanTime) return;

                if (_player != null) _player.RotateToCameraDirection();

                RPC_RequestDirtyInteract(no.Id);

                // 쿨타임 설정
                _nextCleanTime = Time.time + cleanCooldown;
                return;
            }

            // --- Hand (줍기) ---
            if (equipped == EquipmentId.Hand)
            {
                if (_current.interactableType != InteractableType.Trash)
                {
                    ShowWarning("줍기 상태에서는 오물을 닦을 수 없습니다!");
                    return;
                }

                // 쿨타임 체크
                if (Time.time < _nextInteractTime) return;

                if (_player != null)
                {
                    _player.RotateToCameraDirection();
                    _player.PlayPickUpCameraMove(new Vector3(0, -0.5f, 0.2f), 1.0f);
                }

                RPC_RequestInteract(no.Id);

                // 쿨타임 설정
                _nextInteractTime = Time.time + interactCooldown;
                return;
            }

            ShowWarning("줍기 상태에서만 줍기가 가능합니다!");
        }
        else
        {
            // --- TrashThrow (버리기/스폰) ---
            if (equipped != EquipmentId.TrashThrow)
            {
                ShowWarning("쓰레기 생성 상태일 때만 생성이 가능합니다!");
                return;
            }

            // 쿨타임 체크
            if (Time.time < _nextSpawnTime) return;

            TrySpawnTrash();

            // 쿨타임 설정
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

        float max = playerReach + 0.75f;
        if ((Object.transform.position - interactable.transform.position).sqrMagnitude > max * max) return;

        if (_player != null)
            _player.LockMovementForPickup(1.8f);

        _animator.SetTrigger("pickTrigger");

        Vector3 interactedPos = interactable.transform.position;
        Quaternion interactedRot = interactable.transform.rotation;

        TrashItem trashItem = obj.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            trashItem.OnPickedUp();
        }

        interactable.Interact();
        
        // 기본 쓰레기(라운드 시작 시 존재)인지 확인
        // SpawnedByPlayer가 PlayerRef.None이면 기본 쓰레기
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

        var anyCol = interactable.GetComponentInChildren<Collider>();
        if (anyCol == null || !anyCol.CompareTag(TAG)) return;

        float max = playerReach + 0.75f;
        if ((Object.transform.position - interactable.transform.position).sqrMagnitude > max * max) return;

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

        TrashItem trashItem = obj.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            trashItem.OnPickedUp();
        }

        interactable.StartCoroutine(interactable.DespawnAfterDelay(targetId, 0.3f));
    }

    private void TrySpawnTrash()
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (_cam == null) return;

        if (_player != null)
            _player.RotateToCameraDirection();

        int pick = Random.Range(0, trashPrefabs.Length);
        Vector3 camPos = _cam.transform.position;
        Vector3 camForward = _cam.transform.forward;

        RPC_RequestSpawnTrash(pick, camPos, camForward);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnTrash(int prefabIndex, Vector3 cameraPosition, Vector3 cameraForward, RpcInfo _ = default)
    {
        if (_equip != null && _equip.Equipped != EquipmentId.TrashThrow) return;

        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (prefabIndex < 0 || prefabIndex >= trashPrefabs.Length) return;

        var prefab = trashPrefabs[prefabIndex];
        if (prefab == null) return;

        Vector3 camForwardFlat = cameraForward;
        camForwardFlat.y = 0f;
        camForwardFlat.Normalize();

        Vector3 origin = cameraPosition
                       + camForwardFlat * trashForwardOffset
                       + Vector3.up * trashDropHeight;

        Vector3 spawnPos = origin;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 5f, trashGroundMask, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * 0.02f;

        // 카메라 방향 회전 적용
        Quaternion rot = Quaternion.LookRotation(camForwardFlat);

        if (_player != null)
            _player.LockMovementForPickup(1.5f);

        _animator.SetTrigger("pickTrigger");
        var spawnedObj = Runner.Spawn(prefab, spawnPos, rot, Object.InputAuthority);
        
        // 스폰한 쓰레기에 스폰한 플레이어 정보 저장
        if (spawnedObj != null)
        {
            var interactable = spawnedObj.GetComponent<Interactable>();
            if (interactable != null)
            {
                interactable.SetSpawnedByPlayer(Object.InputAuthority);
            }
        }

        var trashItem = spawnedObj.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            // 이 함수는 Start()와 달리, 여기서 명시적으로 호출할 때만 실행됩니다.
            trashItem.OnSpawn();
        }
    }

    private void TryAwardGameCore_Server(Vector3 worldPos, Quaternion worldRot, RpcInfo info, bool isDefaultTrash)
    {
        if (!Object || !Object.HasStateAuthority) return;
        
        // 방해자(Imposter)일 때만 게임 코어 획득 가능
        if (_player == null || _player.Role != PlayerRole.Imposter)
        {
            return;
        }
        
        // 기본 쓰레기(라운드 시작 시 존재)만 코어 드랍 가능
        // 플레이어가 스폰한 쓰레기는 코어 드랍 안 함
        if (!isDefaultTrash)
        {
            return;
        }

        if (UnityEngine.Random.value > gameCoreSpawnChance) return;

        RPC_ShowGameCoreToOwner(gameCoreShowSeconds);
        Debug.Log("Game Core Spawned");

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

            var col = go.GetComponent<Collider>();
            if (col) col.enabled = false;
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