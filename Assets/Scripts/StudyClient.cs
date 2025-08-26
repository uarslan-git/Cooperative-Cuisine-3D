using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;

using System.Collections.Concurrent;

public class StudyClient : MonoBehaviour
{
    public string studyHost = "localhost";
    public int studyPort = 8080;
    public string participantId;
    public int numPlayers = 2;
    public string myPlayerId;
    public string myPlayerHash;

    public Action<StateRepresentation> OnStateReceived;
    private GameConnectionData gameConnectionData;
    private WebSocket websocket;
    private readonly ConcurrentQueue<System.Action> mainThreadActions = new ConcurrentQueue<System.Action>();

    public GameManager gameManager;
    public GameObject nextLevelCanvas;
    
    void Start()
    {
        // Ensure there is a camera in the scene
        if (Camera.main == null)
        {
            Debug.LogWarning("No main camera found. Creating a default camera.");
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(6, 15, -2);
            cameraObj.transform.rotation = Quaternion.Euler(60, 0, 0);
        }

        if (nextLevelCanvas != null)
        {
            nextLevelCanvas.SetActive(false);
        }

        gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager != null)
        {
            OnStateReceived += gameManager.HandleStateReceived;
        }

        participantId = Guid.NewGuid().ToString();
        StartCoroutine(StartStudy());
    }

    void Update()
    {
        // Process any actions that have been queued from background threads
        while (mainThreadActions.TryDequeue(out var action))
        {
            action();
        }

        if (websocket != null)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket.DispatchMessageQueue();
#endif
        }
    }

    

    

    public IEnumerator StartStudy()
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
                Debug.Log($"Start Study Response: {webRequest.downloadHandler.text}");
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
                Debug.Log($"Get Game Connection Response: {jsonResponse}");
                gameConnectionData = JsonConvert.DeserializeObject<GameConnectionData>(jsonResponse);
                Debug.Log("Game connection data received.");
                ConnectToGameServer();
            }
        }
    }

    async void ConnectToGameServer()
    {
        string websocketUrl = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            websocketUrl = player.websocket_url;
            myPlayerId = player.player_id;
            myPlayerHash = player.player_hash;
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
            StartCoroutine(StateUpdateLoop());
        };

        websocket.OnError += (e) => { Debug.LogError("Error! " + e); };
        websocket.OnClose += (e) => { Debug.Log("Connection closed!"); };

        websocket.OnMessage += (bytes) =>
        {
            var message = Encoding.UTF8.GetString(bytes);
            try
            {
                var state = JsonConvert.DeserializeObject<StateRepresentation>(message);
                Debug.Log($"Received state with ended = {state.ended}");
                if (state != null && state.players != null)
                {
                    OnStateReceived?.Invoke(state);

                    if (state.ended)
                    {
                        if (nextLevelCanvas != null && !nextLevelCanvas.activeSelf)
                        {
                            // Enqueue the action to be executed on the main thread
                            mainThreadActions.Enqueue(() => nextLevelCanvas.SetActive(true));
                        }
                    }
                }
                else
                {
                    Debug.Log($"Received non-state message: {message}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize message: {e.Message}\nMessage content: {message}");
            }
        };

        await websocket.Connect();
    }

    public void InitiateNextLevel()
    {
        StartCoroutine(RequestNextLevel());
    }

    private IEnumerator RequestNextLevel()
    {
        // First, notify the server that this player is done
        string levelDoneUrl = $"http://{studyHost}:{studyPort}/level_done/{participantId}";
        using (UnityWebRequest webRequest = UnityWebRequest.Post(levelDoneUrl, new WWWForm()))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error sending level done message: {webRequest.error}");
                yield break; // Stop if this fails
            }
            Debug.Log("Level done message sent successfully.");
        }

        // Now, get the connection for the *next* level
        string gameConnectionUrl = $"http://{studyHost}:{studyPort}/get_game_connection/{participantId}";
        using (UnityWebRequest webRequest = UnityWebRequest.Post(gameConnectionUrl, new WWWForm()))
        {
            // This might take time as the server waits for other players
            Debug.Log("Waiting for other players to finish...");
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Received new game connection data.");
                string jsonResponse = webRequest.downloadHandler.text;
                gameConnectionData = JsonConvert.DeserializeObject<GameConnectionData>(jsonResponse);

                // Hide the canvas and reconnect
                if (nextLevelCanvas != null)
                {
                    nextLevelCanvas.SetActive(false);
                }
                
                ReconnectAfterLevelEnd();
            }
            else
            {
                Debug.LogError($"Error getting new game connection: {webRequest.error}");
            }
        }
    }

    private async void ReconnectAfterLevelEnd()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
        ConnectToGameServer();
    }

    void SendReadyMessage()
    {
        string playerHash = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            playerHash = player.player_hash;
            break;
        }
        WebsocketMessage message = new WebsocketMessage { type = "ready", player_hash = playerHash };
        SendWebSocketMessage(JsonConvert.SerializeObject(message));
    }

    public void SendAction(Action action)
    {
        WebsocketMessage message = new WebsocketMessage { type = "action", player_hash = myPlayerHash, action = action };
        SendWebSocketMessage(JsonConvert.SerializeObject(message));
    }
    
    public void RequestState()
    {
        // This now sends a message that the server might not handle, but it triggers the server to send back a state.
        // This seems to be how the python client works.
        string playerHash = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            playerHash = player.player_hash;
            break;
        }
        WebsocketMessage message = new WebsocketMessage { type = "get_state", player_hash = playerHash };
        SendWebSocketMessage(JsonConvert.SerializeObject(message));
    }

    private async void SendWebSocketMessage(string message)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
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

    IEnumerator StateUpdateLoop()
    {
        while (websocket != null && websocket.State == WebSocketState.Open)
        {
            RequestState();
            yield return new WaitForSeconds(1f / 30f); // 30 FPS
        }
    }
}



