using Photon.Voice.Unity;
using UnityEngine;

public class VoicePTT : MonoBehaviour
{
    [Header("참조")]
    public Recorder recorder;

    [Header("키 바인딩")]
    public KeyCode pushToTalkKey = KeyCode.V; // 누르는 동안만 말하기
    public KeyCode toggleKey = KeyCode.B; // 항상 말하기 토글

    [Header("상태(읽기전용)")]
    public bool alwaysOn = false; // B로 토글되는 상태

    void Awake()
    {
        if (!recorder) recorder = FindObjectOfType<Recorder>(true);
    }

    void OnEnable()
    {
        ApplyState(); // 현재 alwaysOn 상태를 Recorder에 반영
    }

    void Update()
    {
        if (!recorder) return;

        // 항상 말하기 토글
        if (Input.GetKeyDown(toggleKey))
        {
            alwaysOn = !alwaysOn;
            ApplyState();
#if UNITY_EDITOR
            Debug.Log($"[VoicePTT] AlwaysOn={(alwaysOn ? "ON" : "OFF")}");
#endif
        }

        if (alwaysOn)
        {
            // 항상 말하기 모드에선 PTT 무시, 강제로 송출 유지
            if (!recorder.TransmitEnabled) recorder.TransmitEnabled = true;
            return;
        }

        // 푸시투토크
        if (Input.GetKeyDown(pushToTalkKey)) recorder.TransmitEnabled = true;
        if (Input.GetKeyUp(pushToTalkKey)) recorder.TransmitEnabled = false;
    }

    void ApplyState()
    {
        if (!recorder) return;

        if (alwaysOn)
        {
            // 항상 말하기: 즉시 송출 ON (음성검출 사용 중이면 필요에 따라 끄기)
            recorder.TransmitEnabled = true;
            // 필요시: recorder.VoiceDetection = false; // VAD를 끄고 상시 송출
        }
        else
        {
            // PTT 대기: 손을 떼면 송출 OFF
            recorder.TransmitEnabled = false;
            // 필요시: recorder.VoiceDetection = false; // PTT만 쓸 거면 권장
        }
    }
}
