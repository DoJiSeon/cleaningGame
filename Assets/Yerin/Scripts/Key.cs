using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class Key : NetworkBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (!other || !other.CompareTag(playerTag)) return;

        // 🔧 자식 콜라이더 대응
        var playerNO = other.GetComponentInParent<NetworkObject>();
        if (!playerNO)
        {
            Debug.LogWarning("[Key] No NetworkObject found on player collider or parents.", this);
            return;
        }

        PlayerRef who = playerNO.InputAuthority;
        Debug.Log($"[Key] Picked by PlayerRef {who.PlayerId}", this);

        if (Runner.TryGetPlayerObject(who, out var playerObj))
        {
            if (playerObj.TryGetComponent<PlayerInventory>(out var inv))
            {
                inv.Server_SetHasKey(true);
                Debug.Log("[Key] HasKey set to TRUE on server.", this);
            }
            else
            {
                Debug.LogError("[Key] PlayerInventoryNet not found on player object.", this);
            }
        }
        else
        {
            Debug.LogError("[Key] TryGetPlayerObject failed.", this);
        }

        Runner.Despawn(Object);
    }
}

