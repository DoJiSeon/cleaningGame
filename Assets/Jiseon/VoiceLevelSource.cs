// VoiceLevelSource.cs
using UnityEngine;
using Photon.Voice.Unity;
using Photon.Voice.Fusion;

public enum VoiceLevelMode { FromSpeaker, FromRecorder }

public class VoiceLevelSource : MonoBehaviour
{
    [Header("소스 선택")]
    public VoiceLevelMode mode = VoiceLevelMode.FromSpeaker;
    public AudioSource speakerAudio;   // 원격: Speaker와 같은 오브젝트의 AudioSource
    public Recorder recorder;          // 로컬: NetRoot의 Recorder (비워두면 자동 탐색)

    [Header("측정 파라미터")]
    public int sampleWindow = 256;     // 샘플 개수
    public float smoothTime = 0.06f;   // 지수평활

    [Range(0, 1)] public float level01; // 0~1 정규화된 볼륨(읽기 전용)

    float _vel;
    float[] _buf;
    bool _triedResolve;

    void Awake()
    {
        if (_buf == null || _buf.Length != sampleWindow)
            _buf = new float[sampleWindow];

        if (mode == VoiceLevelMode.FromSpeaker && !speakerAudio)
            speakerAudio = GetComponent<AudioSource>();

        if (mode == VoiceLevelMode.FromRecorder && !recorder)
            TryResolveRecorder();  // ← 여기서 자동 연결
    }

    void Update()
    {
        // 씬 로드 타이밍 때문에 Awake에서 못 잡았으면 1회 더 시도
        if (mode == VoiceLevelMode.FromRecorder && !recorder && !_triedResolve)
            TryResolveRecorder();

        float rms = 0f;

        if (mode == VoiceLevelMode.FromSpeaker && speakerAudio)
        {
            speakerAudio.GetOutputData(_buf, 0);
            rms = RMS(_buf);
        }
        else if (mode == VoiceLevelMode.FromRecorder && recorder && recorder.AudioClip)
        {
            var clip = recorder.AudioClip;
            int micPos = Microphone.GetPosition(null);
            if (micPos > 0)
            {
                int start = Mathf.Max(0, micPos - sampleWindow);
                int count = Mathf.Min(sampleWindow, clip.samples - start);
                clip.GetData(_buf, start);
                rms = RMS(_buf, count);
            }
        }

        // 0~1 정규화
        const float openThreshold = 0.02f;
        const float fullLevel = 0.12f;
        float t = Mathf.Clamp01(Mathf.InverseLerp(openThreshold, fullLevel, rms));

        level01 = Mathf.SmoothDamp(level01, t, ref _vel, smoothTime);
    }

    void TryResolveRecorder()
    {
        _triedResolve = true;

        // 1) FusionVoiceClient의 PrimaryRecorder 우선
        var fvc = FindObjectOfType<FusionVoiceClient>();
        if (fvc && fvc.PrimaryRecorder)
        {
            recorder = fvc.PrimaryRecorder;
            return;
        }

        // 2) 프로젝트 내 아무 Recorder (DontDestroyOnLoad 포함)
        recorder = FindObjectOfType<Recorder>(true);
    }

    float RMS(float[] arr, int count = -1)
    {
        if (count < 0) count = arr.Length;
        double sum = 0;
        for (int i = 0; i < count; i++) sum += arr[i] * arr[i];
        return Mathf.Sqrt((float)(sum / Mathf.Max(1, count)));
    }
}
