// using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using DG.Tweening;
using System.Collections.Generic;

public class TutorialBtnUI : MonoBehaviour
{
    public Image timerBar;
    public float timeLimit = 10f; // SlideOut까지 남은 시간
    private float playTimer = 0f;
    [SerializeField]
    public RectTransform slideTarget; // 애니메이션 대상
    public float slideInY = 0f;       // 최종 위치
    public float slideOutY = -300f;   // 초기 위치
    public float slideDuration = 0.5f;

    [Header("Hover 버튼들")]

    public List<Image> buttonImages;         // 각 버튼의 Image 컴포넌트
    public Color normalColor = Color.white;  // 기본 색상
    public Color highlightColor = new Color(1f, 0.8f, 0.3f, 1f); // 강조 색상 (노란 계열 추천)
    public float colorTweenDuration = 0.2f;

    private HashSet<int> selectedIndices = new HashSet<int>();
    public int totalOptions = 4; // hover 가능한 전체 옵션 수

    private float timer;
    private bool hasAppeared = false;
    private bool hasTriggered = false;

    private int currentIndex = -1;
    private const int maxOptions = 4;

    // 참조
    private Player player; 
    public TutorialManager tutorialManager;


    public Image tunnelVisionMask;

    void Start()
    {
        player = FindObjectOfType<Player>();
        tutorialManager = FindObjectOfType<TutorialManager>();

    }


    void Update()
    {
        // 게임 플레이 타이머
        playTimer += Time.deltaTime;

        if (!hasAppeared) return;


        // K 키: Hover 상태 순환
        if (Input.GetKeyDown(KeyCode.K))
        {
            // 이전 버튼 색상 복원
            if (currentIndex >= 0 && currentIndex < buttonImages.Count)
            {
                buttonImages[currentIndex]
                    .DOColor(normalColor, colorTweenDuration)
                    .SetEase(Ease.OutQuad);
            }

            // 인덱스 순환
            currentIndex = (currentIndex + 1) % buttonImages.Count;
            Debug.Log($"[TutorialBtnUI] Hover 상태: {currentIndex}");

            // 현재 버튼 강조 색상
            if (currentIndex >= 0 && currentIndex < buttonImages.Count)
            {
                buttonImages[currentIndex]
                    .DOColor(highlightColor, colorTweenDuration)
                    .SetEase(Ease.OutBack);
            }
        }

        // Enter 키: 현재 상태 클릭 처리
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (currentIndex >= 0 && currentIndex < totalOptions)
            {
                selectedIndices.Add(currentIndex); // 선택 기록
                Debug.Log($"선택된 Hover: {string.Join(", ", selectedIndices)}");

                TriggerSlideOut();
            }

            // 모든 항목 선택 완료되었는지 확인
            if (selectedIndices.Count >= totalOptions)
            {
                Debug.Log("[TutorialBtnUI] 모든 Hover 선택 완료");
                if (tutorialManager != null)
                {
                    tutorialManager.NextStep();
                }
            }
        }

        // 자동 SlideOut 타이머
        if (!hasTriggered)
        {
            timer -= Time.deltaTime;
            if (timerBar != null)
                timerBar.fillAmount = timer / timeLimit;

            if (timer <= 0f)
            {
                TriggerSlideOut();
            }
        }
    }

    void TriggerSlideOut()
    {
        if (hasTriggered) return;
        hasTriggered = true;

        // 1번: 이속제한
        if (currentIndex == 0 && player != null)
        {
            player.SetSpeedLimit(true); // 이속 제한 걸기
            Debug.Log("이속 제한 ON");
            StartCoroutine(ReleaseSpeedLimitAfterSeconds(5f)); // 5초 후 자동 해제
        }

        // 2번: 사운드 뮤트
        else if (currentIndex == 1)
        {
            StartCoroutine(MuteSoundForSeconds(5f));
        }

        // 3번: 터널 비전효과
        else if (currentIndex == 2)
        {
            if (tunnelVisionMask != null)
            {
                tunnelVisionMask.gameObject.SetActive(true);
                tunnelVisionMask.enabled = true;
                Debug.Log("터널비전 ON");
                StartCoroutine(DisableTunnelVisionAfterSeconds(5f));
            }
        }

        SlideOut();
        Debug.Log("SlideOut 발동");

        if (selectedIndices.Count < totalOptions)
        {
            StartCoroutine(ReopenAfterSeconds(1.5f)); // 애니메이션 끝나고 다시 열기
        }

    }

    public void ResetUIAndSlideIn()
    {
        timer = timeLimit;
        hasTriggered = false;
        hasAppeared = true;
        currentIndex = -1;

        // 버튼 색상 초기화
        for (int i = 0; i < buttonImages.Count; i++)
        {
            buttonImages[i].color = normalColor;
        }

        if (slideTarget != null)
        {

            slideTarget.anchoredPosition = new Vector2(slideTarget.anchoredPosition.x, slideOutY); // 시작 위치

            slideTarget.DOAnchorPosY(slideInY, slideDuration)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    Debug.Log("[TutorialBtnUI] 슬라이드 인 완료");
                });
        }

    }

    void SlideOut()
    {
        if (slideTarget != null)
        {
            slideTarget.DOAnchorPosY(slideOutY, slideDuration).SetEase(Ease.InBack);
        }


    }

    IEnumerator ReleaseSpeedLimitAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (player != null)
        {
            player.SetSpeedLimit(false); // 제한 해제
            Debug.Log("이속 제한 OFF");
        }
    }
    IEnumerator MuteSoundForSeconds(float seconds)
    {
        AudioListener.volume = 0f;
        Debug.Log("사운드 OFF");
        yield return new WaitForSeconds(seconds);
        AudioListener.volume = 1f;
        Debug.Log("사운드 ON");
    }
    IEnumerator DisableTunnelVisionAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (tunnelVisionMask != null)
        {
            tunnelVisionMask.enabled = false;
            tunnelVisionMask.gameObject.SetActive(false);
            Debug.Log("터널비전 OFF");
        }
    }
    IEnumerator ReopenAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        hasTriggered = false; // 다시 선택 가능하게
        ResetUIAndSlideIn();  // 다시 SlideIn
    }

}