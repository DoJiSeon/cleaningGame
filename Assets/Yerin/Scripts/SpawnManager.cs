using UnityEngine;
using TMPro;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("UI Elements")]
    public TextMeshProUGUI spawnPercentText;
    public UnityEngine.UI.Image cleanGaugeBar; // Slider 대신 Image Fill로 게이지 표현 (선택사항)

    [Header("Count Settings")]
    private int totalSpawnedCount = 0;   // 총 생성된 쓰레기 개수 (기존: spawnedCount)
    private int cleanedCount = 0;        // 청소한 쓰레기 개수 (기존: targetCount)

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        UpdateUI();
    }

    void Update()
    {
        // 디버그 테스트용
        if (Input.GetKeyDown(KeyCode.P))
        {
            OnTrashCleaned(); // 쓰레기 주웠을 때
            Debug.Log("[SpawnManager] 테스트: 쓰레기 주움 (P키)");
        }

        if (Input.GetKeyDown(KeyCode.O))
        {
            IncrementSpawnCount(); // 쓰레기 생성 시뮬레이션
            Debug.Log("[SpawnManager] 테스트: 쓰레기 생성 (O키)");
        }
    }

    /// <summary>
    /// 쓰레기가 생성될 때 호출 (RandomObjectSpawner에서 호출)
    /// </summary>
    public void IncrementSpawnCount()
    {
        totalSpawnedCount++;
        UpdateUI();

        if (showDebugInfo)
            Debug.Log($"[SpawnManager] 쓰레기 생성: {totalSpawnedCount}개 (청소: {cleanedCount}개, {GetCleanPercentage():F1}%)");
    }

    /// <summary>
    /// 쓰레기를 주웠을 때 호출 (TrashItem에서 호출)
    /// 기존: DecrementSpawnCount()
    /// </summary>
    public void OnTrashCleaned()
    {
        cleanedCount++;
        UpdateUI();

        if (showDebugInfo)
            Debug.Log($"[SpawnManager] 쓰레기 청소: {cleanedCount}/{totalSpawnedCount} ({GetCleanPercentage():F1}%)");

        // 100% 달성 시
        if (GetCleanPercentage() >= 100f)
        {
            OnAllTrashCleaned();
        }
    }

    /// <summary>
    /// 기존 코드 호환용: DecrementSpawnCount() 별칭
    /// </summary>
    public void DecrementSpawnCount()
    {
        OnTrashCleaned();
    }

    /// <summary>
    /// 모든 쓰레기를 청소했을 때
    /// </summary>
    void OnAllTrashCleaned()
    {
        Debug.Log("🎉 [SpawnManager] 모든 쓰레기 청소 완료!");
        // 여기에 라운드 클리어 로직 추가
        // 예: GameManager.Instance.OnRoundComplete();
    }

    /// <summary>
    /// UI 업데이트
    /// </summary>
    void UpdateUI()
    {
        float percentage = GetCleanPercentage();
        int percentInt = Mathf.RoundToInt(percentage);

        // 텍스트 업데이트
        if (spawnPercentText != null)
        {
            spawnPercentText.text = $"{percentInt}%";
        }

        // 게이지 바 업데이트 (있는 경우)
        if (cleanGaugeBar != null)
        {
            cleanGaugeBar.fillAmount = percentage / 100f;
        }
    }

    /// <summary>
    /// 청소 진행률 계산 (0 ~ 100)
    /// </summary>
    public float GetCleanPercentage()
    {
        if (totalSpawnedCount <= 0)
            return 0f;

        return Mathf.Clamp((cleanedCount / (float)totalSpawnedCount) * 100f, 0f, 100f);
    }

    /// <summary>
    /// 라운드 초기화
    /// </summary>
    public void ResetRound()
    {
        totalSpawnedCount = 0;
        cleanedCount = 0;
        UpdateUI();

        Debug.Log("[SpawnManager] 라운드 초기화");
    }

    // ========== 기존 코드 호환성 함수 (별칭) ==========

    /// <summary>
    /// 기존: ResetRoundCounts() → 새로운: ResetRound()
    /// </summary>
    public void ResetRoundCounts()
    {
        ResetRound();
    }

    /// <summary>
    /// 기존: GetDeSpawnPercentage() → 새로운: GetCleanPercentage()
    /// </summary>
    public float GetDeSpawnPercentage()
    {
        return GetCleanPercentage();
    }

    // ========== 상태 확인 함수 ==========

    /// <summary>
    /// 현재 상태 확인용
    /// </summary>
    public int GetTotalSpawned() => totalSpawnedCount;
    public int GetCleanedCount() => cleanedCount;
    public int GetRemainingTrash() => totalSpawnedCount - cleanedCount;
}