/* 레거시 코드
 * 
 * 
 * 
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class OptionButtonUI : MonoBehaviour
{
    [Header("타이머")]
    public Image timerBar;
    public float timeLimit = 20f;   // 슬라이드 아웃까지 남은 시간

    [Header("패널 슬라이드")]
    public RectTransform slideTarget;
    public float slideInY = 0f;
    public float slideOutY = -300f;
    public float slideDur = 0.5f;

    [Header("패널 표시 간격")]
    public float interval = 300f;      // 5분
    private float playTimer, nextTriggerTime;

    [Header("버튼 하이라이트")]
    public List<Image> buttonImages;                // Hover 대상들
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.8f, 0.3f, 1f);
    public float colorTweenDur = 0.2f;
    public int totalOptions = 4;              // 선택해야 할 옵션 수

    [Header("패널티 기능")]
    public Image tunnelVisionMask;
    private Player player;

    // 내부 상태
    float timer;
    bool hasAppeared, hasTriggered;
    int currentIndex = -1;
    HashSet<int> selected = new HashSet<int>();

    void Start()
    {
        player = FindObjectOfType<Player>();
        nextTriggerTime = interval;                 // 5분 뒤 첫 표시
    }

    void Update()
    {
        // --- 패널 표시 주기 ---
        playTimer += Time.deltaTime;
        if (playTimer >= nextTriggerTime)
        {
            ResetUIAndSlideIn();
            nextTriggerTime += interval;
        }
        if (!hasAppeared) return;

        // --- Hover 순환(K) ---
        if (Input.GetKeyDown(KeyCode.K))
        {
            // 이전 버튼 원복
            if (currentIndex >= 0 && currentIndex < buttonImages.Count)
                buttonImages[currentIndex]
                    .DOColor(normalColor, colorTweenDur).SetEase(Ease.OutQuad);

            // 다음 인덱스
            currentIndex = (currentIndex + 1) % buttonImages.Count;
            Debug.Log($"[OptionBtn] Hover: {currentIndex}");

            // 현재 버튼 강조
            buttonImages[currentIndex]
                .DOColor(highlightColor, colorTweenDur).SetEase(Ease.OutBack);
        }

        // --- 확정(Enter) ---
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (currentIndex >= 0)
            {
                selected.Add(currentIndex);                     // 선택 기록
                Debug.Log($"선택된 옵션: {string.Join(", ", selected)}");
                TriggerSlideOut();
            }

            // 모두 선택 완료?
            if (selected.Count >= totalOptions)
            {
                Debug.Log("[OptionBtn] 전 옵션 선택 완료");
                // 필요하다면 여기서 TutorialManager.NextStep() 호출 가능
            }
        }

        // --- 자동 슬라이드 아웃 ---
        if (!hasTriggered)
        {
            timer -= Time.deltaTime;
            if (timerBar) timerBar.fillAmount = timer / timeLimit;
            if (timer <= 0f) TriggerSlideOut();
        }
    }

    /* ---------- 슬라이드/패널티 ---------- */
/*

    void ResetUIAndSlideIn()
    {
        timer = timeLimit;
        hasTriggered = false;
        hasAppeared = true;
        currentIndex = -1;

        // 버튼 색 초기화
        foreach (var img in buttonImages) img.color = normalColor;

        slideTarget.anchoredPosition =
            new Vector2(slideTarget.anchoredPosition.x, slideOutY);

        slideTarget.DOAnchorPosY(slideInY, slideDur).SetEase(Ease.OutBack);
    }

    void TriggerSlideOut()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        // 선택된 인덱스에 따라 패널티 실행
        switch (currentIndex)
        {
            case 0:  // 이동속도 제한
                if (player)
                {
                    player.SetSpeedLimit(true);
                    StartCoroutine(ReleaseSpeedLimitAfter(5f));
                }
                break;

            case 1:  // 사운드 OFF
                StartCoroutine(MuteSoundFor(5f));
                break;

            case 2:  // 터널 비전
                if (tunnelVisionMask)
                {
                    tunnelVisionMask.gameObject.SetActive(true);
                    StartCoroutine(DisableTunnelAfter(5f));
                }
                break;
        }

        SlideOut();

        // 아직 다 선택 안 했으면 1.5초 후 패널 다시 열기
        if (selected.Count < totalOptions)
            StartCoroutine(ReopenAfter(1.5f));
    }

    void SlideOut() =>
        slideTarget.DOAnchorPosY(slideOutY, slideDur).SetEase(Ease.InBack);

    /* ---------- 코루틴 ---------- */
/*
    IEnumerator ReleaseSpeedLimitAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (player) player.SetSpeedLimit(false);
    }
    IEnumerator MuteSoundFor(float sec)
    {
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(sec);
        AudioListener.volume = 1f;
    }
    IEnumerator DisableTunnelAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (tunnelVisionMask) tunnelVisionMask.gameObject.SetActive(false);
    }
    IEnumerator ReopenAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        hasTriggered = false;
        ResetUIAndSlideIn();
    }
}

*/








using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Fusion;   // Fusion
using Fusion.Sockets;
using System;  // for Enum

