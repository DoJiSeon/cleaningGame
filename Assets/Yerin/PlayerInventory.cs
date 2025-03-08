using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory instance;
    public bool HasKey { get; set; } = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
}
