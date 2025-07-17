using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(CharacterController))]
public class Player : MonoBehaviour
{
    public Camera playerCamera; 
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float crouchSpeed = 3f;
    public float jumpPower = 7f;
    public float gravity = 10f;

    // �̼����ѿ� �����߰�
    public bool isSpeedLimited = false; // �̼� ���� ����
    public float slowMultiplier = 0.3f; // ���� �� �ӵ� ���� (30%)

    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    private bool isCrouching = false;
    private float curSpeedX;
    private float curSpeedY;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    public bool canMove = true;

    private Vector3 originalCameraPosition;
    public float crouchCameraHeight = 0.5f;

    CharacterController characterController;
    Animator characterAnimator;
    void Start()
    {
        characterAnimator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        originalCameraPosition = playerCamera.transform.localPosition;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
  
        #region Handles Movement
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (isCrouching)
        {
            float speed = crouchSpeed;
            if (isSpeedLimited) speed *= slowMultiplier; // ���� ���� �̼� ���� ����

            curSpeedX = speed * Input.GetAxis("Vertical");
            curSpeedY = speed * Input.GetAxis("Horizontal");
        }
        else
        {
            float speed = isRunning ? runSpeed : walkSpeed;

            if (isSpeedLimited) speed *= slowMultiplier; // �̼� ���� ����

            curSpeedX = canMove ? speed * Input.GetAxis("Vertical") : 0;
            curSpeedY = canMove ? speed * Input.GetAxis("Horizontal") : 0;
        }

        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX + right * curSpeedY);

        float speedValue = new Vector3(curSpeedX, 0, curSpeedY).magnitude;
        characterAnimator.SetFloat("speed", speedValue);

        #endregion

        #region Handles Jumping
        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
            characterAnimator.SetTrigger("jumpTrigger");
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if(!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        #endregion

        #region Handles Crouching
        //if (Input.GetKeyDown(KeyCode.LeftControl))
        //{
        //    isCrouching = !isCrouching; //  ��� ���
        //    characterAnimator.SetBool("isCrouching", isCrouching); // �ִϸ��̼� ���� ����
        //    if (isCrouching)
        //    {              
        //        StartCoroutine(CrouchCameraAdjust(playerCamera.transform.localPosition, new Vector3(originalCameraPosition.x, crouchCameraHeight, originalCameraPosition.z))); // 
        //    }
        //    else
        //    {
        //        StartCoroutine(CrouchCameraAdjust(playerCamera.transform.localPosition, originalCameraPosition)); // 
        //    }
        //}
        #endregion

        #region Handles Rotation
        characterController.Move(moveDirection * Time.deltaTime);

        if(canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }

        #endregion

    }
    public void SetSpeedLimit(bool value)
    {
        isSpeedLimited = value;
    }

    //IEnumerator CrouchCameraAdjust(Vector3 from, Vector3 to)
    //{
    //    float elapsedTime = 0f;
    //    float duration = 0.2f; // �ε巴�� �̵��ϴ� �ð�

    //    while (elapsedTime < duration)
    //    {
    //        playerCamera.transform.localPosition = Vector3.Lerp(from, to, elapsedTime / duration);
    //        elapsedTime += Time.deltaTime;
    //        yield return null;
    //    }

    //    playerCamera.transform.localPosition = to;
    //}
}