public class OptionButtonUI : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("타이머(패널 오픈 간격)")]
    public float interval = 300f;      // 패널이 열리는 간격(초)

    [Header("패널 슬라이드")]
    public RectTransform slideTarget;
    public float slideInY = 0f;
    public float slideOutY = -300f;
    public float slideDur = 0.5f;

    [Header("패널 내 로컬 타이머(연출용)")]
    public Image timerBar;
    public float timeLimit = 20f;

    [Header("패널티 버튼들(종류 선택 용, 로컬 하이라이트)")]
    public List<Image> buttonImages;
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.8f, 0.3f, 1f);
    public float colorTweenDur = 0.2f;

    [Header("타겟 표시(선택 확인용, 로컬)")]
    public Text targetLabel;

    [Header("패널티 기능(로컬 적용)")]
    public Image tunnelVisionMask;
    private Player player;

    // ====== 네트워크 동기화용 타이머(열림 스케줄) ======
    [Networked] private TickTimer NextOpenTimer { get; set; }

    // ====== 로컬 상태 ======
    private enum Phase { Closed, ChoosingTarget, ChoosingPenalty }
    private Phase _phase = Phase.Closed;

    private float _localPanelTimer;         // 연출용 남은 시간
    private List<PlayerRef> _targets = new List<PlayerRef>(); // 본인 제외 후보
    private int _targetIndex = -1;          // 현재 선택 타겟
    private int _penaltyIndex = 0;          // 현재 선택 패널티

    /* ================== 라이프사이클 ================== */

    void Start()
    {
        player = FindObjectOfType<Player>();
        // StateAuthority가 아니라면 타이머는 건드리지 않음
    }

    public override void Spawned()
    {
        base.Spawned();
        // Runner 콜백 등록
        if (Runner != null) Runner.AddCallbacks(this);

        // 타겟 목록 최신화
        BuildTargetList();
        UpdateTargetLabel();

        // 호스트만 스케줄 시작
        if (Object.HasStateAuthority)
            NextOpenTimer = TickTimer.CreateFromSeconds(Runner, interval);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (runner != null) runner.RemoveCallbacks(this);
    }

    public override void FixedUpdateNetwork()
    {
        // 패널 오픈 스케줄을 호스트가 관리
        if (Object.HasStateAuthority && NextOpenTimer.Expired(Runner))
        {
            RpcOpenPanel(); // 모두 동시에 열리게
            NextOpenTimer = TickTimer.CreateFromSeconds(Runner, interval);
        }
    }

    void Update()
    {
        // 로컬 패널 열림 중이면 남은 시간 바 표시(연출용)
        if (_phase == Phase.ChoosingTarget || _phase == Phase.ChoosingPenalty)
        {
            _localPanelTimer -= Time.deltaTime;
            if (timerBar) timerBar.fillAmount = Mathf.Clamp01(_localPanelTimer / timeLimit);

            // 시간초과 시: 패널 닫기(호스트만 닫기 트리거 → 모두 닫힘)
            if (_localPanelTimer <= 0f && Object.HasStateAuthority)
            {
                RpcClosePanel();
            }
        }

        // 입력은 내 입력권한만 처리
        if (!Object || !Object.HasInputAuthority) return;

        // == 키 바인딩은 K / Enter만 ==
        if (_phase == Phase.ChoosingTarget)
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                CycleTarget();
            }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                // 타겟 확정 → 패널티 선택 단계로
                if (_targetIndex >= 0)
                {
                    EnterPenaltyPhase();
                }
                else
                {
                    // 타겟 후보 없으면 그냥 닫기(호스트 트리거)
                    if (Object.HasStateAuthority) RpcClosePanel();
                }
            }
        }
        else if (_phase == Phase.ChoosingPenalty)
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                CyclePenalty();
            }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                // 패널티 확정 → 타겟에게만 적용 + 모두 패널 닫기
                if (_targetIndex >= 0 && _targetIndex < _targets.Count)
                {
                    var target = _targets[_targetIndex];
                    RpcApplyPenaltyToTarget(target, _penaltyIndex);
                }

                // 닫기는 호스트가 트리거 (중복 호출 방지)
                if (Object.HasStateAuthority) RpcClosePanel();
            }
        }
    }

    /* ================= 동기화: 패널 열림/닫힘 ================= */

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcOpenPanel()
    {
        // 로컬 상태 초기화
        _phase = Phase.ChoosingTarget;
        _localPanelTimer = timeLimit;

        // 타겟/패널티 초기화(플레이어 목록은 입퇴장 콜백에서도 최신화됨)
        BuildTargetList();
        if (_targets.Count > 0) _targetIndex = Mathf.Clamp(_targetIndex, 0, _targets.Count - 1);
        else _targetIndex = -1;

        _penaltyIndex = 0;
        RefreshPenaltyHighlight();
        UpdateTargetLabel();

        // 슬라이드 인(연출 동기)
        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.anchoredPosition = new Vector2(slideTarget.anchoredPosition.x, slideOutY);
            slideTarget.DOAnchorPosY(slideInY, slideDur).SetEase(Ease.OutBack);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcClosePanel()
    {
        _phase = Phase.Closed;

        // 슬라이드 아웃(연출 동기)
        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.DOAnchorPosY(slideOutY, slideDur).SetEase(Ease.InBack);
        }
    }

    private void EnterPenaltyPhase()
    {
        _phase = Phase.ChoosingPenalty;
        _penaltyIndex = Mathf.Clamp(_penaltyIndex, 0, Mathf.Max(0, buttonImages.Count - 1));
        RefreshPenaltyHighlight();
    }

    /* ================= 타겟/패널티 선택(로컬 UI) ================= */

    private void CycleTarget()
    {
        BuildTargetList(); // 혹시 모를 변화 반영
        if (_targets.Count == 0)
        {
            _targetIndex = -1;
            UpdateTargetLabel();
            return;
        }

        _targetIndex = (_targetIndex + 1) % _targets.Count;
        UpdateTargetLabel();
    }

    private void CyclePenalty()
    {
        if (buttonImages == null || buttonImages.Count == 0) return;
        _penaltyIndex = (_penaltyIndex + 1) % buttonImages.Count;
        RefreshPenaltyHighlight();
    }

    private void RefreshPenaltyHighlight()
    {
        if (buttonImages == null) return;

        for (int i = 0; i < buttonImages.Count; i++)
        {
            var img = buttonImages[i];
            if (!img) continue;

            img.DOKill();
            var col = (i == _penaltyIndex && _phase == Phase.ChoosingPenalty) ? highlightColor : normalColor;
            img.DOColor(col, colorTweenDur).SetEase(Ease.OutQuad);
        }
    }

    private void UpdateTargetLabel()
    {
        if (!targetLabel) return;

        if (_targetIndex >= 0 && _targetIndex < _targets.Count)
        {
            var pref = _targets[_targetIndex];
            targetLabel.text = $"Target: {pref.PlayerId}";
        }
        else
        {
            targetLabel.text = "Target: -";
        }
    }

    /* ================= 타겟 목록 최신화 ================= */

    private void BuildTargetList()
    {
        _targets.Clear();
        if (Runner == null) return;

        foreach (var p in Runner.ActivePlayers)
        {
            if (p != Runner.LocalPlayer)  // 본인 제외
                _targets.Add(p);
        }

        // 현재 인덱스 유효성 보정
        if (_targets.Count == 0) _targetIndex = -1;
        else if (_targetIndex < 0) _targetIndex = 0;
        else _targetIndex = Mathf.Min(_targetIndex, _targets.Count - 1);
    }

    /* ================= 패널티 적용(타겟 본인만 로컬 적용) ================= */

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void RpcApplyPenaltyToTarget(PlayerRef target, int penaltyIndex)
    {
        // 이 클라이언트가 타겟 본인이라면 적용
        if (Runner && Runner.LocalPlayer == target)
        {
            ApplyPenaltyLocal(penaltyIndex);
        }
    }

    private void ApplyPenaltyLocal(int optionIndex)
    {
        switch (optionIndex)
        {
            case 0: // 이동속도 제한
                if (player)
                {
                    player.SetSpeedLimit(true);
                    StartCoroutine(ReleaseSpeedLimitAfter(5f));
                }
                break;

            case 1: // 사운드 OFF
                StartCoroutine(MuteSoundFor(5f));
                break;

            case 2: // 터널 비전
                if (tunnelVisionMask)
                {
                    tunnelVisionMask.gameObject.SetActive(true);
                    StartCoroutine(DisableTunnelAfter(5f));
                }
                break;

            default:
                break;
        }
    }

    /* ================= 코루틴 ================= */

    IEnumerator ReleaseSpeedLimitAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (player) player.SetSpeedLimit(false);
    }

    IEnumerator MuteSoundFor(float sec)
    {
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(sec);
        AudioListener.volume = 1f;
    }

    IEnumerator DisableTunnelAfter(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (tunnelVisionMask) tunnelVisionMask.gameObject.SetActive(false);
    }

    /* ============ INetworkRunnerCallbacks (입퇴장 갱신) ============ */

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef playerRef)
    {
        // 어떤 클라이언트든 자신의 로컬 UI 타겟 목록을 즉시 최신화
        BuildTargetList();
        UpdateTargetLabel();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef playerRef)
    {
        BuildTargetList();
        UpdateTargetLabel();
        // 선택했던 타겟이 나갔다면 보정
        if (_targetIndex >= 0 && _targetIndex >= _targets.Count)
        {
            _targetIndex = _targets.Count > 0 ? 0 : -1;
            UpdateTargetLabel();
        }
    }

    #region INetworkRunnerCallbacks (Fusion 2)

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[Fusion] Disconnected: {reason}");
        _phase = Phase.Closed;
        if (slideTarget) { slideTarget.DOKill(); slideTarget.anchoredPosition = new Vector2(slideTarget.anchoredPosition.x, slideOutY); }
        BuildTargetList(); UpdateTargetLabel();
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogWarning($"[Fusion] ConnectFailed: {remoteAddress} / {reason}");
    }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    #endregion

}
