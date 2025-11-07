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
    public HealthBar healthBar;
    public Button readyButton;

    [Networked, OnChangedRender(nameof(ColorChanged))]
    public Color NetworkedColor { get; set; }

    [Networked, OnChangedRender(nameof(OnNameChanged))]
    public NetworkString<_64> playerName { get; set; }

    [Networked, OnChangedRender(nameof(HealthChanged))]
    public float NetworkedHealth { get; set; } = 100;

    [Networked, OnChangedRender(nameof(OnReadyStateChanged))]
    public NetworkBool IsReady { get; private set; }

    // 역할 네트워크 변수
    [Networked, OnChangedRender(nameof(OnRoleChangedRender))]
    public Role PlayerRole { get; private set; }

    GameRuleManager _grm;
    bool _uiWired;

    public string cachedName = "(unnamed)";

    private bool IsHost => Runner != null && (Runner.IsSharedModeMasterClient || Runner.IsServer);

    void Start()
    {
        ResolveManager();

        if (healthBar != null)
            healthBar.SetMaxHealth(NetworkedHealth);

        // 호스트는 Ready 버튼 숨김, 클라는 표시
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

        if (healthBar != null)
            healthBar.SetMaxHealth(NetworkedHealth);

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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void DealDamageRpc(float damage)
    {
        NetworkedHealth = Mathf.Max(0f, NetworkedHealth - damage);
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

    // Host가 역할 세팅을 시도할 때: 내가 StateAuthority면 직접 세팅, 아니면 StateAuthority에게 RPC 요청
    public void SetRoleServer(Role role)
    {
        if (HasStateAuthority) PlayerRole = role;
        else RpcSetRole(role);
    }

    // 각 오브젝트의 StateAuthority가 역할을 기록
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcSetRole(Role role)
    {
        PlayerRole = role;
    }

    // 개인 안내 메시지: 이 오브젝트의 InputAuthority에게만 전송
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

    void HealthChanged()
    {
        if (healthBar != null)
            healthBar.UpdateHealth(NetworkedHealth);
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
        // 필요 시 역할별 로컬 연출을 여기서
        // if (nameDisplayTMP) nameDisplayTMP.color = PlayerRole == Role.Saboteur ? new Color(1f,0.5f,0.5f) : Color.white;
    }

    void ResolveManager()
    {
        if (!_grm) _grm = FindObjectOfType<GameRuleManager>(true);
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
