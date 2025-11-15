using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerHudUI : MonoBehaviour
{
    public static PlayerHudUI Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private Image equipmentIconImage; // 부모: 아이콘
    [SerializeField] private Image cooldownImage;      // 자식: 쿨타임 오버레이 (여기에 연결!)

    [Header("Sprite Mapping")]
    [SerializeField] private List<EquipmentSpriteData> spriteList;

    private Dictionary<EquipmentId, Sprite> _spriteDict;

    [System.Serializable]
    public struct EquipmentSpriteData
    {
        public EquipmentId id;
        public Sprite sprite;
    }

    private void Awake()
    {
        Instance = this;

        _spriteDict = new Dictionary<EquipmentId, Sprite>();
        foreach (var data in spriteList)
        {
            if (!_spriteDict.ContainsKey(data.id))
                _spriteDict.Add(data.id, data.sprite);
        }

        UpdateEquipmentIcon(EquipmentId.None);
        UpdateCooldown(0, 1); // 시작할 때 쿨타임 0으로 초기화
    }

    // 장비 아이콘 변경
    public void UpdateEquipmentIcon(EquipmentId id)
    {
        // 장비가 바뀌면 쿨타임 UI는 일단 초기화(안 보이게) 하는 것이 안전함
        if (cooldownImage != null) cooldownImage.fillAmount = 0;

        if (_spriteDict.TryGetValue(id, out Sprite iconSprite) && iconSprite != null)
        {
            equipmentIconImage.sprite = iconSprite;
            equipmentIconImage.enabled = true;

            // 아이콘이 켜지면 쿨타임 이미지도 (필요하다면) 같이 켜줌
            if (cooldownImage != null) cooldownImage.enabled = true;
        }
        else
        {
            equipmentIconImage.enabled = false;
            if (cooldownImage != null) cooldownImage.enabled = false;
        }
    }

    // ★ 쿨타임 업데이트 함수 (외부에서 호출)
    // current: 현재 남은 시간, max: 전체 쿨타임 시간
    public void UpdateCooldown(float current, float max)
    {
        if (cooldownImage == null) return;

        if (current <= 0 || max <= 0)
        {
            cooldownImage.fillAmount = 0; // 쿨타임 끝남 (투명해짐)
        }
        else
        {
            // 남은 시간 비율만큼 채우기 (0.0 ~ 1.0)
            cooldownImage.fillAmount = current / max;
        }
    }
}