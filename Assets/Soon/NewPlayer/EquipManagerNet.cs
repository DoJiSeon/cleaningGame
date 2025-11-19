using Fusion;
using System;
using UnityEngine;

public enum EquipmentId : int
{
    None = 0,
    Hand = 1,
    Sponge = 2,
    TrashThrow = 3
}

public enum PlayerRole
{
    Cleaner = 0,    // 청소부
    Imposter = 1    // 방해꾼 (임포스터)
}

public class EquipManagerNet : NetworkBehaviour
{
    [Header("시각적 오브젝트 참조 (ID -> GameObject)")]
    [SerializeField] private GameObject handObj;
    [SerializeField] private GameObject spongeObj;
    [SerializeField] private GameObject trashThrowObj;
    // 필요하면 인스펙터에서 추가 할당

    // 네트워크로 동기화되는 현재 장착 아이템
    [Networked] public EquipmentId Equipped { get; private set; }

    // 타 컴포넌트 및 역할 관리 변수
    private NewPlayerController _pc;
    private PlayerRole _lastRole;
    private EquipmentId[] _allowed = new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge };

    private EquipmentId _lastEquipped; // 로컬 렌더링용 이전 상태 캐싱 (변화 감지용)

    public event Action<EquipmentId> OnEquipChanged;

    public override void Spawned()
    {
        _pc = GetComponent<NewPlayerController>();

        RefreshAllowedByRole(force: true); // 스폰 시 역할에 따른 아이템 목록 1회 강제 갱신

        // 스폰 직후 현재 상태에 맞춰 시각적 오브젝트 갱신
        ForceRefreshVisual();

        if (HasInputAuthority)
        {
            // 방법 A: 직접 UI를 찾아서 업데이트 (간단한 방법)
            if (PlayerHudUI.Instance != null)
            {
                // 초기 아이콘 동기화
                PlayerHudUI.Instance.UpdateEquipmentIcon(Equipped);
                // 이후 장비 변경 이벤트에 UI 갱신을 구독
                OnEquipChanged += PlayerHudUI.Instance.UpdateEquipmentIcon;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 매 프레임 역할 상태를 확인하여 허용 아이템 목록 갱신
        RefreshAllowedByRole();

        // StateAuthority가 있는 오브젝트에서만 입력을 처리하여 상태를 변경함
        // GetInput은 이 오브젝트의 InputAuthority의 입력을 가져옴
        if (HasStateAuthority)
        {
            // GetInput이 실패할 수 있으므로 안전하게 처리
            if (GetInput(out PlayerInputData input))
            {
                // 입력이 있으면 처리 (GetKeyDown은 한 프레임에만 true이므로 버퍼링된 입력에서도 작동)
                if (input.NextEquipPressed) 
                {
                    EquipNext();
                }
                else if (input.PrevEquipPressed) 
                {
                    EquipPrev();
                }
                else if (input.SelectSlotIndex > 0) 
                {
                    EquipSlot(input.SelectSlotIndex);
                }
            }
        }
    }

    public override void Render()
    {
        // 로컬 뷰(Visual/이펙트) 처리: 네트워크 변수(Equipped)가 변경되었을 때만 로직 수행
        if (_lastEquipped != Equipped)
        {
            if (HasInputAuthority)
                Debug.Log($"[Equip] Equipped = {Equipped}");

            ApplyVisual(Equipped);

            if (HasInputAuthority)
            {
                // 이벤트 방식 사용 시:
                OnEquipChanged?.Invoke(Equipped);

                //// 혹은 직접 호출 방식 (싱글톤 사용 시 더 직관적):
                //if (PlayerHudUI.Instance != null)
                //{
                //    PlayerHudUI.Instance.UpdateEquipmentIcon(Equipped);
                //}
            }

            _lastEquipped = Equipped;
        }
    }

    private void OnDestroy()
    {
        // UI 인스턴스가 존재하면 구독 해제
        if (HasInputAuthority && PlayerHudUI.Instance != null)
        {
            OnEquipChanged -= PlayerHudUI.Instance.UpdateEquipmentIcon;
        }
    }

    // ---- 외부 API (다른 스크립트에서 장착 변경 요청 시 호출) ----
    public void RequestEquip(EquipmentId id)
    {
        // 권한이 있다면(호스트) 즉시 변경, 클라이언트라면 RPC로 변경 요청
        if (HasStateAuthority)
        {
            SetEquipped(id);
        }
        else if (HasInputAuthority)
        {
            RPC_RequestEquip(id);
        }
    }

    // ---- 내부 로직 ----

    // 역할(Role)에 따라 장착 가능한 아이템 목록을 갱신
    private void RefreshAllowedByRole(bool force = false)
    {
        var role = _pc ? _pc.Role : PlayerRole.Cleaner;

        if (!force && role == _lastRole) return;
        _lastRole = role;

        // 임포스터는 쓰레기 투척(TrashThrow) 가능, 청소부는 불가
        _allowed = (role == PlayerRole.Imposter)
            ? new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge, EquipmentId.TrashThrow }
            : new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge };

        // 현재 장착 중인 아이템이 허용 목록에 없다면 목록의 첫 번째 아이템으로 강제 교체
        if (HasStateAuthority && System.Array.IndexOf(_allowed, Equipped) < 0)
            SetEquipped(_allowed[0]);
    }

    private void EquipNext()
    {
        SetEquipped(NextId(Equipped));
    }

    private void EquipPrev()
    {
        SetEquipped(PrevId(Equipped));
    }

    private void EquipSlot(int index1Based)
    {
        // 인덱스는 1부터 시작하므로 0-based로 변환
        int arrayIndex = index1Based - 1;

        // 요청한 번호가 현재 허용된 아이템 목록(_allowed) 범위 내에 있을 때만 장착
        // (수정됨: 이제 역할에 따라 3번 슬롯도 접근 가능)
        if (arrayIndex >= 0 && arrayIndex < _allowed.Length)
        {
            SetEquipped(_allowed[arrayIndex]);
        }
    }

    private void SetEquipped(EquipmentId id)
    {
        Equipped = id; // [Networked] 변수를 수정하면 다음 Render() 프레임에서 감지됨
    }

    private EquipmentId NextId(EquipmentId cur)
    {
        // 현재 장착 중인 아이템이 허용 목록(_allowed)의 몇 번째인지 확인
        int idx = System.Array.IndexOf(_allowed, cur);

        // 만약 목록에 없다면(예: None) 첫 번째 아이템 반환
        if (idx < 0) return _allowed[0];

        // 다음 인덱스로 이동 (배열 길이를 넘어가면 0으로 순환)
        // (수정됨: 동적으로 배열 길이를 사용하므로 Imposter는 3개, Cleaner는 2개 순환)
        int nextIdx = (idx + 1) % _allowed.Length;

        return _allowed[nextIdx];
    }

    private EquipmentId PrevId(EquipmentId cur)
    {
        int idx = System.Array.IndexOf(_allowed, cur);

        if (idx < 0) return _allowed[0];

        // 이전 인덱스로 이동 (음수가 되면 배열 끝으로 순환)
        int prevIdx = (idx - 1 + _allowed.Length) % _allowed.Length;

        return _allowed[prevIdx];
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestEquip(EquipmentId id)
    {
        SetEquipped(id);
    }

    private void ForceRefreshVisual()
    {
        _lastEquipped = (EquipmentId)(-999); // 강제로 다르게 설정하여 갱신 유도
        ApplyVisual(Equipped);
        _lastEquipped = Equipped;
    }

    // 외부(PlayerInfo 등)에서 역할 타입이 다를 경우 변환하여 적용
    public void ApplyRole(PlayerInfo.Role sourceRole)
    {
        var mappedRole = sourceRole == PlayerInfo.Role.Saboteur
            ? PlayerRole.Imposter
            : PlayerRole.Cleaner;

        ApplyRole(mappedRole);
    }

    // 역할을 적용하고 장비 목록을 재설정
    public void ApplyRole(PlayerRole role)
    {
        if (_pc == null)
            _pc = GetComponent<NewPlayerController>();

        _pc?.ServerSetRole(role);

        RefreshAllowedByRole(force: true);

        // 만약 현재 장착 중인 아이템이 새로운 역할에서 불가능하다면 기본값으로 변경
        if (HasStateAuthority && _allowed.Length > 0 && System.Array.IndexOf(_allowed, Equipped) < 0)
        {
            SetEquipped(_allowed[0]);
        }

        ForceRefreshVisual();
    }

    private void ApplyVisual(EquipmentId id)
    {
        // 여기서만 실제 GameObject(모델)를 켜고 끔 (네트워크 로직 아님, 로컬 표현)
        if (handObj) handObj.SetActive(id == EquipmentId.Hand);
        if (spongeObj) spongeObj.SetActive(id == EquipmentId.Sponge);
        if (trashThrowObj) trashThrowObj.SetActive(id == EquipmentId.TrashThrow);
    }
}