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
        eulerAngleY += mouseX * rotateSpeedX; // 마우스를 좌우로 움직이는 mouseX는 카메라 오브젝트의 y축이 회전해야함

        eulerAngleX = ClampAngle(eulerAngleX, eulerAngleY, limitMaxX);
        // x축 회전은 제한을 둚

        transform.rotation = Quaternion.Euler(eulerAngleX, eulerAngleY, 0);
        // 쿼너티온 회전에 적용
    }

    private float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360) angle += 360;
        if (angle >  360) angle -= 360;

        return Mathf.Clamp(angle, min, max);
    }
}
