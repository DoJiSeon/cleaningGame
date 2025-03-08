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
    // Update is called once per frame
    void Update()
    {
        
    }
}
