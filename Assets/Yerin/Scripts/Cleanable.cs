using System;
using UnityEngine;

public class Cleanable : MonoBehaviour
{
    public event Action<Cleanable> OnCleaned;
    private bool _reported;

    // 네가 직접 청소시킬 때 호출
    public void Clean()
    {
        if (_reported) return;
        _reported = true;
        OnCleaned?.Invoke(this);
        Destroy(gameObject);
    }

    // 다른 스크립트가 그냥 Destroy해도 안전하게 카운트되도록
    private void OnDestroy()
    {
        if (_reported) return;     // Clean()에서 이미 보고했으면 중복 방지
        _reported = true;
        OnCleaned?.Invoke(this);
    }
}
