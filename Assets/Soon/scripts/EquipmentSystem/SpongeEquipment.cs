using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpongeEquipment : EquipmentBase
{
    public override void Initialize()
    {
        // ������ �ʱ�ȭ ���� (��: �ִϸ��̼� ���� ��)
        Debug.Log("������ ��� ����");
    }

    public override void Use()
    {
        // ������ ��� �ൿ ���� (��: ���ɷ��� ȿ��)
        Debug.Log("������ ���");
    }

    public override void Interact()
    {
        // ������ ��� �ൿ ���� (��: ���ɷ��� ȿ��)
        Debug.Log("�Ǽ� ���");
    }
}
