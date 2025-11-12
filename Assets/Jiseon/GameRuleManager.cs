using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameRuleManager : NetworkBehaviour
{
    public static GameRuleManager Instance;

    [Header("UI References")]
    public Button startButton;         // 호스트 전용
    public Button readyButton;         // 클라 전용
    public TMP_Text statusText;        // 중앙 상태 (카운트다운 / Game Start!)
    public TMP_Text timerText;         // 게임 시간
    public TMP_Text playerCountText;   // 현재 인원 표시

    [Networked] private TickTimer CountdownTimer { get; set; }
    [Networked] private TickTimer GameTimer { get; set; }
    [Networked] private bool GameStarted { get; set; }
    [Networked] private NetworkString<_32> StatusMessage { get; set; }


    // 추가
    public bool IsGameLive
    {
        get
        {
            // Runner나 NetworkObject가 준비되지 않았으면 false 처리
            if (Object == null || Object.Runner == null)
                return false;
            return GameStarted;
        }
    }

    private readonly List<PlayerInfo> _players = new();
    private readonly List<PlayerInfo> _saboteurs = new(); // Host 보관용
    private bool _uiReady;

    private const int COUNTDOWN_DURATION = 3;
    private const int GAME_DURATION = 1800; // 30분

    // 역할 메시지 표시 시간
    [SerializeField] private float roleMessageSeconds = 3f;

    // 로컬 전용 상태 텍스트 오버라이드
    private string _localStatusOverride = null;
    private float _localStatusUntil = 0f;

    private bool IsHost => Runner != null && (Runner.IsSharedModeMasterClient || Runner.IsServer);

    void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        StartCoroutine(SetupUIDelayed());
    }

    private IEnumerator SetupUIDelayed()
    {
        yield return null; // Runner 초기화 대기

        bool isHost = IsHost;

        if (isHost)
        {
            if (startButton) startButton.gameObject.SetActive(true);
            if (readyButton) readyButton.gameObject.SetActive(false);

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnStartClicked);
                startButton.interactable = false; // 초기 비활성화
            }
        }
        else
        {
            if (startButton) startButton.gameObject.SetActive(false);
            if (readyButton) readyButton.gameObject.SetActive(true);

            if (readyButton != null)
            {
                readyButton.onClick.RemoveAllListeners();
                readyButton.onClick.AddListener(() =>
                {
                    var localPlayer = GetLocalPlayer();
                    if (localPlayer != null) localPlayer.ToggleReady();
                });
            }
        }

        _uiReady = true;
    }

    void Update()
    {
        if (!_uiReady) return;

        if (GameStarted)
        {
            if (startButton) startButton.gameObject.SetActive(false);
            if (readyButton) readyButton.gameObject.SetActive(false);
            if (playerCountText) playerCountText.gameObject.SetActive(false);
        }
        else
        {
            int currentPlayers = _players.Count;
            int maxPlayers = Runner.SessionInfo != null ? Runner.SessionInfo.MaxPlayers : 0;
            if (playerCountText) playerCountText.text = $"{currentPlayers} / {maxPlayers}";
        }

        // 로컬 오버라이드 > 네트워크 메시지
        if (statusText)
        {
            if (Time.time < _localStatusUntil && !string.IsNullOrEmpty(_localStatusOverride))
                statusText.text = _localStatusOverride;
            else
                statusText.text = StatusMessage.ToString();
        }

        if (GameStarted && GameTimer.IsRunning)
        {
            float elapsed = GAME_DURATION - GameTimer.RemainingTime(Runner).GetValueOrDefault();
            int minutes = Mathf.FloorToInt(elapsed / 60);
            int seconds = Mathf.FloorToInt(elapsed % 60);
            if (timerText) timerText.text = $"{minutes:00}:{seconds:00}";
        }

        if (!GameStarted && !IsHost)
        {
            var local = GetLocalPlayer();
            if (local != null && readyButton != null)
            {
                var tmp = readyButton.GetComponentInChildren<TMP_Text>();
                if (tmp) tmp.text = local.IsReady ? "Wait..." : "Ready";
            }
        }
    }

    // 호스트 안전망 폴링
    void LateUpdate()
    {
        if (!_uiReady || !IsHost || startButton == null || GameStarted) return;
        startButton.interactable = AreAllClientsReady();
    }

    // 로컬 전용 상태 문구 표시 API
    public void ShowLocalStatus(string text, float seconds)
    {
        _localStatusOverride = text;
        _localStatusUntil = Time.time + Mathf.Max(0.1f, seconds);
    }

    // 플레이어 등록/해제
    public void RegisterPlayer(PlayerInfo pi)
    {
        if (!_players.Contains(pi))
        {
            _players.Add(pi);
            Debug.Log($"[GRM] 플레이어 등록됨: {pi.cachedName} / {_players.Count}명");
        }
        UpdateStartButtonState();
    }

    public void UnregisterPlayer(PlayerInfo pi)
    {
        if (_players.Contains(pi))
        {
            _players.Remove(pi);
            string safeName = pi ? pi.cachedName : "(null)";
            Debug.Log($"[GRM] 플레이어 해제됨: {safeName} / {_players.Count}명");
        }

        _players.RemoveAll(x => x == null || x.Object == null);
        UpdateStartButtonState();
    }

    private bool AreAllClientsReady()
    {
        int clientCount = 0;
        int readyClients = 0;

        _players.RemoveAll(x => x == null || x.Object == null);

        foreach (var p in _players)
        {
            if (p == null || p.Object == null) continue;

            // 호스트 자신 제외
            if (Runner != null && p.Object.InputAuthority == Runner.LocalPlayer) continue;

            clientCount++;
            if (p.IsReady) readyClients++;
        }

        // 혼자(호스트만)일 경우에도 바로 true 리턴
        if (clientCount == 0)
            return true;

        return clientCount > 0 && readyClients == clientCount;
    }

    public void UpdateStartButtonState()
    {
        if (!IsHost || startButton == null) return;
        //startButton.interactable = AreAllClientsReady();

        // 플레이어가 1명(호스트뿐)이어도 시작 가능하게
        if (_players.Count <= 1)
        {
            startButton.interactable = true;
        }
        else
        {
            startButton.interactable = AreAllClientsReady();
        }
    }

    private void OnStartClicked()
    {
        if (!IsHost) return;
        Debug.Log("[GRM] 게임 시작 카운트다운 시작!");
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        int count = COUNTDOWN_DURATION;

        while (count > 0)
        {
            StatusMessage = count.ToString();
            yield return new WaitForSeconds(1f);
            count--;
        }

        StatusMessage = "Game Start!";
        yield return new WaitForSeconds(1f);

        StatusMessage = "";
        StartGame();
    }

    private void StartGame()
    {
        if (!IsHost) return;

        GameStarted = true;
        GameTimer = TickTimer.CreateFromSeconds(Runner, GAME_DURATION);
        if (timerText) timerText.text = "00:00";

        // 역할 배정 및 개별 안내
        AssignRolesAndNotify();
    }

    private void AssignRolesAndNotify()
    {
        _saboteurs.Clear();

        // 유효한 플레이어 목록
        List<PlayerInfo> list = new List<PlayerInfo>(_players);
        list.RemoveAll(x => x == null || x.Object == null);

        int n = list.Count;
        if (n == 0) return;

        // Saboteur 수: 총원의 1/3, 최소 1
        int saboteurCount = Mathf.Max(1, Mathf.FloorToInt(n / 3f));

        // Fisher-Yates shuffle
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        // 배정
        for (int i = 0; i < n; i++)
        {
            var p = list[i];
            bool isSaboteur = i < saboteurCount;

            var role = isSaboteur ? PlayerInfo.Role.Saboteur : PlayerInfo.Role.Cleaner;

            // 역할 세팅(권한 없는 경우 각자의 StateAuthority에게 RPC로 위임)
            p.SetRoleServer(role);

            if (isSaboteur) _saboteurs.Add(p);

            // 각자에게만 안내 문구 전송
            p.RpcShowRoleMessage(role, roleMessageSeconds);
        }

        Debug.Log($"[GRM] Roles assigned. Saboteurs: {saboteurCount}/{n}");
    }

    private PlayerInfo GetLocalPlayer()
    {
        foreach (var p in _players)
        {
            if (p != null && p.HasInputAuthority)
                return p;
        }
        return null;
    }
}
