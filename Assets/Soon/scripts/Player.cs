using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    // Start is called before the first frame update
    public float moveSmoothTime;
    public float gravityStrength;
    public float jumpStrength;
    public float walkSpeed;
    public float runSpeed;
    private CharacterController Controller;
    private Vector3 currentMoveVelocity;
    private Vector3 currentForceVelocity;
    private Vector3 moveDampVelocity;

    void Start()
    {
        Controller = GetComponent<CharacterController> ();
    }

    void Update()
    {
        Vector3 PlayerInput = new Vector3
        {
            x = Input.GetAxisRaw("Horizontal"),
            y = 0f,
            z = Input.GetAxisRaw("Vertical")
        };

        if (PlayerInput.magnitude >1f)
        {
            PlayerInput.Normalize();
        }

        Vector3 MoveVector = transform.TransformDirection(PlayerInput);
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
             
        currentMoveVelocity = Vector3.SmoothDamp(
            currentMoveVelocity,
            MoveVector * currentSpeed,
            ref moveDampVelocity,
            moveSmoothTime
            );

        Controller.Move(currentMoveVelocity * Time.deltaTime);

        Ray groundChechRay = new Ray(transform.position, Vector3.down);
        if (Physics.Raycast(groundChechRay, 1.1f))
        {
            currentForceVelocity.y = -2f;
            if(Input.GetKey(KeyCode.Space))
            {
                currentForceVelocity.y = jumpStrength;
            }
        }
        else
        {
            currentForceVelocity.y -= gravityStrength * Time.deltaTime;
        }
  
    }
}
