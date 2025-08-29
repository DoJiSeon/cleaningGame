using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;

public class MeetingDirector : NetworkBehaviour
{

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

    // 투표: key=투표자, value=피투표자
    private readonly Dictionary<PlayerRef, PlayerRef> _votes = new();

    // 회의 시간(초)
    public float meetingDuration = 20f;

    // MeetingDirector.cs (클래스 본문 상단 어딘가)
    [SerializeField] private bool _meetingOnCached = false;

    [SerializeField] private VoteUI voteUI;
    // 외부(UI 등)에서 안전하게 읽기 위한 getter — 네트워크 상태와 무관하게 안전
    public bool IsMeetingOn => _meetingOnCached;

    // UI 업데이트 루프
    private Coroutine _uiTimerRoutine;

    // --- 라이프사이클 ---
    public override void Spawned()
    {
        _meetingOnCached = false; // 초기화
        if (Object.HasStateAuthority)
            RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);

        if (_uiTimerRoutine == null)
            _uiTimerRoutine = StartCoroutine(CoUpdateTimersUI());

        if (Runner != null) Runner.AddCallbacks(new RunnerHooks(this));
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _meetingOnCached = false; // 정리
        if (_uiTimerRoutine != null) { StopCoroutine(_uiTimerRoutine); _uiTimerRoutine = null; }
    }
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // 라운드 시간 종료 시 회의 조건 체크
        if (!IsMeetingActive && RoundTimer.Expired(Runner))
        {
            float percent = Mathf.Clamp01(SpawnManager.Instance?.GetDeSpawnPercentage() ?? 0f);
            if (percent < requiredPercent)
            {
                StartMeeting_Server();
            }
            else
            {
                // 성공 라운드 처리(원한다면 별도 UI/이벤트)
                RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration); // 다음 라운드
            }
        }

        // 회의 중 타임아웃 → 자동 마감
        if (IsMeetingActive && MeetingTimer.Expired(Runner))
        {
            EndMeetingAndAnnounce_Server();
        }
    }

    // --- 서버(호스트)에서 회의 시작 판정 ---
    private void StartMeeting_Server()
    {
        IsMeetingActive = true;
        MeetingTimer = TickTimer.CreateFromSeconds(Runner, meetingDuration);
        _votes.Clear();

        // 모두를 회의장으로 보내고, 이동/행동 봉인 + 회의 UI 오픈
        RpcStartMeeting_All(meetingPoint ? meetingPoint.position : Vector3.zero, freezeMovementDuringMeeting);
    }

    // --- 서버(호스트)에서 회의 종료/발표 ---
    private void EndMeetingAndAnnounce_Server()
    {
        // 최다득표자 계산
        var tally = new Dictionary<PlayerRef, int>();
        foreach (var pair in _votes)
        {
            if (!tally.ContainsKey(pair.Value)) tally[pair.Value] = 0;
            tally[pair.Value]++;
        }

        PlayerRef? winner = null;
        int max = 0;
        foreach (var kv in tally)
        {
            if (kv.Value > max)
            {
                max = kv.Value;
                winner = kv.Key;
            }
        }

        int selectedId = winner.HasValue ? winner.Value.PlayerId : -1;
        RpcEndMeeting_All(selectedId);

        // 회의 종료 상태
        IsMeetingActive = false;

        // 다음 라운드 시작(게이지/카운트 초기화도 여기서)
        SpawnManager.Instance?.ResetRoundCounts();
        RoundTimer = TickTimer.CreateFromSeconds(Runner, roundDuration);
    }

    // --- 모든 클라: 회의 시작 연출/TP/봉인 ---
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcStartMeeting_All(Vector3 tpPos, bool freeze)
    {
        Debug.Log("[Meeting] RpcStartMeeting_All fired on " + (Runner ? Runner.LocalPlayer.PlayerId : -1));

        _meetingOnCached = true; // ← 캐시 갱신
        SetRoundTimerVisible(false);

        TeleportLocalOwnedCharacter(tpPos);
        if (freeze) FreezeLocalOwnedCharacter(true);

        if (meetingUI) meetingUI.SetActive(true);
        voteUI?.Rebuild(Runner);
    }

    // --- 모든 클라: 회의 종료 + 결과 알림/해제 ---
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcEndMeeting_All(int selectedPlayerId)
    {

        _meetingOnCached = false; // ← 캐시 갱신
        SetRoundTimerVisible(true);

        FreezeLocalOwnedCharacter(false);
        if (meetingUI) meetingUI.SetActive(false);


        // 봉인 해제 + UI 닫기
        FreezeLocalOwnedCharacter(false);
        if (meetingUI) meetingUI.SetActive(false);

        // 결과 처리(선택된 플레이어가 로컬이면 별도 연출 가능)
        if (selectedPlayerId >= 0 && Runner != null)
        {
            var localId = Runner.LocalPlayer.PlayerId;
            if (localId == selectedPlayerId)
            {
                Debug.Log("[Meeting] 내가 지목됨!");
                // TODO: 마피아/추방 연출, 리스폰/관전 전환 등
            }
        }
    }

    // --- 투표 제출: 각 클라 → 서버 ---
    public void SubmitVote(PlayerRef voted)   // UI에서 호출
    {
        if (!Object || !Object.HasInputAuthority) return;
        RpcSubmitVote_Server(voted);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RpcSubmitVote_Server(PlayerRef voted)
    {
        if (!IsMeetingActive) return;

        // 주의: InputAuthority 사용 방식은 프로젝트 구조에 맞게 조정 필요
        var voter = Object.InputAuthority;
        if (_votes.ContainsKey(voter)) _votes[voter] = voted;
        else _votes.Add(voter, voted);

        // 모두 투표 완료 시 즉시 마감하고 싶다면:
        // if (_votes.Count >= Runner.ActivePlayers.Count()) EndMeetingAndAnnounce_Server();
    }

    // --- 로컬 유틸: 내 캐릭터 TP/봉인 ---
    private void TeleportLocalOwnedCharacter(Vector3 pos)
    {
        // 각 프로젝트에서 "내 캐릭터" 참조 얻는 방법이 다름
        var my = FindObjectOfType<Player>();
        if (my) my.TeleportTo(pos);  // Player 스크립트에 TeleportTo(Vector3) 필요
    }

    private void FreezeLocalOwnedCharacter(bool freeze)
    {
        var my = FindObjectOfType<Player>();
        if (!my) return;
        my.SetInputLocked(freeze);   // Player 스크립트에 SetInputLocked(bool) 필요
    }

    // --- UI 갱신 루프 ---
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
        if (!roundTimerText)
            return;

        if (Runner == null)
        {
            roundTimerText.text = "…"; // 네트워크 준비 대기
            return;
        }

        if (IsMeetingActive)
        {
            roundTimerText.text = "—:—"; // 회의 중엔 라운드 타이머 숨김
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
            roundTimerText.text = "—:—"; // 아직 동기화 전
        }
    }

    private void UpdateMeetingTimerUI()
    {
        if (!meetingTimerText)
            return;

        if (Runner == null)
        {
            meetingTimerText.text = "";
            return;
        }

        if (!IsMeetingActive)
        {
            meetingTimerText.text = ""; // 회의 아닐 때 숨김
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

    // 디버그용: 강제 회의 시작/종료 키
    private void Update()
    {
        if (Runner == null) return;

        if (Object.HasStateAuthority)
        {
            if (Input.GetKeyDown(KeyCode.F1) && !IsMeetingActive)
                StartMeeting_Server();
            if (Input.GetKeyDown(KeyCode.F2) && IsMeetingActive)
                EndMeetingAndAnnounce_Server();
        }
    }

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

        // 나머지 콜백은 비워둔다
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
