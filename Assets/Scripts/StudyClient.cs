using Newtonsoft.Json;
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
    public int studyPort = 8080;
    public string participantId;
    public int numPlayers = 1;
    public string myPlayerId;

    public Action<StateRepresentation> OnStateReceived;
    private GameConnectionData gameConnectionData;
    private WebSocket websocket;
    
    private Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>();
    private StateRepresentation lastState;

    void Start()
    {
        // Ensure there is a camera in the scene
        if (Camera.main == null)
        {
            Debug.LogWarning("No main camera found. Creating a default camera.");
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.AddComponent<Camera>();
            cameraObj.tag = "MainCamera";
            cameraObj.transform.position = new Vector3(5, 15, 5);
            cameraObj.transform.rotation = Quaternion.Euler(60, 0, 0);
        }

        OnStateReceived += RenderGameState;
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

    void OnGUI()
    {
        if (lastState != null && lastState.orders != null)
        {
            GUI.Box(new Rect(10, 10, 150, 30), $"Score: {lastState.score}");
            GUI.Box(new Rect(10, 50, 200, 150), "Orders");
            for (int i = 0; i < lastState.orders.Count; i++)
            {
                var order = lastState.orders[i];
                float timeRemaining = order.max_duration - (float.Parse(lastState.env_time) - float.Parse(order.start_time));
                GUI.Label(new Rect(20, 80 + (i * 20), 180, 20), $"{order.meal} ({timeRemaining:F1}s)");
            }
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
            // The server sends the raw state, not a wrapped message object.
            try
            {
                var state = JsonConvert.DeserializeObject<StateRepresentation>(message);
                if (state != null)
                {
                    OnStateReceived?.Invoke(state);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to deserialize state message: {e.Message}\nMessage content: {message}");
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
        WebsocketMessage message = new WebsocketMessage { type = "ready", player_hash = playerHash };
        SendWebSocketMessage(JsonConvert.SerializeObject(message));
    }

    public void SendAction(Action action)
    {
        string playerHash = "";
        foreach (var player in gameConnectionData.player_info.Values)
        {
            playerHash = player.player_hash;
            break;
        }
        WebsocketMessage message = new WebsocketMessage { type = "action", player_hash = playerHash, action = action };
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
    
    private void RenderGameState(StateRepresentation state)
    {
        if (state == null)
        {
            Debug.LogWarning("RenderGameState called with a null state.");
            return;
        }

        lastState = state;
        foreach (var obj in spawnedObjects.Values)
        {
            Destroy(obj);
        }
        spawnedObjects.Clear();

        // Render Floor
        GameObject floorPrefab = Resources.Load<GameObject>("Prefabs/Floor");
        GameObject floor = floorPrefab ? Instantiate(floorPrefab) : CreateTextPlaceholder("Floor");
        if (state.kitchen != null)
        {
            floor.transform.position = new Vector3(state.kitchen.width / 2f - 0.5f, 0, state.kitchen.height / 2f - 0.5f);
            if (!floorPrefab)
            {
                 floor.transform.localScale = new Vector3(state.kitchen.width, 1, state.kitchen.height);
            }
        }
        spawnedObjects.Add("floor", floor);

        // Render Counters
        if (state.counters != null)
        {
            foreach (var counter in state.counters)
            {
                GameObject prefab = Resources.Load<GameObject>($"Prefabs/{counter.type}");
                GameObject counterObj = prefab ? Instantiate(prefab) : CreateTextPlaceholder(counter.type);
                
                counterObj.transform.position = new Vector3(counter.pos[0], 0, counter.pos[1]);
                if (counter.orientation != null && counter.orientation.Count > 1)
                {
                    counterObj.transform.rotation = Quaternion.Euler(0, counter.orientation[1] * 90, 0);
                }
                counterObj.name = $"Counter_{counter.type}_{counter.id}";
                spawnedObjects.Add(counter.id, counterObj);

                if (counter.occupied_by != null)
                {
                    foreach (var item in counter.occupied_by)
                    {
                        RenderItem(item, counterObj.transform.position + Vector3.up * 1f);
                    }
                }
            }
        }

        // Render Players
        if (state.players != null)
        {
            foreach (var player in state.players)
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
                GameObject playerObj = prefab ? Instantiate(prefab) : CreateTextPlaceholder("Player");

                playerObj.transform.position = new Vector3(player.pos[0], 0, player.pos[1]);
                playerObj.name = $"Player_{player.id}";
                spawnedObjects.Add(player.id, playerObj);

                if (player.holding != null)
                {
                    RenderItem(player.holding, playerObj.transform.position + Vector3.up * 1.5f);
                }
            }
        }
    }

    private void RenderItem(ItemState item, Vector3 position)
    {
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/{item.type}");
        GameObject itemObj = prefab ? Instantiate(prefab) : CreateTextPlaceholder(item.type);

        itemObj.transform.position = position;
        itemObj.name = $"Item_{item.type}_{item.id}";
        spawnedObjects.Add(item.id, itemObj);
    }

    private GameObject CreateTextPlaceholder(string objectName)
    {
        GameObject textObj = new GameObject(objectName + " (Missing Prefab)");
        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = objectName;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.fontSize = 10;
        textMesh.color = Color.red;
        textObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
        return textObj;
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

