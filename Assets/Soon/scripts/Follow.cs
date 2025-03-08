using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Follow : MonoBehaviour
{
    public Transform playerCamera;
    public Vector2 sensitivity;

    private Vector2 rotXY;

    void Update()
    {
        Vector2 MouseInput = new Vector2
        {
            x = Input.GetAxis("Mouse X"),
            y = Input.GetAxis("Mouse Y")
        };

        rotXY.x -= MouseInput.y * sensitivity.y;
        rotXY.y += MouseInput.x * sensitivity.x;

        rotXY.x = Mathf.Clamp(rotXY.x, -70f, 70f);

        transform.eulerAngles = new Vector3(0f, rotXY.y, 0f);
        playerCamera.localEulerAngles = new Vector3(rotXY.x, 0f, 0f);

    }
}
