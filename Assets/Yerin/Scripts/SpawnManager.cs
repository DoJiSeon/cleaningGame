using UnityEngine;
using TMPro;
using Fusion;

public class SpawnManager : NetworkBehaviour
{
    public static SpawnManager Instance { get; private set; }

    [Header("UI Elements")]
    public TextMeshProUGUI spawnPercentText;
    public UnityEngine.UI.Image cleanGaugeBar;

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;

    // ⭐ [Networked] 프로퍼티 (Header 제거)
    [Networked] private int TotalSpawnedCount { get; set; }
    [Networked] private int CleanedCount { get; set; }

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
        UpdateUI();

        // 디버그 테스트용 (서버만)
        if (Object != null && Object.HasStateAuthority)
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                OnTrashCleaned();
                Debug.Log("[SpawnManager] 테스트: 쓰레기 주움 (P키)");
            }

            if (Input.GetKeyDown(KeyCode.O))
            {
                IncrementSpawnCount();
                Debug.Log("[SpawnManager] 테스트: 쓰레기 생성 (O키)");
            }
        }
    }

    /// <summary>
    /// 쓰레기가 생성될 때 호출 (서버만 호출해야 함!)
    /// </summary>
    public void IncrementSpawnCount()
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            Debug.LogWarning("[SpawnManager] 서버 권한 없음 - IncrementSpawnCount 무시");
            return;
        }

        TotalSpawnedCount++;

        if (showDebugInfo)
            Debug.Log($"[SpawnManager] 쓰레기 생성: {TotalSpawnedCount}개 (청소: {CleanedCount}개, {GetCleanPercentage():F1}%)");
    }

    /// <summary>
    /// 쓰레기를 주웠을 때 호출
    /// </summary>
    public void OnTrashCleaned()
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            Debug.LogWarning("[SpawnManager] 서버 권한 없음 - OnTrashCleaned 무시");
            return;
        }

        CleanedCount++;

        if (showDebugInfo)
            Debug.Log($"[SpawnManager] 쓰레기 청소: {CleanedCount}/{TotalSpawnedCount} ({GetCleanPercentage():F1}%)");

        if (GetCleanPercentage() >= 100f)
        {
            OnAllTrashCleaned();
        }
    }

    public void DecrementSpawnCount()
    {
        OnTrashCleaned();
    }

    void OnAllTrashCleaned()
    {
        Debug.Log("🎉 [SpawnManager] 모든 쓰레기 청소 완료!");
    }

    void UpdateUI()
    {
        float percentage = GetCleanPercentage();
        int percentInt = Mathf.RoundToInt(percentage);

        if (spawnPercentText != null)
        {
            spawnPercentText.text = $"{percentInt}%";
        }

        if (cleanGaugeBar != null)
        {
            cleanGaugeBar.fillAmount = percentage / 100f;
        }
    }

    public float GetCleanPercentage()
    {
        if (TotalSpawnedCount <= 0)
            return 0f;

        return Mathf.Clamp((CleanedCount / (float)TotalSpawnedCount) * 100f, 0f, 100f);
    }

    public void ResetRound()
    {
        if (Object != null && !Object.HasStateAuthority)
            return;

        TotalSpawnedCount = 0;
        CleanedCount = 0;

        Debug.Log("[SpawnManager] 라운드 초기화");
    }

    public void ResetRoundCounts() => ResetRound();
    public float GetDeSpawnPercentage() => GetCleanPercentage();

    public int GetTotalSpawned() => TotalSpawnedCount;
    public int GetCleanedCount() => CleanedCount;
    public int GetRemainingTrash() => TotalSpawnedCount - CleanedCount;
}