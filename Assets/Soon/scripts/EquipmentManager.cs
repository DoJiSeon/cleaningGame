using UnityEngine;

public class EquipmentManager : MonoBehaviour
{
    public PlayerType playerType;
    public Transform handTransform; // 손 위치
    public GameObject spongePrefab, trashPrefab, bottlePrefab, sludgePrefab; // 프리팹 사용

    private EquipmentType currentEquipment = EquipmentType.None;
    private EquipmentType[] availableEquipments;
    private GameObject currentEquipmentObject; // 현재 손에 장착된 장비

    void Start()
    {
        SetAvailableEquipments();
        UpdateEquipment();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q)) SwitchEquipment(-1);
        else if (Input.GetKeyDown(KeyCode.E)) SwitchEquipment(1);
    }

    void SetAvailableEquipments()
    {
        if (playerType == PlayerType.Citizen)
        {
            availableEquipments = new EquipmentType[] { EquipmentType.None, EquipmentType.Sponge };
        }
        else if (playerType == PlayerType.Imposter)
        {
            availableEquipments = new EquipmentType[] { EquipmentType.None, EquipmentType.Sponge, EquipmentType.Trash, EquipmentType.Bottle, EquipmentType.Sludge };
        }
    }

    void SwitchEquipment(int direction)
    {
        int index = System.Array.IndexOf(availableEquipments, currentEquipment);
        index = (index + direction + availableEquipments.Length) % availableEquipments.Length;
        currentEquipment = availableEquipments[index];

        UpdateEquipment();
    }

    void UpdateEquipment()
    {
        // 기존 장비 제거
        if (currentEquipmentObject != null)
        {
            Destroy(currentEquipmentObject);
        }

        // 새 장비 생성
        GameObject newEquipment = null;

        switch (currentEquipment)
        {
            case EquipmentType.Sponge:
                newEquipment = Instantiate(spongePrefab, handTransform);
                break;
            case EquipmentType.Trash:
                newEquipment = Instantiate(trashPrefab, handTransform);
                break;
            case EquipmentType.Bottle:
                newEquipment = Instantiate(bottlePrefab, handTransform);
                break;
            case EquipmentType.Sludge:
                newEquipment = Instantiate(sludgePrefab, handTransform);
                break;
        }

        // 장비를 손에 배치
        if (newEquipment != null)
        {
            newEquipment.transform.localPosition = Vector3.zero;
            newEquipment.transform.localRotation = Quaternion.identity;
            currentEquipmentObject = newEquipment;
        }

        Debug.Log($"현재 장비: {currentEquipment}");
    }
}
