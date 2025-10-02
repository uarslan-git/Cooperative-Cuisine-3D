using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Newtonsoft.Json;

public class GameManager : MonoBehaviour
{
    public StudyClient studyClient;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public Transform ordersContainer;
    public GameObject orderTextPrefab;

    public Dictionary<string, GameObject> itemObjects = new Dictionary<string, GameObject>();
    public Dictionary<string, GameObject> counters = new Dictionary<string, GameObject>();
    public StateRepresentation lastState;

    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private Dictionary<string, Slider> progressBars = new Dictionary<string, Slider>();
    private GameObject progressBarPrefab;

    private bool floorInstantiated = false;
    
    // Track last known object states to avoid unnecessary updates
    private string lastCountersHash = "";
    private string lastItemsHash = "";
    private float lastScore = -1;
    private float lastTime = -1;

    void Start()
    {
        studyClient.OnStateReceived += HandleStateReceived;
        progressBarPrefab = Resources.Load<GameObject>("Prefabs/ProgressBar");
        if (progressBarPrefab == null) Debug.LogError("ProgressBar prefab not found in Resources/Prefabs!");
        orderTextPrefab.SetActive(false);
    }

    public void HandleStateReceived(StateRepresentation newState)
    {
        lastState = newState;
        
        // Always update players (they move frequently)
        UpdatePlayers();
        
        // Only update UI if score or time changed
        UpdateUI();
        
        // Only update objects if their state actually changed
        UpdateObjectsIfChanged();
        
        // Always update orders (they can appear/disappear)
        UpdateOrders();
    }

    // Old UpdateWorld method - replaced with selective update methods above
    // void UpdateWorld() - REMOVED to prevent performance issues

    private void UpdateItem(ItemState itemState, Transform parent)
    {
        GameObject itemObj;
        if (itemObjects.ContainsKey(itemState.id)) {
            itemObj = itemObjects[itemState.id];
            itemObj.SetActive(true);
        } else {
            GameObject itemPrefab = Resources.Load<GameObject>($"Prefabs/{itemState.type}");
            if (itemPrefab == null) {
                itemPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                itemPrefab.name = $"Item_{itemState.type}_Default";
                itemPrefab.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            itemObj = Instantiate(itemPrefab);
            itemObj.name = $"Item_{itemState.type}_{itemState.id}";
            itemObjects[itemState.id] = itemObj;
        }

        if (itemObj.transform.parent != parent)
        {
            itemObj.transform.SetParent(parent, false);
            itemObj.transform.localRotation = Quaternion.identity;
            itemObj.transform.localPosition = (parent.name == "HoldingSpot") ? Vector3.zero : new Vector3(0, 1f, 0);
        }
    }

    private GameObject UpdatePlayer(PlayerState playerState) {
        GameObject playerObj;
        float invertedPlayerZ = (lastState.kitchen.height - 1) - playerState.pos[1];
        Vector3 position = new Vector3(playerState.pos[0], 0, invertedPlayerZ);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(playerState.facing_direction[0], 0, -playerState.facing_direction[1]));

        if (!players.ContainsKey(playerState.id)) {
            GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
            playerObj = Instantiate(playerPrefab, position, rotation);
            players.Add(playerState.id, playerObj);
            if (playerState.id == studyClient.myPlayerId) {
                PlayerInputController pic = GetComponent<PlayerInputController>();
                if (pic != null) pic.controlledPlayerGameObject = playerObj;
            }
        } else {
            playerObj = players[playerState.id];
            playerObj.transform.position = position;
            playerObj.transform.rotation = rotation;
        }
        return playerObj;
    }

    private GameObject UpdateCounter(CounterState counterState) {
        GameObject counterObj;
        float invertedCounterZ = (lastState.kitchen.height - 1) - counterState.pos[1];
        Vector3 position = new Vector3(counterState.pos[0], 0, invertedCounterZ);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(counterState.orientation[0], 0, -counterState.orientation[1]));

