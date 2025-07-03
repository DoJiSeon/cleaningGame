using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public enum PlayerRole { Citizen, Imposter }

public class EquipmentManager : MonoBehaviour
{
    [Header("플레이어 역할")]
    public PlayerRole playerRole;

    [Header("플레이어 Transform")]
    public Transform handTransform; // ��� ������ ��ġ

    [Header("시민 장비")]
    public List<GameObject> citizenEquipments;

    [Header("임포스터 장비")]
    public List<GameObject> imposterEquipments;

    [Header("UI 이미지")]
    public Image equipmentIconImage; // ��� �������� ǥ���� UI �̹���
    //public Text equipmentNameText;   // ��� �̸��� ǥ���� UI �ؽ�Ʈ

    [Header("플레이어 애니메이터")]
    public Animator playerAnimator;

    private List<GameObject> currentEquipmentList;
    private int currentIndex = 0;
    private GameObject currentEquipmentInstance;

    public GameObject trashPrefab;          // 생성할 쓰레기 프리팹
    public float trashSpawnDistance = 2f;   // 히트가 없을 때 카메라 앞까지의 거리
    public LayerMask groundMask;            // 레이캐스트가 맞아야 하는 레이어(예: Ground)

    void Start()
    {
        // �÷��̾� ���ҿ� ���� ��� ����Ʈ ����
        currentEquipmentList = (playerRole == PlayerRole.Citizen) ? citizenEquipments : imposterEquipments;

        // �ʱ� ��� ���� �� UI ������Ʈ
        EquipItem(currentIndex);
    }

    void Update()
    {
        // ���� ���� ��ȯ (Q Ű)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentIndex = (currentIndex - 1 + currentEquipmentList.Count) % currentEquipmentList.Count;
            EquipItem(currentIndex);
        }
        // ���� ���� ��ȯ (E Ű)
        else if (Input.GetKeyDown(KeyCode.E))
        {
            currentIndex = (currentIndex + 1) % currentEquipmentList.Count;
            EquipItem(currentIndex);
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            EquipmentBase equipmentScript = currentEquipmentInstance.GetComponent<EquipmentBase>();

            // ���� ��� �´� �ִϸ��̼� Ʈ���� ����
            if (playerAnimator != null)
            {
                if (equipmentScript != null && !string.IsNullOrEmpty(equipmentScript.interactAnimationTrigger))
                {
                    playerAnimator.SetTrigger(equipmentScript.interactAnimationTrigger);
                }
                else
                {
                    playerAnimator.SetTrigger("DefaultInteract");
                }
            }

            // ��ȣ�ۿ� ���� ȣ�� (�ʿ� �� �ִϸ��̼� �̺�Ʈ�� ����ȭ)
            if (equipmentScript != null)
            {
                equipmentScript.Interact();
            }
        }

        if (playerRole == PlayerRole.Imposter && currentIndex == 2)
        {
            if (Input.GetKeyDown(KeyCode.R))
            {
                SpawnTrash();
            }

        }
    }

    void SpawnTrash()
    {
        Camera cam = Camera.main;
        Vector3 spawnPos;

        // 카메라 정면으로 레이캐스트
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 5f, groundMask))
        {
            // 땅이나 맞은 지점에 배치
            spawnPos = hit.point;
        }
        else
        {
            // 맞은 지점이 없으면 카메라 앞 고정 거리
            spawnPos = cam.transform.position + cam.transform.forward * trashSpawnDistance;
        }

        // 약간 위로 올려서 땅에 파묻히지 않도록
        spawnPos.y += 0.05f;

        Instantiate(trashPrefab, spawnPos, Quaternion.identity);
    }


    void EquipItem(int index)
    {
        // ���� ��� ����
        if (currentEquipmentInstance != null)
        {
            Destroy(currentEquipmentInstance);
        }

        // ���ο� ��� �ν��Ͻ� ���� �� �÷��̾��� �տ� ����
        GameObject equipmentPrefab = currentEquipmentList[index];
        currentEquipmentInstance = Instantiate(equipmentPrefab, handTransform);
        currentEquipmentInstance.transform.localPosition = Vector3.zero;
        currentEquipmentInstance.transform.localRotation = Quaternion.identity;

        // ��� �ʱ�ȭ �� UI ������Ʈ
        EquipmentBase equipmentScript = currentEquipmentInstance.GetComponent<EquipmentBase>();
        if (equipmentScript != null)
        {
            equipmentScript.Initialize();

            // UI ������Ʈ: �����ܰ� �̸� ǥ��
            if (equipmentIconImage != null)
            {
                equipmentIconImage.sprite = equipmentScript.equipmentIcon;
            }
            //if (equipmentNameText != null)
            //{
            //    equipmentNameText.text = equipmentScript.equipmentName;
            //}
        }
    }
}
