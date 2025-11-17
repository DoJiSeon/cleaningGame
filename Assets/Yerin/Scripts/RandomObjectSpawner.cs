using System.Collections.Generic;
using UnityEngine;
using Fusion; 

public class RandomObjectSpawner : NetworkBehaviour
{
    [Header("스폰 리소스")]
    public GameObject[] myObjects;

    [Tooltip("Floor 타일들의 최상위 부모 오브젝트")]
    public Transform parentObject;

    [Header("스폰 설정")]
    [SerializeField] private float yFixedValue = -9.5f;
    [SerializeField] private int spawnCount = 50;

    [Header("콜라이더 랜덤 스폰 설정")]
    [Tooltip("콜라이더 경계에서 안쪽 여백")]
    [SerializeField] private float edgeMargin = 0.3f;

    [Header("TrashItem 자동 추가")]
    [Tooltip("생성된 오브젝트에 TrashItem 스크립트 자동 추가")]
    [SerializeField] private bool autoAddTrashComponent = true;

    private List<Collider> floorColliders = new List<Collider>();

    // Start() ���� Spawned()�� �����մϴ�.
    public override void Spawned()
    {
        // Runner.Spawn을 사용하려면 서버 권한이 필요합니다
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[Spawner] 서버 권한이 없어 스폰을 건너뜁니다.", this);
            return;
        }

        if (!ValidateSetup())
            return;

        CollectFloorColliders();

        if (floorColliders.Count == 0)
        {
            Debug.LogError("[Spawner] 사용 가능한 콜라이더를 찾을 수 없습니다!", this);
            return;
        }

        SpawnObjectsOnStart(spawnCount);
    }

    bool ValidateSetup()
    {
        if (!parentObject)
        {
            Debug.LogError("[Spawner] parentObject가 할당되지 않았습니다.", this);
            return false;
        }

        if (myObjects == null || myObjects.Length == 0)
        {
            Debug.LogError("[Spawner] myObjects 배열이 비어있습니다.", this);
            return false;
        }

        for (int i = 0; i < myObjects.Length; i++)
        {
            if (!myObjects[i])
            {
                Debug.LogError($"[Spawner] myObjects[{i}]가 null입니다.", this);
                return false;
            }

            // Runner.Spawn을 사용하려면 프리팹에 NetworkObject 컴포넌트가 있어야 합니다
            var networkObj = myObjects[i].GetComponent<NetworkObject>();
            if (networkObj == null)
            {
                networkObj = myObjects[i].GetComponentInParent<NetworkObject>();
            }
            if (networkObj == null)
            {
                Debug.LogWarning($"[Spawner] myObjects[{i}] ({myObjects[i].name})에 NetworkObject 컴포넌트가 없습니다. Runner.Spawn이 실패할 수 있습니다.", this);
            }
        }

        return true;
    }

    void CollectFloorColliders()
    {
        floorColliders.Clear();

        Collider[] allColliders = parentObject.GetComponentsInChildren<Collider>();

        foreach (Collider col in allColliders)
        {
            if (col.gameObject.activeSelf && col.enabled)
            {
                floorColliders.Add(col);
            }
        }

        Debug.Log($"[Spawner] {floorColliders.Count}개의 콜라이더 발견, {spawnCount}개 생성 예정", this);
    }

    void SpawnObjectsOnStart(int count)
    {
        int successCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (SpawnRandomObjectInRandomCollider())
                successCount++;
        }

        Debug.Log($"[Spawner] 생성 완료: {successCount}/{count}", this);
    }

    bool SpawnRandomObjectInRandomCollider()
    {
        if (floorColliders.Count == 0)
            return false;

        // Runner와 서버 권한 확인
        if (Runner == null)
        {
            Debug.LogError("[Spawner] Runner가 null입니다!", this);
            return false;
        }

        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[Spawner] 서버 권한이 없어 스폰을 건너뜁니다.", this);
            return false;
        }

        // 랜덤 콜라이더 선택
        Collider randomCollider = floorColliders[Random.Range(0, floorColliders.Count)];

        // 콜라이더 내부 랜덤 위치
        Vector3 spawnPos = GetRandomPointInsideCollider(randomCollider);

        // 랜덤 프리팹 선택
        int prefabIndex = Random.Range(0, myObjects.Length);
        GameObject prefab = myObjects[prefabIndex];

        try
        {
            // 네트워크 오브젝트로 스폰
            Quaternion rotation = Quaternion.identity;
            NetworkObject spawnedNetworkObj = Runner.Spawn(prefab, spawnPos, rotation, Object.InputAuthority);
            
            if (spawnedNetworkObj == null)
            {
                Debug.LogError($"[Spawner] Runner.Spawn 실패: {prefab.name}", this);
                return false;
            }

            GameObject spawnedObject = spawnedNetworkObj.gameObject;
            spawnedObject.name = $"{prefab.name}_{randomCollider.gameObject.name}";

            Debug.Log($"[Spawner] 네트워크 오브젝트 스폰 성공: {spawnedObject.name}, NetworkId: {spawnedNetworkObj.Id}");

            // TrashItem 컴포넌트 자동 추가 (프리팹에 없다면)
            if (autoAddTrashComponent && spawnedObject.GetComponent<TrashItem>() == null)
            {
                spawnedObject.AddComponent<TrashItem>();
            }

            // SpawnManager에 생성 알림 ⭐
            if (SpawnManager.Instance != null)
            {
                SpawnManager.Instance.IncrementSpawnCount();
            }
            else
            {
                Debug.LogWarning("[Spawner] SpawnManager.Instance가 null입니다!");
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Spawner] 생성 실패: {e.Message}", this);
            return false;
        }
    }

    Vector3 GetRandomPointInsideCollider(Collider col)
    {
        Bounds bounds = col.bounds;

        float minX = bounds.min.x + edgeMargin;
        float maxX = bounds.max.x - edgeMargin;
        float minZ = bounds.min.z + edgeMargin;
        float maxZ = bounds.max.z - edgeMargin;

        // 범위 체크
        if (minX >= maxX)
        {
            minX = maxX = bounds.center.x;
        }
        if (minZ >= maxZ)
        {
            minZ = maxZ = bounds.center.z;
        }

        float randomX = Random.Range(minX, maxX);
        float randomZ = Random.Range(minZ, maxZ);

        return new Vector3(randomX, yFixedValue, randomZ);
    }

    /// <summary>
    /// 런타임 추가 생성
    /// </summary>
    public void SpawnAdditionalObjects(int count)
    {
        // 서버 권한 확인
        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[Spawner] 서버 권한이 없어 추가 스폰을 건너뜁니다.", this);
            return;
        }

        Debug.Log($"[Spawner] 추가 생성: {count}개", this);
        SpawnObjectsOnStart(count);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!parentObject)
            return;

        Collider[] colliders = parentObject.GetComponentsInChildren<Collider>();

        foreach (Collider col in colliders)
        {
            if (!col.gameObject.activeSelf || !col.enabled)
                continue;

            Bounds bounds = col.bounds;

            // 콜라이더 전체 (파란색)
            Gizmos.color = new Color(0, 1, 1, 0.3f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // 스폰 가능 영역 (초록색)
            Vector3 safeSize = bounds.size - Vector3.one * (edgeMargin * 2);
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawWireCube(bounds.center, safeSize);
        }
    }
#endif
}

