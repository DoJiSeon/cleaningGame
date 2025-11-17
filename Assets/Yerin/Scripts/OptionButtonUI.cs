using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using Fusion.Sockets;
using System;
using TMPro;

/// <summary>
/// 두 단계 플로우:
/// 1) 타겟(플레이어) 선택 패널 먼저 표시 (K로 순환, Enter로 확정)
/// 2) 제재(패널티) 선택 패널 슬라이드 인 (K로 순환, Enter로 확정)
/// 모든 플레이어가 선택 완료 or 타이머 종료 시 일괄 적용
/// </summary>
public class OptionButtonUI : NetworkBehaviour, INetworkRunnerCallbacks
{
    // ===================== 테스트/일반 옵션 =====================
    [Header("테스트 옵션")]
    [SerializeField] private bool allowSelfTargetForTest = false;

    [Header("패널 오픈 간격 (호스트 스케줄)")]
    public float interval = 300f; // 초

    [Header("회의 중 옵션 패널 차단")]
    [SerializeField] private bool suppressDuringMeeting = true;

    private MeetingDirector _meeting;   // 회의 상태 참조

    // ===================== 제재 패널(기존) =====================
    [Header("제재 패널 슬라이드")]
    public RectTransform slideTarget;   // 제재 선택 패널 루트(RectTransform)
    public float slideInY = 0f;
    public float slideOutY = -300f;
    public float slideDur = 0.5f;

    [Header("제재 패널 타이머(연출용)")]
    public Image timerBar;              // 제재 패널 상단 타이머 바(선택)
    public float timeLimit = 20f;

    [Header("제재 항목(버튼 이미지들)")]
    public List<Image> buttonImages;
    public Color normalColor = Color.white;
    public Color highlightColor = new Color(1f, 0.8f, 0.3f, 1f);
    public float colorTweenDur = 0.2f;

    [Header("슬라이드 연동 텍스트(선택)")]
    [SerializeField] private GameObject slideTextGO; // 예: 클린 게이지 텍스트

    [Header("타겟 표시(선택)")]
    public Text targetLabel; // 선택된 타겟 표기용(선택)

    [Header("패널티 기능")]
    public Image tunnelVisionMask;  // 터널 비전용 마스크 이미지(타겟 본인만 on/off)

    // ===================== 타겟 선택 패널(신규) =====================
    [Header("타겟 선택 안내 텍스트")]
    [SerializeField] private TMP_Text targetTitleText;
    [SerializeField] private string targetTitleMessage = "제재할 플레이어를 선택하세요";

    [Header("타겟 선택 패널 (플레이어 이름 리스트)")]
    [SerializeField] private GameObject targetPanelRoot;   // 비활성 시작
    [SerializeField] private RectTransform targetContent;  // ScrollRect/Viewport/Content
    [SerializeField] private GameObject targetItemPrefab;  // Image(bg) + TMP_Text("Label")
    [SerializeField] private ScrollRect targetScroll;      // (선택) 자동 스크롤

    [Header("타겟 항목 색/연출")]
    [SerializeField] private Color targetBgNormal = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Color targetBgSelected = new Color(0.15f, 0.35f, 1f, 0.7f);
    [SerializeField] private float targetColorTween = 0.15f;
    [SerializeField] private bool targetUsePulse = true;
    [SerializeField] private float targetPulseScale = 1.03f;
    [SerializeField] private float targetPulseDur = 0.35f;

    // 타겟 항목 내부 구조
    private class TargetItem
    {
        public GameObject go;
        public RectTransform rt;
        public Image bg;
        public TMP_Text label;
        public PlayerRef pref;
        public Tween colorTw, scaleTw;
    }
    private readonly List<TargetItem> _targetItems = new();

    // ===================== 네트워크 동기화 =====================
    [Networked] private TickTimer NextOpenTimer { get; set; }

