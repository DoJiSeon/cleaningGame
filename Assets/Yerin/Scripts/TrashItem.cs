using UnityEngine;

/// <summary>
/// 쓰레기 오브젝트에 붙이는 스크립트
/// 플레이어가 주우면 SpawnManager에 알림
/// </summary>
public class TrashItem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool autoDestroyOnPickup = true;
    [SerializeField] private float destroyDelay = 0f;

    [Header("Effects (Optional)")]
    [SerializeField] private GameObject pickupEffectPrefab;
    [SerializeField] private AudioClip pickupSound;

    private bool isPickedUp = false;

    /// <summary>
    /// 플레이어가 쓰레기를 주웠을 때 호출 (외부에서 호출)
    /// </summary>
    public void OnPickedUp()
    {
        if (isPickedUp)
            return; // 중복 방지

        isPickedUp = true;

        // SpawnManager에 청소 알림
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnTrashCleaned();
        }
        else
        {
            Debug.LogWarning("[TrashItem] SpawnManager.Instance가 null입니다!");
        }

        // 이펙트 생성
        if (pickupEffectPrefab != null)
        {
            Instantiate(pickupEffectPrefab, transform.position, Quaternion.identity);
        }

        // 사운드 재생
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // 오브젝트 제거
        if (autoDestroyOnPickup)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    /// <summary>
    /// 트리거 충돌로 자동 수집 (선택 사항)
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // Player 태그 확인
        if (other.CompareTag("Player"))
        {
            OnPickedUp();
        }
    }

    /// <summary>
    /// 키 입력으로 수집 (선택 사항)
    /// </summary>
    void OnInteract()
    {
        // 플레이어가 E키를 눌렀을 때 등
        OnPickedUp();
    }
}