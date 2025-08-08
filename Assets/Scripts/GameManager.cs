using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class GameManager : MonoBehaviour
{
    public StudyClient studyClient;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timeText;
    public Transform ordersContainer;
    public GameObject orderTextPrefab;

    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> counters = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> items = new Dictionary<string, GameObject>();
    private Dictionary<string, Slider> progressBars = new Dictionary<string, Slider>();
    private GameObject progressBarPrefab;
    private StateRepresentation lastState;

    void Start()
    {
        studyClient.OnStateReceived += HandleStateReceived;
        progressBarPrefab = Resources.Load<GameObject>("Prefabs/ProgressBar");
        if (progressBarPrefab == null)
        {
            Debug.LogError("ProgressBar prefab not found in Resources/Prefabs!");
        }
        orderTextPrefab.SetActive(false);
    }

    void HandleStateReceived(StateRepresentation state)
    {
        lastState = state;
        UpdateWorld();
    }

    void UpdateWorld()
    {
        if (lastState == null) return;

        scoreText.text = $"Score: {lastState.score}";
        timeText.text = $"Time: {Mathf.FloorToInt(lastState.remaining_time)}";
        UpdateOrders();

        HashSet<string> activeItemIds = new HashSet<string>();
        if (lastState.players != null)
        {
            foreach (var p in lastState.players)
            {
                if (p.holding != null) activeItemIds.Add(p.holding.id);
            }
        }
        if (lastState.counters != null)
        {
            foreach (var c in lastState.counters)
            {
                if (c.occupied_by != null)
                {
                    foreach (var item in c.occupied_by)
                    {
                        activeItemIds.Add(item.id);
                    }
                }
            }
        }

        foreach (var itemId in items.Keys.ToList())
        {
            if (!activeItemIds.Contains(itemId))
            {
                Destroy(items[itemId]);
                items.Remove(itemId);
            }
        }
        
        HashSet<string> activeProgressIds = new HashSet<string>();

        if (lastState.players != null)
        {
            foreach (var playerState in lastState.players)
            {
                GameObject playerObj;
                if (!players.ContainsKey(playerState.id))
                {
                    GameObject playerPrefab = Resources.Load<GameObject>("Prefabs/Player");
                    playerObj = Instantiate(playerPrefab, new Vector3(playerState.pos[0], 0.5f, playerState.pos[1]), Quaternion.identity);
                    players.Add(playerState.id, playerObj);
                    if (playerState.id == studyClient.myPlayerId)
                    {
                        playerObj.AddComponent<PlayerController>().studyClient = studyClient;
                    }
                }
                else
                {
                    playerObj = players[playerState.id];
                    playerObj.transform.position = new Vector3(playerState.pos[0], 0.5f, playerState.pos[1]);
                }

                if (playerState.holding != null)
                {
                    UpdateItem(playerState.holding, playerObj.transform.Find("HoldingSpot"));
                    if (playerState.holding.progress_percentage > 0)
                    {
                        activeProgressIds.Add(playerState.holding.id);
                        UpdateProgressBar(playerState.holding.id, items[playerState.holding.id].transform, playerState.holding.progress_percentage);
                    }
                }
            }
        }

        if (lastState.counters != null)
        {
            foreach (var counterState in lastState.counters)
            {
                GameObject counterObj;
                if (!counters.ContainsKey(counterState.id))
                {
                    GameObject counterPrefab = Resources.Load<GameObject>($"Prefabs/{counterState.type}");
                    if (counterPrefab == null)
                    {
                        Debug.LogWarning($"Prefab for counter type '{counterState.type}' not found. Using default cube.");
                        counterPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        counterPrefab.name = counterState.type;
                    }
                    counterObj = Instantiate(counterPrefab, new Vector3(counterState.pos[0], 0.5f, counterState.pos[1]), Quaternion.identity);
                    counters.Add(counterState.id, counterObj);
                }
                else
                {
                    counterObj = counters[counterState.id];
                }

                if (counterState.occupied_by != null)
                {
                    foreach (var itemState in counterState.occupied_by)
                    {
                        UpdateItem(itemState, counterObj.transform);
                        if (itemState.progress_percentage > 0)
                        {
                            activeProgressIds.Add(itemState.id);
                            UpdateProgressBar(itemState.id, items[itemState.id].transform, itemState.progress_percentage);
                        }
                    }
                }
            }
        }
        
        foreach (var progressId in progressBars.Keys.ToList())
        {
            if (!activeProgressIds.Contains(progressId))
            {
                Destroy(progressBars[progressId].gameObject);
                progressBars.Remove(progressId);
            }
        }
    }

    private void UpdateItem(ItemState itemState, Transform parent)
    {
        GameObject itemObj;
        if (!items.ContainsKey(itemState.id))
        {
            GameObject itemPrefab = Resources.Load<GameObject>($"Prefabs/{itemState.type}");
            if (itemPrefab == null)
            {
                Debug.LogWarning($"Prefab for item type '{itemState.type}' not found. Using default sphere.");
                itemPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                itemPrefab.name = itemState.type;
                itemPrefab.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            itemObj = Instantiate(itemPrefab);
            items.Add(itemState.id, itemObj);
        }
        else
        {
            itemObj = items[itemState.id];
        }

        itemObj.transform.SetParent(parent);
        itemObj.transform.localPosition = Vector3.zero;
        itemObj.transform.localRotation = Quaternion.identity;
    }

    private void UpdateProgressBar(string id, Transform parent, float progress)
    {
        Slider progressBar;
        if (!progressBars.ContainsKey(id))
        {
            // Create a world space canvas
            GameObject canvasObj = new GameObject($"ProgressBarCanvas_{id}");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasObj.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
            canvasObj.AddComponent<LookAtCamera>(); // Add the script to make it face the camera

            // Instantiate the progress bar prefab as a child of the canvas
            GameObject progressBarObj = Instantiate(progressBarPrefab, canvasObj.transform);
            progressBar = progressBarObj.GetComponentInChildren<Slider>();
            progressBars.Add(id, progressBar);

            // Configure the canvas rect
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.position = parent.position + Vector3.up * 1.2f;
            canvasRect.sizeDelta = new Vector2(1, 0.3f);
            canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }
        else
        {
            progressBar = progressBars[id];
        }

        // Update progress value
        progressBar.value = progress / 100f;
        
        // Ensure the canvas is active
        progressBar.transform.root.gameObject.SetActive(true);
    }

    private void UpdateOrders()
    {
        // Clear existing orders
        foreach (Transform child in ordersContainer)
        {
            if(child.gameObject.activeSelf)
                Destroy(child.gameObject);
        }

        // Create new orders
        if (lastState.orders != null)
        {
            foreach (var order in lastState.orders)
            {
                GameObject orderObj = Instantiate(orderTextPrefab, ordersContainer);
                orderObj.GetComponent<TextMeshProUGUI>().text = order.meal;
                orderObj.SetActive(true);
            }
        }
    }
}
