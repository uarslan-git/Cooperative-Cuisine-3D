using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// This script is responsible for sending player input actions to the StudyClient.
// It works alongside PlayerController, which handles the actual in-game character movement and interaction.
public class PlayerInputController : MonoBehaviour
{
    public StudyClient studyClient;
    public GameObject controlledPlayerGameObject;
    public PlayerInput playerInput;
    public Camera mainCamera;

    void Awake()
    {
        // Automatically find the StudyClient in the scene when the player is instantiated.
        if (studyClient == null)
        {
            studyClient = FindFirstObjectByType<StudyClient>();
        }
        if (studyClient == null)
        {
            Debug.LogError("PlayerInputController could not find a StudyClient in the scene!");
        }

        if (playerInput != null)
        {
            playerInput.actions = playerInput.actions; // Ensure actions are loaded
            playerInput.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            playerInput.camera = mainCamera;
            playerInput.actions.FindActionMap("Player").Enable();
        }
        else
        {
            Debug.LogError("PlayerInput component not assigned to PlayerInputController!");
        }
    }

    // Called by the PlayerInput component when the "Move" action is triggered
    public void OnMove(InputValue value)
    {
        Vector2 move = value.Get<Vector2>();
        Debug.Log("Key Pressed. Move value: " + move);

        // Only send an action if there is movement input
        if (move != Vector2.zero)
        {
            SendMoveAction(move);
        }
    }

    // Called by the PlayerInput component when the "PickUp" action is triggered
    public void OnPickUp(InputValue value)
    {
        if (value.isPressed)
        {
            SendButtonAction("pickup");
        }
    }

    // Called by the PlayerInput component when the "Interact" action is triggered
    public void OnInteract(InputValue value)
    {
        if (value.isPressed)
        {
            SendButtonAction("interact");
        }
    }

    private void SendMoveAction(Vector2 move)
    {
        if (studyClient == null) return;
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            type = "movement",
            action_data = new float[] { move.x, move.y },
            duration = 1.0f,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        Debug.Log("Sent Move Action: " + move);
    }

    private void SendButtonAction(string actionType)
    {
        if (studyClient == null) return;
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            type = actionType,
            action_data = null,
            duration = 0.0f,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        Debug.Log("Sent Button Action: " + actionType);
    }
}
