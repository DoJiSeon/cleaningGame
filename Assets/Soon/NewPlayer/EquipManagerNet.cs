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
    Cleaner = 0,    // û�Һ�
    Imposter = 1    // ������
}


public class EquipManagerNet : NetworkBehaviour
{
    [Header("���� ��� ������Ʈ ���� (ID -> GameObject)")]
    [SerializeField] private GameObject handObj;
    [SerializeField] private GameObject spongeObj;
    [SerializeField] private GameObject trashThrowObj;
    // �ʿ��ϸ� ��ųʸ�ȭ ����

    // ��Ʈ��ũ�� ����ȭ�� ���� ���
    [Networked] public EquipmentId Equipped { get; private set; }

    // �� ����/����� ����
    private NewPlayerController _pc;
    private PlayerRole _lastRole;
    private EquipmentId[] _allowed = new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge };

    private EquipmentId _lastEquipped; // ���� ĳ��(�� ���ſ�)


    public override void Spawned()
    {
        _pc = GetComponent<NewPlayerController>();

        RefreshAllowedByRole(force: true); // �� ���� �� 1ȸ ����
        // ���� �� ���� ���´�� �� �������
        ForceRefreshVisual();
    }

    public override void FixedUpdateNetwork()
    {
        RefreshAllowedByRole();

        // StateAuthority������ �Է��� ó���ؼ� ���¸� �ٲٴ� ����
        if (HasStateAuthority && GetInput(out PlayerInputData input))
        {
            if (input.NextEquipPressed) EquipNext();
            else if (input.PrevEquipPressed) EquipPrev();
            else if (input.SelectSlotIndex > 0) EquipSlot(input.SelectSlotIndex);
        }
    }

    public override void Render()
    {
        // ��� ���(����/���Ͻ�)���� ��Ʈ��ũ �� ��ȭ�� ������ �丸 ����
        if (_lastEquipped != Equipped)
        {
            if (HasInputAuthority)
                Debug.Log($"[Equip] Equipped = {Equipped}");

            ApplyVisual(Equipped);
            _lastEquipped = Equipped;
        }
    }

    // ---- ���� API (�ٸ� ��ũ��Ʈ�� ȣ��) ----
    public void RequestEquip(EquipmentId id)
    {
        // �Է±����� ���� �ٲ� �� ������ ��û �� ���±����� ����
        if (HasStateAuthority)
        {
            SetEquipped(id);
        }
        else if (HasInputAuthority)
        {
            RPC_RequestEquip(id);
        }
    }

    // ---- ���� ���� ----
    private void RefreshAllowedByRole(bool force = false)
    {
        var role = _pc ? _pc.Role : PlayerRole.Cleaner;

        if (!force && role == _lastRole) return;
        _lastRole = role;

        _allowed = (role == PlayerRole.Imposter)
            ? new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge, EquipmentId.TrashThrow }
            : new EquipmentId[] { EquipmentId.Hand, EquipmentId.Sponge };

        // ���� ��� ��� ���̸� ������ ������ ���� ��ü
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
        // ������ ���� ���: index �� EquipmentId �����ؼ� SetEquipped ȣ��
        // ���÷� 1=Hand, 2=Sponge
        var id = index1Based == 1 ? EquipmentId.Hand :
                 index1Based == 2 ? EquipmentId.Sponge : EquipmentId.None;
        SetEquipped(id);
    }

    private void SetEquipped(EquipmentId id)
    {
        Equipped = id; // ��Ʈ��ũ ������ �����ϸ� �������� Render()���� �� �ݿ�
    }

    private EquipmentId NextId(EquipmentId cur)
    {
        // ���� ���� ��� ���/������ ���� ��ȯ
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
        _lastEquipped = (EquipmentId)(-999); // ���� ���� �ʰ�
        ApplyVisual(Equipped);
        _lastEquipped = Equipped;
    }

    public void ApplyRole(PlayerInfo.Role sourceRole)
    {
        var mappedRole = sourceRole == PlayerInfo.Role.Saboteur
            ? PlayerRole.Imposter
            : PlayerRole.Cleaner;

        ApplyRole(mappedRole);
    }

    public void ApplyRole(PlayerRole role)
    {
        if (_pc == null)
            _pc = GetComponent<NewPlayerController>();

        _pc?.ServerSetRole(role);

        RefreshAllowedByRole(force: true);

        if (HasStateAuthority && _allowed.Length > 0 && System.Array.IndexOf(_allowed, Equipped) < 0)
        {
            SetEquipped(_allowed[0]);
        }

        ForceRefreshVisual();
    }

    private void ApplyVisual(EquipmentId id)
    {
        // ���⼭�� "ǥ�ø�" å������ (��Ʈ��ũ X, ���� ����)
        if (handObj) handObj.SetActive(id == EquipmentId.Hand);
        if (spongeObj) spongeObj.SetActive(id == EquipmentId.Sponge);
        if (trashThrowObj) trashThrowObj.SetActive(id == EquipmentId.TrashThrow);
    }


}
