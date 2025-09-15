using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;

public class MeetingDirector : NetworkBehaviour
{

    [Header("회의 끝나면 씬 전환")]
    [SerializeField] private bool changeSceneAfterMeeting = true;
    [SerializeField] private float changeSceneDelay = 2.0f;     // 결과패널 보여줄 시간과 맞추기
    [SerializeField] private Multiplayerchat chatManager;        // 인스펙터에 'chat manager' 드래그

    private bool _sceneChangeTriggered;

    // === [추가] 공용 연출 훅 ===
    [Header("결과 연출")]

    // === 컷씬/카메라 ===
    [SerializeField] private UnityEngine.Playables.PlayableDirector executionTimeline; // 타임라인
    [SerializeField] private Cinemachine.CinemachineVirtualCamera vcam;               // 지목자 포커스용 VCam

    // === 결과 패널(UI) ===
    [SerializeField] private GameObject resultPanelLocal;      // 비활성 시작
    [SerializeField] private TMP_Text resultTitleText;         // "정화 성공!" / "방해자 성공!" 등
    [SerializeField] private TMP_Text resultDetailText;        // "(플레이어이름)은 방해자였습니다." 등
    [SerializeField] private TMP_Text resultAccusedNameText;   // "지목: Player 2" 같은 표시(원하면)

    [Header("회의 배너")]
    [SerializeField] private GameObject finalVoteBanner;   // 배너 루트(= FinalVoteBanner)
    [SerializeField] private TMP_Text finalVoteText;       // 배너 안의 텍스트
    [SerializeField] private string meetingBannerText = "최종 투표시간입니다";
    [SerializeField] private string revoteBannerText = "재투표 시간입니다";

    [Header("재투표")]
    [SerializeField] private float revoteDuration = 15f; // 재투표 시간

    // 투표 집계용(서버 전용 사용)
    private readonly Dictionary<PlayerRef, PlayerRef> _votes = new();
    [SerializeField] private VoteUI voteUI;

    [Header("라운드/회의 타이머 UI")]
    [SerializeField] private TextMeshProUGUI roundTimerText;   // 상단 라운드 타이머
    [SerializeField] private TextMeshProUGUI meetingTimerText; // 회의 타이머(회의 패널 안)

    [Header("라운드/회의 설정")]
    public float roundDuration = 150f;                 // 제한시간(초)
    [Range(0f, 1f)] public float requiredPercent = 0.90f; // 90%
    public Transform meetingPoint;                     // 회의장 TP 지점(바닥 Transform)

    [Header("로컬 UI 훅(선택)")]
    public GameObject meetingUI;           // 회의 중 표시될 UI 루트(투표 패널 등)

    [Header("플레이어 제어 봉인 (옵션)")]
    public bool freezeMovementDuringMeeting = true;

    // ===== 네트워크 상태 =====
    [Networked] private TickTimer RoundTimer { get; set; }
    [Networked] private bool IsMeetingActive { get; set; }
    [Networked] private TickTimer MeetingTimer { get; set; }

    // 회의 시간(초)
    public float meetingDuration = 20f;
    [SerializeField] private bool _meetingOnCached = false;

    // 외부(UI 등)에서 안전하게 읽기 위한 getter
    public bool IsMeetingOn => _meetingOnCached;

    // UI 업데이트 루프
    private Coroutine _uiTimerRoutine;

    private bool _wasLive = false;
    private bool IsGameLiveNow()
    {
        return GameRuleManager.Instance != null && GameRuleManager.Instance.IsGameLive;
    }

    // --- 라이프사이클 ---
    public override void Spawned()
    {
        _meetingOnCached = false;
        if (Object.HasStateAuthority)
            RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);

        if (_uiTimerRoutine == null)
            _uiTimerRoutine = StartCoroutine(CoUpdateTimersUI());

        if (Runner != null) Runner.AddCallbacks(new RunnerHooks(this));

        HideFinalVoteBanner();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _meetingOnCached = false;
        if (_uiTimerRoutine != null) { StopCoroutine(_uiTimerRoutine); _uiTimerRoutine = null; }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        bool live = IsGameLiveNow();

        // 게임 시작 전: 아무 것도 돌지 않음
        if (!live)
        {
            if (IsMeetingActive)
            {
                IsMeetingActive = false;
                _votes.Clear();
            }
            RoundTimer = TickTimer.None;
            _wasLive = false;
            return;
        }

        // 게임 시작된 순간에만 타이머 스타트
        if (!_wasLive && live)
        {
            RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
        }

