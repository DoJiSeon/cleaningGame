using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrashEquipment : EquipmentBase
{
    public override void Initialize()
    {
        // 스펀지 초기화 로직 (예: 애니메이션 세팅 등)
        Debug.Log("쓰레기 장비 장착");
    }

    public override void Use()
    {
        // 스펀지 사용 행동 구현 (예: 물걸레질 효과)
        Debug.Log("쓰레기 던짐");
    }

    public override void Interact()
    {
        // 스펀지 사용 행동 구현 (예: 물걸레질 효과)
        Debug.Log("맨손 사용");
    }

}
