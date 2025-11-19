using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerHudUI : MonoBehaviour
{
    public static PlayerHudUI Instance { get; private set; }

    [Header("UI Components")]
    [SerializeField] private Image equipmentIconImage; // �θ�: ������
    [SerializeField] private Image cooldownImage;      // �ڽ�: ��Ÿ�� �������� (���⿡ ����!)

    [Header("Sprite Mapping")]
    [SerializeField] private List<EquipmentSpriteData> spriteList;

    [Header("UI 잠금 옵션")]
    [SerializeField] private Image uiLockMask;          // 잠금 오버레이
    [Range(0f, 1f)] [SerializeField] private float lockedDimAlpha = 0.35f; // 잠금 중 아이콘 투명도

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
        UpdateCooldown(0, 1); // ������ �� ��Ÿ�� 0���� �ʱ�ȭ
    }

    // ��� ������ ����
    public void UpdateEquipmentIcon(EquipmentId id)
    {
        // ��� �ٲ�� ��Ÿ�� UI�� �ϴ� �ʱ�ȭ(�� ���̰�) �ϴ� ���� ������
        if (cooldownImage != null) cooldownImage.fillAmount = 0;

        if (_spriteDict.TryGetValue(id, out Sprite iconSprite) && iconSprite != null)
        {
            equipmentIconImage.sprite = iconSprite;
            equipmentIconImage.enabled = true;

            // �������� ������ ��Ÿ�� �̹����� (�ʿ��ϴٸ�) ���� ����
            if (cooldownImage != null) cooldownImage.enabled = true;
        }
        else
        {
            equipmentIconImage.enabled = false;
            if (cooldownImage != null) cooldownImage.enabled = false;
        }
    }

    // �� ��Ÿ�� ������Ʈ �Լ� (�ܺο��� ȣ��)
    // current: ���� ���� �ð�, max: ��ü ��Ÿ�� �ð�
    public void UpdateCooldown(float current, float max)
    {
        if (cooldownImage == null) return;

        if (current <= 0 || max <= 0)
        {
            cooldownImage.fillAmount = 0; // ��Ÿ�� ���� (��������)
        }
        else
        {
            // ���� �ð� ������ŭ ä��� (0.0 ~ 1.0)
            cooldownImage.fillAmount = current / max;
        }
    }

    // UI 잠금 시각적 효과 적용
    public void ApplyLockVisual(bool locked)
    {
        // 아이콘 흐리기
        if (equipmentIconImage != null)
        {
            var c = equipmentIconImage.color;
            c.a = locked ? lockedDimAlpha : 1f;
            equipmentIconImage.color = c;
        }
        if (cooldownImage != null)
        {
            var c2 = cooldownImage.color;
            c2.a = locked ? lockedDimAlpha : 1f;
            cooldownImage.color = c2;
        }

        // 오버레이 표시(선택)
        if (uiLockMask != null)
            uiLockMask.gameObject.SetActive(locked);
    }
}