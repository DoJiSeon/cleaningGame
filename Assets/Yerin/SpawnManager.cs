using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpawnManager : MonoBehaviour
{
    private int spawnedCount = 0;  // ������ ������ ����
    private int targetCount = 100; // ���� ����

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
        UpdateUI(); // �ʱ� UI ������Ʈ
    }


    public void IncrementSpawnCount()
    {
        spawnedCount++;  // ������ ���� ����
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
