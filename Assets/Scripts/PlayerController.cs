using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    public StudyClient studyClient;
    public GameManager gameManager;
    public float moveSpeed = 5f;

    private PlayerInput playerInput;
    private Vector2 move;
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
        }
    }

    

    

    private void SendMoveAction()
    {
        if (move != Vector2.zero)
        {
            Action action = new Action
            {
                player = studyClient.myPlayerId,
                action_type = "movement",
                action_data = new List<float> { move.x, move.y }
            };
            studyClient.SendAction(action);
        }
    }

    private void SendInteractAction(string interactionType)
    {
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = "interact",
            action_data = (interactionType == "START" ? "keydown" : "keyup")
        };
        studyClient.SendAction(action);
    }

    private void SendPickupAction()
    {
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = "pick_up_drop"
        };
        studyClient.SendAction(action);
    }
}