        if (!counters.ContainsKey(counterState.id)) {
            GameObject counterPrefab = Resources.Load<GameObject>($"Prefabs/{counterState.type}");
            if (counterPrefab == null) {
                counterPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                counterPrefab.name = counterState.type;
            }
            counterObj = Instantiate(counterPrefab, position, rotation);
            counters.Add(counterState.id, counterObj);
        } else {
            counterObj = counters[counterState.id];
        }
        return counterObj;
    }

    private void UpdateProgressBar(string id, Transform parent, float progress) {
        Slider progressBar;
        if (!progressBars.ContainsKey(id)) {
            GameObject canvasObj = new GameObject($"ProgressBarCanvas_{id}");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            canvasObj.AddComponent<LookAtCamera>();

            GameObject progressBarObj = Instantiate(progressBarPrefab, canvasObj.transform);
            progressBar = progressBarObj.GetComponentInChildren<Slider>();
            progressBars.Add(id, progressBar);

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1, 0.3f);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        } else {
            progressBar = progressBars[id];
        }
        progressBar.transform.root.position = parent.position + Vector3.up * 1.5f;
        progressBar.value = progress / 100f;
        progressBar.transform.root.gameObject.SetActive(true);
    }

    private void UpdateOrders() {
        foreach (Transform child in ordersContainer) {
            if(child.gameObject.activeSelf) Destroy(child.gameObject);
        }
        if (lastState.orders != null) {
            foreach (var order in lastState.orders) {
                GameObject orderObj = Instantiate(orderTextPrefab, ordersContainer);
                orderObj.GetComponent<TextMeshProUGUI>().text = order.meal;
                orderObj.SetActive(true);
            }
        }
    }

    private void InstantiateFloor() {
        GameObject floorPrefab = Resources.Load<GameObject>("Prefabs/Floor");
        if (floorPrefab != null) {
            float centerX = (lastState.kitchen.width - 1) / 2.0f;
            float centerZ = (lastState.kitchen.height - 1) / 2.0f;
            Instantiate(floorPrefab, new Vector3(centerX, 0, centerZ), Quaternion.identity);
            floorInstantiated = true;
        } else {
            Debug.LogError("Floor prefab not found in Resources/Prefabs!");
        }
    }

    private void ClearGameObjects()
    {
        foreach (var itemObj in itemObjects.Values)
        {
            Destroy(itemObj);
        }
        itemObjects.Clear();

        foreach (var playerObj in players.Values)
        {
            Destroy(playerObj);
        }
        players.Clear();

        foreach (var counterObj in counters.Values)
        {
            Destroy(counterObj);
        }
        counters.Clear();

        foreach (var progressBar in progressBars.Values)
        {
            Destroy(progressBar.transform.root.gameObject);
        }
        progressBars.Clear();

        // Reset floor instantiated flag
        floorInstantiated = false;
    }

    private void UpdatePlayers()
    {
        if (lastState?.players == null) return;
        
        foreach (var playerState in lastState.players)
        {
            UpdatePlayer(playerState);
        }
    }
    
    private void UpdateUI()
    {
        if (lastState == null) return;
        
        // Only update if values changed
        if (lastScore != lastState.score)
        {
            lastScore = lastState.score;
            scoreText.text = $"Score: {lastState.score}";
        }
        
        if (lastTime != lastState.remaining_time)
        {
            lastTime = lastState.remaining_time;
            timeText.text = $"Time: {Mathf.FloorToInt(lastState.remaining_time)}";
        }
    }
    
    private void UpdateObjectsIfChanged()
    {
        if (lastState == null) return;
        
        if (!floorInstantiated && lastState.kitchen != null) InstantiateFloor();
        
        // Create hash of current counter states to detect changes
        string currentCountersHash = GetCountersHash();
        
        if (currentCountersHash != lastCountersHash)
        {
            lastCountersHash = currentCountersHash;
            UpdateCountersAndItems();
        }
    }
    
    private string GetCountersHash()
    {
        if (lastState?.counters == null) return "";
        
        var hashData = new System.Text.StringBuilder();
        foreach (var counter in lastState.counters)
        {
            hashData.Append($"{counter.id}:{counter.occupied_by?.Count ?? 0}");
            if (counter.occupied_by != null)
            {
                foreach (var item in counter.occupied_by)
                {
                    hashData.Append($":{item.id}:{item.type}:{item.progress_percentage}");
                }
            }
        }
        return hashData.ToString();
    }
    
    private void UpdateCountersAndItems()
    {
        HashSet<string> activeItemIDs = new HashSet<string>();
        HashSet<string> activeProgressIDs = new HashSet<string>();

        // Update items held by players
        if (lastState.players != null)
        {
            foreach (var playerState in lastState.players)
            {
                if (playerState.holding != null)
                {
                    GameObject playerObj = players.ContainsKey(playerState.id) ? players[playerState.id] : null;
                    if (playerObj != null)
                    {
                        activeItemIDs.Add(playerState.holding.id);
                        UpdateItem(playerState.holding, playerObj.transform.Find("HoldingSpot"));
                        if (playerState.holding.progress_percentage > 0)
                        {
                            activeProgressIDs.Add(playerState.holding.id);
                            UpdateProgressBar(playerState.holding.id, itemObjects[playerState.holding.id].transform, playerState.holding.progress_percentage);
                        }
                    }
                }
            }
        }

        // Update counters and their items
        if (lastState.counters != null)
        {
            foreach (var counterState in lastState.counters)
            {
                GameObject counterObj = UpdateCounter(counterState);
                if (counterState.occupied_by != null)
                {
                    foreach (ItemState itemState in counterState.occupied_by)
                    {
                        activeItemIDs.Add(itemState.id);
                        UpdateItem(itemState, counterObj.transform);
                        if (itemState.progress_percentage > 0)
                        {
                            activeProgressIDs.Add(itemState.id);
                            UpdateProgressBar(itemState.id, itemObjects[itemState.id].transform, itemState.progress_percentage);
                        }
                    }
                }
            }
        }

        // Hide inactive items
        foreach (var itemID in itemObjects.Keys.ToList())
        {
            if (!activeItemIDs.Contains(itemID))
            {
                itemObjects[itemID].SetActive(false);
            }
        }

        // Remove inactive progress bars
        foreach (var progressId in progressBars.Keys.ToList())
        {
            if (!activeProgressIDs.Contains(progressId))
            {
                Destroy(progressBars[progressId].transform.root.gameObject);
                progressBars.Remove(progressId);
            }
        }
    }
}
