using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpawnManager : MonoBehaviour
{
    private int spawnedCount = 0;  // 생성된 아이템 개수
    private int targetCount = 100; // 기준 개수

    public static SpawnManager Instance { get; private set; }

    [Header("UI Elements")]
    public Image spawnFillImage;


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

    void Start()
    {
        UpdateUI(); // 초기 UI 업데이트
    }


    public void IncrementSpawnCount()
    {
        spawnedCount++;  // 아이템 개수 증가
        UpdateUI();
    }

    public float GetSpawnPercentage()
    {
        return (spawnedCount / (float)targetCount);
    }

    private void UpdateUI()
    {
        float percentage = GetSpawnPercentage(); 

        if (spawnFillImage != null)
        {
            spawnFillImage.fillAmount = percentage; 
        }
    }
}
