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
        public string OccupiedBy { get; set; }
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

        while (true)
        {
            try
            {
                Task connectTask = websocket.ConnectAsync(uri, cancellationTokenSource.Token);
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
                }
                break;
            }
            catch (Exception e)
            {
                Debug.LogError($"WebSocket Error: {e.Message}");
                isWebSocketConnected = false;
                break;
            }
        }
    }

    IEnumerator SendWebSocketMessage(string message)
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

    IEnumerator ReceiveWebSocketMessages()
    {
        byte[] buffer = new byte[4096];
        while (isWebSocketConnected)
        {
            Task<WebSocketReceiveResult> receiveTask = null;
            try
            {
                receiveTask = websocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationTokenSource.Token);
                yield return new WaitUntil(() => receiveTask.IsCompleted);

                if (receiveTask.Result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("WebSocket closed by server");
                    break;
                }

                string message = Encoding.UTF8.GetString(buffer, 0, receiveTask.Result.Count);
                Debug.Log($"Received: {message}");
                ProcessWebSocketMessage(message);
            }
            catch (Exception e)
            {
                Debug.LogError($"Receive Error: {e.Message}");
                break;
            }
        }
        isWebSocketConnected = false;
    }

    private void ProcessWebSocketMessage(string message)
    {
        if (message.Contains("\"phase\""))
        {
            try
            {
                GameState gameState = JsonConvert.DeserializeObject<GameState>(message);
                ProcessGameState(gameState);
            }
            catch (Exception e)
            {
                Debug.LogError($"Deserialization Error: {e.Message}");
            }
        }
    }

    private void ProcessGameState(GameState state)
    {
        Debug.Log($"Game State - Level: {state.Level}, Phase: {state.Phase}");

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
    }

    private void SpawnKitchenObject(KitchenObject obj)
    {
        if (obj == null || obj.Pos == null || obj.Pos.Count < 2) return;

        try
        {
            GameObject gameObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObj.transform.SetParent(transform);
            gameObj.tag = "KitchenObject";
            gameObj.name = $"KitchenObject_{obj.Type}";

            float scale = obj.Category?.ToLower() == "counter" ? 1.5f : 1.0f;
            gameObj.transform.localScale = new Vector3(scale, scale, scale);
            gameObj.transform.position = new Vector3(obj.Pos[0], scale/2, obj.Pos[1]);

            if (obj.Orientation != null && obj.Orientation.Count >= 2)
            {
                float angle = Mathf.Atan2(obj.Orientation[1], obj.Orientation[0]) * Mathf.Rad2Deg;
                gameObj.transform.rotation = Quaternion.Euler(0, angle, 0);
            }

            // Set color based on category
            Renderer renderer = gameObj.GetComponent<Renderer>();
            Color color = Color.white;
            switch (obj.Category?.ToLower())
            {
                case "counter": color = new Color(0.7f, 0.7f, 0.7f); break;
                case "equipment": color = new Color(0.3f, 0.3f, 1.0f); break;
                case "ingredient": color = new Color(0.2f, 0.8f, 0.2f); break;
                case "food": color = new Color(1.0f, 0.8f, 0.2f); break;
            }
            renderer.material.color = color;

            // Add label
            GameObject label = new GameObject($"{obj.Type}_Label");
            label.transform.SetParent(gameObj.transform);
            TextMesh textMesh = label.AddComponent<TextMesh>();
            textMesh.text = obj.Type;
            textMesh.characterSize = 0.1f;
            textMesh.color = Color.black;
            label.transform.localPosition = Vector3.up * (scale + 0.1f);
            label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        catch (Exception e)
        {
            Debug.LogError($"Spawn Error: {e.Message}");
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
}
