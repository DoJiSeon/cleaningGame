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
    public Image equipmentIconImage;
    public Image coolDownImage;

    [Header("플레이어 애니메이터")]
    public Animator playerAnimator;

    private List<GameObject> currentEquipmentList;
    private int currentIndex = 0;
    private GameObject currentEquipmentInstance;

    public GameObject[] trashPrefab;          // 생성할 쓰레기 프리팹
    public float trashSpawnDistance = 2f;   // 히트가 없을 때 카메라 앞까지의 거리
    public LayerMask groundMask;            // 레이캐스트가 맞아야 하는 레이어(예: Ground)

    [Header("행동 쿨타임(초)")]
    public float equipSwitchCooldown = 0.5f;   // Q/E 장비 변경 쿨타임
    public float interactCooldown = 2.5f;      // R 키(상호작용) 쿨타임
    public float trashCooldown = 10f;         // 임포스터 쓰레기 생성 쿨타임

    // 마지막 입력 시각(각 키별)
    private float lastInteractTime = -999f;       // R (상호작용)
    private float lastTrashTime = -999f;          // R (임포스터 쓰레기)

    void Start()
    {
        // �÷��̾� ���ҿ� ���� ��� ����Ʈ ����
        currentEquipmentList = (playerRole == PlayerRole.Citizen) ? citizenEquipments : imposterEquipments;

        // �ʱ� ��� ���� �� UI ������Ʈ
        EquipItem(currentIndex);
    }

    void Update()
    {
        // Q: 이전 장비로 변경
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentIndex = (currentIndex - 1 + currentEquipmentList.Count) % currentEquipmentList.Count;
            EquipItem(currentIndex);
        }
        // E: 다음 장비로 변경
        else if (Input.GetKeyDown(KeyCode.E))
        {
            currentIndex = (currentIndex + 1) % currentEquipmentList.Count;
            EquipItem(currentIndex);
        }


        // R: 장비 상호작용
        if (Input.GetKeyDown(KeyCode.R))
        {
            // 임포스터이면서 "쓰레기" 장비(예: index==2)를 들었을 때는 별도 처리
            if (playerRole == PlayerRole.Imposter && currentIndex == 2)
            {
                if (Time.time - lastTrashTime >= trashCooldown)
                {
                    SpawnTrash();
                    lastTrashTime = Time.time;
                }
            }
            else
            {
                // 일반 상호작용 (R 쿨타임)
                if (Time.time - lastInteractTime >= interactCooldown)
                {
                    EquipmentBase equipmentScript = currentEquipmentInstance.GetComponent<EquipmentBase>();
                    // 애니메이션 트리거
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
                    // 상호작용 메서드 호출
                    if (equipmentScript != null)
                    {
                        equipmentScript.Interact();
                    }
                    lastInteractTime = Time.time;
                }
            }
        }

        UpdateCooldownUI();


    }



    void SpawnTrash()
    {
        Camera cam = Camera.main;
        Vector3 spawnPos;

        // 카메라 정면으로 레이캐스트
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 10f, groundMask))
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

        int index = Random.Range(0, trashPrefab.Length);
        Instantiate(trashPrefab[index], spawnPos, Quaternion.identity);
    }


    void EquipItem(int index)
    {
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

            if (equipmentIconImage != null)
            {
                equipmentIconImage.sprite = equipmentScript.equipmentIcon;
                coolDownImage.sprite = equipmentScript.equipmentIcon;

            }
            //if (equipmentNameText != null)
            //{
            //    equipmentNameText.text = equipmentScript.equipmentName;
            //}
        }
    }


    void UpdateCooldownUI()
    {
        if (coolDownImage == null) return;

        float fill = 0f;
        float elapsed = 0f, cooldown = 1f;

        // 임포스터의 쓰레기 무기
        if (playerRole == PlayerRole.Imposter && currentIndex == 2)
        {
            elapsed = Time.time - lastTrashTime;
            cooldown = trashCooldown;
        }
        // 그 외 R키
        else
        {
            elapsed = Time.time - lastInteractTime;
            cooldown = interactCooldown;
        }
        fill = 1f - Mathf.Clamp01(elapsed / cooldown);

        coolDownImage.fillAmount = fill;
        //coolDownImage.color = (fill > 0f) ? new Color(1, 1, 1, 0.5f) : Color.white;
    }
}
