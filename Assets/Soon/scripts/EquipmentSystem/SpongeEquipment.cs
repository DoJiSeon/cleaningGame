using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpongeEquipment : MonoBehaviour
{
    public int maxUses = 5;
    public int currentUses = 0;
    public bool isDirty = false;

    public void UseSponge()
    {
        if (isDirty)
        {
            Debug.Log("스펀지 세척 필요");
            return;
        }

        currentUses++;

        if (currentUses >= maxUses)
        {
            isDirty = true;
            Debug.Log("스펀지 사용 불가");
        }
    }

    public void WashSponge()
    {
        if (!isDirty) return;

        currentUses = 0;
        isDirty = false;
    }
}
