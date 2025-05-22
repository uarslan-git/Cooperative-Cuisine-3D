using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for Dictionary and List
using UnityEngine.Networking;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using Newtonsoft.Json; // Required for JsonConvert and JsonProperty attributes
using System.Threading.Tasks;

public class StudyClient : MonoBehaviour
{
    // Singleton instance
    public static StudyClient Instance { get; private set; }

    // Configuration
    private string studyServerUrl = "http://localhost:8080";
    private string participantId; // This will be generated dynamically
    private string myPlayerHash; // This will be set dynamically from the server response
    private int numberOfPlayers = 1;
    private string websocketEndpoint; // This will be set dynamically from the server response
    private ClientWebSocket webSocket;
    private bool isWebSocketConnected = false;
    private CancellationTokenSource cancellationTokenSource;
    private Dictionary<string, string> playerWebSocketUrls = new Dictionary<string, string>();
    private string myPlayerNumber; // Stores which player number (e.g., "0", "1") this client is

    // Game State Data Structures with JsonProperty attributes
    [System.Serializable]
    public class GameState
    {
        [JsonProperty("level")]
        public int level { get; set; }

        [JsonProperty("phase")]
        public string phase { get; set; }

        [JsonProperty("players")]
        public Dictionary<string, object> players { get; set; } // Can be deserialized to PlayerState if needed

        [JsonProperty("objects")]
        public Dictionary<string, object> objects { get; set; } // Can be deserialized to ObjectState if needed

        [JsonProperty("time")]
        public float time { get; set; }

        [JsonProperty("is_ready")]
        public bool is_ready { get; set; }

        [JsonProperty("is_over")]
        public bool is_over { get; set; }
    }

    [System.Serializable]
    public class PlayerInfo
    {
        [JsonProperty("client_id")]
        public string client_id { get; set; }

        [JsonProperty("player_hash")]
        public string player_hash { get; set; }

        [JsonProperty("player_id")]
        public string player_id { get; set; }

        [JsonProperty("websocket_url")]
        public string websocket_url { get; set; }
    }

    [System.Serializable]
    public class LevelInfo
    {
        [JsonProperty("name")]
        public string name { get; set; }

        [JsonProperty("last_level")]
        public bool last_level { get; set; }

        [JsonProperty("recipe_graphs")]
        public List<RecipeGraph> recipe_graphs { get; set; }

        [JsonProperty("number_players")]
        public int number_players { get; set; }

        [JsonProperty("kitchen_size")]
        public List<float> kitchen_size { get; set; }
    }

    [System.Serializable]
    public class RecipeGraph
    {
        [JsonProperty("meal")]
        public string meal { get; set; }

        [JsonProperty("edges")]
        public List<List<string>> edges { get; set; }

        [JsonProperty("layout")]
        public Dictionary<string, List<float>> layout { get; set; }

        [JsonProperty("score")]
        public float score { get; set; }

        [JsonProperty("info")]
        public RecipeInfo info { get; set; }
    }

    [System.Serializable]
    public class RecipeInfo
    {
        [JsonProperty("interactive_counter")]
        public List<string> interactive_counter { get; set; }

        [JsonProperty("equipment")]
        public List<string> equipment { get; set; }
    }

    [System.Serializable]
    public class GetGameConnectionResponse
    {
        [JsonProperty("player_info")]
        public Dictionary<string, PlayerInfo> player_info { get; set; }

        [JsonProperty("level_info")]
        public LevelInfo level_info { get; set; }
    }

    [System.Serializable]
    public class Recipe // If you have a separate JSON for just a Recipe
    {
        [JsonProperty("ingredients")]
        public List<string> ingredients { get; set; }

        [JsonProperty("steps")]
        public List<string> steps { get; set; }
    }

