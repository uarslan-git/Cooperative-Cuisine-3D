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
    // Track what the local player is holding
    private GameObject heldItemObj;
    private string heldItemType;
    // Reference to the local player GameObject
    private GameObject localPlayerObj;
    // Movement speed for the local player
    private float localPlayerMoveSpeed = 12f;
    // Reference to the local player's Rigidbody
    private Rigidbody localPlayerRb;

    void Update()
    {
        // Visualize nearest_counter_pos
        if (localPlayerObj != null && playerObjects.TryGetValue(myPlayerNumber, out var playerObj))
        {
            var playerState = playerObj.GetComponent<PlayerStateHolder>();
            if (playerState != null && playerState.currentNearestCounterPos != null)
            {
                Vector3 markerPos = new Vector3(playerState.currentNearestCounterPos[0], 1.2f, playerState.currentNearestCounterPos[1]);
                if (nearestCounterMarker == null)
                {
                    nearestCounterMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    nearestCounterMarker.transform.localScale = Vector3.one * 0.4f;
                    nearestCounterMarker.GetComponent<Renderer>().material.color = Color.yellow;
                    Destroy(nearestCounterMarker.GetComponent<Collider>());
                }
                nearestCounterMarker.transform.position = markerPos;
                nearestCounterMarker.SetActive(true);
            }
            else if (nearestCounterMarker != null)
            {
                nearestCounterMarker.SetActive(false);
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
                        // Send pick action to backend (pseudo)
                        SendAction($"{{\"action\":\"pick\",\"counter_id\":\"{playerState.currentNearestCounterId}\"}}");
                    }
                    else if (heldItemObj != null && counterState != null && counterState.occupiedByType == null)
                    {
                        // Place item
                        heldItemObj.transform.SetParent(counterObj.transform);
                        heldItemObj.transform.localPosition = Vector3.up * 1.1f;
                        // Send place action to backend (pseudo)
                        SendAction($"{{\"action\":\"place\",\"counter_id\":\"{playerState.currentNearestCounterId}\",\"item_type\":\"{heldItemType}\"}}");
                        heldItemObj = null;
                        heldItemType = null;
                    }
                }
            }
        }

        // Handle WASD movement for the local player only
        if (localPlayerObj != null && localPlayerRb != null)
        {
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            Vector3 move = new Vector3(h, 0, v);
            if (move.magnitude > 0.01f)
            {
                // Update facing direction
                localPlayerObj.transform.rotation = Quaternion.LookRotation(move, Vector3.up);
                // Move using physics
                Vector3 targetPos = localPlayerObj.transform.position + move.normalized * localPlayerMoveSpeed * Time.deltaTime;
                localPlayerRb.MovePosition(targetPos);
            }
        }
    }
    // Track player GameObjects by player ID
    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();
    // Store the current level info for kitchen size and other properties
    private LevelInfo levelInfo;
    // Singleton instance
    public static StudyClient Instance { get; private set; }

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
        public List<int> FacingDirection { get; set; }

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
                SendGetStateMessage();
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
        
        // First try parsing as GameState since it's more complete
        try
        {
            GameState gameState = JsonConvert.DeserializeObject<GameState>(message);
            if (gameState?.Objects != null)
            {
                Debug.Log($"Successfully parsed as GameState with {gameState.Objects.Count} objects");
                ProcessGameState(gameState);
                return;
            }
        }
        catch (JsonException) { /* Continue to try other formats */ }

        // Try parsing as KitchenState
        try
        {
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
        }
        catch (JsonException) { /* Continue to try other formats */ }

        // Try parsing as FullKitchenState (new format)
        try
        {
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
                    foreach (var player in fullState.Players)
                    {
                        Debug.Log($"[ProcessWebSocketMessage] Spawning player: {JsonConvert.SerializeObject(player)}");
                        SpawnOrUpdatePlayer(player);
                    }
                }
                return;
            }
        }
        catch (JsonException) { /* Continue to try other formats */ }

        Debug.LogWarning($"Could not parse message as either GameState, KitchenState, or FullKitchenState: {message}");
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
            Vector3 newPosition = new Vector3(player.Pos[0], 0, player.Pos[1]);
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
                // Always update position and facing from backend JSON
                playerObj.transform.position = newPosition;
                // Use facing_direction for rotation if available
                if (player.FacingDirection != null && player.FacingDirection.Count >= 2)
                {
                    float angle = Mathf.Atan2(player.FacingDirection[0], player.FacingDirection[1]) * Mathf.Rad2Deg;
                    playerObj.transform.rotation = Quaternion.Euler(0, angle, 0);
                }
            }
            else
            {
                // Try to load Player{id} prefab
                string playerType = $"Player{player.Id}";
                GameObject prefab = Resources.Load<GameObject>($"Players/{playerType}");
                if (prefab == null)
                {
                    Debug.LogWarning($"[SpawnOrUpdatePlayer] Player prefab Players/{playerType} not found, using Capsule fallback");
                    prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                }
                playerObj = Instantiate(prefab, newPosition, Quaternion.identity);
                Debug.Log($"[SpawnOrUpdatePlayer] Instantiated player object: {playerObj.name} at {playerObj.transform.position}");
                playerObj.name = $"Player_{player.Id}";
                playerObj.transform.localScale = playerObj.transform.localScale * 0.5f;
                // Add CapsuleCollider if missing
                if (playerObj.GetComponent<Collider>() == null)
                    playerObj.AddComponent<CapsuleCollider>();
                // Add Rigidbody if missing
                Rigidbody rb = playerObj.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = playerObj.AddComponent<Rigidbody>();
                    rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                }
                rb.isKinematic = false;
                playerObjects[player.Id] = playerObj;
            }
            // Always update position and facing from backend JSON (even after instantiation)
            playerObj.transform.position = newPosition;
            // Use facing_direction for rotation if available
            if (player.FacingDirection != null && player.FacingDirection.Count >= 2)
            {
                float angle = Mathf.Atan2(player.FacingDirection[0], player.FacingDirection[1]) * Mathf.Rad2Deg;
                playerObj.transform.rotation = Quaternion.Euler(0, angle, 0);
            }

            // Set the local player reference if this is the local player
            if (!string.IsNullOrEmpty(myPlayerHash) && player.Id == myPlayerNumber)
            {
                localPlayerObj = playerObj;
                localPlayerRb = playerObj.GetComponent<Rigidbody>();
            }
            // Move the camera in front of the player, looking at the player (third-person style)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Offset: further behind and above the player, looking at the player
                Vector3 camOffset = new Vector3(-2, 9, -12); // 35 units up, 35 units behind
                Vector3 camPos = playerObj.transform.position + playerObj.transform.rotation * camOffset;
                mainCam.transform.position = camPos;
                mainCam.transform.LookAt(playerObj.transform.position + Vector3.up * 2f); // Look at player's head
                // Optionally, widen the camera's field of view
                mainCam.fieldOfView = 50f;
                mainCam.transform.rotation = Quaternion.Euler(45, 0, 0); // Tilt camera down slightly
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
                // Scale down FBX models loaded from Resources/Models
                gameObj.transform.localScale = gameObj.transform.localScale * 0.5f;
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
                    gameObj.transform.localScale = new Vector3(1.2f, 0.7f, 1.2f) * 0.5f;
                    break;
                case "sink":
                    gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gameObj.transform.localScale = new Vector3(1.5f, 0.5f, 1.5f) * 0.5f;
                    break;
                case "fridge":
                    gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    gameObj.transform.localScale = new Vector3(1f, 2f, 1f) * 0.5f;
                    break;
                default:
                    switch (obj.Category?.ToLower())
                    {
                        case "counter":
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            gameObj.transform.localScale = new Vector3(1.5f, 0.75f, 1.5f) * 0.5f;
                            break;
                        case "equipment":
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            gameObj.transform.localScale = new Vector3(1f, 1f, 1f) * 0.5f;
                            break;
                        case "ingredient":
                        case "food":
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            gameObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f) * 0.5f;
                            break;
                        default:
                            gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            gameObj.transform.localScale = new Vector3(1f, 1f, 1f) * 0.5f;
                            break;
                    }
                    break;
            }
        }
        // Setup common properties
        gameObj.transform.SetParent(transform);
        gameObj.tag = "KitchenObject";
        gameObj.name = $"{obj.Category}_{obj.Type}_{obj.Id}";
        // Position object
        float yPos = gameObj.transform.localScale.y / 2;
        gameObj.transform.position = new Vector3(obj.Pos[0], yPos, obj.Pos[1]);
        // Always update position and facing from backend JSON
        if (obj.Pos != null && obj.Pos.Count >= 2)
        {
            float y = gameObj.transform.localScale.y / 2;
            gameObj.transform.position = new Vector3(obj.Pos[0], y, obj.Pos[1]);
        }
        if (obj.Orientation != null && obj.Orientation.Count >= 2)
        {
            float angle = Mathf.Atan2(obj.Orientation[0], obj.Orientation[1]) * Mathf.Rad2Deg;
            gameObj.transform.rotation = Quaternion.Euler(0, angle, 0);
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
            if (isWebSocketConnected)
            {
                StartCoroutine(SendWebSocketMessage(action));
            }
        }

        public void SendGetStateMessage()
        {
            if (!string.IsNullOrEmpty(myPlayerHash))
            {
                GetStateMessage message = new GetStateMessage { PlayerHash = myPlayerHash };
                StartCoroutine(SendWebSocketMessage(JsonConvert.SerializeObject(message)));
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
        if (groundSpawned) return;
        groundSpawned = true;

        for (int x = 0; x < kitchen.Width; x++)
        {
            for (int y = 0; y < kitchen.Height; y++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.position = new Vector3(x, -0.05f, y); // Centered so top is at y=0
                cube.transform.localScale = new Vector3(1, 0.1f, 1); // Thin ground
                cube.name = $"GroundCube_{x}_{y}";

                // Set color to white
                var renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.white;
                }

                // Optional: parent to StudyClient for easy cleanup
                cube.transform.SetParent(this.transform);
            }
        }
    }
}
