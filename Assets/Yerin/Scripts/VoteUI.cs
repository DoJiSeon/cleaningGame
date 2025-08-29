using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;
using DG.Tweening; 

public class VoteUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private MeetingDirector meetingDirector;
    [SerializeField] private RectTransform content;     // ScrollRect/Viewport/Content
    [SerializeField] private GameObject itemPrefab;     // 루트 Image + Label(TMP)만 가진 프리팹
    [SerializeField] private ScrollRect scrollRect;     // (선택) 선택된 항목 자동 스크롤

    [Header("색상")]
    [SerializeField] private Color bgNormal = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Color bgSelected = new Color(0.15f, 0.35f, 1f, 0.7f);
    [SerializeField] private Color bgVoted = new Color(0.15f, 0.8f, 0.25f, 0.7f);

    [Header("키 바인딩")]
    [SerializeField] private KeyCode keyCycle = KeyCode.K;        // 다음 항목
    [SerializeField] private KeyCode keyConfirm = KeyCode.Return;   // Enter
    [SerializeField] private KeyCode keyCancel = KeyCode.Escape;   // 팝업 닫기 등

    [Header("옵션")]
    [SerializeField] private bool excludeSelf = true;   // 자기 자신 제외 여부

    [Header("확인 팝업")]
    [SerializeField] private GameObject confirmPanel;   // 비활성 시작
    [SerializeField] private Button btnYes;
    [SerializeField] private Button btnNo;

    [Header("애니메이션")]
    [SerializeField] private float colorTweenDur = 0.15f;
    [SerializeField] private bool usePulse = true;
    [SerializeField] private float pulseScale = 1.03f;
    [SerializeField] private float pulseDur = 0.35f;

    [Header("스티커")]
    [SerializeField] private Sprite votedStickerSprite;     
    [SerializeField] private Vector2 votedStickerSize = new Vector2(56, 56);

    private class Item
    {
        public GameObject go;
        public RectTransform rt;
        public Image bg;
        public TMP_Text nameText;
        public PlayerRef playerRef;

        // 트윈 핸들
        public Tween colorTw;
        public Tween scaleTw;
    }

    private readonly List<Item> _items = new();
    private int _cursorIndex = -1;
    private PlayerRef? _selected = null;
    private bool _hasVoted = false;

    // 커서 복원용 저장
    private CursorLockMode _prevLock;
    private bool _prevVisible;

    private void Awake()
    {
        if (confirmPanel) confirmPanel.SetActive(false);
    }

    public void Rebuild(NetworkRunner runner)
    {
        ClearAll();

        if (runner != null)
        {
            foreach (var p in runner.ActivePlayers)
            {
                if (excludeSelf && p == runner.LocalPlayer) continue;
                AddItem(p);
            }
        }

        _hasVoted = false;

        if (_items.Count > 0)
        {
            _cursorIndex = 0;
            _selected = _items[0].playerRef;
        }
        else
        {
            _cursorIndex = -1;
            _selected = null;
        }

        RefreshVisuals();
        CloseConfirm(); // 혹시 열려있던 팝업 닫기
    }

    private void Update()
    {
        // 팝업 열려있으면 키 입력은 팝업에 양보
        if (confirmPanel && confirmPanel.activeInHierarchy)
        {
            if (Input.GetKeyDown(keyCancel)) CloseConfirm();
            return;
        }

        if (!gameObject.activeInHierarchy) return;
        if (_items.Count == 0 || _hasVoted) return;

        if (Input.GetKeyDown(keyCycle))
        {
            _cursorIndex = (_cursorIndex + 1) % _items.Count;
            _selected = _items[_cursorIndex].playerRef;
            RefreshVisuals();
            EnsureSelectedVisible();
        }

        if (Input.GetKeyDown(keyConfirm))
        {
            if (_cursorIndex >= 0 && _cursorIndex < _items.Count)
            {
                ShowConfirm(_items[_cursorIndex]);
            }
        }
    }

    private void AddItem(PlayerRef pref)
    {
        var go = Instantiate(itemPrefab, content);
        var rt = go.GetComponent<RectTransform>();
        rt.localScale = Vector3.one;
        // 가로 스트레치 + 줄 높이 보장
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(0f, rt.offsetMax.y);

        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (le.preferredHeight < 1f) le.preferredHeight = 60f;

        var img = go.GetComponent<Image>();
        var text = go.transform.Find("Label")?.GetComponent<TMP_Text>();

        if (img) img.color = bgNormal;
        if (text) { text.text = $"Player {pref.PlayerId}"; text.raycastTarget = false; }

        var item = new Item { go = go, rt = rt, bg = img, nameText = text, playerRef = pref };
        _items.Add(item);
    }

    private void RefreshVisuals()
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            bool isSel = (_cursorIndex == i);

            // 색 트윈
            if (it.bg)
            {
                it.colorTw?.Kill();
                var targetCol =
                    _hasVoted && _selected.HasValue && _selected.Value == it.playerRef
                    ? bgVoted
                    : (isSel ? bgSelected : bgNormal);

                it.colorTw = it.bg.DOColor(targetCol, colorTweenDur).SetUpdate(true);
            }

            // 스케일 펄스
            if (usePulse && it.rt)
            {
                it.scaleTw?.Kill();
                if (isSel && !_hasVoted)
                {
                    it.rt.localScale = Vector3.one;
                    it.scaleTw = it.rt.DOScale(pulseScale, pulseDur)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetUpdate(true);
                }
                else
                {
                    it.rt.DOScale(1f, 0.15f).SetUpdate(true);
                }
            }

            // 텍스트 굵기(옵션)
            if (it.nameText)
                it.nameText.fontStyle = isSel ? FontStyles.Bold : FontStyles.Normal;
        }

        // 레이아웃 확정
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
    }

    private void ShowConfirm(Item target)
    {
        if (!confirmPanel) return;

        // 커서 표시(팝업 동안만)
        _prevLock = Cursor.lockState;
        _prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        confirmPanel.SetActive(true);

        // 기존 리스너 정리 후 연결
        btnYes.onClick.RemoveAllListeners();
        btnNo.onClick.RemoveAllListeners();

        btnYes.onClick.AddListener(() =>
        {
            if (!_hasVoted && meetingDirector)
                meetingDirector.SubmitVote(target.playerRef);

            _hasVoted = true;
            _selected = target.playerRef;

            PlaceStickerOnItem(target, votedStickerSprite ?? /*fallback*/ null, votedStickerSize);

            CloseConfirm();
            RefreshVisuals();
        });

        btnNo.onClick.AddListener(CloseConfirm);
    }

    private void CloseConfirm()
    {
        if (confirmPanel) confirmPanel.SetActive(false);
        // 커서 상태 복원
        Cursor.lockState = _prevLock;
        Cursor.visible = _prevVisible;
    }

    private void ClearAll()
    {
        // 트윈 정리
        foreach (var it in _items)
        {
            it.colorTw?.Kill();
            it.scaleTw?.Kill();
            if (it.go) Destroy(it.go);
        }
        _items.Clear();
        _cursorIndex = -1;
        _selected = null;
    }

    private void EnsureSelectedVisible()
    {
        if (!scrollRect || _cursorIndex < 0 || _cursorIndex >= _items.Count) return;

        var viewport = scrollRect.viewport;
        var itemRt = _items[_cursorIndex].rt;

        var itemBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, itemRt);
        var viewBounds = new Bounds(Vector3.zero, viewport.rect.size);

        if (itemBounds.max.y > viewBounds.max.y || itemBounds.min.y < viewBounds.min.y)
        {
            float contentH = content.rect.height - viewport.rect.height;
            if (contentH > 1f)
            {
                float centerY = Mathf.Abs(itemRt.anchoredPosition.y);
                float norm = Mathf.Clamp01(centerY / contentH);
                scrollRect.verticalNormalizedPosition = 1f - norm;
            }
        }
    }

    private void PlaceStickerOnItem(Item it, Sprite sprite, Vector2 size)
    {
        if (it == null || it.go == null || sprite == null) return;

        // 기존 스티커 제거(중복 방지)
        var old = it.go.transform.Find("VotedSticker");
        if (old) Destroy(old.gameObject);

        var go = new GameObject("VotedSticker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(it.go.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); // 오른쪽 중앙
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(-12f, 0f);         // 오른쪽에서 12px 안쪽

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;

        // 팡! 애님 (DOTween)
        rt.localScale = Vector3.zero;
        rt.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
        rt.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
    }


}
