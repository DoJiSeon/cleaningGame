using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialManager : MonoBehaviour
{
    public TextMeshProUGUI instructionText;
    public TutorialBtnUI tutorialBtnUI;
    public GameObject tutorialPanel;
    public GameObject completePanel;
    public GameObject controlPopup;
    public GameObject penaltyPanel;

    public List<TutorialStep> steps = new List<TutorialStep>();

    private int cleanedCount = 0;
    private bool coreFound = false;
    private int stepIndex = 0;

    // Step 1 & 3 사용
    private readonly List<Cleanable> step1Spawned = new List<Cleanable>();
    public GameObject[] cleanablePrefabs;
    public int step1SpawnCount = 5;
    public Transform[] step1SpawnPoints;
    public Vector3 randomAreaMin = new Vector3(-5f, -5f, 0.5f);
    public Vector3 randomAreaMax = new Vector3(5f, 5f, 0.5f);

    // Step 1: 스폰 한번용
    private bool hasSpawnedStep1 = false;
    // Step 3: 코어 확률
    public float coreChancePerClean = 0.10f;   // 10 % 확률


    void Start()
    {
        completePanel.SetActive(false);
        controlPopup.SetActive(false);
        penaltyPanel.SetActive(false);
        if (steps != null && steps.Count > 0)
            instructionText.text = steps[stepIndex].instruction;

        // 현재 스텝(초기 0) 진입 후 실행
        EnterStep(stepIndex);  
    }

    void Update()
    {
        // Tab 키: 조작법 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            controlPopup.SetActive(!controlPopup.activeSelf);
        }

        switch (stepIndex)
        {
            
            case 0:
                if (Input.GetKeyDown(KeyCode.Tab)) // 조작법 확인 시 다음 단계로
                    NextStep();
                break;

            case 1:
                // 스폰/청소는 이벤트로 처리
                break;

            case 2:
                if (Input.GetKeyDown(KeyCode.R)) // r키로 생성 시 다음 단계로 >>> 이거 수정해야댐
                {
                    NextStep();
                }
                break;

            case 3:
                // 웨이브 스폰 → 청소될 때마다 10% 확률로 코어 체크는 이벤트에서 처리
                break;

            case 4:
                if (!penaltyPanel.activeSelf)
                {
                    penaltyPanel.SetActive(true); // 패널티 창 열기

                    if (tutorialBtnUI != null)
                    {
                        tutorialBtnUI.ResetUIAndSlideIn(); // 슬라이드 애니메이션 실행

                    }
                }
                break;
        }
    }

    // 스텝 진입
    private void EnterStep(int index)
    {
        switch (index)
        {
            case 1:
                if (!hasSpawnedStep1)
                {
                    SetupStep1();
                    hasSpawnedStep1 = true;
                }
                break;

            case 3:
                Debug.Log("[Tutorial] Step 3 시작 – 코어 찾기 모드");
                coreFound = false;
                cleanedCount = 0;
                SetupStep3Batch();      // 첫 웨이브 스폰
                break;
        }
    }


    // Step 1 스폰 세팅
    private void SetupStep1()
    {
        cleanedCount = 0;

        // 이벤트 해제 & 리스트 초기화
        foreach (var c in step1Spawned)
            if (c != null) c.OnCleaned -= OnCleanableCleaned_Step1;
        step1Spawned.Clear();

        if (cleanablePrefabs == null || cleanablePrefabs.Length == 0)
        {
            Debug.LogError("[Tutorial] cleanablePrefabs 비어 있음. Inspector에서 프리팹 넣어줘.");
            return;
        }

        for (int i = 0; i < step1SpawnCount; i++)
        {
            Vector3 pos;
            if (step1SpawnPoints != null && step1SpawnPoints.Length > 0)
                pos = step1SpawnPoints[i % step1SpawnPoints.Length].position;
            else
                pos = new Vector3(
                    UnityEngine.Random.Range(randomAreaMin.x, randomAreaMax.x),
                    0f,
                    UnityEngine.Random.Range(randomAreaMin.y, randomAreaMax.y)
                );

            var prefab = cleanablePrefabs[UnityEngine.Random.Range(0, cleanablePrefabs.Length)];
            var go = Instantiate(prefab, pos, Quaternion.identity);

            var c = go.GetComponent<Cleanable>() ?? go.AddComponent<Cleanable>();
            c.OnCleaned += OnCleanableCleaned_Step1;
            step1Spawned.Add(c);
        }
    }

    // 모두 청소 시 Step 2로 이동
    private void OnCleanableCleaned_Step1(Cleanable c)
    {
        cleanedCount++;
        Debug.Log($"[Tutorial] Cleaned {cleanedCount}/{step1SpawnCount}");
        c.OnCleaned -= OnCleanableCleaned_Step1;
        step1Spawned.Remove(c);

        if (cleanedCount >= step1SpawnCount)
        {
            Debug.Log("Step 1 완료: Step 2로 이동");
            NextStep();   // 2로 이동
        }
    }

    // Step 3: 웨이브 반복, 청소될 때마다 10% 확률로 코어 판정 (일단)
    private void SetupStep3Batch()
    {
        // 기존 이벤트 해제 & 리스트 초기화
        foreach (var c in step1Spawned)
            if (c != null) c.OnCleaned -= OnCleanableCleaned_Step3;
        step1Spawned.Clear();

        for (int i = 0; i < step1SpawnCount; i++)
        {
            var pos = GetSpawnPos(i);                                   // 기존 함수 재사용
            var prefab = cleanablePrefabs[Random.Range(0, cleanablePrefabs.Length)];
            var go = Instantiate(prefab, pos, Quaternion.identity);

            var c = go.GetComponent<Cleanable>() ?? go.AddComponent<Cleanable>();
            c.OnCleaned += OnCleanableCleaned_Step3;
            step1Spawned.Add(c);
        }
        Debug.Log($"[Tutorial] Step 3 웨이브 스폰: {step1SpawnCount}개");
    }

    private void OnCleanableCleaned_Step3(Cleanable c)
    {
        if (coreFound) return; // 이미 코어가 떴으면 무시

        cleanedCount++;
        Debug.Log($"[Tutorial] (Step3) Cleaned {cleanedCount}/{step1SpawnCount}");

        c.OnCleaned -= OnCleanableCleaned_Step3;
        step1Spawned.Remove(c);

        // 10 % 확률로 코어 등장 판정
        if (Random.value < coreChancePerClean)
        {
            coreFound = true;
            Debug.LogWarning("[Tutorial] 코어 등장! → Step 4로 이동");
            NextStep();            // Step 4
            return;
        }

        // 코어가 안 떴고 웨이브를 다 청소했다면 다음 웨이브
        if (cleanedCount >= step1SpawnCount)
        {
            Debug.Log("[Tutorial] 코어 미등장 – 다음 웨이브 스폰");
            cleanedCount = 0;
            SetupStep3Batch();
        }
    }

    // 공용 스폰 위치 산출
    private Vector3 GetSpawnPos(int i)
    {
        if (step1SpawnPoints != null && step1SpawnPoints.Length > 0)
        {
            var p = step1SpawnPoints[i % step1SpawnPoints.Length].position;
            return new Vector3(p.x, 0f, Random.Range(randomAreaMin.z, randomAreaMax.z)); // y 고정, z 랜덤
        }
        else
        {
            return new Vector3(
                Random.Range(randomAreaMin.x, randomAreaMax.x), // x 랜덤
                0f,                                             // y 고정
                Random.Range(randomAreaMin.z, randomAreaMax.z)  // z 랜덤
            );
        }
    }


    public void NextStep()
    {
        stepIndex++;
        Debug.Log($"[NextStep] stepIndex={stepIndex}, steps.Count={steps.Count}");
        if (stepIndex < steps.Count)
        {
            tutorialPanel.SetActive(true);
            instructionText.text = steps[stepIndex].instruction;

            EnterStep(stepIndex);
        }
        else
        {
            tutorialPanel.SetActive(false);
            instructionText.text = "튜토리얼 완료!";
            completePanel.SetActive(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void LoadMainScene()
    {
        SceneManager.LoadScene(0);
    }
}
