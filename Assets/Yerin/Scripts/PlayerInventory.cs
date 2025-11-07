using Fusion;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    // 네트워크로 동기화되는 키 보유 여부
    [Networked] public NetworkBool LocalHasKey { get; private set; }

    // 서버(호스트)에서만 변경
    public void Server_SetHasKey(bool value)
    {
        if (!Object || !Object.HasStateAuthority) return;
        LocalHasKey = value;
    }

}
