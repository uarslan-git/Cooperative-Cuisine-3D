
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;

// Backend-compatible ActionType and InterActionData enums
public enum ActionType { movement, pick_up_drop, interact }
public enum InterActionData { keydown, keyup }

public class StudyClient : MonoBehaviour
{
    // Backend-compatible Action class matching Python dataclass exactly
    [System.Serializable]
    private class Action
    {
        [JsonProperty("player")]
        public string player;
        [JsonProperty("action_type")]
        public string action_type;  // Use string to match Python enum values
        [JsonProperty("action_data")]
        public object action_data; // float[], string, or null
        [JsonProperty("duration")]
        public float duration;
    }

    // Track counters by id for interaction
    private Dictionary<string, GameObject> kitchenCounters = new Dictionary<string, GameObject>();

    // Simple holder for player state info (for nearest counter, etc.)
    public class PlayerStateHolder : MonoBehaviour
    {
        public List<float> currentNearestCounterPos;
        public string currentNearestCounterId;
    }

    // Simple holder for counter state info (for occupied_by, etc.)
    public class CounterStateHolder : MonoBehaviour
    {
        public string occupiedByType;
    }
    // Marker for nearest counter
    private GameObject nearestCounterMarker;
    // Track previously highlighted counter
    private GameObject previousHighlightedCounter;
    // Track what the local player is holding
    private GameObject heldItemObj;
    private string heldItemType;
    // Reference to the local player GameObject
    private GameObject localPlayerObj;
    // Movement speed for the local player (reduced to better match Python GUI)
    private float localPlayerMoveSpeed = 4f;
    // Reference to the local player's Rigidbody
    private Rigidbody localPlayerRb;
    
    // UI Elements for recipe counter, points, and countdown
    private GameObject uiCanvas;
    private Text scoreText;
    private Text countdownText;
    private Text recipeCounterText;
    private Text ordersText;
    private GameObject gameEndPanel;
    private Text gameEndText;
    private Button nextLevelButton;
    private float currentScore = 0f;
    private float remainingTime = 0f;
    private int completedRecipes = 0;
    private List<OrderState> currentOrders = new List<OrderState>();
    private bool gameEnded = false;

