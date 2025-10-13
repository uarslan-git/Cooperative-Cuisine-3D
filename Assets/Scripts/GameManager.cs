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
    
    // For smooth movement interpolation
    private Dictionary<string, Vector3> targetPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> targetRotations = new Dictionary<string, Quaternion>();
    // movementLerpSpeed removed - now using fixed values per player type

    private bool floorInstantiated = false;
    
    // Track last known object states to avoid unnecessary updates
    private string lastCountersHash = "";
    private float lastScore = -1;
    private float lastTime = -1;

    void Start()
    {
        studyClient.OnStateReceived += HandleStateReceived;
        progressBarPrefab = Resources.Load<GameObject>("Prefabs/ProgressBar");
        if (progressBarPrefab == null) Debug.LogError("ProgressBar prefab not found in Resources/Prefabs!");
        orderTextPrefab.SetActive(false);
        
        // Clear any existing fallback objects from previous runs
        ClearGameObjects();
    }

    void Update()
    {
        // Smoothly interpolate player positions between network updates
        foreach (var playerEntry in players)
        {
            string playerId = playerEntry.Key;
            GameObject playerObj = playerEntry.Value;
            
            if (targetPositions.ContainsKey(playerId) && targetRotations.ContainsKey(playerId))
            {
                // Apply server updates to ALL players - server is now authoritative for everyone
                if (playerId == studyClient.myPlayerId) {
                    // Local player: smooth interpolation from server position (server-authoritative)
                    playerObj.transform.position = Vector3.Lerp(playerObj.transform.position, targetPositions[playerId], 15f * Time.deltaTime);
                    playerObj.transform.rotation = Quaternion.Lerp(playerObj.transform.rotation, targetRotations[playerId], 15f * Time.deltaTime);
                } else {
                    // Remote players: Ultra-smooth movement with very fast lerp
                    float distance = Vector3.Distance(playerObj.transform.position, targetPositions[playerId]);
                    if (distance > 0.01f) {
                        // Use MoveTowards for consistent speed regardless of distance
                        playerObj.transform.position = Vector3.MoveTowards(playerObj.transform.position, targetPositions[playerId], 10f * Time.deltaTime);
                        playerObj.transform.rotation = Quaternion.RotateTowards(playerObj.transform.rotation, targetRotations[playerId], 720f * Time.deltaTime);
                    } else {
                        // Snap to position if very close
                        playerObj.transform.position = targetPositions[playerId];
                        playerObj.transform.rotation = targetRotations[playerId];
                    }
                }
            }
        }
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
        bool needsRecreate = false;
        
        if (itemObjects.ContainsKey(itemState.id)) {
            itemObj = itemObjects[itemState.id];
            
            // Check if item type has changed (e.g., Tomato → ChoppedTomato)
            string currentType = itemObj.name.Split('_')[1]; // Extract type from "Item_Type_ID" format
            if (currentType != itemState.type) {
                // Item has been processed - destroy old and create new
                Destroy(itemObj);
                itemObjects.Remove(itemState.id);
                needsRecreate = true;
            } else {
                itemObj.SetActive(true);
            }
        } else {
            needsRecreate = true;
        }
        
        if (needsRecreate) {
            GameObject itemPrefab = Resources.Load<GameObject>($"Prefabs/{itemState.type}");
            if (itemPrefab == null) {
                Debug.LogError($"Prefab not found for item type: {itemState.type}");
                return; // Don't create fallback objects
            }
            itemObj = Instantiate(itemPrefab);
            itemObj.name = $"Item_{itemState.type}_{itemState.id}";
            
            // Ensure consistent sizing for all items (especially food items)
            if (itemState.type == "Tomato" || itemState.type == "Onion" || 
                itemState.type == "ChoppedTomato" || itemState.type == "ChoppedOnion" || itemState.type == "ChoppedLettuce")
            {
                itemObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f); // Smaller for most food items
            }
            else if (itemState.type == "Lettuce")
            {
                itemObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f); // Extra small for lettuce
            }
            else
            {
                itemObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Default size for other items
            }
            
            itemObjects[itemState.id] = itemObj;
        } else {
            // Get existing item object if not recreating
            itemObj = itemObjects[itemState.id];
        }

        if (itemObj.transform.parent != parent)
        {
            itemObj.transform.SetParent(parent, false);
            itemObj.transform.localRotation = Quaternion.identity;
            
            // Determine item position based on parent type
            if (parent.name == "HoldingSpot") {
                itemObj.transform.localPosition = Vector3.zero;
            } else if (parent.name.StartsWith("Counter_")) {
                // Check if this counter is a stove for stacking logic
                string counterType = GetCounterType(parent.name);
                
                if (counterType == "Stove" && IsChoppedVegetable(itemState.type)) {
                    // Stack chopped vegetables on stoves to show quantity
                    int itemsOnStove = CountItemsOnCounter(parent, itemState.type);
                    float stackHeight = 1f + (itemsOnStove * 0.2f); // Stack them 0.2 units apart
                    itemObj.transform.localPosition = new Vector3(0, stackHeight, 0);
                } else {
                    // Place all items normally on top of counter
                    itemObj.transform.localPosition = new Vector3(0, 1f, 0);
                }
            } else {
                // Default position for other counter types
                itemObj.transform.localPosition = new Vector3(0, 1f, 0);
            }
        }
    }

    private GameObject UpdatePlayer(PlayerState playerState) {
        GameObject playerObj;
        float invertedPlayerZ = (lastState.kitchen.height - 1) - playerState.pos[1];
        Vector3 targetPosition = new Vector3(playerState.pos[0], 0, invertedPlayerZ);
        Quaternion targetRotation = Quaternion.LookRotation(new Vector3(playerState.facing_direction[0], 0, -playerState.facing_direction[1]));

        if (!players.ContainsKey(playerState.id)) {
            GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
            playerObj = Instantiate(playerPrefab, targetPosition, targetRotation);
            players.Add(playerState.id, playerObj);
            
            // Add collider to player for collision detection if it doesn't have one
            if (playerObj.GetComponent<Collider>() == null)
            {
                CapsuleCollider collider = playerObj.AddComponent<CapsuleCollider>();
                collider.height = 1.8f;
                collider.radius = 0.3f;
                collider.isTrigger = false;
            }
        } else {
            playerObj = players[playerState.id];
            // Don't set position/rotation directly - use interpolation system instead
        }
        
        // Set target position and rotation for smooth interpolation
        targetPositions[playerState.id] = targetPosition;
        targetRotations[playerState.id] = targetRotation;
        
        // For local player, now sync FROM server TO Unity for perfect synchronization
        if (playerState.id == studyClient.myPlayerId) {
            // Server is now authoritative - Unity follows server position
            // This ensures perfect sync since server movement is smooth
        }
        
        // Always ensure the controller is connected for the current player (important for level transitions)
        if (playerState.id == studyClient.myPlayerId) {
            PlayerInputController pic = GetComponent<PlayerInputController>();
            if (pic != null) {
                if (pic.controlledPlayerGameObject != playerObj) {
                    pic.controlledPlayerGameObject = playerObj;
                    Debug.Log($"Player controller reconnected to player {playerState.id} for new level");
                    
                    // Notify VR Camera Controller about player change
                    VRCameraController vrCameraController = FindFirstObjectByType<VRCameraController>();
                    if (vrCameraController != null) {
                        vrCameraController.SetPlayerTransform(playerObj.transform);
                        Debug.Log("VR Camera Controller updated with new player transform");
                    }
                }
                
                // Ensure player input is enabled after level transition
                if (pic.playerInput != null && pic.playerInput.actions != null) {
                    var playerActionMap = pic.playerInput.actions.FindActionMap("Player");
                    if (playerActionMap != null && !playerActionMap.enabled) {
                        playerActionMap.Enable();
                        Debug.Log("Player input re-enabled for new level");
                    }
                }
            }
            
            // Lerp speed is now handled in the Update method per player
        }
        
        return playerObj;
    }

    private GameObject UpdateCounter(CounterState counterState) {
        GameObject counterObj;
        float invertedCounterZ = (lastState.kitchen.height - 1) - counterState.pos[1];
        Vector3 position = new Vector3(counterState.pos[0], 0, invertedCounterZ);
        Quaternion rotation = Quaternion.LookRotation(new Vector3(counterState.orientation[0], 0, -counterState.orientation[1]));

        if (!counters.ContainsKey(counterState.id)) {
            // Create counter based on type
            GameObject counterPrefab = Resources.Load<GameObject>($"Prefabs/{counterState.type}");
            if (counterPrefab == null) {
                Debug.LogError($"Prefab not found for counter type: {counterState.type}");
                return null; // Don't create fallback objects
            }
            counterObj = Instantiate(counterPrefab, position, rotation);
            counterObj.name = $"Counter_{counterState.id}";
            
            // Ensure counter has a collider for collision detection
            if (counterObj.GetComponent<Collider>() == null)
            {
                BoxCollider collider = counterObj.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.9f, 1.0f, 0.9f); // Slightly smaller than visual for better movement
            }
            
            counters.Add(counterState.id, counterObj);
        } else {
            counterObj = counters[counterState.id];
        }
        return counterObj;
    }

    private void UpdateProgressBar(string id, Transform parent, float progress) {
        if (progressBarPrefab == null) {
            Debug.LogError("ProgressBar prefab is null! Cannot create progress bar.");
            return;
        }
        
        Slider progressBar;
        if (!progressBars.ContainsKey(id)) {
            Debug.Log($"Creating new progress bar for item {id}");
            
            // Create progress bar directly from prefab
            GameObject progressBarObj = Instantiate(progressBarPrefab);
            progressBarObj.name = $"ProgressBar_{id}";
            
            // Set up world space canvas
            Canvas canvas = progressBarObj.GetComponent<Canvas>();
            if (canvas == null) {
                canvas = progressBarObj.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.WorldSpace;
            
            CanvasScaler scaler = progressBarObj.GetComponent<CanvasScaler>();
            if (scaler == null) {
                scaler = progressBarObj.AddComponent<CanvasScaler>();
            }
            scaler.dynamicPixelsPerUnit = 10;
            
            // Add LookAtCamera if it doesn't exist
            if (progressBarObj.GetComponent<LookAtCamera>() == null) {
                progressBarObj.AddComponent<LookAtCamera>();
            }

            // Get the slider component
            progressBar = progressBarObj.GetComponent<Slider>();
            if (progressBar == null) {
                progressBar = progressBarObj.GetComponentInChildren<Slider>();
            }
            
            if (progressBar == null) {
                Debug.LogError($"No Slider component found in ProgressBar prefab for item {id}!");
                Destroy(progressBarObj);
                return;
            }
            
            progressBars.Add(id, progressBar);

            // Set up the rect transform for proper sizing
            RectTransform rectTransform = progressBarObj.GetComponent<RectTransform>();
            if (rectTransform != null) {
                rectTransform.sizeDelta = new Vector2(100, 20); // Reasonable size in world space
                rectTransform.localScale = new Vector3(0.01f, 0.01f, 0.01f); // Scale down for world space
            }
            
            Debug.Log($"Successfully created progress bar for item {id}");
        } else {
            progressBar = progressBars[id];
        }
        
        // Position and update the progress bar
        if (progressBar != null && progressBar.transform.root != null) {
            progressBar.transform.root.position = parent.position + Vector3.up * 2.0f;
            progressBar.value = progress / 100f;
            progressBar.transform.root.gameObject.SetActive(true);
            
            Debug.Log($"Updated progress bar for {id}: {progress}% (value: {progressBar.value}) at position {progressBar.transform.root.position}");
        } else {
            Debug.LogError($"Progress bar or its root transform is null for item {id}");
        }
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

        // Also destroy any objects in the scene that might be fallback cubes
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Counter_") || obj.name.Contains("Item_"))
            {
                Destroy(obj);
            }
        }

        // Reset all tracking state
        floorInstantiated = false;
        lastCountersHash = "";
        lastScore = -1;
        lastTime = -1;
        
        // Clear interpolation data
        targetPositions.Clear();
        targetRotations.Clear();
    }
    
    public void OnNewLevelStarted()
    {
        Debug.Log("New level started - clearing previous game state");
        ClearGameObjects();
        
        // Don't clear the player controller reference - let it reconnect naturally
        // The UpdatePlayer method will handle the reconnection properly
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
                        if (playerState.holding.progress_percentage > 0 && playerState.holding.progress_percentage < 100)
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
                if (counterObj != null && counterState.occupied_by != null)
                {
                    foreach (ItemState itemState in counterState.occupied_by)
                    {
                        activeItemIDs.Add(itemState.id);
                        UpdateItem(itemState, counterObj.transform);
                        if (itemState.progress_percentage > 0 && itemState.progress_percentage < 100)
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
    
    // Helper methods for item positioning and stacking
    private string GetCounterType(string counterName)
    {
        // Extract counter ID and find the counter type from lastState
        if (counterName.StartsWith("Counter_"))
        {
            string counterId = counterName.Substring("Counter_".Length);
            if (lastState?.counters != null)
            {
                foreach (var counter in lastState.counters)
                {
                    if (counter.id == counterId)
                    {
                        return counter.type;
                    }
                }
            }
        }
        return "Unknown";
    }
    
    private bool IsChoppedVegetable(string itemType)
    {
        return itemType == "ChoppedTomato" || itemType == "ChoppedOnion" || itemType == "ChoppedLettuce";
    }
    
    private int CountItemsOnCounter(Transform counterTransform, string itemType)
    {
        int count = 0;
        foreach (Transform child in counterTransform)
        {
            if (child.name.Contains($"Item_{itemType}_"))
            {
                count++;
            }
        }
        return count;
    }
}
