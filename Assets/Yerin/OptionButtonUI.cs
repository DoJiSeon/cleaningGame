using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OptionButtonUI : MonoBehaviour
{ 
    public Animator animator;
    public Image timerBar; 
    public float timeLimit = 10f; // SlideOut까지 남은 시간
  
    private float playTimer = 0f;
    [SerializeField]
    public float interval = 300f; // interval - 10*n 마다 반복 (n = 0,1,...)
    private float nextTriggerTime = 300f;

    private float timer;
    private bool hasAppeared = false;
    private bool hasTriggered = false;

    private int currentIndex = -1; 
    private const int maxOptions = 4;

    void Start()
    {
        nextTriggerTime = interval; // 5분 뒤 첫 실행
        animator.speed = 0f;        // 처음엔 Animator 정지
    }

    void Update()
    {
        // 게임 플레이 타이머
        playTimer += Time.deltaTime;

        // 매 interval(5분)마다 SlideIn 실행
        if (playTimer >= nextTriggerTime)
        {
            ResetUIAndSlideIn(); // 슬라이드 인 실행
            nextTriggerTime += interval; // 다음 트리거 시점 갱신
        }

        if (!hasAppeared) return;

      
        // K 키: Hover 상태 순환
        if (Input.GetKeyDown(KeyCode.K))
        {
            currentIndex = (currentIndex + 1) % maxOptions;
            animator.SetInteger("HoverState", currentIndex);
            Debug.Log($"Hover 상태: {currentIndex}");
        }

        // Enter 키: 현재 상태 클릭 처리
        if (Input.GetKeyDown(KeyCode.Return))
        {
            TriggerSlideOut();
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

        animator.SetTrigger("Click Trigger");
        Debug.Log("SlideOut 발동");
    }

    void ResetUIAndSlideIn()
    {
        timer = timeLimit;
        hasTriggered = false;
        hasAppeared = true;
        currentIndex = -1;

        animator.speed = 1f; // Animator 재생
        animator.ResetTrigger("Click Trigger");
        animator.SetTrigger("SlideInTrigger");

        Debug.Log("SlideIn 반복 실행");
    }
}