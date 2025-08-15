using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    public float playerReach = 3.5f;
    Interactable currentInteractable;
    public Player playerScript;

    // Update is called once per frame
    private void Start()
    {
        playerScript = GetComponent<Player>();
    }

    void Update()
    {
        CheckInteraction();
        if (Input.GetKeyDown(KeyCode.R) && currentInteractable != null) 
        {
            currentInteractable.Interact();

            if (playerScript != null)
            {
                playerScript.PlayPickUpCameraMove(new Vector3(0, -0.5f, 0.2f), 1.0f);
            }
        }
    }

    void CheckInteraction()
    {
        RaycastHit hit;
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);

        float sphereRadius = 0.5f;  // 감지 반경
        if (Physics.SphereCast(ray, sphereRadius, out hit, playerReach))
        {
            if (hit.collider.tag == "Interactable")
            {
                Interactable newInteractable = hit.collider.GetComponent<Interactable>();

                if (currentInteractable && newInteractable != currentInteractable)
                {
                    currentInteractable.DisableOutline();
                }
                if (newInteractable.enabled)
                {
                    SetNewCurrentInteractable(newInteractable);
                }
                else
                {
                    DisableCurrentInteractable();
                }

            }
            else
            {
                DisableCurrentInteractable();
            }
        }
        else
        {
            DisableCurrentInteractable();
        }
    }

    void SetNewCurrentInteractable(Interactable newInteractable)
    {
        currentInteractable = newInteractable;
        currentInteractable.EnableOutline();
        HUDController.instance.EnableInteractionText(currentInteractable.message);
    }

    void DisableCurrentInteractable()
    {
        HUDController.instance.DisableInteractionText();
        if (currentInteractable)
        {
            currentInteractable.DisableOutline();
            currentInteractable = null;
        }
    }
}
