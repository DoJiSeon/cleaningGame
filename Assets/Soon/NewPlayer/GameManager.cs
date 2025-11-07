using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Fusion.Sockets;
using UnityEngine.SceneManagement;

public struct PlayerInputData : INetworkInput
{
    public Vector3 move;
    public Vector3 look;
    public bool jump;
    public bool run;

    public bool NextEquipPressed; // E
    public bool PrevEquipPressed; // Q
    public int SelectSlotIndex;  // 1~N, 미선택 시 -1
}

public class GameManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public GameObject playerPrefab;

    private void Awake()
    {
    }

    // 게임 시작은 적절한 시점에 StartGame 호출 필요
    public void StartGame()
    {

    }

    // 입력 수집 및 전송
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {

        PlayerInputData data = new PlayerInputData()
        {

            move = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")),
            look = new Vector3(Input.GetAxis("Mouse X"), 0f, Input.GetAxis("Mouse Y")),
            jump = Input.GetButton("Jump"),
            run = Input.GetKey(KeyCode.LeftShift)
        };
        input.Set(data);
    }

    // 각종 콜백들 (비워둬도 됨)
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
}
