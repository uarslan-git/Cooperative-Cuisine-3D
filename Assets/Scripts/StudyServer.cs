using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

public class StudyClient : MonoBehaviour
{
    // Configuration
    private string studyServerUrl = "http://localhost:8080";
    private string participantId = "d8c146a9-c2c0-40ec-b7b8-4f9647e4d25a";
    private int numberOfPlayers = 2;
    private string websocketEndpoint = "ws://localhost:8080/ws/player/";
    private ClientWebSocket webSocket;
    private bool isWebSocketConnected = false;
    private CancellationTokenSource cancellationTokenSource;
    private Dictionary<string, string> playerWebSocketUrls = new Dictionary<string, string>();
    private string myPlayerHash = "e88f886936bd4962926d4b16a12d9c39";
    private string myPlayerNumber;

    // Game State Data Structures
    [System.Serializable]
    public class GameState
    {
        public int level;
        public string phase;
        public Dictionary<string, object> players;
        public Dictionary<string, object> objects;
        public float time;
        public bool is_ready;
        public bool is_over;
    }

    [System.Serializable]
    public class PlayerInfo
    {
        public string client_id { get; set; }
        public string player_hash { get; set; }
        public string player_id { get; set; }
        public string websocket_url { get; set; }
    }

    [System.Serializable]
    public class LevelInfo
    {
        public string name { get; set; }
        public bool last_level { get; set; }
        public List<RecipeGraph> recipe_graphs { get; set; }
        public int number_players { get; set; }
        public List<float> kitchen_size { get; set; }
    }

    [System.Serializable]
    public class RecipeGraph
    {
        public string meal { get; set; }
        public List<List<string>> edges { get; set; }
        public Dictionary<string, List<float>> layout { get; set; }
        public float score { get; set; } // Changed from int to float
        public RecipeInfo info { get; set; }
    }

    [System.Serializable]
    public class RecipeInfo
    {
        public List<string> interactive_counter { get; set; }
        public List<string> equipment { get; set; }
    }

    [System.Serializable]
    public class GetGameConnectionResponse
    {
        public Dictionary<string, PlayerInfo> player_info { get; set; }
        public LevelInfo level_info { get; set; }
    }

    [System.Serializable]
    public class Recipe
    {
        public List<string> ingredients;
        public List<string> steps;
    }

    [System.Serializable]
    public class LayoutItem
    {
        public string type;
        public List<float> location;
    }

    [System.Serializable]
    public class RecipeLayout
    {
        public List<float> location;
    }

    [System.Serializable]
    public class Equipment
    {
        public string type;
        public List<float> location;
    }

    [System.Serializable]
    public class PlayerState
    {
        public int x;
        public int y;
        public int score;
    }

    [System.Serializable]
    public class ObjectState
    {
        public string type;
        public int x;
        public int y;
    }

    [System.Serializable]
    public class GetStateMessage
    {
        public string type;
        public string player_hash;
    }

