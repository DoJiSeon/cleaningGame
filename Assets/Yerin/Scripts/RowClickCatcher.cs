using UnityEngine;
using UnityEngine.EventSystems;

public class RowClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public System.Action onClick;
    public void OnPointerClick(PointerEventData e)
    {
        Debug.Log($"[RowClick] {name} clicked ({e.button})");
        onClick?.Invoke();
    }
}
