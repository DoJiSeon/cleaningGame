using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private float rotateSpeedX = 3;
    private float rotateSpeedY = 5;
    private float limitMinX = -80;
    private float limitMaxX = 50;
    private float eulerAngleX;
    private float eulerAngleY;
   
    public void RotateTo(float mouseX, float mouseY)
    {
        eulerAngleX -= mouseY * rotateSpeedY; 
        eulerAngleY += mouseX * rotateSpeedX; // ���콺�� �¿�� �����̴� mouseX�� ī�޶� ������Ʈ�� y���� ȸ���ؾ���

        eulerAngleX = ClampAngle(eulerAngleX, eulerAngleY, limitMaxX);
        // x�� ȸ���� ������ �R

        transform.rotation = Quaternion.Euler(eulerAngleX, eulerAngleY, 0);
        // ����Ƽ�� ȸ���� ����
    }

    private float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360) angle += 360;
        if (angle >  360) angle -= 360;

        return Mathf.Clamp(angle, min, max);
    }
}
