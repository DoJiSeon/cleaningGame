using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunner runnerInsatance;

    public string lobbyName = "default";

    public Transform sessionListContentParent;
    public GameObject sessionListEntryPrefab;
    public Dictionary<string, GameObject> sessionListUiDictionary = new Dictionary<string, GameObject>();

#if UNITY_EDITOR
    public SceneAsset gameplaySceneAsset;
    public SceneAsset lobbySceneAsset;
#endif
    [SerializeField] private string gameplaySceneName;
    [SerializeField] private string lobbySceneName;

    public GameObject playerPrefab;

    [Header("Create Session UI")]
    public TMP_InputField roomNameInput;
    public TMP_InputField passwordInput;

    [Header("Password Panel")]
    public GameObject passwordPanel;
    public TMP_InputField passwordCheckInput;
    public TMP_Text passwordWarningText;

    [Header("Network Status UI")]
    public TMP_Text networkStatusText;

    private SessionInfo selectedSession;
    private List<SessionInfo> cachedSessionList = new();

    private Multiplayerchat chatManager;

    private void Awake()
    {
        runnerInsatance = gameObject.GetComponent<NetworkRunner>();

        if (runnerInsatance == null)
        {
            runnerInsatance = gameObject.AddComponent<NetworkRunner>();
        }

#if UNITY_EDITOR
        if (gameplaySceneAsset != null)
            gameplaySceneName = gameplaySceneAsset.name;

        if (lobbySceneAsset != null)
            lobbySceneName = lobbySceneAsset.name;
#endif
    }

    private void Start()
    {
        if (networkStatusText != null)
        {
            networkStatusText.text = "Connecting to network...";
            networkStatusText.color = Color.yellow;
        }

        runnerInsatance.JoinSessionLobby(SessionLobby.Shared, lobbyName);
    }

    // -------------------------------
    // 방 생성
    // -------------------------------
    public void CreateCustomSession()
    {
        StartCoroutine(CreateCustomSessionRoutine());
    }

    private IEnumerator CreateCustomSessionRoutine()
    {
        yield return runnerInsatance.JoinSessionLobby(SessionLobby.Shared, lobbyName);
        yield return new WaitForSeconds(0.5f);

        string customName = string.IsNullOrEmpty(roomNameInput.text) ? "DefaultRoom" : roomNameInput.text;

        var props = new Dictionary<string, SessionProperty>();
        if (!string.IsNullOrEmpty(passwordInput.text))
            props["password"] = passwordInput.text;

        int sceneIndex = GetSceneIndex(gameplaySceneName);

        if (sceneIndex < 0)
        {
            Debug.LogError($"'{gameplaySceneName}' 씬을 Build Settings에서 찾을 수 없습니다!");
            yield break;
        }

        runnerInsatance.StartGame(new StartGameArgs()
        {
            Scene = SceneRef.FromIndex(sceneIndex),
            SessionName = customName,
            GameMode = GameMode.Shared,
            CustomLobbyName = lobbyName,
            IsVisible = true,
            SessionProperties = props
        });
    }

    // -------------------------------
    // 방 참가
    // -------------------------------
    public void RequestJoinSession(SessionInfo session)
    {
        bool hasPwd = session.Properties.ContainsKey("password") &&
                      !string.IsNullOrEmpty(session.Properties["password"].PropertyValue?.ToString());

        if (!hasPwd)
        {
            StartCoroutine(JoinRoomRoutine(session.Name));
        }
        else
        {
            selectedSession = session;
            passwordCheckInput.text = "";
            passwordWarningText.text = "";
            passwordPanel.SetActive(true);
        }
    }

    public void OnConfirmPassword()
    {
        if (selectedSession == null) return;

        var prop = selectedSession.Properties["password"];
        string correctPwd = prop.PropertyValue?.ToString().Trim();
        string inputPwd = passwordCheckInput.text.Trim();

        Debug.Log($"입력값: '{inputPwd}' / 저장값: '{correctPwd}'");

        if (inputPwd == correctPwd)
        {
            passwordWarningText.text = "";
            passwordPanel.SetActive(false);
            StartCoroutine(JoinRoomRoutine(selectedSession.Name));
        }
        else
        {
            passwordWarningText.text = "Wrong password!";
            passwordWarningText.gameObject.SetActive(true);
            StartCoroutine(ClearWarningText());
        }
    }

    private IEnumerator ClearWarningText()
    {
        yield return new WaitForSeconds(3f);
        passwordWarningText.text = "";
    }

    public void OnCancelPassword()
    {
        selectedSession = null;
        passwordPanel.SetActive(false);
    }

    private IEnumerator JoinRoomRoutine(string sessionName)
    {
        var runner = NetworkManager.runnerInsatance;

        if (runner.IsRunning)
            yield return runner.Shutdown();

        int sceneIndex = GetSceneIndex(gameplaySceneName);

        if (sceneIndex < 0)
        {
            Debug.LogError($"'{gameplaySceneName}' 씬 인덱스를 찾을 수 없습니다! Build Settings 등록 확인");
            yield break;
        }

        var args = new StartGameArgs()
        {
            SessionName = sessionName,
            GameMode = GameMode.Shared,
            Scene = SceneRef.FromIndex(sceneIndex),
            CustomLobbyName = "default",
        };

        yield return runner.StartGame(args);
    }

    // -------------------------------
    // 랜덤 방 입장 (비번방 제외)
    // -------------------------------
    public void JoinRandomButtonPressed()
    {
        JoinRandomSession(cachedSessionList);
    }

    private void JoinRandomSession(List<SessionInfo> sessionList)
    {
        if (sessionList == null || sessionList.Count == 0)
        {
            Debug.LogWarning("현재 참가 가능한 세션이 없습니다.");
            return;
        }

        var openSessions = sessionList.FindAll(s =>
            s.IsOpen &&
            s.IsVisible &&
            (!s.Properties.ContainsKey("password") ||
             string.IsNullOrEmpty(s.Properties["password"].PropertyValue?.ToString()))
        );

        if (openSessions.Count == 0)
        {
            Debug.LogWarning("열린 공개 세션이 없습니다.");
            return;
        }

        SessionInfo randomSession = openSessions[UnityEngine.Random.Range(0, openSessions.Count)];
        Debug.Log($"랜덤으로 선택된 공개 세션: {randomSession.Name}");

        StartCoroutine(JoinRoomRoutine(randomSession.Name));
    }

    // -------------------------------
    // 세션 리스트 관리
    // -------------------------------
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        cachedSessionList = sessionList;
        DeleteOldSessionsFromUI(sessionList);
        CompareList(sessionList);

        if (networkStatusText != null && networkStatusText.text != "Connected to network.")
        {
            networkStatusText.text = "Connected to network.";
            networkStatusText.color = Color.green;
        }
    }

    private void CompareList(List<SessionInfo> sessionList)
    {
        foreach (SessionInfo session in sessionList)
        {
            if (sessionListUiDictionary.ContainsKey(session.Name))
            {
                UpdateEntryUI(session);
            }
            else
            {
                CreateEntryUI(session);
            }
        }
    }

    private void CreateEntryUI(SessionInfo session)
    {
        GameObject newEntry = Instantiate(sessionListEntryPrefab, sessionListContentParent, false);
        SessionListEntry entryScript = newEntry.GetComponent<SessionListEntry>();
        entryScript.Setup(session);
        sessionListUiDictionary.Add(session.Name, newEntry);
        newEntry.SetActive(session.IsVisible);
    }

    private void UpdateEntryUI(SessionInfo session)
    {
        sessionListUiDictionary.TryGetValue(session.Name, out GameObject newEntry);
        SessionListEntry entryScript = newEntry.GetComponent<SessionListEntry>();
        entryScript.Setup(session);
        newEntry.SetActive(session.IsVisible);
    }

    private void DeleteOldSessionsFromUI(List<SessionInfo> sessionList)
    {
        List<string> existingKeys = new List<string>(sessionListUiDictionary.Keys);

        foreach (string key in existingKeys)
        {
            bool exists = sessionList.Exists(s => s.Name == key);
            if (!exists)
            {
                Destroy(sessionListUiDictionary[key]);
                sessionListUiDictionary.Remove(key);
            }
        }
    }

    // 이름 기반으로 씬 인덱스 찾기
    private int GetSceneIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (name == sceneName)
                return i;
        }
        return -1;
    }

    public static void ReturnToLobby()
    {
        NetworkManager.runnerInsatance.Despawn(runnerInsatance.GetPlayerObject(runnerInsatance.LocalPlayer));
        NetworkManager.runnerInsatance.Shutdown(true, ShutdownReason.Ok);
    }

    // -------------------------------
    // Fusion 콜백
    // -------------------------------
    public void OnConnectedToServer(NetworkRunner runner)
    {
        if (networkStatusText != null)
        {
            networkStatusText.text = "Connected to network.";
            networkStatusText.color = Color.green;
        }
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        if (networkStatusText != null)
        {
            networkStatusText.text = "Failed to connect.";
            networkStatusText.color = Color.cyan;
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NetworkManager] Runner Shutdown: {shutdownReason}");
        SceneManager.LoadScene(lobbySceneName);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        StartCoroutine(FindChatManager());
    }

    IEnumerator FindChatManager()
    {
        yield return null;
        chatManager = FindObjectOfType<Multiplayerchat>();
        if (chatManager != null)
        {
            Debug.Log("chatManager 연결됨!");
        }
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        
    }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
