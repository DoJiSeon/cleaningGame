using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerContoller : MonoBehaviour
{
    private Movement3D movement3D;

    [SerializeField]
    private KeyCode jumpkeycode = KeyCode.Space;

    [SerializeField]
    private CameraController cameraController;
 
    private void Awake()
    {
        movement3D = GetComponent<Movement3D>();

    }

    private void Update()
    {
        // 좌우상하 이동
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        movement3D.MoveTo(new Vector3(x, 0, z));


        // 점프
        if(Input.GetKeyDown(jumpkeycode))
        {
            movement3D.JumpTo();
        }

        // 마우스 움직임
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        cameraController.RotateTo(mouseX, mouseY);
    }
}
