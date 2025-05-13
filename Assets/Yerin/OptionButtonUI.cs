using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

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

    private Player player; // Player 참조

    public Image tunnelVisionMask;

    void Start()
    {
        player = FindObjectOfType<Player>(); // 씬에 있는 Player 자동 참조
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

        // ★ HoverState 초기화 추가
        animator.SetInteger("HoverState", -1); // 애니메이터도 0 또는 -1로 초기화

        animator.SetTrigger("SlideInTrigger");

        Debug.Log("SlideIn 반복 실행");
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
}