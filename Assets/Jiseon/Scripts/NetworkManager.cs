using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using TMPro;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    public TextMeshProUGUI StatusText;
    public InputField roomInput, NickNameInput;


    void Awake() => Screen.SetResolution(1920, 1080, false); // PC 해상도
    void Update() => StatusText.text = PhotonNetwork.NetworkClientState.ToString();

    public void Connect() => PhotonNetwork.ConnectUsingSettings();
    
    public override void OnConnectedToMaster() // 방만들기는 커넥트 마스터나 로비접속 돼있어야가능함.
    {
        print("서버접속 완료");
        PhotonNetwork.LocalPlayer.NickName = NickNameInput.text;
    }

    public void Disconnect() => PhotonNetwork.Disconnect();

    public override void OnDisconnected(DisconnectCause cause)
    {
        print("연결끊김;");
    }

    public void JoinLobby() => PhotonNetwork.JoinLobby(); // 대형 겜 아닌이상 로비는 하나

    public override void OnJoinedLobby() => print("로비접속완료");

    public void CreateRoom() => PhotonNetwork.CreateRoom(roomInput.text, new RoomOptions { MaxPlayers = 2 });

    public void JoinRoom() => PhotonNetwork.JoinRoom(roomInput.text);

    public void JoinOrCreateRoom() => PhotonNetwork.JoinOrCreateRoom(roomInput.text,new RoomOptions {MaxPlayers = 2}, null);

    public void JoinRandomRoom() => PhotonNetwork.JoinRandomRoom();

    public void LeaveRoom() => PhotonNetwork.LeaveRoom();

    public override void OnCreatedRoom() => print("방만들기완료");

    public override void OnJoinedRoom() => print("방참가완료");

    public override void OnCreateRoomFailed(short returnCode, string message) // 방 이름이 겹치는 경우에 뜸.
    {
        print("방만들기실패");
    }

    [ContextMenu("정보")]
    void Info()
    {
        if (PhotonNetwork.InRoom)
        {
            print("현재 방 이름 : " + PhotonNetwork.CurrentRoom.Name);
            print("현재 방 인원수 : " + PhotonNetwork.CurrentRoom.PlayerCount);
            print("현재 방 최대인원수 : " + PhotonNetwork.CurrentRoom.MaxPlayers);

            string playerStr = "방에 있는 플레이어 목록 : ";
            for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++) playerStr += PhotonNetwork.PlayerList[i].NickName + ", ";
            print(playerStr);
        }
        else
        {
            print("접속한 인원 수 : " + PhotonNetwork.CountOfPlayers);
            print("방 개수 : " + PhotonNetwork.CountOfRooms);
            print("모든 방에 있는 인원수 : " + PhotonNetwork.CountOfPlayersInRooms);
            print("로비에 있는지?" + PhotonNetwork.InLobby);
            print("연결됐는지?" +  PhotonNetwork.IsConnected);
        }
        
    }

}
