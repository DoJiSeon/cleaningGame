using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RandomObjectSpawner : MonoBehaviour
{
    public GameObject[] myObjects; // 생성할 오브젝트 배열
    public Transform parentObject; // 원하는 부모 오브젝트 (Plane)
    [SerializeField]
    public float yFixedValue = -9.5f;  // 스폰될 y 값 (고정할 높이)

    private List<Vector3> spawnPositions = new List<Vector3>(); // 자식들의 위치 저장 리스트

  

    void Start()
    {
        Debug.Log("Start() 실행됨. parentObject: " + parentObject.name);

        // parentObject의 자식들 가져오기
        foreach (Transform child in parentObject)
        {
            spawnPositions.Add(child.position);
            Debug.Log("추가된 위치: " + child.position + " (오브젝트 이름: " + child.name + ")");
        }

        if (spawnPositions.Count == 0)
        {
            Debug.LogError("spawnPositions이 비어 있음! parentObject의 자식 오브젝트들을 확인하세요.");
        }
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && spawnPositions.Count > 0) // 스페이스바 입력
        {
            int randomObjectIndex = Random.Range(0, myObjects.Length); // 랜덤 오브젝트 선택
            int randomPosIndex = Random.Range(0, spawnPositions.Count); // 랜덤 위치 선택

            Vector3 spawnPosition = new Vector3(
               spawnPositions[randomPosIndex].x,
               yFixedValue,
               spawnPositions[randomPosIndex].z
           );
            Debug.Log($"스폰 위치: {spawnPosition} (y 고정 값: {yFixedValue})");
            Instantiate(myObjects[randomObjectIndex], spawnPositions[randomPosIndex], Quaternion.identity);
            SpawnManager.Instance.IncrementSpawnCount();
        }
    }
}