    // 패널티 투표 결과 저장 (서버가 관리)
    [Networked, Capacity(16)]
    private NetworkDictionary<PlayerRef, PenaltyVote> PenaltyVotes => default;

    // 패널티 투표 데이터 구조체
    private struct PenaltyVote : INetworkStruct
    {
        public PlayerRef target;
        public int penaltyIndex;
        public NetworkBool hasVoted;
    }

    // ===================== 로컬 상태 =====================
    private enum Phase { Closed, ChoosingTarget, ChoosingPenalty, WaitingForOthers }
    private Phase _phase = Phase.Closed;

    private float _localPanelTimer;         // 패널(현재 단계) 남은 시간(연출)
    private List<PlayerRef> _targets = new List<PlayerRef>(); // 후보
    private int _targetIndex = -1;          // 타겟 인덱스
    private int _penaltyIndex = 0;          // 제재 인덱스

    [Header("대기 상태 UI")]
    [SerializeField] private GameObject waitingPanel; // "다른 플레이어를 기다리는 중..." 표시용
    [SerializeField] private TMP_Text waitingText;

    // ===================== 라이프사이클 =====================
    void Start()
    {
        _meeting = FindObjectOfType<MeetingDirector>(true);
        if (waitingPanel) waitingPanel.SetActive(false);
    }

    public override void Spawned()
    {
        base.Spawned();
        if (Runner != null) Runner.AddCallbacks(this);

        BuildTargetList();
        UpdateTargetLabel();

        if (Object.HasStateAuthority && _wasLive)
            NextOpenTimer = TickTimer.CreateFromSeconds(Runner, interval);
        else
            NextOpenTimer = TickTimer.None;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        if (runner != null) runner.RemoveCallbacks(this);
    }

    private bool IsGameLive()
    {
        return GameRuleManager.Instance != null && GameRuleManager.Instance.IsGameLive;
    }

    private bool _wasLive = false;

    public override void FixedUpdateNetwork()
    {
        bool live = IsGameLive();

        if (Object.HasStateAuthority)
        {
            if (!live)
            {
                if (_phase != Phase.Closed)
                    RpcClosePanel();

                NextOpenTimer = TickTimer.None;
                _wasLive = false;
                return;
            }

            if (!_wasLive && live)
                NextOpenTimer = TickTimer.CreateFromSeconds(Runner, interval);

            if (NextOpenTimer.Expired(Runner))
            {
                if (suppressDuringMeeting && _meeting && _meeting.IsMeetingOn)
                {
                    NextOpenTimer = TickTimer.CreateFromSeconds(Runner, 1f);
                }
                else
                {
                    RpcOpenPanel();
                    NextOpenTimer = TickTimer.CreateFromSeconds(Runner, interval);
                }
            }

            _wasLive = true;

            // 타이머 종료 시 패널티 일괄 적용
            if (_phase != Phase.Closed && _localPanelTimer <= 0f)
            {
                ApplyAllPenalties();
                RpcClosePanel();
            }
        }
    }

