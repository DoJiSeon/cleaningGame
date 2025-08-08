using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SessionListEntry : MonoBehaviour
{
    public TextMeshProUGUI roomName, playerCount;
    public Button joinButton;
    public Image lockIcon;

    private SessionInfo sessionInfo;

    public void Setup(SessionInfo info)
    {
        sessionInfo = info;
        roomName.text = info.Name;
        playerCount.text = $"{info.PlayerCount}/{info.MaxPlayers}";

        bool hasPwd = info.Properties.ContainsKey("password") &&
                      !string.IsNullOrEmpty(info.Properties["password"].ToString());
        lockIcon.gameObject.SetActive(hasPwd);

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() =>
        {
            FindObjectOfType<NetworkManager>().RequestJoinSession(sessionInfo);
        });
    }
}
