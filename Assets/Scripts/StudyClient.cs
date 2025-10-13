using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NativeWebSocket;
using System.Linq;

using System.Collections.Concurrent;

public class StudyClient : MonoBehaviour
{
    public string studyHost = "0ddfd79c42e9.ngrok-free.app"; //Remove https:// prefix for Unity networking
    public int studyPort = 80; //80 for ngrok, 8080 for local testing
    public string participantId;
    public int numPlayers = 2;
    public string myPlayerId;
    public string myPlayerHash;

    public Action<StateRepresentation> OnStateReceived;
    private GameConnectionData gameConnectionData;
    private LevelInfo levelInfo;
    private WebSocket websocket;
    private readonly ConcurrentQueue<System.Action> mainThreadActions = new ConcurrentQueue<System.Action>();
    private bool isStateUpdateLoopRunning = false;

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

            if (websocket.State == WebSocketState.Open && !isStateUpdateLoopRunning)
            {
                StartCoroutine(StateUpdateLoop());
                isStateUpdateLoopRunning = true;
            }
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
                levelInfo = gameConnectionData.level_info;
                Debug.Log($"Game connection data received for level: {levelInfo.name}");
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
            Debug.Log($"Setting up player connection - ID: {myPlayerId}, Hash: {myPlayerHash}");
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
            // Only start state update loop if not already running
            if (!isStateUpdateLoopRunning)
            {
                StartCoroutine(StateUpdateLoop());
                isStateUpdateLoopRunning = true;
            }
        };

        websocket.OnError += (e) => { Debug.LogError("Error! " + e); };
        websocket.OnClose += (e) => { 
            Debug.Log("Connection closed!"); 
            isStateUpdateLoopRunning = false; // Reset state when connection closes
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = Encoding.UTF8.GetString(bytes);
            try
            {
                // First, check if it's a state message by looking for key fields
                var genericMessage = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                if (genericMessage.ContainsKey("players") && genericMessage.ContainsKey("counters"))
                {
                    var state = JsonConvert.DeserializeObject<StateRepresentation>(message);
                    // Debug.Log($"Received state with ended = {state.ended}"); // Removed to reduce lag
                    if (state != null)
                    {
                        OnStateReceived?.Invoke(state);

                        if (state.ended)
                        {
                            if (nextLevelCanvas != null)
                            {
                                NextLevelUI nextLevelUI = nextLevelCanvas.GetComponent<NextLevelUI>();
                                if (nextLevelUI != null)
                                {
                                    if (nextLevelUI != null)
                                {
                                    Debug.Log($"Attempting to show NextLevelUI. Canvas active: {nextLevelCanvas.activeSelf}. UI component present: {nextLevelUI != null}");
                                    
                                    // Safely parse level number from levelInfo.name
                                    int levelNumber = 1; // Default value
                                    if (!string.IsNullOrEmpty(levelInfo?.name))
                                    {
                                        var nameParts = levelInfo.name.Split('_');
                                        if (nameParts.Length > 0)
                                        {
                                            // Try to parse the last part, or first part if no underscore
                                            string levelPart = nameParts.Length > 1 ? nameParts[nameParts.Length - 1] : nameParts[0];
                                            if (!int.TryParse(levelPart, out levelNumber))
                                            {
                                                levelNumber = 1; // Fallback if parsing fails
                                            }
                                        }
                                    }
                                    
                                    mainThreadActions.Enqueue(() => nextLevelUI.Show(levelNumber, state.score, state.served_meals));
                                }
                                else
                                {
                                    Debug.LogError("NextLevelUI component is null on NextLevelCanvas.");
                                }
                                }
                            }
                        }
                    }
                }
                else if (genericMessage.ContainsKey("request_type") && genericMessage["request_type"].ToString() == "action")
                {
                    // Expected response for an action, no need to log as an error
                    // Debug.Log($"Action response: {message}"); // Removed to reduce lag
                }
                else
                {
                    // Debug.Log($"Received non-state message: {message}"); // Commented out to reduce console spam
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to process message: {e.Message}\nMessage content: {message}");
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
                levelInfo = gameConnectionData.level_info;

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
        // Step 1: Close existing WebSocket connection
        if (websocket != null)
        {
            await websocket.Close();
            websocket = null;
        }
        
        // Step 2: Clear previous level state and reset player controller
        if (gameManager != null)
        {
            gameManager.OnNewLevelStarted();
        }
        
        // Step 3: Reset connection state
        isStateUpdateLoopRunning = false;
        
        // Step 4: Reconnect following the same sequence as initial connection
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
        if (string.IsNullOrEmpty(myPlayerHash)) return; // Don't send if we don't have a hash yet

        WebsocketMessage message = new WebsocketMessage { type = "get_state", player_hash = myPlayerHash };
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
            // Request state updates at same frequency as our input sending (ultra-responsive)
            RequestState();
            yield return null; // Every frame - as smooth as our input system
        }
        isStateUpdateLoopRunning = false;
    }
}



