using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement3D : MonoBehaviour
{

    [SerializeField]
    private float movespeed = 5.0f;

    [SerializeField]
    private float jumpforce = 3.0f;

    [SerializeField]
    private Transform cameraTransform;
    private CharacterController characterController;

    private float gravity = -9.81f;
    private Vector3 moveDirection;

    
  
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }


    private void Update()
    {
        if( characterController.isGrounded == false)
        {
            moveDirection.y += gravity * Time.deltaTime;
        }
        characterController.Move(moveDirection * movespeed * Time.deltaTime);
    }

    public void MoveTo(Vector3 direction)
    {
        Vector3 movedis = cameraTransform.rotation * direction;
        moveDirection = new Vector3(movedis.x, moveDirection.y, movedis.z);
    }

    public void JumpTo()
    {
        if( characterController.isGrounded == true)
        {
            moveDirection.y = jumpforce;
        }
    }
}
