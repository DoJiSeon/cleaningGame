using System.Collections;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class OpenDoor : NetworkBehaviour
{
    [Header("이동 설정")]
    [SerializeField] private float DoorZPos = 3f;
    [SerializeField] private float DoorYPos = 0f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("사운드(선택)")]
    [SerializeField] private AudioSource audioSource;

    [Networked] private NetworkBool IsOpen { get; set; }

    private void OnTriggerEnter(Collider other)
    {
        if (!Object || !Object.HasStateAuthority) return;
        if (IsOpen) return;
        if (!other || !other.CompareTag("Player")) return;

        var playerNO = other.GetComponentInParent<NetworkObject>();
        if (!playerNO)
        {
            Debug.LogWarning("[Door] Player NetworkObject not found on trigger.", this);
            return;
        }

        var who = playerNO.InputAuthority;
        if (Runner.TryGetPlayerObject(who, out var playerObj) &&
            playerObj.TryGetComponent<PlayerInventory>(out var inv))
        {
            Debug.Log($"[Door] Player {who.PlayerId} HasKey={inv.LocalHasKey}", this);
            if (inv.LocalHasKey && !IsOpen)
                Runner.StartCoroutine(ServerOpenDoor());
        }
        else
        {
            Debug.LogWarning("[Door] PlayerInventoryNet not found.", this);
        }
    }

    private IEnumerator ServerOpenDoor()
    {
        if (IsOpen) yield break;
        IsOpen = true;
        Debug.Log("[Door] Opening...", this);

        if (audioSource) audioSource.Play();

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + new Vector3(0f, DoorYPos, DoorZPos);

        float distance = Vector3.Distance(startPosition, targetPosition);
        float duration = (distance <= 0.001f || moveSpeed <= 0.001f) ? 0f : distance / moveSpeed;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            transform.position = Vector3.Lerp(startPosition, targetPosition, a);
            yield return null;
        }
        transform.position = targetPosition;
        Debug.Log("[Door] Opened.", this);
    }
}
