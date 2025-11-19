using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEngine.UIElements;


[RequireComponent(typeof(CharacterController))]
public class NewPlayerController : NetworkBehaviour
{
    public Camera playerCamera;

    private Vector3 _velocity;
    private bool _jumpPressed;
    public float GravityValue = -9.81f;

    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 9f;
    public float lookSpeed = 2f;
    public float curSpeedX;
    public float curSpeedY;

    private bool isSpeedLimited = false;
    private float slowMultiplier = 0.1f;
    private float lookXLimit = 45f;
    private bool isjumping = false;

    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    private bool canMove = true;

    private Vector3 originalCameraPosition;

    //add 
    [Networked] private NetworkBool IsFrozen { get; set; }

    CharacterController characterController;
    Animator characterAnimator;

    [Header("Role (Inspector Override)")]
    [SerializeField] private bool useInspectorRole = true;                 // 임시/디버그용 스위치
    [SerializeField] private PlayerRole inspectorRole = PlayerRole.Cleaner; // 인스펙터에서 선택

    [Networked] public PlayerRole Role { get; private set; }               // 실제 네트워크 동기화되는 

    private float yaw;                 // 본체 Yaw
    private float mouseXSensitivity = 0.2f; // 마우스 X 민감도(취향대로)

    private float teleportLockUntil = 0f;
    private float pickupLockUntil = 0f;
    // [Networked] public bool IsTeleporting { get; set; }

    public void LockMovementForTeleport(float duration)
    {
        teleportLockUntil = Runner.SimulationTime + duration;
    }

    public void LockMovementForPickup(float duration)
    {
        pickupLockUntil = Runner.SimulationTime + duration;
    }

