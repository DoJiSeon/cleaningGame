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

    [Header("Clean Gauge UI")]
    [SerializeField] private TMP_Text cleanGaugeText;
    // 제재 패널 열림 여부 (OptionButtonUI에서 알려줌)
    [HideInInspector] public bool isPenaltyPanelOpen = false;
    // 이전에 알려준 10단위 청소율 기록
    private int lastNotifiedPercent10 = 0;

    [Networked] private TickTimer CountdownTimer { get; set; }
    [Networked] private TickTimer GameTimer { get; set; }
    [Networked] private bool GameStarted { get; set; }
    [Networked] private NetworkString<_32> StatusMessage { get; set; }

    // 게임코어 진행상태
    [Networked] private int GameCoreCount { get; set; }

    // ======== ★ 텔포트 위치 추가 ========
    [Header("Teleport Settings")]
    public Transform meetingTeleportPoint;

    public bool IsGameLive
    {
        get
        {
            if (Object == null || Object.Runner == null)
                return false;
            return GameStarted;
        }
    }

    private readonly List<PlayerInfo> _players = new();
    private readonly List<PlayerInfo> _saboteurs = new();
    private bool _uiReady;

    private const int COUNTDOWN_DURATION = 3;
    private const int GAME_DURATION = 1800;

    [SerializeField] private float roleMessageSeconds = 3f;

    private string _localStatusOverride = null;
    private float _localStatusUntil = 0f;

    private bool IsHost => Runner != null && (Runner.IsSharedModeMasterClient || Runner.IsServer);

    // ======== END REASON ENUM ========
    public enum EndReason
    {
        TimeUp,
        GameCoreWin
    }

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
        yield return null;

        bool isHost = IsHost;

        if (isHost)
        {
            if (startButton) startButton.gameObject.SetActive(true);
            if (readyButton) readyButton.gameObject.SetActive(false);

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnStartClicked);
                startButton.interactable = false;
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

        // === ★ 청소 게이지 표시 + 10% 단위 알림 ===
        if (SpawnManager.Instance != null)
        {
            float percent = SpawnManager.Instance.GetCleanPercentage();

            // 1) 평소에는 "??%"
            //    제재 패널 열렸을 때만 실제 퍼센트 공개
            if (cleanGaugeText != null)
            {
                if (isPenaltyPanelOpen)
                {
                    cleanGaugeText.text = $"청소 게이지: {percent:0}%";
                }
                else
                {
                    cleanGaugeText.text = "청소 게이지: ??%";
                }
            }

            // 2) 10% 단위마다 ShowLocalStatus 호출
            int percent10 = Mathf.FloorToInt(percent / 10f) * 10;
            if (percent10 >= 10 && percent10 <= 100 && percent10 != lastNotifiedPercent10)
            {
                ShowLocalStatus($"청소 게이지 {percent10}% 달성!", 1.5f);
                lastNotifiedPercent10 = percent10;
            }
        }



        if (statusText)
        {
            if (Time.time < _localStatusUntil && !string.IsNullOrEmpty(_localStatusOverride))
                statusText.text = _localStatusOverride;
            else
                statusText.text = StatusMessage.ToString();
        }

        // === ★ 시간 종료 체크 ===
        if (GameStarted && GameTimer.IsRunning && GameTimer.Expired(Runner))
        {
            EndGame(EndReason.TimeUp);
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

    void LateUpdate()
    {
        if (!_uiReady || !IsHost || startButton == null || GameStarted) return;
        startButton.interactable = AreAllClientsReady();
    }

    public void ShowLocalStatus(string text, float seconds)
    {
        _localStatusOverride = text;
        _localStatusUntil = Time.time + Mathf.Max(0.1f, seconds);
    }

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

            if (Runner != null && p.Object.InputAuthority == Runner.LocalPlayer) continue;

            clientCount++;
            if (p.IsReady) readyClients++;
        }

        if (clientCount == 0)
            return true;

        return clientCount > 0 && readyClients == clientCount;
    }

    public void UpdateStartButtonState()
    {
        if (!IsHost || startButton == null) return;

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

        // ★ 텔포트
        TeleportAllPlayersToMeetingPoint();
        StartGame();
    }

    private void StartGame()
    {
        if (!IsHost) return;

        GameStarted = true;
        GameTimer = TickTimer.CreateFromSeconds(Runner, GAME_DURATION);
        if (timerText) timerText.text = "00:00";

        AssignRolesAndNotify();

        GameCoreCount = 0;
    }

    private void AssignRolesAndNotify()
    {
        _saboteurs.Clear();

        List<PlayerInfo> list = new List<PlayerInfo>(_players);
        list.RemoveAll(x => x == null || x.Object == null);

        int n = list.Count;
        if (n == 0) return;

        int saboteurCount = Mathf.Max(1, Mathf.FloorToInt(n / 3f));

        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        for (int i = 0; i < n; i++)
        {
            var p = list[i];
            bool isSaboteur = i < saboteurCount;

            var role = isSaboteur ? PlayerInfo.Role.Saboteur : PlayerInfo.Role.Cleaner;

            p.SetRoleServer(role);

            if (isSaboteur) _saboteurs.Add(p);

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


    // =============== ★ 텔포트 기능 ===============

    public void TeleportAllPlayersToMeetingPoint()
    {
        if (!IsHost) return;
        if (meetingTeleportPoint == null)
        {
            Debug.LogWarning("[GRM] meetingTeleportPoint 설정 안 됨!");
            return;
        }

        foreach (var p in _players)
        {
            if (p == null || p.Object == null) continue;

            p.Object.transform.position = meetingTeleportPoint.position;
            p.Object.transform.rotation = meetingTeleportPoint.rotation;
        }

        Debug.Log("[GRM] 모든 플레이어 텔포트 완료!");
    }


    // =============== ★ Game Core 관련 ===============

    public void AddGameCore_Server()
    {
        if (!IsHost) return;
        if (!GameStarted) return;

        GameCoreCount = GameCoreCount + 1;
        RpcNotifyGameCoreProgress_All(GameCoreCount);

        if (GameCoreCount >= 3)
        {
            // ★ 게임코어 3개 → 게임 종료
            EndGame(EndReason.GameCoreWin);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcNotifyGameCoreProgress_All(int count)
    {
        ShowLocalStatus($"게임코어 {count}/3", 1.5f);
    }


    // =============== ★ EndGame 시스템 ===============

    public void EndGame(EndReason reason)
    {
        if (!IsHost) return;

        GameStarted = false;
        GameTimer = TickTimer.None;

        switch (reason)
        {
            case EndReason.GameCoreWin:
                AnnounceCleanerWin("게임코어 3개 달성");
                break;

            case EndReason.TimeUp:
                HandleEndByTimeUp();
                break;
        }
    }

    private void AnnounceCleanerWin(string detail)
    {
        StatusMessage = "Cleaner 승리!";
        ShowLocalStatus($"Cleaner 승리!\n({detail})", 4f);
        if (timerText) timerText.text = "끝";

        Debug.Log($"[GRM] Cleaner 승리: {detail}");
    }

    private void HandleEndByTimeUp()
    {
        float percent = SpawnManager.Instance.GetCleanPercentage();
        float goal = 70f;  // 원하는 목표치 (필요하면 public 변수로 빼도 됨)

        if (percent >= goal)
        {
            AnnounceCleanerWin($"시간 종료 + 청소율 {percent:0}% 달성");
        }
        else
        {
            //투표를 시작합니다잉
            ShowLocalStatus($"회의를 시작한다.", 1.5f);

        }
    }


    // =============== 기존 Cleaner Win RPC (사용 X) ===============
    // 이제 EndGame이 처리하므로 호출하지 않음
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcAnnounceCleanerWin_All()
    {
        StatusMessage = "Cleaner 승리!";
        ShowLocalStatus("Cleaner 승리!", 3f);
        if (timerText) timerText.text = "끝";
    }
}
