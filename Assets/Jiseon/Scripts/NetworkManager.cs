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


    void Awake() => Screen.SetResolution(1920, 1080, false); // PC �ػ�
    void Update() => StatusText.text = PhotonNetwork.NetworkClientState.ToString();

    public void Connect() => PhotonNetwork.ConnectUsingSettings();
    
    public override void OnConnectedToMaster() // �游���� Ŀ��Ʈ �����ͳ� �κ����� ���־�߰�����.
    {
        print("�������� �Ϸ�");
        PhotonNetwork.LocalPlayer.NickName = NickNameInput.text;
    }

    public void Disconnect() => PhotonNetwork.Disconnect();

    public override void OnDisconnected(DisconnectCause cause)
    {
        print("�������;");
    }

    public void JoinLobby() => PhotonNetwork.JoinLobby(); // ���� �� �ƴ��̻� �κ�� �ϳ�

    public override void OnJoinedLobby() => print("�κ����ӿϷ�");

    public void CreateRoom() => PhotonNetwork.CreateRoom(roomInput.text, new RoomOptions { MaxPlayers = 2 });

    public void JoinRoom() => PhotonNetwork.JoinRoom(roomInput.text);

    public void JoinOrCreateRoom() => PhotonNetwork.JoinOrCreateRoom(roomInput.text,new RoomOptions {MaxPlayers = 2}, null);

    public void JoinRandomRoom() => PhotonNetwork.JoinRandomRoom();

    public void LeaveRoom() => PhotonNetwork.LeaveRoom();

    public override void OnCreatedRoom() => print("�游���Ϸ�");

    public override void OnJoinedRoom() => print("�������Ϸ�");

    public override void OnCreateRoomFailed(short returnCode, string message) // �� �̸��� ��ġ�� ��쿡 ��.
    {
        print("�游������");
    }

    [ContextMenu("����")]
    void Info()
    {
        if (PhotonNetwork.InRoom)
        {
            print("���� �� �̸� : " + PhotonNetwork.CurrentRoom.Name);
            print("���� �� �ο��� : " + PhotonNetwork.CurrentRoom.PlayerCount);
            print("���� �� �ִ��ο��� : " + PhotonNetwork.CurrentRoom.MaxPlayers);

            string playerStr = "�濡 �ִ� �÷��̾� ��� : ";
            for (int i = 0; i < PhotonNetwork.PlayerList.Length; i++) playerStr += PhotonNetwork.PlayerList[i].NickName + ", ";
            print(playerStr);
        }
        else
        {
            print("������ �ο� �� : " + PhotonNetwork.CountOfPlayers);
            print("�� ���� : " + PhotonNetwork.CountOfRooms);
            print("��� �濡 �ִ� �ο��� : " + PhotonNetwork.CountOfPlayersInRooms);
            print("�κ� �ִ���?" + PhotonNetwork.InLobby);
            print("����ƴ���?" +  PhotonNetwork.IsConnected);
        }
        
    }

}
