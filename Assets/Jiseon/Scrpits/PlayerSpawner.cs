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

    // ▼ 위치만 사용(회전은 항상 identity)
    [Header("Spawn Position (Rotation ignored)")]
    [SerializeField] private Transform spawnPositionTarget;
    [SerializeField] private Vector3 defaultSpawnPosition = new(0, 1, 0);

    private PlayerRef localPlayer;
    private NetworkRunner runner;

    public void SaveNameAndPrepare()
    {
        if (uiCanvas) uiCanvas.SetActive(false);

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
        if (uiCanvas) uiCanvas.SetActive(false);

        if (Runner != null)
        {
            Runner.AddCallbacks(this);
            localPlayer = Runner.LocalPlayer;
            if (runner == null) runner = Runner; // null 보호
        }
        else
        {
            Debug.LogError("Runner가 아직 설정되지 않았습니다!");
        }
    }

    public void SpawnPlayer()
    {
        if (localPlayer != default && runner != null)
        {
            Vector3 pos = GetSpawnPosition();
            Quaternion rot = Quaternion.identity; // 회전 무시

            runner.Spawn(PlayerPrefab, pos, rot, localPlayer, (runner, obj) =>
            {
                var info = obj.GetComponent<PlayerInfo>();
                if (info) info.SetPlayerName(nameInputField ? nameInputField.text : string.Empty);
                runner.SetPlayerObject(localPlayer, obj);

                // ★ 스폰 직후 위치를 권위 쪽에서 확정(카메라 위치로 스냅되는 문제 방지)
                ForceInitialPose(obj, pos);
                StartCoroutine(ForceInitialPoseNextFrame(obj, pos)); // 다음 프레임 한 번 더
            });
        }
        else
        {
            Debug.LogError("runner나 localPlayer가 null입니다!");
        }
    }

    private IEnumerator WaitUntilLocalPlayerReady()
    {
        while (runner == null || runner.LocalPlayer == default)
        {
            Debug.Log("LocalPlayer가 아직 설정되지 않음. 대기 중...");
            yield return null;
        }

        localPlayer = runner.LocalPlayer;
        Debug.Log("Runner와 LocalPlayer가 준비됨. Spawn 실행!");

        Vector3 pos = GetSpawnPosition();
        Quaternion rot = Quaternion.identity;

        runner.Spawn(PlayerPrefab, pos, rot, localPlayer, (runner, obj) =>
        {
            var info = obj.GetComponent<PlayerInfo>();
            if (info) info.SetPlayerName(nameInputField ? nameInputField.text : string.Empty);
            runner.SetPlayerObject(localPlayer, obj);

            ForceInitialPose(obj, pos);
            StartCoroutine(ForceInitialPoseNextFrame(obj, pos));
        });
    }

    // === 위치만 계산 ===
    private Vector3 GetSpawnPosition()
    {
        if (spawnPositionTarget) return spawnPositionTarget.position;
        return defaultSpawnPosition;
    }

    // === 의존성 없이 위치 확정(권위에서만) ===
    private void ForceInitialPose(NetworkObject obj, Vector3 pos)
    {
        if (!obj.HasStateAuthority) return;

        // 1) CharacterController가 있으면 안전하게 토글 후 배치
        var cc = obj.GetComponent<CharacterController>();
        if (cc != null)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            obj.transform.position = pos;
            cc.enabled = was;
        }
        else
        {
            // 2) Rigidbody가 있으면 물리값도 정리
            var rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = pos;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            obj.transform.position = pos;
        }
    }

    private IEnumerator ForceInitialPoseNextFrame(NetworkObject obj, Vector3 pos)
    {
        yield return null; // 다음 프레임
        if (obj && obj.HasStateAuthority)
            ForceInitialPose(obj, pos);
    }

    // ====== 콜백들(원본 유지, 경고 무시 가능) ======
    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectedToServer(NetworkRunner runner) { } // UNT 경고는 무시해도 됩니다
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { } // 경고 무시 가능
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