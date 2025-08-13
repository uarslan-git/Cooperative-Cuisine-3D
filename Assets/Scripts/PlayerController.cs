using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

public class PlayerController : MonoBehaviour
{
    public StudyClient studyClient;
    public GameManager gameManager;
    public float moveSpeed = 5f;

    private PlayerInput playerInput;
    private Vector2 move;
    private GameObject heldItem = null;
    private Transform holdingSpot;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        holdingSpot = transform.Find("HoldingSpot");
    }

    void Update()
    {
        // Local movement
        Vector3 movement = new Vector3(move.x, 0f, move.y);
        transform.position += movement * moveSpeed * Time.deltaTime;

        // Also send movement to server for authoritative state
        SendMoveAction();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }

    public void OnInteract(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            SendInteractAction("START");
        }
        else if (context.canceled)
        {
            SendInteractAction("STOP");
        }
    }

    public void OnPickup(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            // Send the action to the server
            SendPickupAction();

            // Also apply the action locally for immediate feedback (client-side prediction)
            HandlePickupAndDrop();
        }
    }

    private void HandlePickupAndDrop()
    {
        if (heldItem == null)
        {
            // Attempt to pick up an item
            TryPickupItem();
        }
        else
        {
            // Attempt to place the held item
            TryPlaceItem();
        }
    }

    private void TryPickupItem()
    {
        CounterState nearestCounter = FindNearestCounterWithItem();
        if (nearestCounter != null)
        {
            ItemState itemToPick = nearestCounter.occupied_by.FirstOrDefault();
            if (itemToPick != null && gameManager.items.ContainsKey(itemToPick.id))
            {
                heldItem = gameManager.items[itemToPick.id];
                heldItem.transform.SetParent(holdingSpot);
                heldItem.transform.localPosition = Vector3.zero;
            }
        }
    }

    private void TryPlaceItem()
    {
        GameObject nearestCounterObj = FindNearestCounter();
        if (nearestCounterObj != null)
        {
            heldItem.transform.SetParent(nearestCounterObj.transform);
            heldItem.transform.localPosition = Vector3.zero;
            heldItem = null;
        }
    }

    private CounterState FindNearestCounterWithItem()
    {
        float minDistance = float.MaxValue;
        CounterState nearestCounter = null;

        if (gameManager.lastState == null || gameManager.lastState.counters == null)
        {
            return null;
        }

        foreach (var counter in gameManager.lastState.counters)
        {
            if (counter.occupied_by != null && counter.occupied_by.Count > 0)
            {
                Vector3 counterPos = new Vector3(counter.pos[0], 0, counter.pos[1]);
                float distance = Vector3.Distance(transform.position, counterPos);
                if (distance < minDistance && distance < 2.0f) // 2.0f is interaction range
                {
                    minDistance = distance;
                    nearestCounter = counter;
                }
            }
        }
        return nearestCounter;
    }

    private GameObject FindNearestCounter()
    {
        float minDistance = float.MaxValue;
        GameObject nearestCounterObj = null;

        if (gameManager.counters == null)
        {
            return null;
        }

        foreach (var counter in gameManager.counters.Values)
        {
            float distance = Vector3.Distance(transform.position, counter.transform.position);
            if (distance < minDistance && distance < 2.0f) // 2.0f is interaction range
            {
                minDistance = distance;
                nearestCounterObj = counter;
            }
        }
        return nearestCounterObj;
    }

    private void SendMoveAction()
    {
        if (move != Vector2.zero)
        {
            Action action = new Action
            {
                player = studyClient.myPlayerId,
                type = "movement",
                action_data = new float[] { move.x, move.y },
                duration = Time.deltaTime
            };
            studyClient.SendAction(action);
        }
    }

    private void SendInteractAction(string interactionType)
    {
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            type = "interact",
            action_data = (interactionType == "START" ? "keydown" : "keyup")
        };
        studyClient.SendAction(action);
    }

    private void SendPickupAction()
    {
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            type = "pick_up_drop"
        };
        studyClient.SendAction(action);
    }
}