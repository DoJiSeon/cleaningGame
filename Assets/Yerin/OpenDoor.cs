using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenDoor : MonoBehaviour
{

    [SerializeField]
    private float DoorZPos = 3f;

    [SerializeField]
    private float DoorYPos = 0f;
    private float moveSpeed = 2f;
    private bool isOpen = false;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>(); 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (PlayerInventory.instance.HasKey && !isOpen)
            {
                StartCoroutine(Dooropen());
            }
        }
    }

    private IEnumerator Dooropen()
    {

        isOpen = true;

        Debug.Log("문이 열렸습니다!");

        // 오디오 재생
        if (audioSource != null)
        {
            audioSource.Play(); 
        }

        /*Vector3 currentPosition = transform.position;
        currentPosition.z += DoorZPos;
        currentPosition.y += DoorYPos;

        transform.position = currentPosition;*/

        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + new Vector3(0, DoorYPos, DoorZPos); // 목표 위치 설정

        float journeyLength = Vector3.Distance(startPosition, targetPosition);
        float journeyTime = 0f;

        while (journeyTime < journeyLength / moveSpeed)
        {
            journeyTime += Time.deltaTime; 
            float t = journeyTime / (journeyLength / moveSpeed); 
            transform.position = Vector3.Lerp(startPosition, targetPosition, t); 
            yield return null; 
        }

        transform.position = targetPosition; 
    }

}