    void Update()
    {
        // Controls (matching Python GUI exactly):
        // WASD - Move player (A=left[-1,0], D=right[1,0], W=up[0,-1], S=down[0,1])
        // E - Pick up / Drop items (pickup_key)
        // F - Interact with counters (interact_key - hold to use cutting boards, stoves, etc.)
        // R - Send ready signal to start game (manual override)
        
        // Visualize nearest_counter_pos
        if (localPlayerObj != null && playerObjects.TryGetValue(myPlayerNumber, out var playerObj))
        {
            var playerState = playerObj.GetComponent<PlayerStateHolder>();
            if (playerState != null && playerState.currentNearestCounterPos != null)
            {
                // Scale the marker position to match kitchen object spacing
                Vector3 markerPos = new Vector3(playerState.currentNearestCounterPos[0] * 4.0f, 1.2f, playerState.currentNearestCounterPos[1] * 4.0f);
                if (nearestCounterMarker == null)
                {
                    nearestCounterMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    nearestCounterMarker.transform.localScale = Vector3.one * 0.4f;
                    nearestCounterMarker.GetComponent<Renderer>().material.color = Color.yellow;
                    Destroy(nearestCounterMarker.GetComponent<Collider>());
                }
                nearestCounterMarker.transform.position = markerPos;
                nearestCounterMarker.SetActive(true);

                // Reset previous counter color
                if (previousHighlightedCounter != null)
                {
                    var prevRenderer = previousHighlightedCounter.GetComponent<Renderer>();
                    if (prevRenderer != null)
                    {
                        prevRenderer.material.color = new Color(0.7f, 0.7f, 0.7f); // Default counter color
                    }
                }

                // Highlight the nearest counter
                if (playerState.currentNearestCounterId != null && 
                    kitchenCounters.TryGetValue(playerState.currentNearestCounterId, out var counterObj))
                {
                    var renderer = counterObj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.green; // Highlight color
                        previousHighlightedCounter = counterObj;
                    }
                }
                else
                {
                    previousHighlightedCounter = null;
                }
            }
            else if (nearestCounterMarker != null)
            {
                nearestCounterMarker.SetActive(false);
                
                // Reset previous counter color
                if (previousHighlightedCounter != null)
                {
                    var renderer = previousHighlightedCounter.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = new Color(0.7f, 0.7f, 0.7f); // Default counter color
                    }
                    previousHighlightedCounter = null;
                }
            }
        }

        // Handle E key for pick/place
        if (Input.GetKeyDown(KeyCode.E) && localPlayerObj != null)
        {
            var playerState = localPlayerObj.GetComponent<PlayerStateHolder>();
            if (playerState != null && playerState.currentNearestCounterId != null)
            {
                // Find the counter GameObject
                if (kitchenCounters.TryGetValue(playerState.currentNearestCounterId, out var counterObj))
                {
                    var counterState = counterObj.GetComponent<CounterStateHolder>();
                    if (heldItemObj == null && counterState != null && counterState.occupiedByType != null)
                    {
                        // Pick up item
                        heldItemType = counterState.occupiedByType;
                        heldItemObj = counterObj.transform.Find("HeldItem")?.gameObject;
                        if (heldItemObj != null)
                            heldItemObj.transform.SetParent(localPlayerObj.transform);
                        // Send pick action to backend
                        SendPickUpDropAction();
                    }
                    else if (heldItemObj != null && counterState != null && counterState.occupiedByType == null)
                    {
                        // Place item
                        heldItemObj.transform.SetParent(counterObj.transform);
                        heldItemObj.transform.localPosition = Vector3.up * 1.1f;
                        // Send place action to backend
                        SendPickUpDropAction();
                        heldItemObj = null;
                        heldItemType = null;
                    }
                }
            }
        }

        // Handle F key for interaction (hold to interact with counters like cutting boards)
        // Matches Python GUI interact_key behavior exactly
        if (Input.GetKeyDown(KeyCode.F) && localPlayerObj != null)
        {
            var playerState = localPlayerObj.GetComponent<PlayerStateHolder>();
            if (playerState != null && playerState.currentNearestCounterId != null)
            {
                SendInteractAction(InterActionData.keydown);
            }
        }
        
        if (Input.GetKeyUp(KeyCode.F) && localPlayerObj != null)
        {
            var playerState = localPlayerObj.GetComponent<PlayerStateHolder>();
            if (playerState != null && playerState.currentNearestCounterId != null)
            {
                SendInteractAction(InterActionData.keyup);
            }
        }

        // Handle R key to send ready manually (for debugging)
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[Update] R key pressed - sending ready message");
            SendPlayerReady();
        }
        
        // Handle G key to request game state (for debugging)
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("[Update] G key pressed - requesting game state");
            SendGetStateMessage();
        }
        
        // Periodically request game state to keep it updated
        if (isWebSocketConnected && Time.time - lastStateRequestTime > stateRequestInterval)
        {
            lastStateRequestTime = Time.time;
            SendGetStateMessage();
        }

        // Handle WASD movement exactly like Python GUI
        // Python key mappings: A=left[-1,0], D=right[1,0], W=up[0,-1], S=down[0,1]
        if (localPlayerObj != null && localPlayerRb != null)
        {
            Vector2 moveVec = Vector2.zero;
            
            // Match Python GUI key mappings exactly
            if (Input.GetKey(KeyCode.A)) moveVec += new Vector2(-1, 0);  // Left
            if (Input.GetKey(KeyCode.D)) moveVec += new Vector2(1, 0);   // Right  
            if (Input.GetKey(KeyCode.W)) moveVec += new Vector2(0, -1);  // Up
            if (Input.GetKey(KeyCode.S)) moveVec += new Vector2(0, 1);   // Down
            
            if (moveVec.magnitude > 0.01f)
            {
                // Normalize movement vector like Python GUI
                moveVec = moveVec.normalized;
                
                // Convert to Unity 3D coordinates (x = horizontal, z = vertical)
                Vector3 move3D = new Vector3(moveVec.x, 0, moveVec.y);
                
                // Update facing direction
                localPlayerObj.transform.rotation = Quaternion.LookRotation(move3D, Vector3.up);
                
                // For local movement prediction, move the player immediately but let server override
                // Use normal movement speed without extra scaling
                Vector3 targetPos = localPlayerObj.transform.position + move3D * localPlayerMoveSpeed * Time.deltaTime;
                localPlayerObj.transform.position = targetPos;
                
                // Send movement action to backend with exact Python format
                SendMovementAction(new float[] { moveVec.x, moveVec.y }, Time.deltaTime);
            }
        }
        
        // Update UI every frame
        UpdateUI();
    }
    // Track player GameObjects by player ID
    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
    // Store the current level info for kitchen size and other properties
    private LevelInfo levelInfo;
    // Singleton instance
    public static StudyClient Instance { get; private set; }
    
    // State update timing (increased frequency for better sync)
    private float lastStateRequestTime = 0f;
    private float stateRequestInterval = 0.1f; // Request state every 100ms for better sync

    // Configuration
    private string studyServerUrl = "http://localhost:8080";
    private string participantId;
    private string myPlayerHash;
    private int numberOfPlayers = 1;
    private string websocketEndpoint;
    private ClientWebSocket websocket;
    private bool isWebSocketConnected = false;
    private CancellationTokenSource cancellationTokenSource;
    private Dictionary<string, string> playerWebSocketUrls = new Dictionary<string, string>();
    private string myPlayerNumber;

    // Serializable classes
    [System.Serializable]
    public class GameState
    {
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("phase")]
        public string Phase { get; set; }

        [JsonProperty("players")]
        public Dictionary<string, object> Players { get; set; }

        [JsonProperty("objects")]
        public Dictionary<string, object> Objects { get; set; }

        [JsonProperty("time")]
        public float Time { get; set; }

        [JsonProperty("is_ready")]
        public bool IsReady { get; set; }

        [JsonProperty("is_over")]
        public bool IsOver { get; set; }
    }

    [System.Serializable]
    public class PlayerInfo
    {
        [JsonProperty("client_id")]
        public string ClientId { get; set; }

        [JsonProperty("player_hash")]
        public string PlayerHash { get; set; }

        [JsonProperty("player_id")]
        public string PlayerId { get; set; }

        [JsonProperty("websocket_url")]
        public string WebSocketUrl { get; set; }
    }

    [System.Serializable]
    public class LevelInfo
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("last_level")]
        public bool LastLevel { get; set; }

        [JsonProperty("recipe_graphs")]
        public List<RecipeGraph> RecipeGraphs { get; set; }

        [JsonProperty("number_players")]
        public int NumberPlayers { get; set; }

        [JsonProperty("kitchen_size")]
        public List<float> KitchenSize { get; set; }
    }

    [System.Serializable]
    public class RecipeGraph
    {
        [JsonProperty("meal")]
        public string Meal { get; set; }

        [JsonProperty("edges")]
        public List<List<string>> Edges { get; set; }

        [JsonProperty("layout")]
        public Dictionary<string, List<float>> Layout { get; set; }

        [JsonProperty("score")]
        public float Score { get; set; }

        [JsonProperty("info")]
        public RecipeInfo Info { get; set; }
    }

    [System.Serializable]
    public class RecipeInfo
    {
        [JsonProperty("interactive_counter")]
        public List<string> InteractiveCounter { get; set; }

        [JsonProperty("equipment")]
        public List<string> Equipment { get; set; }
    }

    [System.Serializable]
    public class GetGameConnectionResponse
    {
        [JsonProperty("player_info")]
        public Dictionary<string, PlayerInfo> PlayerInfo { get; set; }

        [JsonProperty("level_info")]
        public LevelInfo LevelInfo { get; set; }
    }

    // Example method to process GetGameConnectionResponse and set levelInfo
    private void ProcessConnectionResponse(GetGameConnectionResponse response)
    {
        if (response != null && response.LevelInfo != null)
        {
            levelInfo = response.LevelInfo;
        }
    }

    [System.Serializable]
    public class KitchenState
    {
        [JsonProperty("objects")]
        public List<KitchenObject> Objects { get; set; }

        [JsonProperty("kitchen")]
        public KitchenDimensions Kitchen { get; set; }
    }

    [System.Serializable]
    public class KitchenObject
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("pos")]
        public List<float> Pos { get; set; }

        [JsonProperty("orientation")]
        public List<int> Orientation { get; set; }

        [JsonProperty("occupied_by")]
        public object OccupiedBy { get; set; } // Can be null, object, or array

        [JsonProperty("active_effects")]
        public List<object> ActiveEffects { get; set; } // Add this line
    }

    [System.Serializable]
    public class KitchenDimensions
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    [System.Serializable]
    public class GetStateMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "get_state";

        [JsonProperty("player_hash")]
        public string PlayerHash { get; set; }
    }

    [System.Serializable]
    public class FullKitchenState
    {
        [JsonProperty("players")]
        public List<PlayerState> Players { get; set; }

        [JsonProperty("counters")]
        public List<KitchenObject> Counters { get; set; }

        [JsonProperty("kitchen")]
        public KitchenDimensions Kitchen { get; set; }

        [JsonProperty("score")]
        public float Score { get; set; }

        [JsonProperty("orders")]
        public List<OrderState> Orders { get; set; }

        [JsonProperty("ended")]
        public bool Ended { get; set; }

        [JsonProperty("env_time")]
        public string EnvTime { get; set; }

        [JsonProperty("remaining_time")]
        public float RemainingTime { get; set; }

        [JsonProperty("view_restrictions")]
        public object ViewRestrictions { get; set; }

        [JsonProperty("served_meals")]
        public List<object> ServedMeals { get; set; }

        [JsonProperty("info_msg")]
        public List<object> InfoMsg { get; set; }

        [JsonProperty("all_players_ready")]
        public bool AllPlayersReady { get; set; }
    }

    [System.Serializable]
    public class PlayerState
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("pos")]
        public List<float> Pos { get; set; }

        [JsonProperty("facing_direction")]
        public List<float> FacingDirection { get; set; }

        [JsonProperty("holding")]
        public object Holding { get; set; }

        [JsonProperty("current_nearest_counter_pos")]
        public object CurrentNearestCounterPos { get; set; }

        [JsonProperty("current_nearest_counter_id")]
        public object CurrentNearestCounterId { get; set; }

        [JsonProperty("player_info")]
        public object PlayerInfo { get; set; }
    }

    [System.Serializable]
    public class OrderState
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("meal")]
        public string Meal { get; set; }

        [JsonProperty("start_time")]
        public string StartTime { get; set; }

        [JsonProperty("max_duration")]
        public float MaxDuration { get; set; }

        [JsonProperty("score")]
        public float Score { get; set; }
    }

    void Awake()
    {
        // Handle singleton pattern and prevent editor errors
        if (gameObject.scene.name == null) return; // Skip if this is a prefab

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Found more than one StudyClient instance. Destroying this duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

void Start()
{
    if (Instance == this)
    {
        Debug.Log($"StudyServer starting on GameObject: {gameObject.name} at position {transform.position}");
        CreateUI();
        StartStudy();
    }
}

    void OnDestroy()
    {
        if (Instance == this)
        {
            DisconnectWebSocket();
            CleanUpKitchenObjects();
            Instance = null;
        }
    }

    private void CleanUpKitchenObjects()
    {
        foreach (Transform child in transform)
        {
            if (child != null && child.CompareTag("KitchenObject"))
            {
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }
    }

    // HTTP Request Methods
    IEnumerator PostRequest(string endpoint, string dataJson, Action<string> callback)
    {
        var url = studyServerUrl + endpoint;
        using (UnityWebRequest www = UnityWebRequest.Post(url, dataJson, "application/json"))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Error: {www.error}\nURL: {url}\nResponse Code: {www.responseCode}");
                if (www.downloadHandler != null)
                {
                    Debug.LogError($"Response Text: {www.downloadHandler.text}");
                }
                callback(null);
            }
            else
            {
                Debug.Log($"POST Request Successful: {www.downloadHandler.text}");
                callback(www.downloadHandler.text);
            }
        }
    }

    // Study Flow Methods
    public void StartStudy()
    {
        participantId = Guid.NewGuid().ToString();
        string endpoint = $"/start_study/{participantId}/{numberOfPlayers}";
        string emptyJson = "{}";

        StartCoroutine(PostRequest(endpoint, emptyJson, (response) =>
                    {
                    if (response != null)
                    {
                    Debug.Log($"Start Study Response: {response}");
                    GetGameConnection();
                    }
                    else
                    {
                    Debug.LogError("Failed to start study.");
                    }
                    }));
    }

    public void GetGameConnection()
    {
        string endpoint = $"/get_game_connection/{participantId}";
        string emptyJson = "{}";

        StartCoroutine(PostRequest(endpoint, emptyJson, (response) =>
                    {
                    if (response != null)
                    {
                    try
                    {
                    GetGameConnectionResponse gameConnectionResponse = JsonConvert.DeserializeObject<GetGameConnectionResponse>(response);

                    if (gameConnectionResponse?.PlayerInfo != null)
                    {
                    foreach (var playerEntry in gameConnectionResponse.PlayerInfo)
                    {
                    myPlayerNumber = playerEntry.Key;
                    myPlayerHash = playerEntry.Value.PlayerHash;
                    websocketEndpoint = playerEntry.Value.WebSocketUrl;
                    Debug.Log($"Found player info - Number: {myPlayerNumber}, Hash: {myPlayerHash}, WS URL: {websocketEndpoint}");
                    break;
                    }

                    if (!string.IsNullOrEmpty(myPlayerHash))
                    {
                        StartCoroutine(ConnectWebSocket(websocketEndpoint));
                    }
                    }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing response: {e.Message}\n{e.StackTrace}");
                    }
                    }
                    }));
    }

    IEnumerator ConnectWebSocket(string webSocketUrl)
    {
        websocket = new ClientWebSocket();
        cancellationTokenSource = new CancellationTokenSource();
        Uri uri = new Uri(webSocketUrl);

        bool connected = false;
        while (!connected)
        {
            Task connectTask = null;
            try
            {
                connectTask = websocket.ConnectAsync(uri, cancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"WebSocket Error: {e.Message}");
                isWebSocketConnected = false;
                yield break;
            }

            while (!connectTask.IsCompleted)
            {
                yield return null;
            }

            if (websocket.State == WebSocketState.Open)
            {
                isWebSocketConnected = true;
                Debug.Log("WebSocket connected!");
                
                // Send READY message exactly like Python GUI
                SendPlayerReady();
                
                StartCoroutine(ReceiveWebSocketMessages());
                connected = true;
            }
        }
    }

    private IEnumerator SendWebSocketMessage(string message)
    {
        if (websocket?.State != WebSocketState.Open)
        {
            Debug.LogError("WebSocket not connected");
            yield break;
        }

        byte[] buffer = Encoding.UTF8.GetBytes(message);
        Task sendTask = websocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, cancellationTokenSource.Token);
        yield return new WaitUntil(() => sendTask.IsCompleted);

        if (sendTask.IsFaulted)
        {
            Debug.LogError($"Send Error: {sendTask.Exception?.Message}");
        }
    }

