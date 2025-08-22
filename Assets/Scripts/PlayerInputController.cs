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

    private Vector2 _moveInput;

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

    void Update()
    {
        if (_moveInput != Vector2.zero)
        {
            SendMoveAction(_moveInput);
        }
    }

    // Called by the PlayerInput component when the "Move" action is triggered
    public void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
        _moveInput.y = -_moveInput.y;
    }

    // Called by the PlayerInput component when the "Interact" action is triggered
    public void OnInteract(InputValue value)
    {
        Debug.Log("OnInteract called");
        if (value.isPressed)
        {
            Debug.Log("Interact action is pressed");
            SendButtonAction("pick_up_drop");
        }
    }

    private void SendMoveAction(Vector2 move)
    {
        if (studyClient == null) return;
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = "movement",
            action_data = new System.Collections.Generic.List<float> { move.x, move.y },
            duration = Time.deltaTime,
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
            action_type = actionType,
            action_data = null,
            duration = 0.0f,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        Debug.Log("Sent Button Action: " + actionType);
    }
}