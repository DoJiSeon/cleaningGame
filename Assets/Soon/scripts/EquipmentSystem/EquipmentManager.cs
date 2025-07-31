using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public enum EquipmentState
{
    None,      // 맨손
    Sponge,    // 스펀지
    TrashMode  // 임포스터: 쓰레기 생성 모드 (손에 아무것도 없음)
}

public enum PlayerRole
{
    Citizen,
    Imposter
}

public class EquipmentManager : MonoBehaviour
{
    [Header("플레이어 역할")]
    public PlayerRole playerRole;

    [Header("Hand에 직접 배치한 스펀지 오브젝트")]
    public GameObject spongePrefab; // 씬 내 hand 자식으로 직접 할당

    [Header("UI 이미지 (장비 아이콘, 쿨타임 동일)")]
    public Image equipmentIconImage;
    public Image coolDownImage;

    [Header("상태별 아이콘(Inspector에서 각각 할당)")]
    public Sprite handIcon;           // None(맨손) 상태 아이콘
    public Sprite spongeIcon;         // Sponge 상태 아이콘
    public Sprite trashModeIcon;      // TrashMode 상태 아이콘

    [Header("플레이어 애니메이터")]
    public Animator playerAnimator;

    [Header("임포스터: 쓰레기 프리팹")]
    public GameObject[] trashPrefab;
    public float trashSpawnDistance = 2f;
    public LayerMask groundMask;

    [Header("쿨타임(초)")]
    public float equipSwitchCooldown = 0.5f;
    public float interactCooldown = 2.5f;
    public float trashCooldown = 10f;

    private int currentIndex = 0;
    private List<EquipmentState> availableEquipments;
    private EquipmentState currentState = EquipmentState.None;

    private float lastInteractTime = -999f;
    private float lastTrashTime = -999f;

    void Start()
    {
        availableEquipments = new List<EquipmentState>();
        if (playerRole == PlayerRole.Citizen)
        {
            availableEquipments.Add(EquipmentState.None);
            availableEquipments.Add(EquipmentState.Sponge);
        }
        else
        {
            availableEquipments.Add(EquipmentState.None);
            availableEquipments.Add(EquipmentState.Sponge);
            availableEquipments.Add(EquipmentState.TrashMode);
        }

        // 시작할 때 스폰지 항상 off
        if (spongePrefab != null) spongePrefab.SetActive(false);

        SetEquipment(0); // 첫 장비로 초기화
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            currentIndex = (currentIndex - 1 + availableEquipments.Count) % availableEquipments.Count;
            SetEquipment(currentIndex);
        }
        else if (Input.GetKeyDown(KeyCode.E))
        {
            currentIndex = (currentIndex + 1) % availableEquipments.Count;
            SetEquipment(currentIndex);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (currentState == EquipmentState.Sponge)
            {
                if (Time.time - lastInteractTime >= interactCooldown)
                {
                    if (playerAnimator != null)
                        playerAnimator.SetTrigger("cleanTrigger");
                    lastInteractTime = Time.time;
                }
            }
            else if (playerRole == PlayerRole.Imposter && currentState == EquipmentState.TrashMode)
            {
                if (Time.time - lastTrashTime >= trashCooldown)
                {
                    SpawnTrash();
                    lastTrashTime = Time.time;
                    if (playerAnimator != null)
                        playerAnimator.SetTrigger("pickTrigger");
                }
            }
            // 맨손일 땐 필요에 따라 Interact... (상호작용 로직 분기 가능)
        }
        UpdateCooldownUI();
    }

    void SetEquipment(int idx)
    {
        currentState = availableEquipments[idx];

        // 1. 스폰지 제어 (상태 전환 시 확실히 꺼줌)
        if (spongePrefab != null)
            spongePrefab.SetActive(currentState == EquipmentState.Sponge);

        // 2. UI는 무조건 항상 보이게
        if (equipmentIconImage != null) equipmentIconImage.enabled = true;
        if (coolDownImage != null) coolDownImage.enabled = true;

        // 3. 상태별 아이콘 교체
        Sprite iconToUse = null;
        switch (currentState)
        {
            case EquipmentState.None:
                iconToUse = handIcon;
                break;
            case EquipmentState.Sponge:
                iconToUse = spongeIcon;
                break;
            case EquipmentState.TrashMode:
                iconToUse = trashModeIcon;
                break;
        }
        if (equipmentIconImage != null)
            equipmentIconImage.sprite = iconToUse;
        if (coolDownImage != null)
            coolDownImage.sprite = iconToUse;
    }

    void SpawnTrash()
    {
        Camera cam = Camera.main;
        Vector3 spawnPos;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 10f, groundMask))
            spawnPos = hit.point;
        else
            spawnPos = cam.transform.position + cam.transform.forward * trashSpawnDistance;
        spawnPos.y += 0.05f;
        int index = Random.Range(0, trashPrefab.Length);
        Instantiate(trashPrefab[index], spawnPos, Quaternion.identity);
    }

    void UpdateCooldownUI()
    {
        if (coolDownImage == null) return;
        float fill = 0f;
        float elapsed = 0f, cooldown = 1f;

        if (playerRole == PlayerRole.Imposter && currentState == EquipmentState.TrashMode)
        {
            elapsed = Time.time - lastTrashTime;
            cooldown = trashCooldown;
        }
        else if (currentState == EquipmentState.Sponge)
        {
            elapsed = Time.time - lastInteractTime;
            cooldown = interactCooldown;
        }
        else
        {
            fill = 0f;
            coolDownImage.fillAmount = fill;
            return;
        }
        fill = 1f - Mathf.Clamp01(elapsed / cooldown);
        coolDownImage.fillAmount = fill;
    }
}