private IEnumerator ReceiveWebSocketMessages()
{
    byte[] buffer = new byte[8192]; // Increased buffer size
    StringBuilder messageBuilder = new StringBuilder();
    
    while (isWebSocketConnected)
    {
        ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
        Task<WebSocketReceiveResult> receiveTask = null;
        
        try
        {
            receiveTask = websocket.ReceiveAsync(segment, cancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Debug.LogError($"Receive Error: {e.Message}");
            break;
        }

        while (!receiveTask.IsCompleted)
        {
            yield return null;
        }

        try
        {
            WebSocketReceiveResult result = receiveTask.Result;
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Debug.Log("WebSocket closed by server");
                break;
            }

            string partialMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
            messageBuilder.Append(partialMessage);

            if (result.EndOfMessage)
            {
                string completeMessage = messageBuilder.ToString();
                Debug.Log($"Complete message received, length: {completeMessage.Length}");
                ProcessWebSocketMessage(completeMessage);
                messageBuilder.Clear();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Message Processing Error: {e.Message}");
            break;
        }
    }
    
    isWebSocketConnected = false;
}
private void ProcessWebSocketMessage(string message)
{
    Debug.Log($"[ProcessWebSocketMessage] Raw message: {message}");
    try 
    {
        Debug.Log($"Processing message length: {message.Length}");
        
        // Try to find the start of a valid JSON object
        int startIndex = message.IndexOf("{");
        if (startIndex > 0)
        {
            message = message.Substring(startIndex);
            Debug.Log($"Fixed message: {message}");
        }
        
        // First check if this is a server response message
        try
        {
            var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
            if (responseObj.ContainsKey("request_type") && responseObj.ContainsKey("status"))
            {
                string requestType = responseObj["request_type"].ToString();
                int status = Convert.ToInt32(responseObj["status"]);
                string msg = responseObj.ContainsKey("msg") ? responseObj["msg"].ToString() : "";
                
                Debug.Log($"[ProcessWebSocketMessage] Server response: {requestType} - {msg} (status: {status})");
                
                if (requestType == "ready" && status == 200)
                {
                    Debug.Log("[ProcessWebSocketMessage] Ready message accepted by server!");
                    // Request game state after ready is accepted, like Python GUI
                    Debug.Log("[ProcessWebSocketMessage] About to call SendGetStateMessage()");
                    SendGetStateMessage();
                }
                else if (requestType == "action" && status == 200)
                {
                    Debug.Log("[ProcessWebSocketMessage] Action accepted by server!");
                }
                else if (status != 200)
                {
                    Debug.LogWarning($"[ProcessWebSocketMessage] Server rejected {requestType}: {msg}");
                }
                return; // Message handled
            }
        }
        catch (JsonException) { /* Continue to try game state formats */ }
        
        Debug.Log("[ProcessWebSocketMessage] Not a server response, trying to parse as game state...");
        
        // Try parsing as GameState since it's more complete
        try
        {
            Debug.Log("[ProcessWebSocketMessage] Attempting to parse as GameState...");
            GameState gameState = JsonConvert.DeserializeObject<GameState>(message);
            if (gameState?.Objects != null)
            {
                Debug.Log($"Successfully parsed as GameState with {gameState.Objects.Count} objects");
                ProcessGameState(gameState);
                return;
            }
            else
            {
                Debug.Log("[ProcessWebSocketMessage] GameState parsed but Objects is null");
            }
        }
        catch (JsonException ex) 
        { 
            Debug.Log($"[ProcessWebSocketMessage] Failed to parse as GameState: {ex.Message}");
        }

        // Try parsing as KitchenState
        try
        {
            Debug.Log("[ProcessWebSocketMessage] Attempting to parse as KitchenState...");
            KitchenState kitchenState = JsonConvert.DeserializeObject<KitchenState>(message);
            if (kitchenState?.Objects != null)
            {
                Debug.Log($"Successfully parsed as KitchenState with {kitchenState.Objects.Count} objects");
                CleanUpKitchenObjects();
                foreach (var obj in kitchenState.Objects)
                {
                    SpawnKitchenObject(obj);
                }
                return;
            }
            else
            {
                Debug.Log("[ProcessWebSocketMessage] KitchenState parsed but Objects is null");
            }
        }
        catch (JsonException ex) 
        { 
            Debug.Log($"[ProcessWebSocketMessage] Failed to parse as KitchenState: {ex.Message}");
        }

        // Try parsing as FullKitchenState (new format)
        try
        {
            Debug.Log("[ProcessWebSocketMessage] Attempting to parse as FullKitchenState...");
            FullKitchenState fullState = JsonConvert.DeserializeObject<FullKitchenState>(message);
            if (fullState?.Counters != null)
            {
                Debug.Log($"Successfully parsed as FullKitchenState with {fullState.Counters.Count} counters and {fullState.Players?.Count ?? 0} players");
                SpawnGround(fullState.Kitchen);
                CleanUpKitchenObjects();
                foreach (var obj in fullState.Counters)
                {
                    SpawnKitchenObject(obj);
                }
                if (fullState.Players != null)
                {
                    Debug.Log($"[ProcessWebSocketMessage] Processing {fullState.Players.Count} players from state update");
                    foreach (var player in fullState.Players)
                    {
                        Debug.Log($"[ProcessWebSocketMessage] Processing player: {JsonConvert.SerializeObject(player)}");
                        SpawnOrUpdatePlayer(player);
                    }
                }
                
                // Update UI with game state information
                currentScore = fullState.Score;
                remainingTime = fullState.RemainingTime;
                completedRecipes = fullState.ServedMeals?.Count ?? 0;
                currentOrders = fullState.Orders ?? new List<OrderState>();
                gameEnded = fullState.Ended;
                UpdateUI();
                
                // Handle game end
                if (gameEnded && !gameEndPanel.activeSelf)
                {
                    ShowGameEndScreen();
                }
                
                return;
            }
            else
            {
                Debug.Log("[ProcessWebSocketMessage] FullKitchenState parsed but Counters is null");
            }
        }
        catch (JsonException ex) 
        { 
            Debug.Log($"[ProcessWebSocketMessage] Failed to parse as FullKitchenState: {ex.Message}");
        }

        // Try parsing as simple player state update (just players array)
        try
        {
            Debug.Log("[ProcessWebSocketMessage] Attempting to parse as simple player state...");
            var simpleState = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
            if (simpleState.ContainsKey("players"))
            {
                var playersArray = simpleState["players"] as Newtonsoft.Json.Linq.JArray;
                if (playersArray != null)
                {
                    Debug.Log($"[ProcessWebSocketMessage] Found {playersArray.Count} players in simple state update");
                    foreach (var playerToken in playersArray)
                    {
                        var player = playerToken.ToObject<PlayerState>();
                        if (player != null)
                        {
                            Debug.Log($"[ProcessWebSocketMessage] Processing player from simple state: {JsonConvert.SerializeObject(player)}");
                            SpawnOrUpdatePlayer(player);
                        }
                    }
                    return;
                }
            }
        }
        catch (JsonException ex) 
        { 
            Debug.Log($"[ProcessWebSocketMessage] Failed to parse as simple player state: {ex.Message}");
        }

        Debug.LogWarning($"[ProcessWebSocketMessage] Could not parse message as server response or game state. Full message: {message}");
    }
    catch (Exception e)
    {
        Debug.LogError($"Message Processing Error: {e.Message}\nStack Trace: {e.StackTrace}\nMessage: {message}");
    }
}

        private void ProcessGameState(GameState state)
        {
            Debug.Log($"Game State - Level: {state.Level}, Phase: {state.Phase}");

        // If state has LevelInfo, update the field (add this logic if LevelInfo is available in state)
        // Example: if (state.LevelInfo != null) levelInfo = state.LevelInfo;

            if (state.Objects != null)
            {
                CleanUpKitchenObjects();

                foreach (var obj in state.Objects)
                {
                    try
                    {
                        var jobject = obj.Value as Newtonsoft.Json.Linq.JObject;
                        if (jobject != null)
                        {
                            KitchenObject kitchenObj = jobject.ToObject<KitchenObject>();
                            if (kitchenObj != null && kitchenObj.Pos != null)
                            {
                                SpawnKitchenObject(kitchenObj);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Object Processing Error: {e.Message}");
                    }
                }
            }

            // Spawn or update players if present
            if (state.Players != null)
            {
                foreach (var playerObj in state.Players)
                {
                    var jobject = playerObj.Value as Newtonsoft.Json.Linq.JObject;
                    if (jobject != null)
                    {
                        PlayerState player = jobject.ToObject<PlayerState>();
                        SpawnOrUpdatePlayer(player);
                    }
                }
            }
        }

        // Spawns a player at the given position with WASD movement
        // Spawns or updates a player at the given position
        private void SpawnOrUpdatePlayer(PlayerState player)
        {
            Debug.Log($"[SpawnOrUpdatePlayer] Called with player id: {player?.Id}, pos: {JsonConvert.SerializeObject(player?.Pos)}, facing: {JsonConvert.SerializeObject(player?.FacingDirection)}");
            if (player?.Pos == null || player.Pos.Count < 2)
            {
                Debug.LogWarning("Invalid player data - null or missing position");
                return;
            }
            
            // Convert game coordinates to Unity coordinates with proper spacing
            // Scale up the positions to match kitchen object spacing (multiply by 4)
            Vector3 newPosition = new Vector3(player.Pos[0] * 4.0f, 0.5f, player.Pos[1] * 4.0f); // Y=0.5 to place above ground
            Debug.Log($"[SpawnOrUpdatePlayer] Spawning at position: {newPosition}");
            Quaternion newRotation = Quaternion.identity;
            if (player.FacingDirection != null && player.FacingDirection.Count >= 2)
            {
                float angle = Mathf.Atan2(player.FacingDirection[1], player.FacingDirection[0]) * Mathf.Rad2Deg;
                newRotation = Quaternion.Euler(0, angle, 0);
            }
            GameObject playerObj;
            Debug.Log($"[SpawnOrUpdatePlayer] Called with player id: {player?.Id}, pos: {JsonConvert.SerializeObject(player?.Pos)}, facing: {JsonConvert.SerializeObject(player?.FacingDirection)}");
            if (playerObjects.TryGetValue(player.Id, out playerObj))
            {
                // Only update non-local players from server (to avoid lag/resets)
                if (string.IsNullOrEmpty(myPlayerNumber) || player.Id != myPlayerNumber)
                {
                    // Update remote players directly from server
                    playerObj.transform.position = newPosition;
                    Debug.Log($"[SpawnOrUpdatePlayer] Updated REMOTE player {player.Id} position to {newPosition}");
                }
                else
                {
                    // For local player, only update if position difference is significant (server correction)
                    float distance = Vector3.Distance(playerObj.transform.position, newPosition);
                    if (distance > 0.5f) // Reduced threshold for more responsive sync
                    {
                        // Smooth lerp to reduce jarring movement
                        playerObj.transform.position = Vector3.Lerp(playerObj.transform.position, newPosition, Time.deltaTime * 10f);
                        Debug.Log($"[SpawnOrUpdatePlayer] Smoothly correcting LOCAL player {player.Id} position to {newPosition} (distance: {distance})");
                    }
                    else
                    {
                        Debug.Log($"[SpawnOrUpdatePlayer] Ignoring LOCAL player {player.Id} position update (distance: {distance} < 0.5)");
                    }
                }
                
                // Use facing_direction for rotation if available
                if (player.FacingDirection != null && player.FacingDirection.Count >= 2)
                {
                    float angle = Mathf.Atan2(player.FacingDirection[1], player.FacingDirection[0]) * Mathf.Rad2Deg;
                    playerObj.transform.rotation = Quaternion.Euler(0, angle, 0);
                }
            }
            else
            {
                // Create new player GameObject
                string playerType = $"Player{player.Id}";
                GameObject prefab = Resources.Load<GameObject>($"Players/{playerType}");
                if (prefab == null)
                {
                    Debug.LogWarning($"[SpawnOrUpdatePlayer] Player prefab Players/{playerType} not found, using Capsule fallback");
                    // Create a primitive capsule with different colors for different players
                    GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    // Set different colors for different players
                    var renderer = capsule.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Material mat = new Material(Shader.Find("Standard"));
                        switch (player.Id)
                        {
                            case "0":
                                mat.color = Color.blue;
                                break;
                            case "1":
                                mat.color = Color.red;
                                break;
                            default:
                                mat.color = Color.green;
                                break;
                        }
                        renderer.material = mat;
                    }
                    prefab = capsule;
                }
                playerObj = Instantiate(prefab, newPosition, Quaternion.identity);
                Debug.Log($"[SpawnOrUpdatePlayer] Instantiated NEW player object: {playerObj.name} at {playerObj.transform.position}");
                playerObj.name = $"Player_{player.Id}";
                playerObj.transform.localScale = playerObj.transform.localScale * 0.8f; // Slightly smaller scale
                
                // Add CapsuleCollider if missing
                if (playerObj.GetComponent<Collider>() == null)
                    playerObj.AddComponent<CapsuleCollider>();
                
                // Add Rigidbody for collision detection with local player
                if (!string.IsNullOrEmpty(myPlayerNumber) && player.Id == myPlayerNumber)
                {
                    Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                    if (rb == null)
                    {
                        rb = playerObj.AddComponent<Rigidbody>();
                        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;
                        rb.mass = 1f;
                        rb.linearDamping = 5f; // Add drag to prevent sliding
                    }
                    // Don't make kinematic - we want physics collision
                    rb.isKinematic = false;
                }
                
                playerObjects[player.Id] = playerObj;
            }

            // Set the local player reference if this is the local player
            if (!string.IsNullOrEmpty(myPlayerHash) && player.Id == myPlayerNumber)
            {
                localPlayerObj = playerObj;
                localPlayerRb = playerObj.GetComponent<Rigidbody>();
            }

            // Update player state information
            var playerStateHolder = playerObj.GetComponent<PlayerStateHolder>();
            if (playerStateHolder == null)
                playerStateHolder = playerObj.AddComponent<PlayerStateHolder>();
            
            // Update nearest counter information from backend
            if (player.CurrentNearestCounterPos != null)
            {
                var nearestCounterPosList = player.CurrentNearestCounterPos as Newtonsoft.Json.Linq.JArray;
                if (nearestCounterPosList != null && nearestCounterPosList.Count >= 2)
                {
                    playerStateHolder.currentNearestCounterPos = new List<float> 
                    { 
                        (float)nearestCounterPosList[0], 
                        (float)nearestCounterPosList[1] 
                    };
                }
                else
                {
                    playerStateHolder.currentNearestCounterPos = null;
                }
            }
            else
            {
                playerStateHolder.currentNearestCounterPos = null;
            }

            if (player.CurrentNearestCounterId != null)
            {
                playerStateHolder.currentNearestCounterId = player.CurrentNearestCounterId.ToString();
            }
            else
            {
                playerStateHolder.currentNearestCounterId = null;
            }
        }
private void SpawnKitchenObject(KitchenObject obj)
{
    Debug.Log($"Attempting to spawn object: {JsonConvert.SerializeObject(obj)}");
    Vector3 spawnPosition = new Vector3(obj.Pos[0], 0, obj.Pos[1]);
    Debug.Log($"Spawn position will be: {spawnPosition}");
    if (obj == null || obj.Pos == null || obj.Pos.Count < 2)
    {
        Debug.LogWarning("Invalid kitchen object data - null or missing position");
        return;
    }

    try
    {
        GameObject gameObj = null;
        bool usedPrefab = false;
        // Try to load a custom model from Resources/Models by Type
        if (!string.IsNullOrEmpty(obj.Type))
        {
            GameObject prefab = Resources.Load<GameObject>("Models/" + obj.Type);
            if (prefab != null)
            {
                gameObj = Instantiate(prefab);
                usedPrefab = true;
                // Use original scale for FBX models
                // gameObj.transform.localScale = gameObj.transform.localScale * 0.5f;
            }
        }
        if (gameObj == null)
        {
            // Fallback to primitive if no model found
            string modelName = obj.Type?.ToLower();
            switch (modelName)
            {
                case "stove":
                    gameObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    gameObj.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f); // Slightly bigger
                    break;
                case "sink":
                    gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gameObj.transform.localScale = new Vector3(1.4f, 0.6f, 1.4f); // Slightly bigger
                    break;
                case "fridge":
                    gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gameObj.transform.localScale = new Vector3(1.2f, 1.8f, 1.2f); // Slightly bigger
                    break;
                default:
                    switch (obj.Category?.ToLower())
                    {
                        case "counter":
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            gameObj.transform.localScale = new Vector3(1.4f, 0.8f, 1.4f); // Slightly bigger
                            break;
                        case "equipment":
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            gameObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f); // Slightly bigger
                            break;
                        case "ingredient":
                        case "food":
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            gameObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f); // Slightly bigger
                            break;
                        default:
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            gameObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f); // Slightly bigger
                            break;
                    }
                    break;
            }
        }
        // Setup common properties
        gameObj.transform.SetParent(transform);
        gameObj.tag = "KitchenObject";
        gameObj.name = $"{obj.Category}_{obj.Type}_{obj.Id}";
        
        // Store counters in the dictionary for highlighting
        if (obj.Category?.ToLower() == "counter")
        {
            kitchenCounters[obj.Id] = gameObj;
        }
        
        // Position object at correct coordinates with proper spacing to match Python version
        float yPos = gameObj.transform.localScale.y / 2;
        // Scale up the positions significantly to match Python map size (multiply by 4 for proper spacing)
        gameObj.transform.position = new Vector3(obj.Pos[0] * 4.0f, yPos, obj.Pos[1] * 4.0f);
        if (obj.Orientation != null && obj.Orientation.Count >= 2)
        {
            float angle = Mathf.Atan2(obj.Orientation[0], obj.Orientation[1]) * Mathf.Rad2Deg;
            gameObj.transform.rotation = Quaternion.Euler(0, angle, 0);
        }

        // Add colliders for physics collision (unless it's a primitive which already has one)
        if (usedPrefab)
        {
            BoxCollider collider = gameObj.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gameObj.AddComponent<BoxCollider>();
            }
        }
        
        // Add Rigidbody to make it a static collision object
        Rigidbody rb = gameObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObj.AddComponent<Rigidbody>();
            rb.isKinematic = true; // Static object - doesn't move
        }
        // Only assign custom material if not using a prefab (i.e., for primitives)
        if (!usedPrefab)
        {
            Renderer renderer = gameObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material mat = new Material(Shader.Find("Standard"));
                switch (obj.Category?.ToLower())
                {
                    case "counter":
                        mat.color = new Color(0.7f, 0.7f, 0.7f);
                        mat.SetFloat("_Metallic", 0.5f);
                        mat.SetFloat("_Glossiness", 0.5f);
                        break;
                    case "equipment":
                        mat.color = new Color(0.3f, 0.3f, 1.0f);
                        mat.SetFloat("_Metallic", 0.8f);
                        mat.SetFloat("_Glossiness", 0.7f);
                        break;
                    case "ingredient":
                        mat.color = new Color(0.2f, 0.8f, 0.2f);
                        mat.SetFloat("_Metallic", 0.1f);
                        mat.SetFloat("_Glossiness", 0.3f);
                        break;
                    case "food":
                        mat.color = new Color(1.0f, 0.8f, 0.2f);
                        mat.SetFloat("_Metallic", 0.1f);
                        mat.SetFloat("_Glossiness", 0.4f);
                        break;
                }
                renderer.material = mat;
            }
        }
        // Add label
        GameObject label = new GameObject($"{obj.Type}_Label");
        label.transform.SetParent(gameObj.transform);
        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = obj.Type;
        textMesh.fontSize = 14;
        textMesh.characterSize = 0.1f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.black;
        label.transform.localPosition = Vector3.up * (gameObj.transform.localScale.y + 0.1f);
        label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        // Add collider for interaction
        gameObj.AddComponent<BoxCollider>();

        // Track counters by id for interaction
        if (kitchenCounters == null) kitchenCounters = new Dictionary<string, GameObject>();
        kitchenCounters[obj.Id] = gameObj;

        // Visualize occupied_by item
        if (obj.OccupiedBy != null && obj.OccupiedBy is Newtonsoft.Json.Linq.JObject itemObj)
        {
            string itemType = itemObj["type"]?.ToString();
            GameObject itemPrefab = Resources.Load<GameObject>($"Items/{itemType}");
            GameObject itemGo = null;
            if (itemPrefab != null)
            {
                itemGo = Instantiate(itemPrefab, gameObj.transform);
            }
            else
            {
                itemGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                itemGo.transform.SetParent(gameObj.transform);
                itemGo.transform.localScale = Vector3.one * 0.3f;
                itemGo.GetComponent<Renderer>().material.color = Color.white;
            }
            itemGo.name = "HeldItem";
            itemGo.transform.localPosition = Vector3.up * 1.1f;
            // Store type for interaction
            var state = gameObj.GetComponent<CounterStateHolder>();
            if (state == null) state = gameObj.AddComponent<CounterStateHolder>();
            state.occupiedByType = itemType;
        }
        else
        {
            var state = gameObj.GetComponent<CounterStateHolder>();
            if (state == null) state = gameObj.AddComponent<CounterStateHolder>();
            state.occupiedByType = null;
        }
    }
    catch (Exception e)
    {
        Debug.LogError($"Spawn Error for {obj.Type}: {e.Message}");
    }
}

        public void SendAction(string action)
        {
            // Only send actions for the Unity-controlled player
            if (!string.IsNullOrEmpty(myPlayerNumber) && isWebSocketConnected)
            {
                // Example: parse action string or receive structured input
                // Here, assume action is a JSON string with keys: action_type, action_data, duration
                var actionDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(action);
                var actionTypeStr = actionDict.ContainsKey("action_type") ? actionDict["action_type"].ToString() : "movement";
                object actionData = null;
                if (actionTypeStr == "movement" && actionDict.ContainsKey("action_data"))
                {
                    var arr = actionDict["action_data"] as Newtonsoft.Json.Linq.JArray;
                    if (arr != null)
                        actionData = arr.ToObject<float[]>();
                }
                else if (actionTypeStr == "interact" && actionDict.ContainsKey("action_data"))
                {
                    actionData = actionDict["action_data"].ToString();
                }
                float duration = 0f;
                if (actionDict.ContainsKey("duration"))
                    float.TryParse(actionDict["duration"].ToString(), out duration);

                var actionObj = new Action
                {
                    player = myPlayerNumber,
                    action_type = actionTypeStr,
                    action_data = actionData,
                    duration = duration
                };
                var message = new
                {
                    type = "action",
                    action = actionObj,
                    player_hash = myPlayerHash
                };
                string json = JsonConvert.SerializeObject(message);
                StartCoroutine(SendWebSocketMessage(json));
            }
        }

        // Helper methods for sending backend-compatible actions
        public void SendMovementAction(float[] moveVector, float duration)
        {
            if (!string.IsNullOrEmpty(myPlayerNumber) && !string.IsNullOrEmpty(myPlayerHash) && isWebSocketConnected)
            {
                var action = new Action
                {
                    player = myPlayerNumber,
                    action_type = "movement",  // Use string value like Python
                    action_data = moveVector,
                    duration = duration
                };
                var message = new
                {
                    type = "action",  // PlayerRequestType.ACTION.value
                    action = action,
                    player_hash = myPlayerHash
                };
                string json = JsonConvert.SerializeObject(message);
                Debug.Log($"[SendMovementAction] Sending movement action: {json}");
                StartCoroutine(SendWebSocketMessage(json));
            }
            else
            {
                Debug.LogWarning($"[SendMovementAction] Cannot send movement - myPlayerNumber: {myPlayerNumber}, myPlayerHash: {myPlayerHash}, isWebSocketConnected: {isWebSocketConnected}");
            }
        }

        public void SendPickUpDropAction()
        {
            if (!string.IsNullOrEmpty(myPlayerNumber) && !string.IsNullOrEmpty(myPlayerHash) && isWebSocketConnected)
            {
                var action = new Action
                {
                    player = myPlayerNumber,
                    action_type = "pick_up_drop",  // Use string value like Python
                    action_data = null,
                    duration = 0f
                };
                var message = new
                {
                    type = "action",  // PlayerRequestType.ACTION.value
                    action = action,
                    player_hash = myPlayerHash
                };
                string json = JsonConvert.SerializeObject(message);
                Debug.Log($"[SendPickUpDropAction] Sending pick/drop action: {json}");
                StartCoroutine(SendWebSocketMessage(json));
            }
            else
            {
                Debug.LogWarning($"[SendPickUpDropAction] Cannot send pick/drop - myPlayerNumber: {myPlayerNumber}, myPlayerHash: {myPlayerHash}, isWebSocketConnected: {isWebSocketConnected}");
            }
        }

        public void SendInteractAction(InterActionData interactionData)
        {
            if (!string.IsNullOrEmpty(myPlayerNumber) && !string.IsNullOrEmpty(myPlayerHash) && isWebSocketConnected)
            {
                var action = new Action
                {
                    player = myPlayerNumber,
                    action_type = "interact",  // Use string value like Python
                    action_data = interactionData.ToString(),  // Convert enum to string
                    duration = 0f
                };
                var message = new
                {
                    type = "action",  // PlayerRequestType.ACTION.value
                    action = action,
                    player_hash = myPlayerHash
                };
                string json = JsonConvert.SerializeObject(message);
                Debug.Log($"[SendInteractAction] Sending interact action: {json}");
                StartCoroutine(SendWebSocketMessage(json));
            }
            else
            {
                Debug.LogWarning($"[SendInteractAction] Cannot send interact - myPlayerNumber: {myPlayerNumber}, myPlayerHash: {myPlayerHash}, isWebSocketConnected: {isWebSocketConnected}");
            }
        }

        public void SendPlayerReady()
        {
            if (!string.IsNullOrEmpty(myPlayerHash) && isWebSocketConnected)
            {
                // Match exact Python GUI format: PlayerRequestType.READY.value
                var message = new
                {
                    type = "ready",  // PlayerRequestType.READY.value
                    player_hash = myPlayerHash
                };
                string json = JsonConvert.SerializeObject(message);
                StartCoroutine(SendWebSocketMessage(json));
                Debug.Log($"[SendPlayerReady] Sent ready message: {json}");
            }
            else
            {
                Debug.LogWarning($"[SendPlayerReady] Cannot send ready - myPlayerHash: {myPlayerHash}, isWebSocketConnected: {isWebSocketConnected}");
            }
        }

        public void SendGetStateMessage()
        {
            if (!string.IsNullOrEmpty(myPlayerHash) && isWebSocketConnected)
            {
                // Match exact simple example format for state request (from __init__.py)
                var message = new
                {
                    type = "get_state",
                    player_hash = myPlayerHash
                };
                string json = JsonConvert.SerializeObject(message);
                Debug.Log($"[SendGetStateMessage] About to send get_state message: {json}");
                StartCoroutine(SendWebSocketMessage(json));
                Debug.Log($"[SendGetStateMessage] Called StartCoroutine for get_state message");
            }
            else
            {
                Debug.LogWarning($"[SendGetStateMessage] Cannot send get_state - myPlayerHash: {myPlayerHash}, isWebSocketConnected: {isWebSocketConnected}");
            }
        }

        public void DisconnectWebSocket()
        {
            if (websocket != null)
            {
                isWebSocketConnected = false;
                cancellationTokenSource?.Cancel();

                try
                {
                    Task closeTask = websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
                    closeTask.ContinueWith(t =>
                            {
                            if (t.IsFaulted)
                            {
                            Debug.LogError($"Close Error: {t.Exception?.Message}");
                            }
                            websocket.Dispose();
                            websocket = null;
                            });
                }
                catch (Exception e)
                {
                    Debug.LogError($"Disconnect Error: {e.Message}");
                }
            }
        }

    private bool groundSpawned = false;

    private void SpawnGround(KitchenDimensions kitchen)
    {
        if (groundSpawned || kitchen == null) return;
        
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "KitchenGround";
        ground.transform.SetParent(transform);
        
        // Scale the ground to match the scaled kitchen dimensions (multiply by 4 for proper spacing)
        // Plane is 10x10 units by default, so scale accordingly
        float scaleX = (kitchen.Width * 4.0f) / 10f;
        float scaleZ = (kitchen.Height * 4.0f) / 10f;
        ground.transform.localScale = new Vector3(scaleX, 1f, scaleZ);
        
        // Position at center of scaled kitchen
        ground.transform.position = new Vector3((kitchen.Width * 4.0f) / 2f, 0f, (kitchen.Height * 4.0f) / 2f);
        
        // Set ground material to white color
        Renderer renderer = ground.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Use Unity's default material and set a white color
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.white; // Clean white color
            mat.SetFloat("_Metallic", 0.0f); // Non-metallic
            mat.SetFloat("_Smoothness", 0.3f); // Slightly smooth surface
            renderer.material = mat;
        }
        
        // Position camera above the kitchen to show the entire map from top-down view
        PositionCameraForKitchen(kitchen);
        
        groundSpawned = true;
    }

    private void PositionCameraForKitchen(KitchenDimensions kitchen)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null && kitchen != null)
        {
            // Calculate center of the scaled kitchen (remember we use 4x scaling)
            float centerX = (kitchen.Width * 4.0f) / 2f;
            float centerZ = (kitchen.Height * 4.0f) / 2f;
            
            // Position camera at 45-degree angle for better view
            float kitchenSize = Mathf.Max(kitchen.Width * 4.0f, kitchen.Height * 4.0f);
            float cameraHeight = kitchenSize * 0.6f + 8f; // Adjust height for 45-degree view
            float cameraDistance = kitchenSize * 0.4f; // Distance back from center for 45-degree angle
            
            // Set camera position at 45-degree angle to the kitchen
            mainCam.transform.position = new Vector3(centerX, cameraHeight, centerZ - cameraDistance);
            
            // Look at the kitchen center with 45-degree angle
            mainCam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            
            // Adjust camera to orthographic for better top-down view (optional)
            if (mainCam.orthographic == false)
            {
                mainCam.orthographic = true;
                mainCam.orthographicSize = kitchenSize * 0.6f; // Adjust size to fit kitchen nicely
            }
            
            Debug.Log($"[PositionCamera] Set camera to position: {mainCam.transform.position}, rotation: {mainCam.transform.rotation}, orthographic size: {mainCam.orthographicSize}");
        }
    }
    
    private void CreateUI()
    {
        // Create UI Canvas
        uiCanvas = new GameObject("GameUI");
        Canvas canvas = uiCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        uiCanvas.AddComponent<CanvasScaler>();
        uiCanvas.AddComponent<GraphicRaycaster>();
        
        // Score display (top left)
        GameObject scoreObj = new GameObject("ScoreText");
        scoreObj.transform.SetParent(uiCanvas.transform);
        scoreText = scoreObj.AddComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 24;
        scoreText.color = Color.white;
        scoreText.text = "Score: 0";
        
        RectTransform scoreRect = scoreObj.GetComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0, 1);
        scoreRect.anchorMax = new Vector2(0, 1);
        scoreRect.anchoredPosition = new Vector2(120, -30);
        scoreRect.sizeDelta = new Vector2(200, 50);
        
        // Countdown display (top center)
        GameObject countdownObj = new GameObject("CountdownText");
        countdownObj.transform.SetParent(uiCanvas.transform);
        countdownText = countdownObj.AddComponent<Text>();
        countdownText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countdownText.fontSize = 28;
        countdownText.color = Color.yellow;
        countdownText.text = "Time: --:--";
        countdownText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform countdownRect = countdownObj.GetComponent<RectTransform>();
        countdownRect.anchorMin = new Vector2(0.5f, 1);
        countdownRect.anchorMax = new Vector2(0.5f, 1);
        countdownRect.anchoredPosition = new Vector2(0, -30);
        countdownRect.sizeDelta = new Vector2(200, 50);
        
        // Recipe counter display (top right)
        GameObject recipeObj = new GameObject("RecipeCounterText");
        recipeObj.transform.SetParent(uiCanvas.transform);
        recipeCounterText = recipeObj.AddComponent<Text>();
        recipeCounterText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        recipeCounterText.fontSize = 24;
        recipeCounterText.color = Color.green;
        recipeCounterText.text = "Completed: 0";
        recipeCounterText.alignment = TextAnchor.MiddleRight;
        
        RectTransform recipeRect = recipeObj.GetComponent<RectTransform>();
        recipeRect.anchorMin = new Vector2(1, 1);
        recipeRect.anchorMax = new Vector2(1, 1);
        recipeRect.anchoredPosition = new Vector2(-120, -30);
        recipeRect.sizeDelta = new Vector2(200, 50);
        
        // Orders display (right side)
        GameObject ordersObj = new GameObject("OrdersText");
        ordersObj.transform.SetParent(uiCanvas.transform);
        ordersText = ordersObj.AddComponent<Text>();
        ordersText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ordersText.fontSize = 18;
        ordersText.color = Color.cyan;
        ordersText.text = "Orders:\n";
        ordersText.alignment = TextAnchor.UpperLeft;
        
        RectTransform ordersRect = ordersObj.GetComponent<RectTransform>();
        ordersRect.anchorMin = new Vector2(1, 0.5f);
        ordersRect.anchorMax = new Vector2(1, 0.5f);
        ordersRect.anchoredPosition = new Vector2(-10, 0);
        ordersRect.sizeDelta = new Vector2(300, 400);
        
        // Game End Panel
        gameEndPanel = new GameObject("GameEndPanel");
        gameEndPanel.transform.SetParent(uiCanvas.transform);
        gameEndPanel.SetActive(false);
        
        // Background for game end panel
        Image panelBg = gameEndPanel.AddComponent<Image>();
        panelBg.color = new Color(0, 0, 0, 0.8f);
        
        RectTransform panelRect = gameEndPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        
        // Game End Text
        GameObject gameEndTextObj = new GameObject("GameEndText");
        gameEndTextObj.transform.SetParent(gameEndPanel.transform);
        gameEndText = gameEndTextObj.AddComponent<Text>();
        gameEndText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gameEndText.fontSize = 36;
        gameEndText.color = Color.white;
        gameEndText.text = "Game Ended!";
        gameEndText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform gameEndTextRect = gameEndTextObj.GetComponent<RectTransform>();
        gameEndTextRect.anchorMin = new Vector2(0.5f, 0.6f);
        gameEndTextRect.anchorMax = new Vector2(0.5f, 0.6f);
        gameEndTextRect.anchoredPosition = Vector2.zero;
        gameEndTextRect.sizeDelta = new Vector2(600, 100);
        
        // Next Level Button
        GameObject buttonObj = new GameObject("NextLevelButton");
        buttonObj.transform.SetParent(gameEndPanel.transform);
        nextLevelButton = buttonObj.AddComponent<Button>();
        
        Image buttonImg = buttonObj.AddComponent<Image>();
        buttonImg.color = Color.blue;
        
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.4f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.4f);
        buttonRect.anchoredPosition = Vector2.zero;
        buttonRect.sizeDelta = new Vector2(200, 60);
        
        // Button Text
        GameObject buttonTextObj = new GameObject("ButtonText");
        buttonTextObj.transform.SetParent(buttonObj.transform);
        Text buttonText = buttonTextObj.AddComponent<Text>();
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.fontSize = 20;
        buttonText.color = Color.white;
        buttonText.text = "Next Level";
        buttonText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform buttonTextRect = buttonTextObj.GetComponent<RectTransform>();
        buttonTextRect.anchorMin = Vector2.zero;
        buttonTextRect.anchorMax = Vector2.one;
        buttonTextRect.offsetMin = Vector2.zero;
        buttonTextRect.offsetMax = Vector2.zero;
        
        // Add button click handler
        nextLevelButton.onClick.AddListener(StartNextLevel);
    }
    
    private void UpdateUI()
    {
        if (scoreText != null)
            scoreText.text = $"Score: {currentScore:F0}";
            
        if (countdownText != null)
        {
            if (remainingTime > 0)
            {
                int minutes = Mathf.FloorToInt(remainingTime / 60f);
                int seconds = Mathf.FloorToInt(remainingTime % 60f);
                countdownText.text = $"Time: {minutes:00}:{seconds:00}";
            }
            else
            {
                countdownText.text = "Time: 00:00";
            }
        }
        
        if (recipeCounterText != null)
            recipeCounterText.text = $"Completed: {completedRecipes}";
        
        if (ordersText != null)
        {
            string ordersList = "Orders:\n";
            foreach (var order in currentOrders)
            {
                ordersList += $"• {order.Meal} (Score: {order.Score})\n";
            }
            ordersText.text = ordersList;
        }
    }

    private void ShowGameEndScreen()
    {
        gameEndPanel.SetActive(true);
        gameEndText.text = $"Level Complete!\nFinal Score: {currentScore:F0}\nRecipes Completed: {completedRecipes}";
    }

    private void StartNextLevel()
    {
        gameEndPanel.SetActive(false);
        gameEnded = false;
        
        // Reset game state
        currentScore = 0f;
        remainingTime = 0f;
        completedRecipes = 0;
        currentOrders.Clear();
        
        // Clean up current objects
        CleanUpKitchenObjects();
        foreach (var player in playerObjects.Values)
        {
            if (player != null)
                Destroy(player);
        }
        playerObjects.Clear();
        
        // Start new study session
        StartStudy();
    }
}