    [System.Serializable]
    public class LayoutItem // If you have a separate JSON for a layout item
    {
        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("location")]
        public List<float> location { get; set; }
    }

    [System.Serializable]
    public class PlayerState // For deserializing individual player objects from GameState.players
    {
        [JsonProperty("x")]
        public int x { get; set; }

        [JsonProperty("y")]
        public int y { get; set; }

        [JsonProperty("score")]
        public int score { get; set; }
    }

    [System.Serializable]
    public class ObjectState // For deserializing individual object objects from GameState.objects
    {
        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("x")]
        public int x { get; set; }

        [JsonProperty("y")]
        public int y { get; set; }
    }

    [System.Serializable]
    public class GetStateMessage
    {
        [JsonProperty("type")]
        public string type { get; set; }

        [JsonProperty("player_hash")]
        public string player_hash { get; set; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Found more than one StudyClient instance. Destroying this duplicate.");
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Optional: if you want this client to persist across scenes
        }
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
        participantId = Guid.NewGuid().ToString();
        Debug.Log($"[StartStudy] Generated participantId: {participantId}");
        string endpoint = $"/start_study/{participantId}/{numberOfPlayers}";
        string emptyJson = "{}";

        Debug.Log($"Attempting to start study with Participant ID: {participantId}");
        Debug.Log($"[StartStudy] Sending POST to: {studyServerUrl + endpoint}");

        StartCoroutine(PostRequest(endpoint, emptyJson, (response) =>
        {
            if (response != null)
            {
                Debug.Log("Start Study Response: " + response);
                Debug.Log($"[StartStudy Callback] Current participantId before GetGameConnection: {participantId}");
                GetGameConnection(); // Proceed to get game connection with the new participantId
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
        Debug.Log($"[GetGameConnection] Using participantId for GET: {participantId}");
        string endpoint = $"/get_game_connection/{participantId}";
        string emptyJson = "{}";

        Debug.Log($"Attempting to get game connection for Participant ID: {participantId}");
        Debug.Log($"[GetGameConnection] Sending POST to: {studyServerUrl + endpoint}");

        StartCoroutine(PostRequest(endpoint, emptyJson, (response) =>
        {
            if (response != null)
            {
                Debug.Log("Get Game Connection Response: " + response);
                try
                {
                    GetGameConnectionResponse gameConnectionResponse = JsonConvert.DeserializeObject<GetGameConnectionResponse>(response);

                    if (gameConnectionResponse?.player_info != null && gameConnectionResponse.player_info.Count > 0)
                    {
                        var firstPlayerEntry = gameConnectionResponse.player_info.GetEnumerator().Current;

                        // Iterate through the player_info to find the first entry
                        // Or, if you need to find a *specific* player based on some criteria
                        // (e.g., if you had a specific ID assigned by the server that you later
                        // expected in this response), you'd add a condition here.
                        // For now, assuming the first one is 'ours' as you indicated.
                        // Using a simple foreach to safely get the first element
                        foreach (var playerEntry in gameConnectionResponse.player_info) // The foreach loop handles the enumerator correctly
                        {
                            myPlayerNumber = playerEntry.Key;
                            myPlayerHash = playerEntry.Value.player_hash; // Set myPlayerHash dynamically
                            websocketEndpoint = playerEntry.Value.websocket_url; // Set websocketEndpoint dynamically

                            Debug.Log($"[GetGameConnection Callback] Found my player info. Player Number: {myPlayerNumber}, Player Hash: {myPlayerHash}, WebSocket URL: {websocketEndpoint}");
                            break; // Take the first one and break, assuming it's the relevant player
                        }

                        // Now, make sure myPlayerHash and websocketEndpoint are actually set
                        if (!string.IsNullOrEmpty(myPlayerHash) && !string.IsNullOrEmpty(websocketEndpoint))
                        {
                            StartCoroutine(ConnectWebSocket(websocketEndpoint)); // Use the received websocket_url
                        }
                        else
                        {
                            Debug.LogError("player_info was not empty but could not extract valid myPlayerHash or websocketEndpoint. Response: " + response);
                        }

                    }
                    else
                    {
                        Debug.LogError("player_info is null or empty in Get Game Connection response. Cannot proceed with WebSocket connection.");
                    }

                    if (gameConnectionResponse?.level_info != null)
                    {
                        Debug.Log("Level Info: " + JsonConvert.SerializeObject(gameConnectionResponse.level_info, Formatting.Indented));
                    }
                    else
                    {
                        Debug.LogError("level_info is null in Get Game Connection response. This indicates a problem with the server response or deserialization.");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("Error parsing Get Game Connection response: " + e.Message + "\nStack Trace: " + e.StackTrace);
                }
            }
            else
            {
                Debug.LogError("Failed to get game connection. Response was null.");
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

        yield return new WaitUntil(() => connectTask.IsCompleted);

        if (webSocket.State == WebSocketState.Open)
        {
            isWebSocketConnected = true;
            Debug.Log("WebSocket connected!");

            // --- CRITICAL CHANGE HERE ---
            // Instead of sending "ready", send the initial "get_state" message with myPlayerHash
            SendGetStateMessage(); // This method already constructs the correct JSON
            // --- END CRITICAL CHANGE ---

            StartCoroutine(ReceiveWebSocketMessages());
        }
        else
        {
            Debug.LogError("Failed to connect to WebSocket. State: " + webSocket.State + (connectTask.Exception != null ? " Exception: " + connectTask.Exception.Message : ""));
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
                    string playerJson = JsonConvert.SerializeObject(player.Value); // Serialize the object to JSON string first
                    PlayerState playerState = JsonConvert.DeserializeObject<PlayerState>(playerJson); // Then deserialize into PlayerState
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
                    string objectJson = JsonConvert.SerializeObject(obj.Value); // Serialize the object to JSON string first
                    ObjectState objectState = JsonConvert.DeserializeObject<ObjectState>(objectJson); // Then deserialize into ObjectState
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
        if (string.IsNullOrEmpty(myPlayerHash))
        {
            Debug.LogError("myPlayerHash is not set. Cannot send get_state message.");
            return;
        }

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
        if (Instance == this)
        {
            StartStudy();
        }
    }

    void OnDestroy()
    {
        DisconnectWebSocket();
    }
}