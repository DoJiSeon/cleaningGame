using Fusion;
using TMPro;
using UnityEngine;

public class PlayerInfo : NetworkBehaviour
{
    public MeshRenderer MeshRenderer;
    public TMP_Text nameDisplayTMP;

    // 추가: HealthBar UI 참조
    public HealthBar healthBar;

    [Networked, OnChangedRender(nameof(ColorChanged))]
    public Color NetworkedColor { get; set; }

    [Networked, OnChangedRender(nameof(OnNameChanged))]
    public NetworkString<_64> playerName { get; set; }

    [Networked, OnChangedRender(nameof(EISPressed))]
    public NetworkBool EIsPressed { get; set; }

    [Networked, OnChangedRender(nameof(HealthChanged))]
    public float NetworkedHealth { get; set; } = 100;

    private ChangeDetector _changeDetector;

    void Start()
    {
        // 시작 시 최대 체력 UI 설정
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(NetworkedHealth);
        }
    }

    void Update()
    {
        // 로컬 입력만 감지 (InputAuthority가 있는 경우)
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.E))
        {
            // E 키를 누르면 색상 변경 RPC 실행
            NetworkedColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);
        }

        // 현재 E키 누름 상태를 네트워크로 반영
        if (HasInputAuthority)
            EIsPressed = Input.GetKey(KeyCode.E);
    }

    public override void Spawned()
    {
        nameDisplayTMP.text = playerName.ToString();
        transform.gameObject.name = nameDisplayTMP.text;

        // 스폰 시 HealthBar 초기화
        if (healthBar != null)
        {
            healthBar.SetMaxHealth(NetworkedHealth);
        }
    }

    public void SetPlayerName(string name)
    {
        if (HasStateAuthority)
        {
            playerName = name;
            nameDisplayTMP.text = name;
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
        nameDisplayTMP.text = name;
    }


    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void DealDamageRpc(float damage)
    {
        Debug.Log("Received DealDamageRpc on StateAuthority, modifying Networked variable");
        NetworkedHealth -= damage;
    }

    public void EISPressed()
    {
        Debug.Log("EIsPressed = " + EIsPressed);
    }

    public void ColorChanged()
    {
        MeshRenderer.material.color = NetworkedColor;
    }

    public void OnNameChanged()
    {
        nameDisplayTMP.text = playerName.ToString();
    }

    // HealthBar와 연동되도록 수정
    void HealthChanged()
    {
        Debug.Log($"Health changed to: {NetworkedHealth}");

        if (healthBar != null)
        {
            healthBar.UpdateHealth(NetworkedHealth);
        }
    }
}
