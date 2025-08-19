using Fusion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class Multiplayerchat : NetworkBehaviour
{
    public TMP_InputField input;
    public TMP_InputField usernameInput;
    public string username = "default";

    [Header("Chat UI References")]
    public Transform chatContentParent;
    public GameObject chatTextPrefab;
    public TMP_Dropdown playerDropdown;

    private Dictionary<string, List<string>> chatLogs = new Dictionary<string, List<string>>();
    private string currentChannel = "ALL";

    void Awake()
    {
        // 엔터(Submit)로 바로 전송
        if (input != null)
        {
            // 안전빵: 라인 타입을 전송에 맞게 강제 (원하면 주석처리)
            // SingleLine 또는 MultiLineSubmit 권장
            input.lineType = TMP_InputField.LineType.SingleLine;

            input.onSubmit.AddListener(_ => SubmitFromInput());
            // 일부 플랫폼/설정에서 onSubmit이 안 올 수도 있으니 onEndEdit도 백업으로 등록 가능
            // input.onEndEdit.AddListener(_ => SubmitFromInput());
        }
    }

    void OnDestroy()
    {
        if (input != null)
        {
            input.onSubmit.RemoveListener(_ => SubmitFromInput());
            // input.onEndEdit.RemoveListener(_ => SubmitFromInput());
        }
    }

    // 인풋필드에서 엔터로 호출되는 실 전송 함수
    private void SubmitFromInput()
    {
        // 포커스 + 빈 문자열 체크
        if (!input.isFocused) return;
        if (string.IsNullOrWhiteSpace(input.text)) return;

        CallMessagePRC();

        // 전송 후에도 입력 계속 치게 포커스 유지
        input.ActivateInputField();
        input.MoveTextEnd(false);
    }

    void Update()
    {
        // 보조: 엔터/넘버패드 엔터 직접 감지 (IME 환경 등 onSubmit 누락 대비)
        if (input.isFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SubmitFromInput();
            return;
        }

        // 인풋 포커스 중엔 다른 키 로직 막기
        if (input.isFocused)
            return;

        if (Input.GetKeyDown(KeyCode.Y))
        {
            ShowAllPlayerNames();
        }
    }

    void ShowAllPlayerNames()
    {
        foreach (var playerRef in Runner.ActivePlayers)
        {
            NetworkObject playerObj = Runner.GetPlayerObject(playerRef);
            if (playerObj != null)
            {
                PlayerInfo info = playerObj.GetComponent<PlayerInfo>();
                if (info != null)
                {
                    Debug.Log($"현재 방에 있는 플레이어: {info.playerName}");
                }
            }
        }
    }

    public void RefreshPlayerDropdown()
    {
        playerDropdown.ClearOptions();
        List<string> names = new List<string> { "ALL" };

        foreach (var playerRef in Runner.ActivePlayers)
        {
            var obj = Runner.GetPlayerObject(playerRef);
            if (obj == null) continue;

            var info = obj.GetComponent<PlayerInfo>();
            if (info == null) continue;

            string name = info.playerName.ToString();

            // 자기 자신은 "memo"로 표시
            if (playerRef == Runner.LocalPlayer)
            {
                name = "memo";
            }

            if (!string.IsNullOrEmpty(name))
            {
                names.Add(name);
            }
        }

        playerDropdown.AddOptions(names);
        playerDropdown.value = 0;
    }

    public void RemovePlayerFromDropdown(string nameToRemove)
    {
        List<string> currentOptions = new List<string>();
        playerDropdown.options.ForEach(opt => currentOptions.Add(opt.text));

        if (currentOptions.Contains(nameToRemove))
        {
            currentOptions.Remove(nameToRemove);
            playerDropdown.ClearOptions();
            playerDropdown.AddOptions(currentOptions);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_RemovePlayerName(string name)
    {
        RemovePlayerFromDropdown(name);
    }

    public void OnLeaveRoomButtonPressed()
    {
        string myName = username;
        StartCoroutine(RemoveNameAndReturnToLobby(myName));
    }

    IEnumerator RemoveNameAndReturnToLobby(string name)
    {
        RPC_RemovePlayerName(name);
        RemovePlayerFromDropdown(name);
        yield return new WaitForSeconds(0.3f);
        NetworkManager.ReturnToLobby();
    }

    public void SetUnername()
    {
        username = usernameInput.text;
        var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (localPlayer != null)
        {
            var info = localPlayer.GetComponent<PlayerInfo>();
            if (info != null)
                info.SetPlayerName(username);
        }
        RPC_RefreshDropdownForAll();
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_RefreshDropdownForAll()
    {
        RefreshPlayerDropdown();
    }

    public void OnDropdownValueChanged()
    {
        string selected = playerDropdown.options[playerDropdown.value].text;
        currentChannel = selected;
        DisplayChatLogForChannel(currentChannel);
    }

    void DisplayChatLogForChannel(string channel)
    {
        foreach (Transform child in chatContentParent)
        {
            Destroy(child.gameObject);
        }

        if (chatLogs.TryGetValue(channel, out var messages))
        {
            foreach (var msg in messages)
            {
                GameObject chatObj = Instantiate(chatTextPrefab, chatContentParent);
                TMP_Text tmp = chatObj.GetComponent<TMP_Text>();
                if (tmp != null) tmp.text = msg;
            }
        }
    }

    public void CallMessagePRC()
    {
        string message = input.text;
        if (string.IsNullOrEmpty(message)) return;

        string target = playerDropdown.options[playerDropdown.value].text;
        input.text = "";

        if (target == "ALL")
        {
            RPC_SendPublicMessage(username, message);
        }
        else if (target == "memo")
        {
            string formatted = $"[memo] {message}";
            AppendToChat("memo", formatted);
            if (currentChannel == "memo")
                AddMessageToUI(formatted);
        }
        else
        {
            RPC_SendWhisper(username, target, message);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendPublicMessage(string sender, string message)
    {
        string formatted = $"<color=red>{sender}:</color> {message}";
        AppendToChat("ALL", formatted);
        if (currentChannel == "ALL")
            AddMessageToUI(formatted);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendWhisper(string sender, string receiver, string message)
    {
        var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
        if (localPlayer == null) return;

        var info = localPlayer.GetComponent<PlayerInfo>();
        if (info == null) return;

        string myName = info.playerName.ToString();

        if (myName == sender || myName == receiver)
        {
            string formatted = $"<color=green>{sender} > {receiver}:</color> {message}";
            string channelKey = (myName == sender) ? receiver : sender;

            AppendToChat(channelKey, formatted);

            if (currentChannel == channelKey)
                AddMessageToUI(formatted);
        }
    }

    void AppendToChat(string channel, string message)
    {
        if (!chatLogs.ContainsKey(channel))
            chatLogs[channel] = new List<string>();

        chatLogs[channel].Add(message);
    }

    void AddMessageToUI(string message)
    {
        GameObject chatObj = Instantiate(chatTextPrefab, chatContentParent);
        TMP_Text tmp = chatObj.GetComponent<TMP_Text>();
        if (tmp != null) tmp.text = message;
    }
}