        // 라운드 타임아웃
        if (!IsMeetingActive && RoundTimer.Expired(Runner))
        {
            float percent = Mathf.Clamp01(SpawnManager.Instance?.GetDeSpawnPercentage() ?? 0f);
            if (percent < requiredPercent) StartMeeting_Server();
            else RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
        }

        // 회의 타임아웃
        if (IsMeetingActive && MeetingTimer.Expired(Runner))
        {
            EndMeetingAndAnnounce_Server();
        }

        _wasLive = true;
    }

    // --- 서버(호스트)에서 회의 시작 ---
    private void StartMeeting_Server()
    {
        if (!IsGameLiveNow()) return; // 게임 전이면 무시

        IsMeetingActive = true;
        MeetingTimer = TickTimer.CreateFromSeconds(Runner, meetingDuration);
        _votes.Clear();

        RpcStartMeeting_All(meetingPoint ? meetingPoint.position : Vector3.zero, freezeMovementDuringMeeting);
    }

    // --- 서버(호스트)에서 회의 종료/발표 ---
    private void EndMeetingAndAnnounce_Server()
    {

        if (!IsGameLiveNow()) return; // 게임 전이면 무시


        var tally = new Dictionary<PlayerRef, int>();
        foreach (var pair in _votes)
        {
            if (!tally.ContainsKey(pair.Value)) tally[pair.Value] = 0;
            tally[pair.Value]++;
        }

        if (tally.Count == 0)
        {
            RpcEndMeeting_All(-1, false);
            IsMeetingActive = false;
            SpawnManager.Instance?.ResetRoundCounts();
            RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
            return;
        }

        // 최대 득표 탐색 + 동률 후보 수집
        int max = 0;
        var topCandidates = new List<PlayerRef>();
        foreach (var kv in tally)
        {
            if (kv.Value > max)
            {
                max = kv.Value;
                topCandidates.Clear();
                topCandidates.Add(kv.Key);
            }
            else if (kv.Value == max)
            {
                topCandidates.Add(kv.Key);
            }
        }

        if (topCandidates.Count == 1)
        {
            var winner = topCandidates[0];
            bool caughtSaboteur = IsSaboteur(winner);

            RpcPlayExecution_All(winner.PlayerId);
            RpcEndMeeting_All(winner.PlayerId, caughtSaboteur);

            IsMeetingActive = false;
            SpawnManager.Instance?.ResetRoundCounts();
            RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
        }
        else
        {
            StartRevote_Server(topCandidates);
        }
    }

    // === 회의 표시 ===
    private void ShowFinalVoteBanner(string msg)
    {
        if (finalVoteText) finalVoteText.text = msg;
        if (finalVoteBanner) finalVoteBanner.SetActive(true);
    }

    private void HideFinalVoteBanner()
    {
        if (finalVoteBanner) finalVoteBanner.SetActive(false);
    }

    // === 결과 표시 RPC ===
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcEndMeeting_All(int accusedPlayerId, bool caughtSaboteur)
    {

        HideFinalVoteBanner();

        _meetingOnCached = false;
        SetRoundTimerVisible(true);

        FreezeLocalOwnedCharacter(false);
        if (meetingUI) meetingUI.SetActive(false);

        var accusedRef = PlayerRefFromId(accusedPlayerId);
        var accusedName = GetPlayerDisplayName(accusedRef);

        var me = GetLocalPlayerInfo();
        bool iAmSaboteur = me && me.PlayerRole == PlayerInfo.Role.Saboteur;

        string title = caughtSaboteur
            ? (iAmSaboteur ? "방해자 패배…" : "정화 성공!")
            : (iAmSaboteur ? "방해자 성공!" : "검거 실패…");

        string detail = caughtSaboteur
            ? $"{accusedName} 은(는) 방해자였습니다."
            : $"{accusedName} 은(는) 청소부였습니다.";

        if (resultTitleText) resultTitleText.text = title;
        if (resultDetailText) resultDetailText.text = detail;
        if (resultAccusedNameText) resultAccusedNameText.text = $"지목: {accusedName}";

        if (resultPanelLocal)
        {
            resultPanelLocal.SetActive(true);
            StartCoroutine(CoShowResultPanelThenHide(2.0f));
        }

        if (Runner != null && Runner.LocalPlayer.PlayerId == accusedPlayerId)
        {
            Debug.Log("[Meeting] 내가 지목됨!");
            // TODO: 추방/관전 전환 처리
        }

        if (changeSceneAfterMeeting)
            StartCoroutine(CoChangeSceneViaChatManagerAfter(changeSceneDelay));
    }

    // === 컷씬 실행 RPC ===
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcPlayExecution_All(int accusedPlayerId)
    {
        var t = FindAccusedTransform(accusedPlayerId);
        if (vcam && t)
        {
            vcam.Follow = t;
            vcam.LookAt = t;
        }

        if (executionTimeline)
        {
            executionTimeline.time = 0;
            executionTimeline.Evaluate();
            executionTimeline.Play();
        }
    }

    // --- 회의 시작 연출 ---
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcStartMeeting_All(Vector3 tpPos, bool freeze)
    {
        Debug.Log("[Meeting] RpcStartMeeting_All fired on " + (Runner ? Runner.LocalPlayer.PlayerId : -1));

        _meetingOnCached = true;
        SetRoundTimerVisible(false);

        TeleportLocalOwnedCharacter(tpPos);
        if (freeze) FreezeLocalOwnedCharacter(true);

        if (meetingUI) meetingUI.SetActive(true);
        voteUI?.Rebuild(Runner);

        ShowFinalVoteBanner(meetingBannerText);
    }

    // 씬전환
    private IEnumerator CoChangeSceneViaChatManagerAfter(float sec)
    {
        if (_sceneChangeTriggered) yield break;   // 중복 방지
        _sceneChangeTriggered = true;

        yield return new WaitForSecondsRealtime(sec);

        var cm = chatManager ? chatManager : FindObjectOfType<Multiplayerchat>(true);
        if (cm != null)
        {
            // 버튼 없이도 동일 동작
            cm.OnLeaveRoomButtonPressed();
        }
        else
        {
            Debug.LogWarning("[Meeting] chatManager(Multiplayerchat) 를 찾지 못해 씬 전환 실패");
        }
    }

    private IEnumerator CoShowResultPanelThenHide(float showSec)
    {
        resultPanelLocal.SetActive(true);
        yield return new WaitForSecondsRealtime(showSec);
        resultPanelLocal.SetActive(false);
    }

    // --- 투표 제출: 각 클라 → 서버 ---
    public void SubmitVote(PlayerRef voted)
    {
        if (!Object) return;
        RpcSubmitVote_Server(voted);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RpcSubmitVote_Server(PlayerRef voted, RpcInfo info = default)
    {
        if (!IsMeetingActive) return;

        var voter = info.Source;
        _votes[voter] = voted;

        int need = GetEligibleVoterCount();
        if (_votes.Count >= need)
        {
            StartCoroutine(CoEarlyFinishDelay(3f)); // 3초 대기 후 집계
        }
    }

    private IEnumerator CoEarlyFinishDelay(float delaySec)
    {
        if (_earlyFinishPending) yield break;
        _earlyFinishPending = true;

        yield return new WaitForSeconds(delaySec);

        EndMeetingAndAnnounce_Server();
        _earlyFinishPending = false;
    }

    private bool _earlyFinishPending = false;

    private int GetEligibleVoterCount()
    {
        return Runner != null ? Runner.ActivePlayers.Count() : 0;
    }

    // --- 재투표 ---
    private void StartRevote_Server(List<PlayerRef> finalists)
    {
        IsMeetingActive = true;
        MeetingTimer = TickTimer.CreateFromSeconds(Runner, revoteDuration);
        _votes.Clear();

        RpcStartRevote_All(ConvertToIdArray(finalists), (int)revoteDuration);
    }

    private int[] ConvertToIdArray(List<PlayerRef> list)
    {
        var arr = new int[list.Count];
        for (int i = 0; i < list.Count; i++) arr[i] = list[i].PlayerId;
        return arr;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcStartRevote_All(int[] finalistIds, int durationSec)
    {
        Debug.Log("[Meeting] 재투표 시작. 후보: " + string.Join(",", finalistIds ?? Array.Empty<int>()));

        _meetingOnCached = true;
        SetRoundTimerVisible(false);

        if (meetingUI) meetingUI.SetActive(true);

        var whiteList = new List<int>(finalistIds ?? Array.Empty<int>());
        voteUI?.RebuildWithWhitelist(Runner, whiteList);

        meetingDuration = durationSec;

        ShowFinalVoteBanner(revoteBannerText);
    }

    // --- 로컬 유틸 ---
    private void TeleportLocalOwnedCharacter(Vector3 pos)
    {
        var my = FindObjectOfType<Player>();
        if (my) my.TeleportTo(pos);
    }

    private void FreezeLocalOwnedCharacter(bool freeze)
    {
        var my = FindObjectOfType<Player>();
        if (!my) return;
        my.SetInputLocked(freeze);
    }

    private IEnumerator CoUpdateTimersUI()
    {
        var wait = new WaitForSecondsRealtime(0.1f);
        while (true)
        {
            UpdateRoundTimerUI();
            UpdateMeetingTimerUI();
            yield return wait;
        }
    }

    private void UpdateRoundTimerUI()
    {
        if (!roundTimerText) return;

        if (Runner == null)
        {
            roundTimerText.text = "…";
            return;
        }

        if (IsMeetingActive)
        {
            roundTimerText.text = "—:—";
            return;
        }

        var remain = RoundTimer.RemainingTime(Runner);
        if (remain.HasValue)
        {
            double s = Math.Max(0.0, remain.Value);
            roundTimerText.text = FormatMMSS(s);
        }
        else
        {
            roundTimerText.text = "—:—";
        }
    }

    private void UpdateMeetingTimerUI()
    {
        if (!meetingTimerText) return;

        if (Runner == null)
        {
            meetingTimerText.text = "";
            return;
        }

        if (!IsMeetingActive)
        {
            meetingTimerText.text = "";
            return;
        }

        var remain = MeetingTimer.RemainingTime(Runner);
        if (remain.HasValue)
        {
            double s = Math.Max(0.0, remain.Value);
            meetingTimerText.text = $"{FormatMMSS(s)}";
        }
        else
        {
            meetingTimerText.text = "—:—";
        }
    }

    private static string FormatMMSS(double seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m:00}:{s:00}";
    }

    private void SetRoundTimerVisible(bool visible)
    {
        if (roundTimerText)
            roundTimerText.gameObject.SetActive(visible);
    }

    private void Update()
    {
        if (Runner == null) return;
        if (!IsGameLiveNow()) return; // 게임 전에는 F1/F2 디버그키 무효화

        if (Object.HasStateAuthority)
        {
            if (Input.GetKeyDown(KeyCode.F1) && !IsMeetingActive)
                StartMeeting_Server();
            if (Input.GetKeyDown(KeyCode.F2) && IsMeetingActive)
                EndMeetingAndAnnounce_Server();
        }
    }

    // --- Helper Methods ---
    private PlayerInfo GetPlayerInfo(PlayerRef pref)
    {
        foreach (var pi in FindObjectsOfType<PlayerInfo>())
            if (pi && pi.Object && pi.Object.InputAuthority == pref)
                return pi;
        return null;
    }

    private PlayerInfo GetLocalPlayerInfo()
    {
        if (Runner == null) return null;
        foreach (var pi in FindObjectsOfType<PlayerInfo>())
            if (pi && pi.Object && pi.Object.InputAuthority == Runner.LocalPlayer)
                return pi;
        return null;
    }

    private bool IsSaboteur(PlayerRef pref)
    {
        var pi = GetPlayerInfo(pref);
        return pi && pi.PlayerRole == PlayerInfo.Role.Saboteur;
    }

    private string GetPlayerDisplayName(PlayerRef pref)
    {
        var pi = GetPlayerInfo(pref);
        return pi ? (string.IsNullOrEmpty(pi.cachedName) ? $"Player {pref.PlayerId}" : pi.cachedName)
                  : $"Player {pref.PlayerId}";
    }

    private Transform FindAccusedTransform(int accusedPlayerId)
    {
        foreach (var pi in FindObjectsOfType<PlayerInfo>())
            if (pi && pi.Object && pi.Object.InputAuthority.PlayerId == accusedPlayerId)
                return pi.transform;
        return null;
    }

    private PlayerRef PlayerRefFromId(int id)
    {
        if (Runner != null)
            foreach (var p in Runner.ActivePlayers)
                if (p.PlayerId == id) return p;
        return default;
    }

    // --- Runner Hooks ---
    private class RunnerHooks : INetworkRunnerCallbacks
    {
        private readonly MeetingDirector _dir;
        public RunnerHooks(MeetingDirector d) { _dir = d; }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (_dir.IsMeetingOn) _dir.voteUI?.Rebuild(runner);
        }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (_dir.IsMeetingOn) _dir.voteUI?.Rebuild(runner);
        }

        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnInput(NetworkRunner r, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner r, ShutdownReason shutdownReason) { }
        public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessionList) { }
        public void OnUserSimulationMessage(NetworkRunner r, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner r, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject obj, PlayerRef player) { }
    }
}
