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
