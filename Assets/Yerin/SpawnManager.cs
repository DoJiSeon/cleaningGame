using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    private int spawnedCount = 0;  // 생성된 아이템 개수
    private int targetCount = 1000; // 기준 개수

    public static SpawnManager Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void IncrementSpawnCount()
    {
        spawnedCount++;  // 아이템 개수 증가
        DisplaySpawnPercentage();
    }

    public float GetSpawnPercentage()
    {
        return (spawnedCount / (float)targetCount) * 100f;
    }

    private void DisplaySpawnPercentage()
    {
        Debug.Log($"생성된 아이템: {spawnedCount}/{targetCount} ({GetSpawnPercentage():F2}%)");
    }
}
