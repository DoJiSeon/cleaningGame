using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public enum DebuffType { SpeedLimit, Mute, TunnelVision, UILock }

[System.Serializable]
public struct DebuffSpec
{
    public DebuffType type;
    public Sprite icon;          // 인스펙터에서 연결
}

public class DebuffUI : MonoBehaviour
{
    [Header("아이콘들이 놓일 부모(가로/세로 배치는 Layout으로 관리)")]
    public RectTransform container;

    [Header("디버프 타입 ↔ 아이콘 매핑")]
    public List<DebuffSpec> specs;

    [Header("비주얼 옵션")]
    public float iconSize = 48f;        // 생성되는 아이콘 크기
    public float fadeIn = 0.15f;        // 등장 페이드
    public float fadeOut = 0.15f;       // 제거 페이드
    public float blinkStart = 0.8f;     // 남은 시간이 이 값 이하일 때부터 깜빡임 시작(초)
    public float blinkFreq = 6f;        // 깜빡임 속도(초당 깜빡임 횟수)

    class Slot
    {
        public DebuffType type;
        public GameObject go;
        public Image img;
        public CanvasGroup cg;
        public float endTime;
        public Tween blinkTween;
    }

    readonly Dictionary<DebuffType, Slot> _active = new();
    readonly Dictionary<DebuffType, Sprite> _map = new();

    void Awake()
    {
        foreach (var s in specs) _map[s.type] = s.icon;
        if (!container) container = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (_active.Count == 0) return;

        float now = Time.time;
        var toRemove = new List<DebuffType>();

        foreach (var kv in _active)
        {
            var s = kv.Value;
            float remain = s.endTime - now;

            // 깜빡임 시작
            if (remain <= blinkStart && s.blinkTween == null && s.cg != null)
            {
                // 0↔1 사이를 왕복하며 깜빡임
                float halfPeriod = 0.5f / Mathf.Max(0.0001f, blinkFreq); // 한 번 꺼지거나 켜지는 시간
                s.blinkTween = s.cg.DOFade(0f, halfPeriod)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.Linear);
            }

            if (remain <= 0f) toRemove.Add(kv.Key);
        }

        foreach (var key in toRemove)
            RemoveSlot(key);
    }

    /// <summary>
    /// 디버프 표시(같은 타입이면 남은 시간만 갱신)
    /// </summary>
    public void Show(DebuffType type, float duration)
    {
        float end = Time.time + duration;

        if (_active.TryGetValue(type, out var s))
        {
            // 연장만
            s.endTime = end;
            return;
        }

        // 새 아이콘 생성
        var go = new GameObject(type.ToString(), typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(container, false);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(iconSize, iconSize);

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = _map.TryGetValue(type, out var sp) ? sp : null;
        img.preserveAspect = true;

        var cg = go.GetComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.DOFade(1f, fadeIn);

        var slot = new Slot { type = type, go = go, img = img, cg = cg, endTime = end, blinkTween = null };
        _active[type] = slot;
    }

    void RemoveSlot(DebuffType type)
    {
        if (!_active.TryGetValue(type, out var s)) return;

        s.blinkTween?.Kill();
        s.blinkTween = null;

        if (s.cg)
        {
            s.cg.DOFade(0f, fadeOut).OnComplete(() =>
            {
                if (s.go) Destroy(s.go);
            });
        }
        else if (s.go) Destroy(s.go);

        _active.Remove(type);
    }

    public void ClearAll()
    {
        foreach (var kv in _active) { kv.Value.blinkTween?.Kill(); if (kv.Value.go) Destroy(kv.Value.go); }
        _active.Clear();
    }
}

