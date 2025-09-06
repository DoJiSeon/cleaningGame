using Fusion;
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
    Imposter = 1    // 방해자
}


public class EquipManagerNet : NetworkBehaviour
{
    [Header("로컬 장비 오브젝트 매핑 (ID -> GameObject)")]
    [SerializeField] private GameObject handObj;
    [SerializeField] private GameObject spongeObj;
    [SerializeField] private GameObject trashThrowObj;
    // 필요하면 딕셔너리화 가능

    // 네트워크로 동기화할 현재 장비
    [Networked] public EquipmentId Equipped { get; private set; }

    // ★ 역할/허용목록 관련
    private NewPlayerController _pc;
    private PlayerRole _lastRole;
    private EquipmentId[] _allowed = new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge };

    private EquipmentId _lastEquipped; // 로컬 캐시(뷰 갱신용)


    public override void Spawned()
    {
        _pc = GetComponent<NewPlayerController>();

        RefreshAllowedByRole(force: true); // ★ 스폰 시 1회 세팅
        // 스폰 시 현재 상태대로 뷰 맞춰놓기
        ForceRefreshVisual();
    }

    public override void FixedUpdateNetwork()
    {
        // StateAuthority에서만 입력을 처리해서 상태를 바꾸는 패턴
        if (HasStateAuthority && GetInput(out PlayerInputData input))
        {
            if (input.NextEquipPressed) EquipNext();
            else if (input.PrevEquipPressed) EquipPrev();
            else if (input.SelectSlotIndex > 0) EquipSlot(input.SelectSlotIndex);
        }
    }

    public override void Render()
    {
        // 모든 노드(소유/프록시)에서 네트워크 값 변화를 감지해 뷰만 갱신
        if (_lastEquipped != Equipped)
        {
            if (HasInputAuthority)
                Debug.Log($"[Equip] Equipped = {Equipped}");

            ApplyVisual(Equipped);
            _lastEquipped = Equipped;
        }
    }

    // ---- 공개 API (다른 스크립트가 호출) ----
    public void RequestEquip(EquipmentId id)
    {
        // 입력권한이 직접 바꿀 수 없으니 요청 → 상태권한이 적용
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
    private void RefreshAllowedByRole(bool force = false)
    {
        var role = _pc ? _pc.Role : PlayerRole.Cleaner;

        if (!force && role == _lastRole) return;
        _lastRole = role;

        _allowed = (role == PlayerRole.Imposter)
            ? new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge, EquipmentId.TrashThrow }
            : new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge };

        // 현재 장비가 허용 밖이면 서버가 안전한 장비로 교체
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
        // 슬롯을 쓰는 경우: index → EquipmentId 매핑해서 SetEquipped 호출
        // 예시로 1=Hand, 2=Sponge
        var id = index1Based == 1 ? EquipmentId.Hand :
                 index1Based == 2 ? EquipmentId.Sponge : EquipmentId.None;
        SetEquipped(id);
    }

    private void SetEquipped(EquipmentId id)
    {
        Equipped = id; // 네트워크 변수만 수정하면 나머지는 Render()에서 뷰 반영
    }

    private EquipmentId NextId(EquipmentId cur)
    {
        // 실제 보유 장비 목록/슬롯을 보고 순환
        if (cur == EquipmentId.None) return EquipmentId.Hand;
        if (cur == EquipmentId.Hand) return EquipmentId.Sponge;
        if (cur == EquipmentId.Sponge) return EquipmentId.Hand;
        return EquipmentId.Hand;
    }
    private EquipmentId PrevId(EquipmentId cur)
    {
        if (cur == EquipmentId.None) return EquipmentId.Sponge;
        if (cur == EquipmentId.Hand) return EquipmentId.Sponge;
        if (cur == EquipmentId.Sponge) return EquipmentId.Hand;
        return EquipmentId.Hand;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestEquip(EquipmentId id)
    {
        SetEquipped(id);
    }

    private void ForceRefreshVisual()
    {
        _lastEquipped = (EquipmentId)(-999); // 절대 같지 않게
        ApplyVisual(Equipped);
        _lastEquipped = Equipped;
    }

    private void ApplyVisual(EquipmentId id)
    {
        // 여기서는 "표시만" 책임진다 (네트워크 X, 로컬 전용)
        if (handObj) handObj.SetActive(id == EquipmentId.Hand);
        if (spongeObj) spongeObj.SetActive(id == EquipmentId.Sponge);
        if (trashThrowObj) trashThrowObj.SetActive(id == EquipmentId.TrashThrow);
    }


}
