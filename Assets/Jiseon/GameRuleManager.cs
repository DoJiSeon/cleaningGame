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
    public Button startButton;
    public Button readyButton;
    public TMP_Text statusText;
    public TMP_Text timerText;
    public TMP_Text playerCountText;

    [Networked] private TickTimer CountdownTimer { get; set; }
    [Networked] private TickTimer GameTimer { get; set; }
    [Networked] private bool GameStarted { get; set; }
    [Networked] private NetworkString<_32> StatusMessage { get; set; }

    [Networked] private int GameCoreCount { get; set; }

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


    // -----------------------------------------------
    //  NEW: Game Result Enum
    // -----------------------------------------------
    public enum GameResult
    {
        CleanerWin,
        ImpostorWin,
        NeedVoting
    }

    [Header("Meeting")]
    public Transform meetingPoint;



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
                    var local = GetLocalPlayer();
                    if (local != null) local.ToggleReady();
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
            if (local != null)
            {
                var tmp = readyButton.GetComponentInChildren<TMP_Text>();
                if (tmp) tmp.text = local.IsReady ? "Wait..." : "Ready";
            }
        }

        // -----------------------------------------------
        //  NEW: 외부 감시자 방식(원본 함수 건드리지 않음)
        // -----------------------------------------------
        if (IsHost)
        {
            CheckCoreWinState();
            CheckTimerEnd();
        }
    }



    void LateUpdate()
    {
        if (!_uiReady || !IsHost || startButton == null || GameStarted) return;
        startButton.interactable = AreAllClientsReady();
    }



    // -----------------------------------------------
    //  LOCAL MESSAGE
    // -----------------------------------------------
    public void ShowLocalStatus(string text, float seconds)
    {
        _localStatusOverride = text;
        _localStatusUntil = Time.time + Mathf.Max(0.1f, seconds);
    }



    // -----------------------------------------------
    //  PLAYER REGISTER
    // -----------------------------------------------
    public void RegisterPlayer(PlayerInfo pi)
    {
        if (!_players.Contains(pi))
        {
            _players.Add(pi);
            Debug.Log($"[GRM] Player Registered: {pi.cachedName}");
        }
        UpdateStartButtonState();
    }

    public void UnregisterPlayer(PlayerInfo pi)
    {
        if (_players.Contains(pi))
        {
            _players.Remove(pi);
            Debug.Log($"[GRM] Player Unregistered: {pi.cachedName}");
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
            startButton.interactable = true;
        else
            startButton.interactable = AreAllClientsReady();
    }



    private void OnStartClicked()
    {
        if (!IsHost) return;

        Debug.Log("[GRM] Countdown!");
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

        AssignRolesAndNotify();

        GameCoreCount = 0;
    }



    private void AssignRolesAndNotify()
    {
        _saboteurs.Clear();

        List<PlayerInfo> list = new(_players);
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
            bool isImpo = i < saboteurCount;
            var role = isImpo ? PlayerInfo.Role.Saboteur : PlayerInfo.Role.Cleaner;

            p.SetRoleServer(role);

            if (isImpo) _saboteurs.Add(p);

            p.RpcShowRoleMessage(role, roleMessageSeconds);
        }
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

    // ----------------------------------------------------------
    //  ⚠ ABSOLUTELY DO NOT MODIFY (요청사항: 팀원이 만든 코드 그대로 유지)
    // ----------------------------------------------------------
    public void AddGameCore_Server()
    {
        if (!IsHost) return;
        if (!GameStarted) return;

        GameCoreCount = GameCoreCount + 1;
        RpcNotifyGameCoreProgress_All(GameCoreCount);

        if (GameCoreCount >= 3)
        {
            RpcAnnounceCleanerWin_All();

            GameStarted = false;
            GameTimer = TickTimer.None;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcNotifyGameCoreProgress_All(int count)
    {
        ShowLocalStatus($"게임코어 {count}/3", 1.5f);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcAnnounceCleanerWin_All()
    {
        StatusMessage = "Cleaner 승리!";
        ShowLocalStatus("Cleaner 승리!", 3f);
        if (timerText) timerText.text = "끝";
    }


    // ----------------------------------------------------------------
    //  NEW: Core 3개 모인 후 '게임 종료 상태를 감시해서' 진짜 종료시키는 부분
    // ----------------------------------------------------------------
    private void CheckCoreWinState()
    {
        if (!GameStarted) return;

        // 위의 AddGameCore_Server는 건드릴 수 없으므로
        // 여기서 최종 승리 처리 실행
        if (GameCoreCount >= 3)
        {
            GameOver(GameResult.ImpostorWin, "Impostor Core Win");
        }
    }


    // ----------------------------------------------------------------
    //  NEW: 타이머 종료 처리
    // ----------------------------------------------------------------
    private void CheckTimerEnd()
    {
        if (!GameStarted) return;
        if (!GameTimer.Expired(Runner)) return;

        float gauge = 0f;

        if (SpawnManager.Instance != null)
            gauge = SpawnManager.Instance.GetDeSpawnPercentage();

        const float CLEAN_GOAL = 0.8f;

        if (gauge >= CLEAN_GOAL)
        {
            GameOver(GameResult.CleanerWin, "Cleaner Gauge Reached");
        }
        else
        {
            GameOver(GameResult.NeedVoting, "Gauge Insufficient → Voting");
        }
    }


    // ----------------------------------------------------------------
    //  NEW: Game Over Router
    // ----------------------------------------------------------------
    public void GameOver(GameResult result, string reason = "")
    {
        if (!IsHost) return;
        if (!GameStarted) return;

        Debug.Log($"[GRM] GameOver() / {result} / {reason}");

        GameStarted = false;
        GameTimer = TickTimer.None;

        switch (result)
        {
            case GameResult.CleanerWin:
                RpcShowMessage_All("청소부 승리!", 4f);
                break;

            case GameResult.ImpostorWin:
                RpcShowMessage_All("임포스터 승리!", 4f);
                break;

            case GameResult.NeedVoting:
                RpcShowMessage_All("시간 종료! 투표 시작합니다!", 4f);
                BeginVotingPhase();
                break;
        }
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcShowMessage_All(string msg, float seconds)
    {
        ShowLocalStatus(msg, seconds);
        StatusMessage = msg;
    }


    // ----------------------------------------------------------------
    //  NEW: Voting Phase start
    // ----------------------------------------------------------------
    private void BeginVotingPhase()
    {
        Debug.Log("[GRM] Voting Phase START");

        if (meetingPoint)
            TeleportAllPlayersToMeetingPoint();
    }


    // ----------------------------------------------------------------
    //  NEW: Teleport All
    // ----------------------------------------------------------------
    public void TeleportAllPlayersToMeetingPoint()
    {
        if (!IsHost) return;
        if (meetingPoint == null) return;

        foreach (var pi in _players)
        {
            if (pi == null || pi.Object == null) continue;

            pi.Object.transform.position = meetingPoint.position;
            pi.Object.transform.rotation = meetingPoint.rotation;

            var controller = pi.Object.GetComponent<NewPlayerController>();
            if (controller != null)
                controller.LockMovementForTeleport(0.2f);
        }
    }
}
