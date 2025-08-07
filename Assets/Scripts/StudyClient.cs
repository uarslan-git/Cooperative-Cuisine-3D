using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;

public class StudyClient : MonoBehaviour
{
    public string studyHost = "localhost";
    public int studyPort = 8001;
    public string participantId;
    public int numPlayers = 1;
    public string myPlayerId;

    public Action<StateRepresentation> OnStateReceived;
    private GameConnectionData gameConnectionData;
    private WebSocket websocket;

    void Start()
    {
        participantId = Guid.NewGuid().ToString();
        StartCoroutine(StartStudy());
    }

    void Update()
    {
        if (websocket != null)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket.DispatchMessageQueue();
#endif
        }
    }

    IEnumerator StartStudy()
    {
        string url = $"http://{studyHost}:{studyPort}/start_study/{participantId}/{numPlayers}";
        using (UnityWebRequest webRequest = UnityWebRequest.Post(url, new WWWForm()))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error starting study: {webRequest.error}");
            }
            else
            {
                Debug.Log("Study started successfully.");
                StartCoroutine(GetGameConnection());
            }
        }
    }

    IEnumerator GetGameConnection()
    {
        string url = $"http://{studyHost}:{studyPort}/get_game_connection/{participantId}";
        using (UnityWebRequest webRequest = UnityWebRequest.Post(url, new WWWForm()))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"Error getting game connection: {webRequest.error}");
            }
            else
            {
                string jsonResponse = webRequest.downloadHandler.text;
                gameConnectionData = JsonUtility.FromJson<GameConnectionData>(jsonResponse);
                Debug.Log("Game connection data received.");
                ConnectToGameServer();
            }
        }
    }

    async void ConnectToGameServer()
    {
        // Assuming we are controlling the first player
        string websocketUrl = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            websocketUrl = player.websocket_url;
            break;
        }

        if (string.IsNullOrEmpty(websocketUrl))
        {
            Debug.LogError("WebSocket URL not found.");
            return;
        }

        websocket = new WebSocket(websocketUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("Connection open!");
            SendReadyMessage();
        };

        websocket.OnError += (e) =>
        {
            Debug.LogError("Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = Encoding.UTF8.GetString(bytes);
            var receivedMessage = JsonUtility.FromJson<WebsocketMessage>(message);
            if(receivedMessage.type == "state")
            {
                OnStateReceived?.Invoke(receivedMessage.state);
            }
        };

        await websocket.Connect();
    }

    void SendReadyMessage()
    {
        string playerHash = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            playerHash = player.player_hash;
            break;
        }

        WebsocketMessage message = new WebsocketMessage
        {
            type = "ready",
            player_hash = playerHash
        };
        SendWebSocketMessage(JsonUtility.ToJson(message));
    }

    public void SendAction(Action action)
    {
        string playerHash = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            playerHash = player.player_hash;
            break;
        }

        WebsocketMessage message = new WebsocketMessage
        {
            type = "action",
            player_hash = playerHash,
            action = action
        };
        SendWebSocketMessage(JsonUtility.ToJson(message));
    }
    
    public void RequestState()
    {
        string playerHash = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            playerHash = player.player_hash;
            break;
        }

        WebsocketMessage message = new WebsocketMessage
        {
            type = "get_state",
            player_hash = playerHash,
        };
        SendWebSocketMessage(JsonUtility.ToJson(message));
    }

    private async void SendWebSocketMessage(string message)
    {
        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText(message);
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }
}