using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class TutorialInfo : MonoBehaviour
{
    public TextMeshProUGUI instructionText;
    public GameObject tutorialPanel;
    public List<TutorialStep> steps = new List<TutorialStep>();

    private int stepIndex = 0;

    void Start()
    {
        instructionText.text = steps[stepIndex].instruction;
    }

    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            NextStep();
        }
    
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
            tutorialPanel.SetActive(false); // Æ©Åä¸®¾ó Á¾·á ½Ã ¼û±è
            instructionText.text = "Æ©Åä¸®¾ó ¿Ï·á!";
        }
    }
}