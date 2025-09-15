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
    private float slowMultiplier = 0.3f;
    private float lookXLimit = 45f;
    private bool isjumping = false;

    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    private bool canMove = true;

    private Vector3 originalCameraPosition;

    CharacterController characterController;
    Animator characterAnimator;
    [SerializeField, Min(0f)] private float rotateLerp = 12f;

    [Header("Role (Inspector Override)")]
    [SerializeField] private bool useInspectorRole = true;                 // 임시/디버그용 스위치
    [SerializeField] private PlayerRole inspectorRole = PlayerRole.Cleaner; // 인스펙터에서 선택

    [Networked] public PlayerRole Role { get; private set; }               // 실제 네트워크 동기화되는 

    private float yaw;                 // 본체 Yaw
    private float mouseXSensitivity = 0.2f; // 마우스 X 민감도(취향대로)
    [SerializeField] private float turnSpeed = 540f; // 360~720 추천(°/s)

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

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (GetInput(out PlayerInputData input))
        {

            // ② 이동 처리
            Move(input);
        }
    }

    private float targetPitch, currentPitch, pitchVel;

    private void Move(PlayerInputData inputData)
    {
        //yaw += inputData.look.x * mouseXSensitivity;
        //transform.rotation = Quaternion.Euler(0f, yaw, 0f);

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
            isjumping = false;
            if (inputData.jump && !isjumping)
            {
                Debug.Log("점프 클릭");
                //characterAnimator.SetTrigger("jumpTrigger");
                characterAnimator.SetBool("isJump", true);
                moveDirection.y = jumpPower;
                isjumping = true;
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



        characterAnimator.SetFloat("speed", new Vector3(curSpeedX, 0f, curSpeedY).magnitude);

        Vector2 input2D = inputData.move;
        if (input2D.sqrMagnitude > 0.0001f)
        {
            float targetY = playerCamera.transform.eulerAngles.y; // 카메라 yaw
            Quaternion targetRot = Quaternion.Euler(0f, targetY, 0f);

            // ✅ 프레임/틱 독립적이고 직관적인 회전
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnSpeed * Runner.DeltaTime
            );
        }
    }

    // ✅ 부드러운 화면을 위해 렌더 프레임에서만 카메라 pitch 보간 적용 (선택이지만 강추)
    private void LateUpdate()
    {
        if (!Object || !Object.HasInputAuthority) return;
        currentPitch = Mathf.SmoothDampAngle(currentPitch, targetPitch, ref pitchVel, 0.03f);
        playerCamera.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }


    //void Jumping()
    //{
    //    moveDirection.y = jumpPower;
    //    isjumping = false;
    //}

    public void SetSpeedLimit(bool value)
    {
        isSpeedLimited = value;
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

}
