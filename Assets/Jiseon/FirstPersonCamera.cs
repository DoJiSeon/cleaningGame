using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    public Transform Target;
    public float MouseSensitivity = 10f;
    public Vector3 Offset = new Vector3(0f, 1.8f, 0f); // ← 추가

    private float verticalRotation;
    private float horizontalRotation;

    void LateUpdate()
    {
        if (Target == null) return;

        // 타깃의 월드 위치 + 키높이 오프셋
        transform.position = Target.position + Offset;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * MouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);

        horizontalRotation += mouseX * MouseSensitivity;

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
    }
}
