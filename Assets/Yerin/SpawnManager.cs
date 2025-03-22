using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpawnManager : MonoBehaviour
{
    private int spawnedCount = 0;  // 생성된 아이템 개수
    private int targetCount= 0; // 청소한 개수

    public static SpawnManager Instance { get; private set; }

    [Header("UI Elements")]
    public TextMeshProUGUI spawnPercentText;


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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            DecrementSpawnCount();
          
        }
    }

    public void IncrementSpawnCount()
    {
        spawnedCount++;  // 아이템 개수 증가
        UpdateUI();
    }

    public void DecrementSpawnCount() // 삭제될 때 호출
    {
        targetCount++;  // 삭제한 개수 증가
        UpdateUI();
    }


    public float GetDeSpawnPercentage()
    {
        return (targetCount / (float)spawnedCount);
    }

    private void UpdateUI()
    {
        float percentage = GetDeSpawnPercentage(); 
        int percentInt = Mathf.RoundToInt(percentage * 100f);

        if (spawnPercentText != null)
        {
            spawnPercentText.text = percentInt + "%";
           
        }
    }
}
