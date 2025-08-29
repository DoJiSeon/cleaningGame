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
    public SpongeEquipment sponge;

    [Header("UI 이미지 (장비 아이콘, 쿨타임 동일)")]
    public Image equipmentIconImage;
    public Image coolDownImage;

    [Header("상태별 아이콘(Inspector에서 각각 할당)")]
    public Sprite handIcon;           // None(맨손) 상태 아이콘
    public Sprite spongeIcon;         // Sponge 상태 아이콘
    public Sprite trashModeIcon;      // TrashMode 상태 아이콘

    [Header("플레이어 애니메이터")]
    public Animator playerAnimator;

    //추가
    [Header("UI 잠금 옵션")]
    public Image uiLockMask;          // 잠금 오버레이
    [Range(0f, 1f)] public float lockedDimAlpha = 0.35f; // 잠금 중 아이콘 투명도

    private bool _isUILocked = false;
    private float _uiLockUntil = 0f;
    private EquipmentState _stateBeforeLock = EquipmentState.None;

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
        //추가
        // UI 잠금 중: 입력 전부 무시
        if (_isUILocked)
        {
            if (Time.time >= _uiLockUntil)
            {
                UnlockUI();
            }
            else
            {
                // 잠금 중엔 쿨다운 UI만 유지하고 입력은 전부 무시
                UpdateCooldownUI();
                return;
            }
        }

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
                if(!sponge.isDirty)
                {
                    sponge.UseSponge();

                    if (Time.time - lastInteractTime >= interactCooldown)
                    {
                        if (playerAnimator != null)
                            playerAnimator.SetTrigger("cleanTrigger");
                        lastInteractTime = Time.time;
                    }
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

    // 추가
    public void LockUI(float seconds)
    {
        if (_isUILocked && Time.time < _uiLockUntil)
        {
            // 이미 잠금 중이면 시간만 연장
            _uiLockUntil = Mathf.Max(_uiLockUntil, Time.time + seconds);
            return;
        }

        _isUILocked = true;
        _uiLockUntil = Time.time + seconds;

        // 현재 상태 저장 후, 입력 완전 차단을 위해 맨손으로 전환
        _stateBeforeLock = currentState;
        // 아이콘/쿨다운은 보이되 "사용 불가" 느낌만 주고, 실제 입력은 위에서 return 처리
        ForceSetNoneForLock();

        ApplyLockVisual(true);
    }

    private void UnlockUI()
    {
        _isUILocked = false;
        _uiLockUntil = 0f;

        // 잠금 전 상태로 복귀
        RestoreStateAfterLock();

        ApplyLockVisual(false);
    }

    private void ForceSetNoneForLock()
    {
        // 시각적으로는 None(맨손)으로 돌려 사용 불가 느낌 강화 (원치 않으면 주석)
        int noneIdx = availableEquipments.IndexOf(EquipmentState.None);
        if (noneIdx >= 0) SetEquipment(noneIdx);
    }

    private void RestoreStateAfterLock()
    {
        int idx = availableEquipments.IndexOf(_stateBeforeLock);
        if (idx >= 0) SetEquipment(idx);
    }

    private void ApplyLockVisual(bool locked)
    {
        // 아이콘 흐리기
        if (equipmentIconImage != null)
        {
            var c = equipmentIconImage.color;
            c.a = locked ? lockedDimAlpha : 1f;
            equipmentIconImage.color = c;
        }
        if (coolDownImage != null)
        {
            var c2 = coolDownImage.color;
            c2.a = locked ? lockedDimAlpha : 1f;
            coolDownImage.color = c2;
        }

        // 오버레이 표시(선택)
        if (uiLockMask != null)
            uiLockMask.gameObject.SetActive(locked);
    }
}
