using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Key : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if( other.tag == "Player")
        {
            PlayerInventory.instance.HasKey = true;
            Destroy(this.gameObject);
        }
    }
}
