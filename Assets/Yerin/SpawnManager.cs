using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    private int spawnedCount = 0;  // ������ ������ ����
    private int targetCount = 1000; // ���� ����

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
        spawnedCount++;  // ������ ���� ����
        DisplaySpawnPercentage();
    }

    public float GetSpawnPercentage()
    {
        return (spawnedCount / (float)targetCount) * 100f;
    }

    private void DisplaySpawnPercentage()
    {
        Debug.Log($"������ ������: {spawnedCount}/{targetCount} ({GetSpawnPercentage():F2}%)");
    }
}
