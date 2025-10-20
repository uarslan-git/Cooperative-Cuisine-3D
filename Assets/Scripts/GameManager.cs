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
        
        // Debug: Print the game state (only once per state to avoid spam)
        
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
                itemPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                itemPrefab.name = $"Item_{itemState.type}_Default";
            }
            itemObj = Instantiate(itemPrefab);
            itemObj.name = $"Item_{itemState.type}_{itemState.id}";
            itemObjects[itemState.id] = itemObj;
        } else {
            // Get existing item object if not recreating
            itemObj = itemObjects[itemState.id];
        }

        // Ensure consistent sizing for all items (especially food items) - apply every time
        if (itemState.type == "Tomato")
        {
            itemObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f); // Consistent size for food items
        }
        else if (itemState.type == "ChoppedTomato")
        {
            itemObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f); // Consistent size for food items
        }
        else if (itemState.type == "Onion")
        {
            itemObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f); // Consistent size for food items
        }
        else if (itemState.type == "ChoppedOnion")
        {
            itemObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f); // Slightly smaller for chopped items
        }
        else if (itemState.type == "Lettuce" || itemState.type == "ChoppedLettuce")
        {
            itemObj.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f); // Same size as other vegetables
        }
        else if (itemState.type == "Plate")
        {
            itemObj.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f); // Bigger, flatter plates from server state
        }
        else if (itemState.type == "Pot" || itemState.type == "Pan")
        {
            itemObj.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f); // Appropriate size for cooking equipment
        }
        else if (itemState.category == "ItemCookingEquipment")
        {
            itemObj.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f); // General cooking equipment size
        }
        else
        {
            itemObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f); // Default size for other items
        }

        if (itemObj.transform.parent != parent)
        {
            itemObj.transform.SetParent(parent, false);
            itemObj.transform.localRotation = Quaternion.identity;
            
            // Check if this is a base vegetable that needs a plate
            bool isBaseVegetable = itemState.type == "Lettuce" || itemState.type == "Onion" || itemState.type == "Tomato";
            
            // Determine item position based on parent type
            if (parent.name == "HoldingSpot") {
                itemObj.transform.localPosition = Vector3.zero;
            } else if (parent.name.StartsWith("Counter_")) {
                // Check if this counter is a stove for stacking logic
                string counterType = GetCounterType(parent.name);
                
                // Special positioning for cooking equipment (Pot, Pan) on stoves
                if (counterType == "Stove" && (itemState.type == "Pot" || itemState.type == "Pan" || itemState.category == "ItemCookingEquipment")) {
                    // Position cooking equipment at center of stove, on top of surface
                    float surfaceHeight = GetCounterSurfaceHeight(parent);
                    
                    // Special case for Cooktop items - always center at (0,0,0) with height adjustment
                    if (itemState.type.Contains("Cooktop"))
                    {
                        float itemHeight = GetItemHeight(itemState, parent);
                        itemObj.transform.localPosition = new Vector3(0, itemHeight, 0); // Centered at X=0, Z=0
                    }
                    else
                    {
                        itemObj.transform.localPosition = new Vector3(0, surfaceHeight, 0); // Centered on stove surface
                    }
                } else if (counterType == "Stove" && IsChoppedVegetable(itemState.type)) {
                    // Stack chopped vegetables on stoves to show quantity - also centered
                    int itemsOnStove = CountItemsOnCounter(parent, itemState.type);
                    float surfaceHeight = GetCounterSurfaceHeight(parent);
                    float stackHeight = surfaceHeight + (itemsOnStove * 0.2f); // Stack them 0.2 units apart
                    itemObj.transform.localPosition = new Vector3(0, stackHeight, 0); // Centered stacking
                } else if (isBaseVegetable) {
                    // For base vegetables on counters, place them directly on the counter surface
                    float vegetableCounterHeight = GetCounterSurfaceHeight(parent);
                    itemObj.transform.localPosition = new Vector3(0, vegetableCounterHeight + 0.1f, 0); // Directly on counter surface
                } else {
                    // General item positioning based on counter type and item type
                    float itemHeight = GetItemHeight(itemState, parent);
                    if (counterType == "Stove") {
                        // Items on stoves should be centered and at appropriate height
                        itemObj.transform.localPosition = new Vector3(0, itemHeight, 0);
                    } else if (counterType == "CuttingBoard") {
                        // Items on cutting boards should be centered at surface level
                        itemObj.transform.localPosition = new Vector3(0, itemHeight * 0.6f, 0);
                    } else {
                        // Default counter positioning - centered
                        itemObj.transform.localPosition = new Vector3(0, itemHeight, 0);
                    }
                }
            } else if (isBaseVegetable) {
                // For base vegetables on other surfaces, place them directly on the surface
                float vegetableSurfaceHeight = GetCounterSurfaceHeight(parent);
                itemObj.transform.localPosition = new Vector3(0, vegetableSurfaceHeight + 0.1f, 0); // Directly on surface
            } else {
                // Default position for other surface types - all centered with dynamic height adjustment
                float itemHeight = GetItemHeight(itemState, parent);
                if (parent.name.Contains("Stove")) {
                    itemObj.transform.localPosition = new Vector3(0, itemHeight, 0); // Centered on stoves
                } else if (parent.name.Contains("CuttingBoard")) {
                    itemObj.transform.localPosition = new Vector3(0, itemHeight * 0.6f, 0); // Centered on cutting boards
                } else if (parent.name.Contains("Sink")) {
                    itemObj.transform.localPosition = new Vector3(0, itemHeight * 0.8f, 0); // Centered on sinks
                } else {
                    itemObj.transform.localPosition = new Vector3(0, itemHeight, 0); // Centered on default counters
                }
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
        // Ensure counters are positioned at exactly the grid positions without any offset
        Vector3 position = new Vector3(counterState.pos[0], 0, invertedCounterZ);
        
        // Debug log to check positioning
        Debug.Log($"Counter {counterState.id} type {counterState.type} at pos ({counterState.pos[0]}, {counterState.pos[1]}) -> Unity pos {position}");
        
        Quaternion rotation = Quaternion.LookRotation(new Vector3(counterState.orientation[0], 0, -counterState.orientation[1]));

        if (!counters.ContainsKey(counterState.id)) {
            // Create counter based on type
            GameObject counterPrefab = Resources.Load<GameObject>($"Prefabs/{counterState.type}");
            if (counterPrefab == null) {
                counterPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                counterPrefab.name = counterState.type;
            }
            counterObj = Instantiate(counterPrefab, position, rotation);
            counterObj.name = $"Counter_{counterState.id}";
            
            // Don't modify counter children positions - let them stay as designed in prefabs
            
            // Ensure counter has a collider for collision detection
            if (counterObj.GetComponent<Collider>() == null)
            {
                BoxCollider collider = counterObj.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.9f, 1.0f, 0.9f); // Slightly smaller than visual for better movement
            }
            
            counters.Add(counterState.id, counterObj);
        } else {
            counterObj = counters[counterState.id];
            
            // Update counter position and rotation if they changed
            if (counterObj.transform.position != position || counterObj.transform.rotation != rotation)
            {
                counterObj.transform.position = position;
                counterObj.transform.rotation = rotation;
                
                // Don't modify children positions when counter moves - they should maintain their relative positions
            }
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
                    
                    // Include cooking equipment content in hash
                    if (item is CookingEquipmentState cookingEquipment)
                    {
                        hashData.Append($":content_list_count:{cookingEquipment.content_list?.Count ?? 0}");
                        if (cookingEquipment.content_list != null)
                        {
                            foreach (var contentItem in cookingEquipment.content_list)
                            {
                                hashData.Append($":content:{contentItem.id}:{contentItem.type}:{contentItem.progress_percentage}");
                            }
                        }
                        
                        hashData.Append($":content_ready:{cookingEquipment.content_ready?.id ?? "null"}");
                        if (cookingEquipment.content_ready != null)
                        {
                            hashData.Append($":{cookingEquipment.content_ready.type}");
                        }
                    }
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
                        
                        // Handle CookingEquipmentState held by players
                        if (playerState.holding is CookingEquipmentState heldCookingEquipment)
                        {
                            UpdateCookingEquipment(heldCookingEquipment, playerObj.transform.Find("HoldingSpot"), activeItemIDs, activeProgressIDs);
                        }
                        
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
                if (counterState.occupied_by != null)
                {
                    foreach (ItemState itemState in counterState.occupied_by)
                    {
                        activeItemIDs.Add(itemState.id);
                        UpdateItem(itemState, counterObj.transform);
                        
                        // Handle CookingEquipmentState (like stoves) with content_list and content_ready
                        if (itemState is CookingEquipmentState cookingEquipment)
                        {
                            UpdateCookingEquipment(cookingEquipment, counterObj.transform, activeItemIDs, activeProgressIDs);
                        }
                        
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
    
    private float GetCounterSurfaceHeight(Transform parent)
    {
        // Get the actual height of the counter surface
        if (parent.name.StartsWith("Counter_"))
        {
            Renderer counterRenderer = parent.GetComponent<Renderer>();
            if (counterRenderer != null)
            {
                return counterRenderer.bounds.size.y;
            }
        }
        // Default height for non-counter parents or if no renderer found
        return 1.0f;
    }
    
    private float GetItemHeight(ItemState itemState, Transform parent)
    {
        float baseHeight = GetCounterSurfaceHeight(parent);
        
        // Special height adjustments for specific items
        if (itemState.type.Contains("Cooktop"))
        {
            return baseHeight + 0.0f; // Raise cooktop higher
        }
        else if (itemState.type == "CuttingBoardOld")
        {
            return baseHeight + 0.3f; // Raise cutting board
        }
        else if (itemState.type == "Knife")
        {
            return baseHeight + 0.4f; // Raise knife
        }
        else if (itemState.type == "Plate")
        {
            return baseHeight + 1.0f; // Raise plates higher
        }
        
        return baseHeight; // Default height
    }
    
    private void UpdateCookingEquipment(CookingEquipmentState cookingEquipment, Transform parent, HashSet<string> activeItemIDs, HashSet<string> activeProgressIDs)
    {
        // Find the cooking equipment GameObject (like a stove)
        GameObject cookingEquipmentObj = null;
        if (itemObjects.ContainsKey(cookingEquipment.id))
        {
            cookingEquipmentObj = itemObjects[cookingEquipment.id];
        }
        
        if (cookingEquipmentObj == null) return;
        
        // Create a container for cooking contents if it doesn't exist
        Transform contentsContainer = cookingEquipmentObj.transform.Find("CookingContents");
        if (contentsContainer == null)
        {
            GameObject container = new GameObject("CookingContents");
            container.transform.SetParent(cookingEquipmentObj.transform, false);
            container.transform.localPosition = new Vector3(0, 0.3f, 0); // Inside the cooking equipment (pot/pan)
            contentsContainer = container.transform;
        }
        
        // Handle content_list (items currently being cooked)
        if (cookingEquipment.content_list != null && cookingEquipment.content_list.Count > 0)
        {
            for (int i = 0; i < cookingEquipment.content_list.Count; i++)
            {
                ItemState contentItem = cookingEquipment.content_list[i];
                activeItemIDs.Add(contentItem.id);
                
                // Create or update the content item
                GameObject contentObj;
                if (itemObjects.ContainsKey(contentItem.id))
                {
                    contentObj = itemObjects[contentItem.id];
                    contentObj.SetActive(true);
                }
                else
                {
                    GameObject itemPrefab = Resources.Load<GameObject>($"Prefabs/{contentItem.type}");
                    if (itemPrefab == null)
                    {
                        itemPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        itemPrefab.name = $"Item_{contentItem.type}_Default";
                    }
                    contentObj = Instantiate(itemPrefab);
                    contentObj.name = $"Item_{contentItem.type}_{contentItem.id}";
                    itemObjects[contentItem.id] = contentObj;
                }
                
                // Position content items in the cooking equipment
                contentObj.transform.SetParent(contentsContainer, false);
                
                // Stack items vertically in the center of the cooking equipment
                float stackHeight = i * 0.1f; // Stack items vertically
                
                contentObj.transform.localPosition = new Vector3(0, stackHeight, 0); // Centered stacking
                contentObj.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f); // Smaller size when inside cooking equipment
                
                // Show progress if the item is cooking
                if (contentItem.progress_percentage > 0 && contentItem.progress_percentage < 100)
                {
                    activeProgressIDs.Add(contentItem.id);
                    UpdateProgressBar(contentItem.id, contentObj.transform, contentItem.progress_percentage);
                }
            }
        }
        else
        {
            // No content_list - hide any existing content items for this cooking equipment
            foreach (Transform child in contentsContainer)
            {
                child.gameObject.SetActive(false);
            }
        }
        
        // Handle content_ready (finished cooked item ready for pickup)
        if (cookingEquipment.content_ready != null)
        {
            activeItemIDs.Add(cookingEquipment.content_ready.id);
            
            // Create a "ready" spot on the cooking equipment
            Transform readySpot = cookingEquipmentObj.transform.Find("ReadySpot");
            if (readySpot == null)
            {
                GameObject readyContainer = new GameObject("ReadySpot");
                readyContainer.transform.SetParent(cookingEquipmentObj.transform, false);
                readyContainer.transform.localPosition = new Vector3(0, 0.8f, 0); // Centered above the cooking equipment
                readySpot = readyContainer.transform;
            }
            
            // Create or update the ready item
            GameObject readyObj;
            if (itemObjects.ContainsKey(cookingEquipment.content_ready.id))
            {
                readyObj = itemObjects[cookingEquipment.content_ready.id];
                readyObj.SetActive(true);
            }
            else
            {
                GameObject itemPrefab = Resources.Load<GameObject>($"Prefabs/{cookingEquipment.content_ready.type}");
                if (itemPrefab == null)
                {
                    itemPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    itemPrefab.name = $"Item_{cookingEquipment.content_ready.type}_Default";
                }
                readyObj = Instantiate(itemPrefab);
                readyObj.name = $"Item_{cookingEquipment.content_ready.type}_{cookingEquipment.content_ready.id}";
                itemObjects[cookingEquipment.content_ready.id] = readyObj;
            }
            
            readyObj.transform.SetParent(readySpot, false);
            readyObj.transform.localPosition = Vector3.zero;
            readyObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            
            // Add a visual indicator that this item is ready (like a glow or different color)
            AddReadyIndicator(readyObj);
        }
        else
        {
            // No content_ready - hide the ready spot
            Transform readySpot = cookingEquipmentObj.transform.Find("ReadySpot");
            if (readySpot != null)
            {
                foreach (Transform child in readySpot)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }
    
    private void AddReadyIndicator(GameObject readyItem)
    {
        // Add a visual indicator that the item is ready for pickup
        Renderer renderer = readyItem.GetComponent<Renderer>();
        if (renderer != null)
        {
            // Add a subtle glow or highlight
            Material material = renderer.material;
            if (material != null)
            {
                // Enable emission to make it glow
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.green * 0.3f);
            }
        }
        
        // TODO: Add ReadyItemIndicator component once compilation issues are resolved
        // The component will provide a gentle bobbing animation
    }
}
