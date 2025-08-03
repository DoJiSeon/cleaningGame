using Fusion;
using Fusion.Sockets;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerSpawner : SimulationBehaviour, INetworkRunnerCallbacks
{
    public GameObject PlayerPrefab;
    public GameObject uiCanvas;
    public TMP_InputField nameInputField;

    private PlayerRef localPlayer;

    private NetworkRunner runner;

    public void SaveNameAndPrepare()
    {
        uiCanvas.SetActive(false);

        if (runner == null)
            runner = NetworkManager.runnerInsatance;

        if (runner == null)
        {
            Debug.LogError("Runner가 아직 설정되지 않았습니다!");
            return;
        }


        StartCoroutine(WaitUntilLocalPlayerReady());
    }

    public void SaveNameHideCanvas()
    {
        uiCanvas.SetActive(false);

        // Runner를 미리 설정해놓았다고 가정
        if (Runner != null)
        {
            Runner.AddCallbacks(this);
            localPlayer = Runner.LocalPlayer;
        }
        else
        {
            Debug.LogError("Runner가 아직 설정되지 않았습니다!");
        }
    }

    public void SpawnPlayer()
    {
        if (localPlayer != null && runner != null)
        {
            runner.Spawn(PlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity, localPlayer, (runner, obj) =>
            {
                obj.GetComponent<PlayerInfo>().SetPlayerName(nameInputField.text);
                runner.SetPlayerObject(localPlayer, obj);
            });
        }
        else
        {
            Debug.LogError("runner나 localPlayer가 null입니다!");
        }
    }

    private IEnumerator WaitAndSpawn()
    {
        while (NetworkManager.runnerInsatance == null || NetworkManager.runnerInsatance.LocalPlayer == null)
        {
            Debug.Log("Runner나 LocalPlayer가 아직 준비되지 않음. 대기 중...");
            yield return null;
        }

        Debug.Log("Runner와 LocalPlayer가 준비됨. Spawn 실행!");
        SpawnPlayer();
    }
    private IEnumerator WaitUntilLocalPlayerReady()
    {
        while (runner.LocalPlayer == null)
        {
            Debug.Log("LocalPlayer가 아직 설정되지 않음. 대기 중...");
            yield return null;
        }

        Debug.Log("Runner와 LocalPlayer가 준비됨. Spawn 실행!");

        localPlayer = runner.LocalPlayer;

        runner.Spawn(PlayerPrefab, new Vector3(0, 1, 0), Quaternion.identity, localPlayer, (runner, obj) =>
        {
            obj.GetComponent<PlayerInfo>().SetPlayerName(nameInputField.text);
            runner.SetPlayerObject(localPlayer, obj); // 확실히 LocalPlayerObject로 등록
        });
    }



    // 필요 없는 PlayerJoined는 빈 함수 처리
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