    void Update()
    {
        if (_meeting == null) _meeting = FindObjectOfType<MeetingDirector>(true);

        bool live = IsGameLive();

        if (suppressDuringMeeting && _meeting && _meeting.IsMeetingOn && _phase != Phase.Closed)
        {
            RpcClosePanel();
        }

        if (!live)
        {
            if (_phase != Phase.Closed)
                LocalClosePanelVisual();
            return;
        }

        // 패널 타이머
        if (_phase == Phase.ChoosingTarget || _phase == Phase.ChoosingPenalty || _phase == Phase.WaitingForOthers)
        {
            _localPanelTimer -= Time.deltaTime;
            if (timerBar) timerBar.fillAmount = Mathf.Clamp01(_localPanelTimer / timeLimit);
        }

        // K/Enter 바인딩
        if (_phase == Phase.ChoosingTarget)
        {
            if (Input.GetKeyDown(KeyCode.K))
            {
                CycleTarget();
            }
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (_targetIndex >= 0) EnterPenaltyPhase();
                else if (Object && Object.HasStateAuthority) RpcClosePanel();
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
                if (_targetIndex >= 0 && _targetIndex < _targets.Count)
                {
                    var target = _targets[_targetIndex];
                    // 서버에 투표 제출
                    RPC_SubmitPenaltyVote(target, _penaltyIndex);

                    // 대기 상태로 전환
                    EnterWaitingPhase();
                }
            }
        }
    }

    // ===================== 패널 열림/닫힘 (RPC) =====================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcOpenPanel()
    {
        if (!IsGameLive()) return;

        // 서버: 투표 딕셔너리 초기화
        if (Object.HasStateAuthority)
        {
            PenaltyVotes.Clear();
        }

        _phase = Phase.ChoosingTarget;
        _localPanelTimer = timeLimit;

        BuildTargetList();
        _penaltyIndex = 0;
        RefreshPenaltyHighlight();
        UpdateTargetLabel();

        OpenTargetPanel();

        if (slideTextGO) slideTextGO.SetActive(true);
        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.anchoredPosition = new Vector2(slideTarget.anchoredPosition.x, slideOutY);
        }

        if (waitingPanel) waitingPanel.SetActive(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcClosePanel()
    {
        _phase = Phase.Closed;

        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.DOAnchorPosY(slideOutY, slideDur).SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    if (slideTextGO) slideTextGO.SetActive(false);
                    CloseTargetPanel();
                });
        }
        else
        {
            if (slideTextGO) slideTextGO.SetActive(false);
            CloseTargetPanel();
        }

        if (waitingPanel) waitingPanel.SetActive(false);
    }

    private void LocalClosePanelVisual()
    {
        _phase = Phase.Closed;
        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.DOAnchorPosY(slideOutY, slideDur).SetEase(Ease.InBack);
        }
        if (slideTextGO) slideTextGO.SetActive(false);
        CloseTargetPanel();
        if (waitingPanel) waitingPanel.SetActive(false);
    }

    private void EnterPenaltyPhase()
    {
        if (_targetIndex < 0 || _targetIndex >= _targets.Count)
        {
            if (Object.HasStateAuthority) RpcClosePanel();
            return;
        }

        _phase = Phase.ChoosingPenalty;

        CloseTargetPanel();

        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.DOAnchorPosY(slideInY, slideDur).SetEase(Ease.OutBack);
        }

        _penaltyIndex = Mathf.Clamp(_penaltyIndex, 0, Mathf.Max(0, buttonImages.Count - 1));
        RefreshPenaltyHighlight();
    }

    private void EnterWaitingPhase()
    {
        _phase = Phase.WaitingForOthers;

        // 제재 패널 숨김
        if (slideTarget)
        {
            slideTarget.DOKill();
            slideTarget.DOAnchorPosY(slideOutY, slideDur).SetEase(Ease.InBack);
        }

        // 대기 패널 표시
        if (waitingPanel) waitingPanel.SetActive(true);
        if (waitingText) waitingText.text = "다른 플레이어를 기다리는 중...";
    }

    // ===================== 패널티 투표 제출 및 처리 =====================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SubmitPenaltyVote(PlayerRef target, int penaltyIndex, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        var voter = info.Source;

        // 투표 저장
        PenaltyVotes.Set(voter, new PenaltyVote
        {
            target = target,
            penaltyIndex = penaltyIndex,
            hasVoted = true
        });

        Debug.Log($"[Server] {voter} voted: target={target}, penalty={penaltyIndex}");

        // 모든 플레이어가 투표했는지 확인
        if (CheckAllPlayersVoted())
        {
            Debug.Log("[Server] All players voted! Applying penalties...");
            ApplyAllPenalties();
            RpcClosePanel();
        }
    }

    private bool CheckAllPlayersVoted()
    {
        if (Runner == null) return false;

        int totalPlayers = 0;
        foreach (var p in Runner.ActivePlayers)
        {
            totalPlayers++;
        }

        return PenaltyVotes.Count >= totalPlayers;
    }

    private void ApplyAllPenalties()
    {
        if (!Object.HasStateAuthority) return;

        // 각 타겟별로 받은 패널티 수집
        Dictionary<PlayerRef, List<int>> targetPenalties = new Dictionary<PlayerRef, List<int>>();

        foreach (var kvp in PenaltyVotes)
        {
            var vote = kvp.Value;
            if (!vote.hasVoted) continue;

            if (!targetPenalties.ContainsKey(vote.target))
            {
                targetPenalties[vote.target] = new List<int>();
            }
            targetPenalties[vote.target].Add(vote.penaltyIndex);
        }

        // 각 타겟에게 패널티 일괄 적용
        foreach (var kvp in targetPenalties)
        {
            var target = kvp.Key;
            var penalties = kvp.Value;

            // 가장 많이 받은 패널티 or 첫 번째 패널티 적용
            // (여러 패널티를 동시 적용하려면 foreach로 모두 적용 가능)
            if (penalties.Count > 0)
            {
                int selectedPenalty = penalties[0]; // 또는 투표수로 결정
                RPC_ApplyPenaltyToTarget(target, selectedPenalty);
            }
        }

        // 투표 초기화
        PenaltyVotes.Clear();
    }

    // ===================== 타겟 목록/리스트 UI =====================
    private void BuildTargetList()
    {
        _targets.Clear();
        if (Runner == null) return;

        foreach (var p in Runner.ActivePlayers)
        {
            if (!allowSelfTargetForTest && Runner.LocalPlayer == p) continue;
            _targets.Add(p);
        }

        if (_targets.Count == 0) _targetIndex = -1;
        else if (_targetIndex < 0) _targetIndex = 0;
        else _targetIndex = Mathf.Min(_targetIndex, _targets.Count - 1);
    }

    private void OpenTargetPanel()
    {
        if (targetPanelRoot) targetPanelRoot.SetActive(true);
        if (targetTitleText) targetTitleText.text = targetTitleMessage;
        RebuildTargetListUI();
        RefreshTargetListVisuals();
        EnsureTargetSelectedVisible();
    }

    private void CloseTargetPanel()
    {
        if (targetPanelRoot) targetPanelRoot.SetActive(false);
        if (targetTitleText) targetTitleText.text = "";
        ClearTargetListUI();
    }

    private void RebuildTargetListUI()
    {
        ClearTargetListUI();

        if (Runner == null || targetContent == null || targetItemPrefab == null) return;

        for (int i = 0; i < _targets.Count; i++)
        {
            var pref = _targets[i];

            var go = Instantiate(targetItemPrefab, targetContent);
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            var label = go.transform.Find("Label")?.GetComponent<TMP_Text>();

            if (rt) rt.localScale = Vector3.one;
            if (img) img.color = targetBgNormal;
            if (label) label.text = ResolvePlayerName(pref);

            var item = new TargetItem
            {
                go = go,
                rt = rt,
                bg = img,
                label = label,
                pref = pref
            };
            _targetItems.Add(item);
        }

        if (_targets.Count == 0) _targetIndex = -1;
        else _targetIndex = Mathf.Clamp(_targetIndex, 0, _targets.Count - 1);
    }

    private void ClearTargetListUI()
    {
        foreach (var it in _targetItems)
        {
            it.colorTw?.Kill();
            it.scaleTw?.Kill();
            if (it.go) Destroy(it.go);
        }
        _targetItems.Clear();
    }

    private void RefreshTargetListVisuals()
    {
        for (int i = 0; i < _targetItems.Count; i++)
        {
            var it = _targetItems[i];
            bool sel = (i == _targetIndex);

            if (it.bg)
            {
                it.colorTw?.Kill();
                it.colorTw = it.bg.DOColor(sel ? targetBgSelected : targetBgNormal, targetColorTween)
                                  .SetEase(Ease.OutQuad);
            }

            if (targetUsePulse && it.rt)
            {
                it.scaleTw?.Kill();
                if (sel)
                {
                    it.rt.localScale = Vector3.one;
                    it.scaleTw = it.rt.DOScale(targetPulseScale, targetPulseDur)
                                      .SetLoops(-1, LoopType.Yoyo)
                                      .SetEase(Ease.InOutSine);
                }
                else
                {
                    it.rt.DOScale(1f, 0.15f);
                }
            }

            if (it.label) it.label.fontStyle = sel ? FontStyles.Bold : FontStyles.Normal;
        }

        Canvas.ForceUpdateCanvases();
        if (targetContent) LayoutRebuilder.ForceRebuildLayoutImmediate(targetContent);
    }

    private void EnsureTargetSelectedVisible()
    {
        if (!targetScroll || _targetIndex < 0 || _targetIndex >= _targetItems.Count) return;

        var viewport = targetScroll.viewport;
        var itemRt = _targetItems[_targetIndex].rt;
        var itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, itemRt);
        var viewBounds = new Bounds(Vector3.zero, viewport.rect.size);

        if (itemBounds.max.y > viewBounds.max.y || itemBounds.min.y < viewBounds.min.y)
        {
            float contentH = targetContent.rect.height - viewport.rect.height;
            if (contentH > 1f)
            {
                float centerY = Mathf.Abs(itemRt.anchoredPosition.y);
                float norm = Mathf.Clamp01(centerY / contentH);
                targetScroll.verticalNormalizedPosition = 1f - norm;
            }
        }
    }

    private void CycleTarget()
    {
        BuildTargetList();

        if (_targets.Count == 0)
        {
            _targetIndex = -1;
            UpdateTargetLabel();
            return;
        }

        _targetIndex = (_targetIndex + 1) % _targets.Count;
        UpdateTargetLabel();

        RefreshTargetListVisuals();
        EnsureTargetSelectedVisible();
    }

    private void UpdateTargetLabel()
    {
        if (!targetLabel) return;

        if (_targetIndex >= 0 && _targetIndex < _targets.Count)
        {
            var pref = _targets[_targetIndex];
            targetLabel.text = $"Target: {ResolvePlayerName(pref)}";
        }
        else
        {
            targetLabel.text = "Target: -";
        }
    }

    // ===================== 제재 선택 UI =====================
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

    // ===================== RPC: 패널티 적용 (타겟 본인만 로컬 적용) =====================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyPenaltyToTarget(PlayerRef target, int penaltyIndex)
    {
        // 타겟 본인 클라이언트만 적용
        if (Runner && Runner.LocalPlayer == target)
        {
            ApplyPenaltyLocal(penaltyIndex);
        }
    }
    private NewPlayerController FindLocalPlayerController()
    {
        var allControllers = FindObjectsOfType<NewPlayerController>(true);
        foreach (var controller in allControllers)
        {
            var no = controller.GetComponent<NetworkObject>();
            if (no && no.HasInputAuthority)
            {
                Debug.Log($"[OptionButtonUI] Found local player controller: {controller.gameObject.name}");
                return controller;
            }
        }
        Debug.LogError("[OptionButtonUI] Local player controller NOT found!");
        return null;
    }
    private void ApplyPenaltyLocal(int optionIndex)
    {
        float dur = 5f;

        // 내 Player 컴포넌트 찾기
        NewPlayerController localPlayer = FindLocalPlayerController(); // 이것만 바꾸면 됨

        // Debuff HUD 찾기
        var ui = FindObjectOfType<DebuffUI>(true);

        switch (optionIndex)
        {
            case 0: // 이동속도 제한
                if (localPlayer)
                {
                    localPlayer.SetSpeedLimit(true);
                    StartCoroutine(ReleaseSpeedLimitAfter(localPlayer, dur));
                }
                if (ui) ui.Show(DebuffType.SpeedLimit, dur);
                break;

            case 1: // 무음
                StartCoroutine(MuteSoundFor(dur));
                if (ui) ui.Show(DebuffType.Mute, dur);
                break;

            case 2: // 터널 비전
                if (tunnelVisionMask)
                {
                    tunnelVisionMask.gameObject.SetActive(true);
                    StartCoroutine(DisableTunnelAfter(dur));
                }
                if (ui) ui.Show(DebuffType.TunnelVision, dur);
                break;

            case 3: // UI 잠금
                {
                    var equip = FindObjectOfType<EquipmentManager>(true);
                    if (equip) equip.LockUI(dur);
                    if (ui) ui.Show(DebuffType.UILock, dur);
                    break;
                }

            default:
                break;
        }
    }

    // 로컬 플레이어 찾기 (입력 권한 보유한 Player)
    private Player FindLocalPlayer()
    {
        var allPlayers = FindObjectsOfType<Player>(true);
        foreach (var p in allPlayers)
        {
            var no = p.GetComponent<NetworkObject>();
            if (no && no.HasInputAuthority)
            {
                return p;
            }
        }
        return null;
    }

    // ===================== 코루틴 =====================
    IEnumerator ReleaseSpeedLimitAfter(NewPlayerController player, float sec)
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

    // ===================== INetworkRunnerCallbacks =====================
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef playerRef)
    {
        BuildTargetList();
        UpdateTargetLabel();

        if (_phase == Phase.ChoosingTarget && targetPanelRoot && targetPanelRoot.activeInHierarchy)
        {
            RebuildTargetListUI();
            RefreshTargetListVisuals();
            EnsureTargetSelectedVisible();
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef playerRef)
    {
        BuildTargetList();
        UpdateTargetLabel();

        if (_targetIndex >= 0 && _targetIndex >= _targets.Count)
        {
            _targetIndex = _targets.Count > 0 ? 0 : -1;
            UpdateTargetLabel();
        }

        if (_phase == Phase.ChoosingTarget && targetPanelRoot && targetPanelRoot.activeInHierarchy)
        {
            RebuildTargetListUI();
            RefreshTargetListVisuals();
            EnsureTargetSelectedVisible();
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"[Fusion] Disconnected: {reason}");
        _phase = Phase.Closed;
        if (slideTarget) { slideTarget.DOKill(); slideTarget.anchoredPosition = new Vector2(slideTarget.anchoredPosition.x, slideOutY); }
        if (slideTextGO) slideTextGO.SetActive(false);
        CloseTargetPanel();
        if (waitingPanel) waitingPanel.SetActive(false);
        BuildTargetList();
        UpdateTargetLabel();
    }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
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

    // ===================== 로컬 유틸/보강 =====================
    private string ResolvePlayerName(PlayerRef pref)
    {
        if (Runner != null && Runner.TryGetPlayerObject(pref, out var obj) && obj != null)
        {
            var pi = obj.GetComponent<PlayerInfo>();
            if (pi != null)
            {
                if (!string.IsNullOrEmpty(pi.cachedName)) return pi.cachedName;
                var netName = pi.playerName.ToString();
                if (!string.IsNullOrEmpty(netName)) return netName;
            }
        }

        foreach (var pi in UnityEngine.Object.FindObjectsOfType<PlayerInfo>(true))
        {
            if (pi != null && pi.Object != null && pi.Object.InputAuthority == pref)
            {
                if (!string.IsNullOrEmpty(pi.cachedName)) return pi.cachedName;
                var netName = pi.playerName.ToString();
                if (!string.IsNullOrEmpty(netName)) return netName;
            }
        }

        return $"Player {pref.PlayerId}";
    }
}