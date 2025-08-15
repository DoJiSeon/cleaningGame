using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bucket : MonoBehaviour
{
    public string message => "양동이와 상호작용: R키를 눌러 스펀지를 세척하세요";

    // 상호작용할 때 플레이어 GameObject를 받아서 해당 플레이어의 스펀지를 세척
    public void Interact(GameObject interactor)
    {
        SpongeEquipment playerSponge = interactor.GetComponentInChildren<SpongeEquipment>();
        if (playerSponge != null && playerSponge.isDirty)
        {
            playerSponge.WashSponge();
            Debug.Log("플레이어의 스펀지를 양동이에서 세척했습니다.");
        }
        else
        {
            Debug.Log("세척할 스펀지가 없거나 깨끗합니다.");
        }
    }
}
