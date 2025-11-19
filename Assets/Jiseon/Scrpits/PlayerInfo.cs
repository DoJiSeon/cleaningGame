using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInfo : NetworkBehaviour
{
    public enum Role : byte
    {
        Cleaner = 0,
        Saboteur = 1
    }

    [Header("Render / UI")]
    public MeshRenderer MeshRenderer;
    public TMP_Text nameDisplayTMP;
    //public HealthBar healthBar;
    public Button readyButton;

    [Networked, OnChangedRender(nameof(ColorChanged))]
    public Color NetworkedColor { get; set; }

    [Networked, OnChangedRender(nameof(OnNameChanged))]
    public NetworkString<_64> playerName { get; set; }

    [Networked, OnChangedRender(nameof(OnReadyStateChanged))]
    public NetworkBool IsReady { get; private set; }

    // ���� ��Ʈ��ũ ����
    [Networked, OnChangedRender(nameof(OnRoleChangedRender))]
    public Role PlayerRole { get; private set; }

    GameRuleManager _grm;
    bool _uiWired;

    public string cachedName = "(unnamed)";

    private bool IsHost => Runner != null && (Runner.IsSharedModeMasterClient || Runner.IsServer);

    void Start()
    {
        ResolveManager();

        // ȣ��Ʈ�� Ready ��ư ����, Ŭ��� ǥ��
        if (HasInputAuthority && readyButton != null && !_uiWired)
        {
            if (!IsHost)
            {
                readyButton.onClick.AddListener(ToggleReady);
                readyButton.gameObject.SetActive(true);
                _uiWired = true;
            }
            else
            {
                readyButton.gameObject.SetActive(false);
            }
        }
        else if (readyButton != null)
        {
            readyButton.gameObject.SetActive(false);
        }
    }

    public override void Spawned()
    {
        ResolveManager();

        cachedName = playerName.ToString();

        if (nameDisplayTMP != null)
        {
            nameDisplayTMP.text = cachedName;
            transform.gameObject.name = cachedName;
        }

        if (_grm) _grm.RegisterPlayer(this);
    }

    void OnDestroy()
    {
        if (_grm) _grm.UnregisterPlayer(this);
    }

    public void SetPlayerName(string name)
    {
        if (HasStateAuthority)
        {
            playerName = name;
            cachedName = name;
            if (nameDisplayTMP) nameDisplayTMP.text = name;
        }
        else
        {
            RPC_SetPlayerName(name);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SetPlayerName(string name)
    {
        playerName = name;
        cachedName = name;
        if (nameDisplayTMP) nameDisplayTMP.text = name;
    }

    public void ToggleReady()
    {
        if (!HasInputAuthority) return;
        RPC_ToggleReady(!IsReady);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    void RPC_ToggleReady(bool newState)
    {
        IsReady = newState;

        if (!_grm) ResolveManager();
        if (_grm) _grm.UpdateStartButtonState();
    }

    // Host�� ���� ������ �õ��� ��: ���� StateAuthority�� ���� ����, �ƴϸ� StateAuthority���� RPC ��û
    public void SetRoleServer(Role role)
    {
        if (HasStateAuthority)
        {
            PlayerRole = role;
            ApplyRoleToEquip(role);
        }
        else
        {
            RpcSetRole(role);
        }
    }

    // �� ������Ʈ�� StateAuthority�� ������ ���
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcSetRole(Role role)
    {
        PlayerRole = role;
        ApplyRoleToEquip(role);
    }

    // ���� �ȳ� �޽���: �� ������Ʈ�� InputAuthority���Ը� ����
    [Rpc(RpcSources.All, RpcTargets.InputAuthority)]
    public void RpcShowRoleMessage(Role role, float seconds)
    {
        var grm = GameRuleManager.Instance;
        if (grm == null) return;

        string msg = role == Role.Saboteur ? "You are a Saboteur." : "You are a Cleaner.";
        grm.ShowLocalStatus(msg, seconds);
    }

    public void ColorChanged()
    {
        if (MeshRenderer && MeshRenderer.material)
            MeshRenderer.material.color = NetworkedColor;
    }

    public void OnNameChanged()
    {
        cachedName = playerName.ToString();
        if (nameDisplayTMP)
            nameDisplayTMP.text = cachedName;
    }


    void OnReadyStateChanged()
    {
        if (HasInputAuthority && readyButton != null)
        {
            var tmp = readyButton.GetComponentInChildren<TMP_Text>();
            if (tmp) tmp.text = IsReady ? "Wait..." : "Ready";
        }

        if (nameDisplayTMP)
            nameDisplayTMP.color = IsReady ? Color.green : Color.white;
    }

    void OnRoleChangedRender()
    {
        // �ʿ� �� ���Һ� ���� ������ ���⼭
        // if (nameDisplayTMP) nameDisplayTMP.color = PlayerRole == Role.Saboteur ? new Color(1f,0.5f,0.5f) : Color.white;
    }

    void ResolveManager()
    {
        if (!_grm) _grm = FindObjectOfType<GameRuleManager>(true);
    }

    private void ApplyRoleToEquip(Role role)
    {
        var equip = GetComponent<EquipManagerNet>();
        if (equip != null)
            equip.ApplyRole(role);
    }

    void OnDisable()
    {
        if (_uiWired && readyButton != null)
        {
            readyButton.onClick.RemoveListener(ToggleReady);
            _uiWired = false;
        }
    }
}
