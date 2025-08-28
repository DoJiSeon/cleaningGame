// VoteUI.cs
using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VoteUI : MonoBehaviour
{
    [Header("참조")]
    public MeetingDirector director;
    public RectTransform listRoot;
    public GameObject voteButtonPrefab;
    public bool allowSelfVote = false;

    public void Rebuild(NetworkRunner runner)
    {
        if (!runner || !director || !listRoot || !voteButtonPrefab) return;

        // 기존 버튼 모두 제거
        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        foreach (var pr in runner.ActivePlayers)
        {
            if (!allowSelfVote && pr == runner.LocalPlayer)
                continue;

            var go = Instantiate(voteButtonPrefab, listRoot);
            var btn = go.GetComponent<Button>();
            var label = go.GetComponentInChildren<TextMeshProUGUI>(true);

            // 표시 이름: 찾아지면 닉네임, 아니면 ID
            string display = $"Player {pr.PlayerId}";
            // (원하면 PlayerObject에서 닉네임 가져와서 대체)

            if (label) label.text = display;
            if (btn)
            {
                var target = pr; // 클로저 캡처 주의
                btn.onClick.AddListener(() =>
                {
                    director.SubmitVote(target);
                });
            }
        }
    }
}
