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
    private Vector2 _lastSentMoveInput;
    private float _lastMoveSentTime;
    private float _moveSendInterval = 0.016f; // Send movement every 16ms (60 FPS) for ultra-smooth network updates

    public GameManager gameManager;

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

        // Automatically find the GameManager in the scene.
        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }
        if (gameManager == null)
        {
            Debug.LogError("PlayerInputController could not find a GameManager in the scene!");
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput != null)
        {
            playerInput.actions.FindActionMap("Player").Enable();
        }
        else
        {
            Debug.LogError("PlayerInput component not found on the same GameObject as PlayerInputController!");
        }
    }

    void Update()
    {
        if (gameManager != null && gameManager.lastState != null && gameManager.lastState.ended)
        {
            // Don't process movement when game ended, but don't disable input entirely 
            // (UI buttons still need to work)
            return;
        }

        // Throttle movement sending and only send when input changes or at regular intervals
        bool shouldSendMovement = false;
        
        // Send if input changed even slightly (ultra-sensitive for immediate response)
        if (Vector2.Distance(_moveInput, _lastSentMoveInput) > 0.001f)
        {
            shouldSendMovement = true;
        }
        // Or send at regular intervals if still moving
        else if (_moveInput != Vector2.zero && Time.time - _lastMoveSentTime >= _moveSendInterval)
        {
            shouldSendMovement = true;
        }
        // Or send stop command when input becomes zero
        else if (_moveInput == Vector2.zero && _lastSentMoveInput != Vector2.zero)
        {
            shouldSendMovement = true;
        }
        
        if (shouldSendMovement)
        {
            SendMoveAction(_moveInput);
            _lastSentMoveInput = _moveInput;
            _lastMoveSentTime = Time.time;
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
        
        // CLIENT-SIDE PREDICTION: Move the player immediately in Unity
        if (controlledPlayerGameObject != null)
        {
            Vector3 movement = new Vector3(move.x * 1.8f * Time.deltaTime, 0, move.y * 1.8f * Time.deltaTime);
            controlledPlayerGameObject.transform.Translate(movement, Space.World);
            
            // Update facing direction
            if (move != Vector2.zero)
            {
                Vector3 facingDirection = new Vector3(move.x, 0, move.y).normalized;
                controlledPlayerGameObject.transform.rotation = Quaternion.LookRotation(facingDirection);
            }
        }
        
        // Still send action to server for authoritative state
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = "movement",
            action_data = new System.Collections.Generic.List<float> { move.x * 1.8f, move.y * 1.8f },
            duration = Time.deltaTime,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        // Debug.Log("Sent Move Action: " + move); // Commented out to reduce console spam
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

    // Called by the PlayerInput component for the new "InteractHold" action
    public void OnInteractHold(InputValue value)
    {
        if (value.isPressed)
        {
            SendInteractAction("keydown");
        }
        else
        {
            SendInteractAction("keyup");
        }
    }

    private void SendInteractAction(string actionData)
    {
        if (studyClient == null) return;
        Action action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = "interact",
            action_data = actionData,
            duration = 0.0f,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        Debug.Log("Sent Interact Action: " + actionData);
    }
}