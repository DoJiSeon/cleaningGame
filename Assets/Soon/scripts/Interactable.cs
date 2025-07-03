using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class Interactable : MonoBehaviour
{
    // Start is called before the first frame update

    Outline outline;
    public string message;

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
        outline.enabled = false;
    }

    public void EnableOutline()
    {
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

    IEnumerator FadeOut()
    {
        float f = 1;
        while (f > 0)
        {
            f -= 0.1f;
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
