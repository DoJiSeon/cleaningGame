using Photon.Voice.Unity;
using UnityEngine;

public class VoicePTT : MonoBehaviour
{
    public Recorder recorder;

    void Awake()
    {
        if (!recorder) recorder = FindObjectOfType<Recorder>();
    }

    void Update()
    {
        if (!recorder) return;
        if (Input.GetKeyDown(KeyCode.V)) recorder.TransmitEnabled = true;
        if (Input.GetKeyUp(KeyCode.V)) recorder.TransmitEnabled = false;
    }
}