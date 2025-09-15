using Fusion;
using UnityEngine;

public class PlayerInteractionNet : NetworkBehaviour
{
    [SerializeField] private float playerReach = 3.5f;
    [SerializeField] private float sphereRadius = 0.5f;

    private const string TAG = "Interactable";

    private Interactable _current;
    public NewPlayerController _player;
    private Camera _cam;
    [SerializeField] private Animator _animator;

    [Header("Trash (Prototype)")]
    [SerializeField] private GameObject[] trashPrefabs;   // NetworkObject가 붙은 프리팹들
    [SerializeField] private float trashForwardOffset = 1.6f; // 발 앞 거리
    [SerializeField] private float trashDropHeight = 1.0f; // 살짝 위에서 떨어뜨리기
    [SerializeField] private LayerMask trashGroundMask = ~0;   // 바닥 레이어(없으면 기본 전부)

    public override void Spawned()
    {
        _player = GetComponent<NewPlayerController>();
        _cam = GetComponentInChildren<Camera>(true);
        _animator = GetComponent<Animator>();
        if (_cam) _cam.gameObject.SetActive(HasInputAuthority);
    }

    void Update()
    {
        if (!HasInputAuthority) return;
        CheckInteractionLocal();

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (_current != null)
            {
                if (_player != null)
                    _player.PlayPickUpCameraMove(new Vector3(0, -0.5f, 0.2f), 1.0f);

                var no = _current.GetComponent<NetworkObject>();
                if (no != null) RPC_RequestInteract(no.Id);
                else Debug.LogWarning("Interactable has no NetworkObject. Consider making it networked.");
            }
            else
            {
                TrySpawnTrash();
            }
        }

    }


    void CheckInteractionLocal()
    {
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        // ← 레이어 마스크 없이 캐스트
        if (Physics.SphereCast(ray, sphereRadius, out var hit, playerReach, ~0, QueryTriggerInteraction.Ignore))
        {
            // 태그로만 필터
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
        var obj = Runner.FindObject(targetId);
        if (obj == null) return;

        var interactable = obj.GetComponent<Interactable>();
        if (interactable == null || !interactable.enabled) return;

        // 서버에서도 태그 재검증(치트 방지)
        var anyCol = interactable.GetComponentInChildren<Collider>();
        if (anyCol == null || !anyCol.CompareTag(TAG)) return;

        // 거리 검증
        float max = playerReach + 0.75f;
        if ((Object.transform.position - interactable.transform.position).sqrMagnitude > max * max) return;
        _animator.SetTrigger("pickTrigger");
        interactable.Interact();
    }

    private void TrySpawnTrash()
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;

        // 어떤 쓰레기를 뽑을지 클라에서 랜덤 선택
        int pick = Random.Range(0, trashPrefabs.Length);

        // 서버에 스폰 요청 (좌표계산은 서버에서 안전하게)
        RPC_RequestSpawnTrash(pick);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnTrash(int prefabIndex, RpcInfo _ = default)
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (prefabIndex < 0 || prefabIndex >= trashPrefabs.Length) return;

        var prefab = trashPrefabs[prefabIndex];
        if (prefab == null) return;

        // 플레이어 기준으로 앞/위 오프셋 잡고 바닥을 향해 레이캐스트
        Vector3 origin = transform.position
                       + transform.forward * trashForwardOffset
                       + Vector3.up * trashDropHeight;

        Vector3 spawnPos = origin;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 5f, trashGroundMask, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * 0.02f; // z-fighting 방지 살짝 띄우기

        float yaw = UnityEngine.Random.Range(0f, 360f);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        _animator.SetTrigger("pickTrigger");
        Runner.Spawn(prefab, spawnPos, rot, Object.InputAuthority);
    }
}
