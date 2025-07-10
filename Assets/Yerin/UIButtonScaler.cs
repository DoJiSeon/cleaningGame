using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class UIButtonScalerMover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Vector3 originalScale;
    private Vector3 originalPosition;

    public float scaleFactor = 1.1f;
    public float moveDistance = 40f; // 오른쪽으로 이동할 거리
    public float duration = 0.2f;

    void Start()
    {
        originalScale = transform.localScale;
        originalPosition = transform.localPosition;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.DOScale(originalScale * scaleFactor, duration).SetEase(Ease.OutBack);
        transform.DOLocalMove(originalPosition + new Vector3(moveDistance, 0f, 0f), duration).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.DOScale(originalScale, duration).SetEase(Ease.InOutQuad);
        transform.DOLocalMove(originalPosition, duration).SetEase(Ease.InOutQuad);
    }
}
