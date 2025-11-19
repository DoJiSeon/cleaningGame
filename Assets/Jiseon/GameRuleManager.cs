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

    // ===== ★ SpawnManager UI는 사용하지 않지만 10% 알림 유지 =====
    private int lastNotifiedPercent10 = 0;

    [Networked] private TickTimer CountdownTimer { get; set; }
    [Networked] private TickTimer GameTimer { get; set; }
    [Networked] private bool GameStarted { get; set; }
    [Networked] private NetworkString<_32> StatusMessage { get; set; }

    // 게임코어 진행상태
    [Networked] private int GameCoreCount { get; set; }

    // ======== ★ 텔포트 위치 ========
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

        // ========== ★ 10% 단위 청소율 안내만 유지 ==========
        if (SpawnManager.Instance != null)
        {
            float percent = SpawnManager.Instance.GetCleanPercentage();

            int percent10 = Mathf.FloorToInt(percent / 10f) * 10;

            if (percent10 >= 10 && percent10 <= 100 && percent10 != lastNotifiedPercent10)
            {
                ShowLocalStatus($"청소 게이지 {percent10}% 달성!", 1.5f);
                lastNotifiedPercent10 = percent10;
            }
        }

        // ===== 상태 메시지 UI =====
        if (statusText)
        {
            if (Time.time < _localStatusUntil && !string.IsNullOrEmpty(_localStatusOverride))
                statusText.text = _localStatusOverride;
            else
                statusText.text = StatusMessage.ToString();
        }

        // ===== 시간 종료 체크 =====
        if (GameStarted && GameTimer.IsRunning && GameTimer.Expired(Runner))
        {
            EndGame(EndReason.TimeUp);
        }

        // ===== Ready 버튼 텍스트 =====
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

    // =============== Player 등록 ===============
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

        return readyClients == clientCount;
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

    // ========== ★ GetLocalPlayer 복구 ==========
    private PlayerInfo GetLocalPlayer()
    {
        foreach (var p in _players)
        {
            if (p != null && p.HasInputAuthority)
                return p;
        }
        return null;
    }

    public void TeleportAllPlayersToMeetingPoint()
    {
        if (!IsHost) return;
        if (meetingTeleportPoint == null) return;

        // 호스트에서만 한 번 호출 → 모든 클라에서 로컬 플레이어 텔포
        RpcTeleportAllToMeetingPoint(meetingTeleportPoint.position, meetingTeleportPoint.rotation);
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcTeleportAllToMeetingPoint(Vector3 pos, Quaternion rot)
    {
        // 각 클라이언트에서 자기 플레이어만 텔포
        var localController = FindLocalPlayerController();
        if (localController != null)
        {
            localController.TeleportToPosition(pos, rot);
            Debug.Log("[GRM] RpcTeleportAllToMeetingPoint → 로컬 플레이어 텔포 완료");
        }
        else
        {
            Debug.Log("[GRM] RpcTeleportAllToMeetingPoint → 로컬 플레이어 컨트롤러를 찾지 못함");
        }
    }


    private NewPlayerController FindLocalPlayerController()
    {
        var all = FindObjectsOfType<NewPlayerController>(true);
        foreach (var c in all)
        {
            var no = c.GetComponent<NetworkObject>();
            if (no != null && no.HasInputAuthority)
            {
                return c;
            }
        }
        return null;
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
        float goal = 70f;

        if (percent >= goal)
        {
            AnnounceCleanerWin($"시간 종료 + 청소율 {percent:0}% 달성");
        }
        else
        {
            ShowLocalStatus($"회의를 시작한다.", 1.5f);
        }
    }
}