    // Coroutine to make POST requests
    IEnumerator PostRequest(string endpoint, string dataJson, Action<string> callback)
    {
        var url = studyServerUrl + endpoint;
        using (UnityWebRequest www = UnityWebRequest.Post(url, dataJson, "application/json"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + www.error);
                Debug.LogError("URL: " + url);
                Debug.LogError("Response Code: " + www.responseCode);
                if (www.downloadHandler != null)
                {
                    Debug.LogError("Response Text: " + www.downloadHandler.text);
                }
                callback(null);
            }
            else
            {
                Debug.Log("Post request successful: " + www.downloadHandler.text);
                callback(www.downloadHandler.text);
            }
        }
    }

    // 1. Start Study POST Request
    public void StartStudy()
    {
        string endpoint = $"/start_study/{participantId}/{numberOfPlayers}";
        string emptyJson = "{}";

        StartCoroutine(PostRequest(endpoint, emptyJson, (response) =>
        {
            if (response != null)
            {
                Debug.Log("Start Study Response: " + response);
                GetGameConnection();
            }
            else
            {
                Debug.LogError("Failed to start study.");
            }
        }));
    }


    // 2. Get Game Connection POST Request
    public void GetGameConnection()
    {
        string endpoint = $"/get_game_connection/{participantId}";
        string emptyJson = "{}";

        StartCoroutine(PostRequest(endpoint, emptyJson, (response) =>
        {
            if (response != null)
            {
                Debug.Log("Get Game Connection Response: " + response);
                try
                {
                    GetGameConnectionResponse gameConnectionResponse = JsonConvert.DeserializeObject<GetGameConnectionResponse>(response);

                    if (gameConnectionResponse?.player_info != null)
                    {
                        foreach (var playerEntry in gameConnectionResponse.player_info)
                        {
                            playerWebSocketUrls[playerEntry.Key] = playerEntry.Value.websocket_url;
                            if (playerEntry.Value.player_hash == myPlayerHash)
                            {
                                myPlayerNumber = playerEntry.Key;
                                string wsUrl = playerEntry.Value.websocket_url;
                                Debug.Log($"My Player Number: {myPlayerNumber}, WebSocket URL: {wsUrl}");
                                StartCoroutine(ConnectWebSocket(wsUrl));
                                break; // Found our player, no need to continue iterating
                            }
                        }

                        if (string.IsNullOrEmpty(myPlayerNumber))
                        {
                            Debug.LogError($"Could not find my player hash '{myPlayerHash}' in the player_info.");
                        }
                    }
                    else
                    {
                        Debug.LogError("player_info is null in Get Game Connection response.");
                    }

                    if (gameConnectionResponse?.level_info != null)
                    {
                        Debug.Log("Level Info: " + JsonConvert.SerializeObject(gameConnectionResponse.level_info, Formatting.Indented));
                        // You can now access level_info.name, level_info.last_level, etc.
                    }
                    else
                    {
                        Debug.LogError("level_info is null in Get Game Connection response.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Error parsing Get Game Connection response: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("Failed to get game connection.");
            }
        }));
    }


    // 3. WebSocket Connection and Communication
    IEnumerator ConnectWebSocket(string websocketUrl)
    {
        webSocket = new ClientWebSocket();
        cancellationTokenSource = new CancellationTokenSource();

        Uri uri = null;
        Task connectTask = null;

        try
        {
            uri = new Uri(websocketUrl);
            Debug.Log("Connecting to WebSocket: " + uri.AbsoluteUri);
            connectTask = webSocket.ConnectAsync(uri, cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Debug.LogError("WebSocket setup error: " + e.Message);
            isWebSocketConnected = false;
            yield break;
        }

        // Wait for the connection to complete
        yield return new WaitUntil(() => connectTask.IsCompleted);

        // Check result after connection attempt
        if (webSocket.State == WebSocketState.Open)
        {
            isWebSocketConnected = true;
            Debug.Log("WebSocket connected!");
            yield return StartCoroutine(SendWebSocketMessage("ready"));
            StartCoroutine(ReceiveWebSocketMessages());
        }
        else
        {
            Debug.LogError("Failed to connect to WebSocket. State: " + webSocket.State);
            isWebSocketConnected = false;
        }
    }



    IEnumerator SendWebSocketMessage(string message)
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("WebSocket is not connected.");
            yield break;
        }

        byte[] buffer = Encoding.UTF8.GetBytes(message);
        Task sendTask = null;

        try
        {
            sendTask = webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Debug.LogError("Exception starting SendAsync: " + e.Message);
            yield break;
        }

        // Wait for the send task to complete
        yield return new WaitUntil(() => sendTask.IsCompleted);

        if (sendTask.IsFaulted)
        {
            Debug.LogError("Error sending message: " + sendTask.Exception?.GetBaseException().Message);
        }
        else
        {
            Debug.Log("Sent message: " + message);
        }
    }


    IEnumerator ReceiveWebSocketMessages()
    {
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            Debug.LogError("WebSocket is not connected.");
            yield break;
        }

        byte[] buffer = new byte[1024 * 4];
        while (isWebSocketConnected)
        {
            WebSocketReceiveResult result = null;
            Task<WebSocketReceiveResult> receiveTask = null;
            bool hasError = false;
            Exception error = null;

            try
            {
                receiveTask = webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                hasError = true;
                error = e;
            }

            if (hasError)
            {
                Debug.LogError("Error starting WebSocket receive: " + error.Message);
                isWebSocketConnected = false;
                break;
            }

            yield return new WaitUntil(() => receiveTask.IsCompleted);

            try
            {
                if (receiveTask.IsFaulted)
                {
                    throw receiveTask.Exception;
                }

                result = receiveTask.Result;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("WebSocket connection closed by server.");
                    isWebSocketConnected = false;
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Debug.Log("Received message: " + message);

                if (message.Contains("\"phase\":") && message.Contains("\"players\":"))
                {
                    try
                    {
                        GameState gameState = JsonConvert.DeserializeObject<GameState>(message);
                        ProcessGameState(gameState);
                    }
                    catch (JsonException e)
                    {
                        Debug.LogError("Error deserializing GameState: " + e.Message + "\nMessage: " + message);
                    }
                }
                else if (message == "ok")
                {
                    Debug.Log("Received OK");
                }
                else
                {
                    Debug.Log("Received unknown message: " + message);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error processing WebSocket message: " + e.Message);
                isWebSocketConnected = false;
                if (receiveTask != null && receiveTask.Exception != null)
                {
                    Debug.LogError("Inner Exception: " + receiveTask.Exception.InnerException?.Message);
                }
                break;
            }

            yield return null;
        }
    }

    void ProcessGameState(GameState state)
    {
        Debug.Log("Received Game State - Level: " + state.level + ", Phase: " + state.phase);

        if (state.players != null)
        {
            foreach (var player in state.players)
            {
                Debug.Log("Player: " + player.Key);
                try
                {
                    string playerJson = JsonConvert.SerializeObject(player.Value);
                    PlayerState playerState = JsonConvert.DeserializeObject<PlayerState>(playerJson);
                    Debug.Log($"  x: {playerState.x}, y: {playerState.y}, score: {playerState.score}");
                }
                catch (JsonException e)
                {
                    Debug.LogError("Error deserializing player data: " + e.Message + " Player Data: " + JsonConvert.SerializeObject(player.Value));
                }
            }
        }

        if (state.objects != null)
        {
            foreach (var obj in state.objects)
            {
                Debug.Log("Object: " + obj.Key);
                try
                {
                    string objectJson = JsonConvert.SerializeObject(obj.Value);
                    ObjectState objectState = JsonConvert.DeserializeObject<ObjectState>(objectJson);
                    Debug.Log($"  Type: {objectState.type}, x: {objectState.x}, y: {objectState.y}");
                }
                catch (JsonException e)
                {
                    Debug.LogError("Error deserializing object data: " + e.Message + " Object Data: " + JsonConvert.SerializeObject(obj.Value));
                }
            }
        }
    }

    public void SendAction(string action)
    {
        if (isWebSocketConnected)
        {
            StartCoroutine(SendWebSocketMessage(action));
        }
        else
        {
            Debug.LogError("WebSocket is not connected. Cannot send action.");
        }
    }

    public void SendGetStateMessage()
    {
        GetStateMessage getStateMessage = new GetStateMessage
        {
            type = "get_state",
            player_hash = myPlayerHash
        };
        string jsonMessage = JsonConvert.SerializeObject(getStateMessage);
        StartCoroutine(SendWebSocketMessage(jsonMessage));
    }

    public void DisconnectWebSocket()
    {
        if (webSocket != null)
        {
            isWebSocketConnected = false;
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
            }

            Task closeTask = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
            StartCoroutine(WaitForClose(closeTask));
        }
    }

    IEnumerator WaitForClose(Task closeTask)
    {
        yield return new WaitUntil(() => closeTask.IsCompleted);

        if (closeTask.IsFaulted)
        {
            Debug.LogError("WebSocket close failed: " + closeTask.Exception.Message);
        }
        else
        {
            Debug.Log("WebSocket disconnected.");
        }
        webSocket = null;
        cancellationTokenSource = null;
    }

    void Start()
    {
        StartStudy();
    }

    void OnDestroy()
    {
        DisconnectWebSocket();
    }
}