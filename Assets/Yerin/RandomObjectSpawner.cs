using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RandomObjectSpawner : MonoBehaviour
{
    public GameObject[] myObjects; // ������ ������Ʈ �迭
    public Transform parentObject; // ���ϴ� �θ� ������Ʈ (Plane)
    [SerializeField]
    public float yFixedValue = -9.5f;  // ������ y �� (������ ����)

    private List<Vector3> spawnPositions = new List<Vector3>(); // �ڽĵ��� ��ġ ���� ����Ʈ

  

    void Start()
    {
        Debug.Log("Start() �����. parentObject: " + parentObject.name);

        // parentObject�� �ڽĵ� ��������
        foreach (Transform child in parentObject)
        {
            spawnPositions.Add(child.position);
            Debug.Log("�߰��� ��ġ: " + child.position + " (������Ʈ �̸�: " + child.name + ")");
        }

        if (spawnPositions.Count == 0)
        {
            Debug.LogError("spawnPositions�� ��� ����! parentObject�� �ڽ� ������Ʈ���� Ȯ���ϼ���.");
        }
    }


    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && spawnPositions.Count > 0) // �����̽��� �Է�
        {
            int randomObjectIndex = Random.Range(0, myObjects.Length); // ���� ������Ʈ ����
            int randomPosIndex = Random.Range(0, spawnPositions.Count); // ���� ��ġ ����

            Vector3 spawnPosition = new Vector3(
               spawnPositions[randomPosIndex].x,
               yFixedValue,
               spawnPositions[randomPosIndex].z
           );
            Debug.Log($"���� ��ġ: {spawnPosition} (y ���� ��: {yFixedValue})");
            Instantiate(myObjects[randomObjectIndex], spawnPositions[randomPosIndex], Quaternion.identity);
            SpawnManager.Instance.IncrementSpawnCount();
        }
    }
}
