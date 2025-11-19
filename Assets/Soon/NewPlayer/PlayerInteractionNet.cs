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

    [Header("Trash (Prototype)")]
    [SerializeField] private GameObject[] trashPrefabs;   // NetworkObject가 붙은 프리팹들
    [SerializeField] private float trashForwardOffset = 1.6f; // 발 앞 거리
    [SerializeField] private float trashDropHeight = 1.0f; // 살짝 위에서 떨어뜨리기
    [SerializeField] private LayerMask trashGroundMask = ~0;   // 바닥 레이어(없으면 기본 전부)

    [Header("Game Core")]
    [SerializeField, Range(0f, 1f)] private float gameCoreSpawnChance = 0.2f; // 상호작용 시 생성 확률
    [SerializeField] private float gameCoreShowSeconds = 2.5f;                // 안내 UI 표시 시간
	[SerializeField] private GameObject gameCoreVisualPrefab;                 // 상호작용 위치에 잠깐 표시할 비주얼(비네트워크 오브젝트 허용)
	[SerializeField] private float gameCoreSpinSpeedY = 180f;                 // 초당 Y축 회전 속도(도/초)
	[SerializeField] private float gameCoreHeightOffset = 0.15f;              // 쓰레기 위치에서 살짝 위로 띄우기

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
        CheckInteractionLocal();
        
        if (Input.GetKeyDown(KeyCode.R))
        {
				// 상태별 입력 제한
				var equipped = _equip ? _equip.Equipped : EquipmentId.Hand;

				if (_current != null)
            {
					// NetworkObject 찾기 (공통)
					var no = _current.GetComponent<NetworkObject>();
					if (no == null) no = _current.GetComponentInParent<NetworkObject>();
					
					if (no == null)
					{
						Debug.LogWarning("Interactable has no NetworkObject. Consider making it networked.");
						return;
					}

					// Sponge 상태: Dirty 타입만 상호작용 가능 (치우기)
					if (equipped == EquipmentId.Sponge)
					{
						// Dirty 타입이 아니면 상호작용 불가
						if (_current.interactableType != InteractableType.Dirty)
						{
							if (GameRuleManager.Instance) GameRuleManager.Instance.ShowLocalStatus("Sponge 상태에서는 Dirty만 치울 수 있습니다", 1.2f);
							return;
						}

						// 플레이어를 카메라 방향으로 회전 (클라이언트에서 처리)
						if (_player != null)
						{
							_player.RotateToCameraDirection();
						}

						RPC_RequestDirtyInteract(no.Id);
						return;
					}

					// Hand 상태: Trash 타입만 상호작용 가능 (줍기)
					if (equipped == EquipmentId.Hand)
					{
						// Trash 타입이 아니면 상호작용 불가
						if (_current.interactableType != InteractableType.Trash)
						{
							if (GameRuleManager.Instance) GameRuleManager.Instance.ShowLocalStatus("Hand 상태에서는 Trash만 줍을 수 있습니다", 1.2f);
							return;
						}

						// 플레이어를 카메라 방향으로 회전 (클라이언트에서 처리)
						if (_player != null)
						{
							_player.RotateToCameraDirection();
							_player.PlayPickUpCameraMove(new Vector3(0, -0.5f, 0.2f), 1.0f);
						}

						RPC_RequestInteract(no.Id);
						return;
					}

					// Hand 상태가 아니면 안내 메시지
					if (GameRuleManager.Instance) GameRuleManager.Instance.ShowLocalStatus("Hand 상태에서만 줍기가 가능합니다", 1.2f);
            }
            else
            {
					// TrashSpawn(=TrashThrow) 상태에서만 스폰 허용
					if (equipped != EquipmentId.TrashThrow)
					{
						if (GameRuleManager.Instance) GameRuleManager.Instance.ShowLocalStatus("TrashSpawn 상태에서만 생성이 가능합니다", 1.2f);
						return;
					}
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
			// 서버 권한에서 최종 검증: Hand 상태만 허용
			if (_equip != null && _equip.Equipped != EquipmentId.Hand) return;

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
        
        // Pickup 애니메이션 중 이동 금지 (회전은 클라이언트에서 이미 처리됨)
        if (_player != null)
            _player.LockMovementForPickup(1.5f);
        
        _animator.SetTrigger("pickTrigger");
		// 비주얼 표시를 위한 상호작용 지점 좌표/회전 백업
		Vector3 interactedPos = interactable.transform.position;
		Quaternion interactedRot = interactable.transform.rotation;

        // add - yerin
        TrashItem trashItem = obj.GetComponent<TrashItem>();
        if (trashItem != null)
        {
            trashItem.OnPickedUp();
        }


        interactable.Interact();

        // 상호작용 성공 시 일정 확률로 게임코어 획득 처리 (서버에서 판정)
		TryAwardGameCore_Server(interactedPos, interactedRot, info);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestDirtyInteract(NetworkId targetId, RpcInfo info = default)
    {
			// 서버 권한에서 최종 검증: Sponge 상태만 허용
			if (_equip != null && _equip.Equipped != EquipmentId.Sponge) return;

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
        
        // Clean 애니메이션 중 이동 금지 (회전은 클라이언트에서 이미 처리됨)
        if (_player != null)
            _player.LockMovementForPickup(1.5f);
        
        _animator.SetTrigger("cleanTrigger");
        
        // Interactable의 RPC를 직접 호출 (서버에서만 실행)
        interactable.RPC_RequestDirtyInteract(targetId);
    }

    private void TrySpawnTrash()
    {
        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (_cam == null) return;

        // 플레이어를 카메라 방향으로 회전 (클라이언트에서 처리)
        if (_player != null)
            _player.RotateToCameraDirection();

        // 어떤 쓰레기를 뽑을지 클라에서 랜덤 선택
        int pick = Random.Range(0, trashPrefabs.Length);

        // 카메라 위치와 방향을 서버에 전달
        Vector3 camPos = _cam.transform.position;
        Vector3 camForward = _cam.transform.forward;

        // 서버에 스폰 요청 (카메라 기준 좌표 계산)
        RPC_RequestSpawnTrash(pick, camPos, camForward);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestSpawnTrash(int prefabIndex, Vector3 cameraPosition, Vector3 cameraForward, RpcInfo _ = default)
    {
			// 서버 권한에서 최종 검증: TrashSpawn(=TrashThrow) 상태만 허용
			if (_equip != null && _equip.Equipped != EquipmentId.TrashThrow) return;

        if (trashPrefabs == null || trashPrefabs.Length == 0) return;
        if (prefabIndex < 0 || prefabIndex >= trashPrefabs.Length) return;

        var prefab = trashPrefabs[prefabIndex];
        if (prefab == null) return;

        // 카메라 기준으로 앞/위 오프셋 잡고 바닥을 향해 레이캐스트
        Vector3 camForwardFlat = cameraForward;
        camForwardFlat.y = 0f;
        camForwardFlat.Normalize();

        Vector3 origin = cameraPosition
                       + camForwardFlat * trashForwardOffset
                       + Vector3.up * trashDropHeight;

        Vector3 spawnPos = origin;
        if (Physics.Raycast(origin, Vector3.down, out var hit, 5f, trashGroundMask, QueryTriggerInteraction.Ignore))
            spawnPos = hit.point + Vector3.up * 0.02f; // z-fighting 방지 살짝 띄우기

        float yaw = UnityEngine.Random.Range(0f, 360f);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        // Pickup 애니메이션 중 이동 금지 (회전은 클라이언트에서 이미 처리됨)
        if (_player != null)
            _player.LockMovementForPickup(1.5f);

        _animator.SetTrigger("pickTrigger");
        Runner.Spawn(prefab, spawnPos, rot, Object.InputAuthority);
    }

	// --- Game Core: 서버 판정 + 클라 연출 ---
	private void TryAwardGameCore_Server(Vector3 worldPos, Quaternion worldRot, RpcInfo info)
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (UnityEngine.Random.value > gameCoreSpawnChance) return;

        // 로컬 연출 요청(해당 상호작용자에게만)
        RPC_ShowGameCoreToOwner(gameCoreShowSeconds);
        Debug.Log("Game Core Spawned");

		// 모든 클라이언트에 비주얼 표시 (잠깐 보였다가 사라짐)
		RPC_ShowGameCoreVisualAtAll(worldPos, worldRot, gameCoreShowSeconds);

        // 코어 카운트 증가 → 3개 달성 시 클리너 승리 브로드캐스트
        if (GameRuleManager.Instance != null)
        {
            GameRuleManager.Instance.AddGameCore_Server();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ShowGameCoreToOwner(float seconds, RpcInfo _ = default)
    {
        // 중앙 상태 텍스트에 잠깐 표시
        if (GameRuleManager.Instance)
            GameRuleManager.Instance.ShowLocalStatus("게임코어 획득!", Mathf.Max(0.5f, seconds));
    }

	// 모든 클라에 비주얼 표시: 네트워크 오브젝트 스폰 대신, 로컬 임시 오브젝트 생성/파괴
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
			// 프리팹이 없을 경우 간이 구체로 대체
			go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			go.transform.SetPositionAndRotation(spawnPos, worldRot);
			go.transform.localScale = Vector3.one * 0.3f;

			// 충돌 방지: 임시 콜라이더 비활성화
			var col = go.GetComponent<Collider>();
			if (col) col.enabled = false;
		}

		if (go != null)
		{
			// 수명 동안 Y축 회전 후 파괴
			StartCoroutine(SpinAndDestroy(go, life, gameCoreSpinSpeedY));
		}
	}

	private IEnumerator SpinAndDestroy(GameObject target, float lifeSeconds, float spinSpeedDegPerSec)
	{
		float t = 0f;
		while (t < lifeSeconds && target != null)
		{
			// 월드 좌표계 기준 Y축 회전
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
