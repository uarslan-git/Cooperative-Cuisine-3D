using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public StudyClient studyClient;
    private PlayerInput playerInput;
    private Vector2 move;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
    }

    void Update()
    {
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
                type = "MOVEMENT",
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
            type = "INTERACT",
            action_data = new float[] { (interactionType == "START" ? 1 : 0) }
        };
        studyClient.SendAction(action);
    }

    private void SendPickupAction()
    {
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            type = "PICK_UP_DROP"
        };
        studyClient.SendAction(action);
    }
}
