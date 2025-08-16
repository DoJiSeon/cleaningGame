using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    // Start is called before the first frame update
    Outline outline;
    public string message;
    private EquipmentState currentState = EquipmentState.None;

    public UnityEvent onIntercation;

    public Renderer MyRenderer;
    void Start()
    {
        outline = GetComponent<Outline>();
        DisableOutline();
    }

    public void Interact()
    {
        onIntercation.Invoke();
    }

    public void DisableOutline()
    {
        if (outline != null)
            outline.enabled = false;
    }

    public void EnableOutline()
    {
        if(outline != null)
            outline.enabled = true;
    }

    public void Destroy()
    {
        Destroy(gameObject);
    }

    public void FadeDestroy()
    {
        StartCoroutine(FadeOut());

    }
    public void TryUseSponge(GameObject interactor)
    {
        if (currentState == EquipmentState.Sponge)
        {
            SpongeEquipment playerSponge = interactor.GetComponentInChildren<SpongeEquipment>();

            if (playerSponge == null)
            {
                Debug.LogWarning("스펀지가 없습니다.");
                return;
            }

            if (playerSponge.isDirty)
            {
                Debug.Log("청소 불가(스펀지 더러움)");
                return;
            }

            StartCoroutine(FadeOut());
        }


    }

    public void Wash(GameObject interactor)
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

    IEnumerator FadeOut()
    {
        float f = 1;
        while (f > 0)
        {
            f -= 0.02f;
            Color ColorAlhpa = MyRenderer.material.color;
            ColorAlhpa.a = f;
            MyRenderer.material.color = ColorAlhpa;
            yield return new WaitForSeconds(0.02f);
        }
        Destroy(gameObject);
    }

    void Update()
    {
        
    }
}
