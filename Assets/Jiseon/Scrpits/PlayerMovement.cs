using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    private Vector3 _velocity;
    private bool _jumpPressed;

    private CharacterController _controller;

    public float PlayerSpeed = 2f;
    public float JumpForce = 5f;
    public float GravityValue = -9.81f;

    public Camera MYcamera;

    // 채팅 스크립트 참조
    private Multiplayerchat chat;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            MYcamera = Camera.main;
            MYcamera.GetComponent<FirstPersonCamera>().Target = transform;
        }

        // 씬에 있는 채팅 스크립트 찾기
        chat = FindObjectOfType<Multiplayerchat>();
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    //  인풋필드 포커스 상태 체크 헬퍼
    private bool IsTyping()
    {
        return chat != null && chat.input != null && chat.input.isFocused;
    }

    void Update()
    {
        // 인풋필드 포커스 중이면 점프 입력 무시
        if (IsTyping())
            return;

        if (Input.GetButtonDown("Jump"))
        {
            _jumpPressed = true;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_controller.isGrounded)
        {
            _velocity = new Vector3(0, -1, 0);
        }

        //  인풋필드 포커스면 이동 입력 차단 (WASD/화살표 포함)
        Vector3 inputDir = IsTyping()
            ? Vector3.zero
            : new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        Quaternion cameraRotationY = Quaternion.Euler(0, MYcamera.transform.rotation.eulerAngles.y, 0);
        Vector3 move = cameraRotationY * inputDir * Runner.DeltaTime * PlayerSpeed;

        _velocity.y += GravityValue * Runner.DeltaTime;

        //  포커스 중이면 점프도 차단
        if (_jumpPressed && _controller.isGrounded && !IsTyping())
        {
            _velocity.y += JumpForce;
        }

        _controller.Move(move + _velocity * Runner.DeltaTime);

        float yaw = MYcamera.transform.rotation.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        _jumpPressed = false;
    }
}
