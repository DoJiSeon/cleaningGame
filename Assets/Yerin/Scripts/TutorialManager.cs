using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;


public class TutorialManager : MonoBehaviour
{
    public TextMeshProUGUI instructionText;
    public GameObject tutorialPanel;
    public GameObject completePanel;
    public List<TutorialStep> steps = new List<TutorialStep>();

    private int stepIndex = 0;

    void Start()
    {
        completePanel.SetActive(false);
        instructionText.text = steps[stepIndex].instruction;
    }

    void Update()
    {
        //임의
        if (Input.GetKeyDown(KeyCode.Return)) 
        {
            NextStep();
        }

        /* 예시: 간단한 키 입력으로 다음 단계로 넘어가기
        if (stepIndex == 0 && Input.GetKeyDown(KeyCode.W))
        {
            NextStep();
        }
        else if (stepIndex == 1 && Input.GetKeyDown(KeyCode.Space))
        {
            NextStep();
        } */
    }

    void NextStep()
    {
        stepIndex++;
        if (stepIndex < steps.Count)
        {
            tutorialPanel.SetActive(true);
            instructionText.text = steps[stepIndex].instruction;
        }
        else
        {
            tutorialPanel.SetActive(false); // 튜토리얼 종료 시 숨김
            instructionText.text = "튜토리얼 완료!";
            completePanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
    public void LoadMainScene()
    {
        SceneManager.LoadScene(0);  // 씬 이름 맞게 수정
    }
}
