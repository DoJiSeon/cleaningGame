// VoiceLevelIndicator.cs
using UnityEngine;
using UnityEngine.UI;

public class VoiceLevelIndicator : MonoBehaviour
{
    [Header("이미지 4개 (낮은 막대 -> 높은 막대 순서)")]
    public Image bar1;
    public Image bar2;
    public Image bar3;
    public Image bar4;

    [Header("레벨 소스")]
    public VoiceLevelSource source;

    [Header("임계값 (0~1)")]
    public float th1 = 0.10f;
    public float th2 = 0.35f;
    public float th3 = 0.65f;
    public float th4 = 0.90f;

    void Reset() { source = GetComponentInParent<VoiceLevelSource>(); }

    void Update()
    {
        if (!source) return;
        float v = source.level01;

        SetActive(bar1, v >= th1);
        SetActive(bar2, v >= th2);
        SetActive(bar3, v >= th3);
        SetActive(bar4, v >= th4);
    }

    void SetActive(Graphic g, bool on)
    {
        if (!g) return;
        g.enabled = on;
        var c = g.color; c.a = on ? 1f : 0.2f; g.color = c; // 옅게 표시(선택)
    }
}
