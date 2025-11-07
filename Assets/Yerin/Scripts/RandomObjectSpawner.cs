using System.Collections.Generic;
using UnityEngine;

public class RandomObjectSpawner : MonoBehaviour
{
    [Header("스폰 리소스")]
    public GameObject[] myObjects;           // 생성할 프리팹들
    public Transform parentObject;           // 부모(Plane 등)

    [Header("스폰 설정")]
    [SerializeField] public float yFixedValue = -9.5f; // y 고정
    [SerializeField] private int spawnCount = 5;       // 생성 개수

    private readonly List<Vector3> spawnPositions = new List<Vector3>();

    void Start()
    {
        if (!parentObject)
        {
            Debug.LogError("[Spawner] parentObject가 비어있습니다.");
            return;
        }

        // 자식 포인트 수집
        spawnPositions.Clear();
        foreach (Transform child in parentObject)
        {
            spawnPositions.Add(child.position);
        }

        if (spawnPositions.Count == 0)
        {
            Debug.LogError("[Spawner] spawnPositions 비어있음. parentObject의 자식들을 확인하세요.");
            return;
        }

        Debug.Log($"[Spawner] 포인트 {spawnPositions.Count}개, 프리팹 {myObjects.Length}개, 요청 생성 {spawnCount}개");

        SpawnObjectsOnStart(spawnCount);
    }

    void SpawnObjectsOnStart(int count)
    {
        int created = 0;

        for (int i = 0; i < count; i++)
        {
            bool ok = SpawnRandomObject();
            if (ok) created++;
            else
            {
                // 실패 원인을 빨리 찾기 위한 로그
                Debug.LogWarning($"[Spawner] #{i} 스폰 실패. (프리팹/포인트/매니저 등 확인)");
            }
        }

        Debug.Log($"[Spawner] 생성 완료: {created}/{count}");
    }

    bool SpawnRandomObject()
    {
        if (spawnPositions.Count == 0 || myObjects == null || myObjects.Length == 0)
        {
            Debug.LogError("[Spawner] 스폰할 오브젝트 또는 위치가 없습니다.");
            return false;
        }

        int prefabIndex = Random.Range(0, myObjects.Length);
        int posIndex = Random.Range(0, spawnPositions.Count);

        Vector3 basePos = spawnPositions[posIndex];
        Vector3 spawnPos = new Vector3(basePos.x, yFixedValue, basePos.z);

        GameObject prefab = myObjects[prefabIndex];
        if (!prefab)
        {
            Debug.LogError("[Spawner] 프리팹 배열에 null이 있습니다.");
            return false;
        }

        GameObject go = null;
        try
        {
            // ★ y고정 적용 + 부모 연결
            go = Instantiate(prefab, spawnPos, Quaternion.identity, parentObject);

            // 선택: 이름에 인덱스 표기해서 Hierarchy에서 확인하기 쉽게
            go.name = $"{prefab.name}_Spawned_{posIndex}";
        }
        catch (System.SystemException e)
        {
            Debug.LogError($"[Spawner] Instantiate 예외: {e}");
            return false;
        }

        // 선택: SpawnManager 연동이 문제 원인인지 분리 확인
        try
        {
            if (SpawnManager.Instance != null)
                SpawnManager.Instance.IncrementSpawnCount();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Spawner] SpawnManager 호출 중 예외: {e}");
            // 매니저 문제라면 여기서 막힐 수 있으니, 임시 주석 후 테스트해봐
        }

        return go != null;
    }
}
