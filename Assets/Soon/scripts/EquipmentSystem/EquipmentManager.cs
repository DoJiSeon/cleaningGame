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
