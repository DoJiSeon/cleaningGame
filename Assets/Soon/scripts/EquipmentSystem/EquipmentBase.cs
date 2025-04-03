using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class EquipmentBase : MonoBehaviour
{
    // UI에 표시할 장비 아이콘과 이름
    public Sprite equipmentIcon;
    public string equipmentName;
    public string interactAnimationTrigger;
    // 장비 초기화 (필요시 오버라이드)
    public virtual void Initialize()
    {
        // 기본 초기화 로직
    }

    // 장비 사용 시 호출되는 함수
    public abstract void Use();

    public abstract void Interact();
}
