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

    // The key is a stable "slot ID" (e.g., "counter-X-slot-0" or "player-Y-holding")
    public Dictionary<string, GameObject> itemObjects = new Dictionary<string, GameObject>();
    public Dictionary<string, GameObject> counters = new Dictionary<string, GameObject>();
    public StateRepresentation lastState;

    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private Dictionary<string, Slider> progressBars = new Dictionary<string, Slider>();
    private GameObject progressBarPrefab;

    private bool floorInstantiated = false;
    private string lastStateJson = "";

    void Start()
    {
        studyClient.OnStateReceived += HandleStateReceived;
        progressBarPrefab = Resources.Load<GameObject>("Prefabs/ProgressBar");
        if (progressBarPrefab == null) Debug.LogError("ProgressBar prefab not found in Resources/Prefabs!");
        orderTextPrefab.SetActive(false);
    }

    public void HandleStateReceived(StateRepresentation state)
    {
        string newStateJson = JsonConvert.SerializeObject(state);
        if (newStateJson == lastStateJson) return; // State has not changed, do not update

        lastStateJson = newStateJson;
        lastState = state;
        UpdateWorld();
    }

    void UpdateWorld()
    {
        if (lastState == null) return;

        // Initial scene setup
        if (!floorInstantiated && lastState.kitchen != null) InstantiateFloor();
        scoreText.text = $"Score: {lastState.score}";
        timeText.text = $"Time: {Mathf.FloorToInt(lastState.remaining_time)}";
        UpdateOrders();

        // --- New, Robust State Reconciliation --- 

        HashSet<string> activeSlotIDs = new HashSet<string>();
        HashSet<string> activeProgressIDs = new HashSet<string>();

        // Process Players and their held items
        if (lastState.players != null) {
            foreach (var playerState in lastState.players) {
                GameObject playerObj = UpdatePlayer(playerState);
                if (playerState.holding != null) {
                    string itemId = playerState.holding.id;
                    activeSlotIDs.Add(itemId);
                    UpdateItem(itemId, playerState.holding, playerObj.transform.Find("HoldingSpot"));

                    if (playerState.holding.progress_percentage > 0) {
                        activeProgressIDs.Add(itemId);
                        UpdateProgressBar(itemId, itemObjects[itemId].transform, playerState.holding.progress_percentage);
                    }
                }
            }
        }

        // Process Counters and their items
        if (lastState.counters != null) {
            foreach (var counterState in lastState.counters) {
                GameObject counterObj = UpdateCounter(counterState);
                if (counterState.occupied_by != null) {
                    for (int i = 0; i < counterState.occupied_by.Count; i++) {
                        ItemState itemState = counterState.occupied_by[i];
                        string itemId = itemState.id;
                        activeSlotIDs.Add(itemId);
                        UpdateItem(itemId, itemState, counterObj.transform);

                        if (itemState.progress_percentage > 0) {
                            activeProgressIDs.Add(itemId);
                            UpdateProgressBar(itemId, itemObjects[itemId].transform, itemState.progress_percentage);
                        }
                    }
                }
            }
        }

        // Clean up any items in slots that are no longer active
        foreach (var itemId in itemObjects.Keys.ToList()) {
            if (!activeSlotIDs.Contains(itemId)) {
                Destroy(itemObjects[itemId]);
                itemObjects.Remove(itemId);
            }
        }

        // Clean up progress bars for items no longer progressing
        foreach (var progressId in progressBars.Keys.ToList()) {
            if (!activeProgressIDs.Contains(progressId)) {
                Destroy(progressBars[progressId].transform.root.gameObject);
                progressBars.Remove(progressId);
            }
        }
    }

    private void UpdateItem(string itemId, ItemState itemState, Transform parent)
    {
        GameObject itemObj = null;
        bool needsNewPrefab = false;

        if (!itemObjects.ContainsKey(itemId)) {
            needsNewPrefab = true;
        } else {
            itemObj = itemObjects[itemId];
            string expectedName = $"Item_{itemState.type}";
            if (!itemObj.name.StartsWith(expectedName)) {
                Destroy(itemObj);
                itemObjects.Remove(itemId);
                needsNewPrefab = true;
            }
        }

        if (needsNewPrefab) {
            GameObject itemPrefab = Resources.Load<GameObject>($"Prefabs/{itemState.type}");
            GameObject toDestroy = null;
            if (itemPrefab == null) {
                itemPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                itemPrefab.name = $"Item_{itemState.type}_Default";
                itemPrefab.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                toDestroy = itemPrefab;
            }
            itemObj = Instantiate(itemPrefab);
            if (toDestroy != null) {
                Destroy(toDestroy);
            }
            itemObj.name = $"Item_{itemState.type}_{itemState.id}";
            itemObjects[itemId] = itemObj;
        }

        itemObj.transform.SetParent(parent, false);
        itemObj.transform.localRotation = Quaternion.identity;
        itemObj.transform.localPosition = (parent.name == "HoldingSpot") ? Vector3.zero : new Vector3(0, 1f, 0);
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
}
