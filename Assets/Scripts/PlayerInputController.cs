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
    private float _moveSendInterval = 0.1f; // Send movement every 100ms (10 FPS) to prevent server overload

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

        // Throttle movement sending - only send when direction changes or at intervals
        bool shouldSendMovement = false;
        
        // Send immediately if direction changed significantly
        if (Vector2.Distance(_moveInput, _lastSentMoveInput) > 0.1f)
        {
            shouldSendMovement = true;
        }
        // Or send at regular intervals if still moving in same direction
        else if (_moveInput != Vector2.zero && Time.time - _lastMoveSentTime >= _moveSendInterval)
        {
            shouldSendMovement = true;
        }
        // Or send stop command immediately when input becomes zero
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
        // Don't invert Y here - handle coordinate conversion in movement logic
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
        if (studyClient == null || controlledPlayerGameObject == null) return;
        
        // Convert Unity input to server coordinate system
        // Unity: X = left/right, Y = forward/back (W/S keys)
        // Server: X = left/right, Y = back/forward (inverted)
        Vector2 serverMove = new Vector2(move.x, -move.y); // Invert Y for server
        
        // SERVER-AUTHORITATIVE MOVEMENT: Only send input to server, let server handle movement
        // Unity will receive and apply the server's position updates through GameManager
        // No client-side movement - server controls everything for perfect sync
        
        // Update facing direction
        if (move != Vector2.zero)
        {
            Vector3 facingDirection = new Vector3(move.x, 0, move.y).normalized;
            controlledPlayerGameObject.transform.rotation = Quaternion.LookRotation(facingDirection);
        }
        
        // Send movement vectors to server (what the server expects)
        // Send normalized movement direction, not velocity - let server handle speed
        if (move != Vector2.zero)
        {
            Action action = new Action
            {
                player = studyClient.myPlayerId,
                action_type = "movement",
                action_data = new System.Collections.Generic.List<float> { serverMove.x, serverMove.y }, // Send direction only, not velocity
                duration = _moveSendInterval, // Use the send interval as duration
                player_hash = studyClient.myPlayerHash
            };
            studyClient.SendAction(action);
        }
        else
        {
            // Send zero movement to stop the server player
            Action stopAction = new Action
            {
                player = studyClient.myPlayerId,
                action_type = "movement",
                action_data = new System.Collections.Generic.List<float> { 0f, 0f },
                duration = _moveSendInterval,
                player_hash = studyClient.myPlayerHash
            };
            studyClient.SendAction(stopAction);
        }
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
    
    private bool IsValidPosition(Vector3 position)
    {
        // Basic collision detection for walls and boundaries
        if (gameManager != null && gameManager.lastState != null)
        {
            // Get kitchen boundaries from server state
            var kitchen = gameManager.lastState.kitchen;
            if (kitchen != null)
            {
                // Check boundaries (with small buffer for player size)
                float buffer = 0.4f; // Half player width
                if (position.x < buffer || position.x >= kitchen.width - buffer)
                    return false;
                if (position.z < buffer || position.z >= kitchen.height - buffer)  
                    return false;
            }
        }
        
        // Check collision with counters and other objects
        Collider[] colliders = Physics.OverlapSphere(position, 0.3f);
        foreach (Collider col in colliders)
        {
            // Ignore player colliders, only check static objects
            if (col.gameObject != controlledPlayerGameObject && 
                (col.gameObject.name.Contains("Counter") || 
                 col.gameObject.name.Contains("Stove") ||
                 col.gameObject.name.Contains("Sink")))
            {
                return false;
            }
        }
        
        return true;
    }
}