    // 카메라 방향으로 플레이어 회전
    public void RotateToCameraDirection()
    {
        if (playerCamera == null) return;
        
        float targetY = playerCamera.transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, targetY, 0f);
        yaw = targetY;
    }

    // 특정 위치를 향해 플레이어 회전
    public void RotateToPosition(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        direction.y = 0f; // Y축은 무시하고 수평 방향만
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
            float targetY = targetRotation.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, targetY, 0f);
            yaw = targetY;
        }
    }


    // (임시) 서버가 스폰 시 인스펙터 값 적용 중이라면 그대로 유지
    public void ServerSetRole(PlayerRole role)
    {
        if (HasStateAuthority) Role = role;
    }

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        characterAnimator = GetComponentInChildren<Animator>(true);
        if (characterAnimator == null) Debug.LogError("[Player] Animator not found (check hierarchy).");
        if (playerCamera != null)
            originalCameraPosition = playerCamera.transform.localPosition;

    }

    public override void Spawned()
    {
        characterAnimator = GetComponentInChildren<Animator>(true);
        characterController = GetComponent<CharacterController>();

        if (HasStateAuthority && useInspectorRole)
            Role = inspectorRole;

        // 내 플레이어의 카메라를 자식에서 찾기
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (Object.HasInputAuthority)
        {
            // 로컬만 카메라 ON
            playerCamera.gameObject.SetActive(true);
            originalCameraPosition = playerCamera.transform.localPosition;

            // 혹시 씬에 남아있는 SceneCamera가 있다면 OFF (선택)
            var sceneCam = Camera.main;
            if (sceneCam && sceneCam != playerCamera) sceneCam.gameObject.SetActive(false);

            // Cursor.lockState = CursorLockMode.Locked;
            // Cursor.visible = false;

            ApplyCursorByGameState();
        }
        else
        {
            if (playerCamera) playerCamera.gameObject.SetActive(false);
        }

        yaw = transform.eulerAngles.y;
    }
    private void Update()
    {
        if (!Object.HasInputAuthority) return;

        // 게임 상태 전환 감지해서 커서 갱신
        bool liveNow = GameRuleManager.Instance != null && GameRuleManager.Instance.IsGameLive;
        if (liveNow != _lastLiveState)
            ApplyCursorByGameState();


    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority)
            return;

        if (IsFrozen)
            return;

        // 텔포 직후 이동 금지 (이것만으로 충분)
        if (Runner.SimulationTime < teleportLockUntil)
            return;

        // Pickup 애니메이션 중 이동 금지
        if (Runner.SimulationTime < pickupLockUntil)
            return;

        if (GetInput(out PlayerInputData inputData))
            Move(inputData);
    }

    private float targetPitch, currentPitch, pitchVel;

    private void Move(PlayerInputData inputData)
    {
       

        Vector3 camForward = playerCamera.transform.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = playerCamera.transform.right;
        camRight.y = 0f;
        camRight.Normalize();

        float speed = inputData.run ? runSpeed : walkSpeed;
        if (isSpeedLimited) speed *= slowMultiplier;

        curSpeedX = inputData.move.y * speed; // 앞/뒤
        curSpeedY = inputData.move.x * speed; // 좌/우

        Vector3 move = camForward * curSpeedX + camRight * curSpeedY;

        // ✅ 점프/중력 적용 정리
        if (characterController.isGrounded)
        {
            if (isjumping || characterAnimator.GetBool("isJump"))
            {
                characterAnimator.SetBool("isJump", false); // <-- characterAnimator 사용
                isjumping = false;
            }

            if (inputData.jump) // 점프 입력
            {
                Debug.Log("점프 클릭");
                characterAnimator.SetBool("isJump", true);    // <-- characterAnimator 사용
                isjumping = true;
                Jumping(); // <-- 점프 로직 호출 (이게 빠져있던 것 같습니다)
            }
            else
            {
                moveDirection.y = -1f;
            }

        }
        else
        {
            // 공중이면 중력 적용
            moveDirection.y -= gravity * Runner.DeltaTime;
        }

        move.y = moveDirection.y;

        moveDirection = move;
        characterController.Move(move * Runner.DeltaTime);

        float currentSpeed = new Vector3(curSpeedX, 0f, curSpeedY).magnitude;
        characterAnimator.SetFloat("speed", currentSpeed);

        Vector2 input2D = inputData.move;
        if (input2D.sqrMagnitude > 0.0001f && playerCamera)
        {
            float targetY = playerCamera.transform.eulerAngles.y;
            Quaternion targetRot = Quaternion.Euler(0f, targetY, 0f);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                540f * Runner.DeltaTime   // turnSpeed 사용해도 됨
            );

            // 본체 yaw 기준 유지
            yaw = transform.eulerAngles.y;
        }
    }

    // ✅ 부드러운 화면을 위해 렌더 프레임에서만 카메라 pitch 보간 적용 (선택이지만 강추)
    private void LateUpdate()
    {
        if (!Object || !Object.HasInputAuthority) return;
        currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVel, 0.03f);
        playerCamera.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }


    void Jumping()
    {
        moveDirection.y = jumpPower;
        //isjumping = false;
    }

    public void SetSpeedLimit(bool limited, float multiplier = 0.3f)
    {
        isSpeedLimited = limited;

    }

    public void PlayPickUpCameraMove(Vector3 targetOffset, float duration)
    {
        //StopAllCoroutines();
        StartCoroutine(CameraMoveRoutine(targetOffset, duration));
    }

    private IEnumerator CameraMoveRoutine(Vector3 targetOffset, float duration)
    {
        Vector3 startPos = playerCamera.transform.localPosition;
        Vector3 endPos = originalCameraPosition + targetOffset;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerCamera.transform.localPosition = Vector3.Lerp(startPos, endPos, elapsed / duration);
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerCamera.transform.localPosition = Vector3.Lerp(endPos, originalCameraPosition, elapsed / duration);
            yield return null;
        }
    }

    // 추가
    // 게임 진행 여부 캐시(전/후 전환 감지용)
    private bool _lastLiveState;

    // 커서 토글 헬퍼
    private void ApplyCursorByGameState()
    {
        bool live = GameRuleManager.Instance != null && GameRuleManager.Instance.IsGameLive;

        if (live)
        {
            // 게임 시작 후: 커서 숨김 + 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            // 게임 시작 전: 커서 보임 + 잠금 해제
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        _lastLiveState = live;
    }

    public void TeleportToPosition(Vector3 pos, Quaternion rot)
    {
        // ⭐ 모두 RPC로 통일
        RPC_RequestTeleport(pos, rot);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestTeleport(Vector3 pos, Quaternion rot)
    {
        // State Authority에서만 실행
        if (HasStateAuthority)
        {
            PerformTeleport(pos, rot);
        }
        StartCoroutine(CoDelayedTeleport(pos, rot));
    }

    private IEnumerator CoDelayedTeleport(Vector3 pos, Quaternion rot)
    {
        yield return null;  // 1프레임 대기
        PerformTeleport(pos, rot);
    }

    private void PerformTeleport(Vector3 pos, Quaternion rot)
    {

        IsFrozen = true;
        characterController.enabled = false;
        transform.SetPositionAndRotation(pos, rot);
        characterController.enabled = true;

        // 완전 리셋
        moveDirection = Vector3.zero;
        _velocity = Vector3.zero;
        curSpeedX = 0f;
        curSpeedY = 0f;
        isjumping = false;
        yaw = rot.eulerAngles.y;

        if (characterAnimator != null)
        {
            characterAnimator.SetFloat("speed", 0f);
            characterAnimator.SetBool("isJump", false);
        }

        StartCoroutine(UnfreezeAfterDelay(0.1f));

    }

    private IEnumerator UnfreezeAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        IsFrozen = false;
    }

}
