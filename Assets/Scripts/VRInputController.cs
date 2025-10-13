using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevices = UnityEngine.XR.InputDevices;

/// <summary>
/// VR Input Controller for handling Meta Quest 3 hand controllers and input
/// </summary>
public class VRInputController : MonoBehaviour 
{
    [Header("VR Input Settings")]
    public bool enableVRInput = true;
    public float thumbstickDeadzone = 0.2f;
    
    [Header("References")]
    public PlayerInputController playerInputController;
    public StudyClient studyClient;
    
    [Header("Hand Controllers")]
    public Transform leftHandController;
    public Transform rightHandController;
    
    // VR Input Actions
    private InputAction leftThumbstickAction;
    private InputAction rightThumbstickAction;
    private InputAction leftTriggerAction;
    private InputAction rightTriggerAction;
    private InputAction leftGripAction;
    private InputAction rightGripAction;
    private InputAction leftPrimaryButtonAction;
    private InputAction rightPrimaryButtonAction;
    private InputAction leftSecondaryButtonAction;
    private InputAction rightSecondaryButtonAction;
    
    // Input state
    private Vector2 currentMoveInput;
    private bool leftTriggerPressed;
    private bool rightTriggerPressed;
    private bool leftGripPressed;
    private bool rightGripPressed;
    
    void Start()
    {
        // Find references if not assigned
        if (playerInputController == null)
        {
            playerInputController = FindFirstObjectByType<PlayerInputController>();
        }
        
        if (studyClient == null)
        {
            studyClient = FindFirstObjectByType<StudyClient>();
        }
        
        // Initialize VR input actions
        InitializeVRInput();
    }
    
    void OnEnable()
    {
        EnableVRInputActions();
    }
    
    void OnDisable()
    {
        DisableVRInputActions();
    }
    
    private void InitializeVRInput()
    {
        // Create input actions for VR controllers
        leftThumbstickAction = new InputAction("LeftThumbstick", InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
        rightThumbstickAction = new InputAction("RightThumbstick", InputActionType.Value, "<XRController>{RightHand}/thumbstick");
        
        leftTriggerAction = new InputAction("LeftTrigger", InputActionType.Button, "<XRController>{LeftHand}/trigger");
        rightTriggerAction = new InputAction("RightTrigger", InputActionType.Button, "<XRController>{RightHand}/trigger");
        
        leftGripAction = new InputAction("LeftGrip", InputActionType.Button, "<XRController>{LeftHand}/grip");
        rightGripAction = new InputAction("RightGrip", InputActionType.Button, "<XRController>{RightHand}/grip");
        
        leftPrimaryButtonAction = new InputAction("LeftPrimary", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
        rightPrimaryButtonAction = new InputAction("RightPrimary", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
        
        leftSecondaryButtonAction = new InputAction("LeftSecondary", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
        rightSecondaryButtonAction = new InputAction("RightSecondary", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
        
        // Set up callbacks
        leftThumbstickAction.performed += OnLeftThumbstick;
        leftThumbstickAction.canceled += OnLeftThumbstick;
        
        rightThumbstickAction.performed += OnRightThumbstick;
        rightThumbstickAction.canceled += OnRightThumbstick;
        
        leftTriggerAction.performed += OnLeftTrigger;
        leftTriggerAction.canceled += OnLeftTriggerRelease;
        
        rightTriggerAction.performed += OnRightTrigger;
        rightTriggerAction.canceled += OnRightTriggerRelease;
        
        leftGripAction.performed += OnLeftGrip;
        leftGripAction.canceled += OnLeftGripRelease;
        
        rightGripAction.performed += OnRightGrip;
        rightGripAction.canceled += OnRightGripRelease;
        
        leftPrimaryButtonAction.performed += OnLeftPrimaryButton;
        rightPrimaryButtonAction.performed += OnRightPrimaryButton;
        
        leftSecondaryButtonAction.performed += OnLeftSecondaryButton;
        rightSecondaryButtonAction.performed += OnRightSecondaryButton;
    }
    
    private void EnableVRInputActions()
    {
        if (!enableVRInput) return;
        
        leftThumbstickAction?.Enable();
        rightThumbstickAction?.Enable();
        leftTriggerAction?.Enable();
        rightTriggerAction?.Enable();
        leftGripAction?.Enable();
        rightGripAction?.Enable();
        leftPrimaryButtonAction?.Enable();
        rightPrimaryButtonAction?.Enable();
        leftSecondaryButtonAction?.Enable();
        rightSecondaryButtonAction?.Enable();
    }
    
    private void DisableVRInputActions()
    {
        leftThumbstickAction?.Disable();
        rightThumbstickAction?.Disable();
        leftTriggerAction?.Disable();
        rightTriggerAction?.Disable();
        leftGripAction?.Disable();
        rightGripAction?.Disable();
        leftPrimaryButtonAction?.Disable();
        rightPrimaryButtonAction?.Disable();
        leftSecondaryButtonAction?.Disable();
        rightSecondaryButtonAction?.Disable();
    }
    
    void Update()
    {
        // Send movement input to PlayerInputController
        if (playerInputController != null && enableVRInput)
        {
            // Apply deadzone to thumbstick input
            Vector2 processedInput = ApplyDeadzone(currentMoveInput);
            
            // Send movement through existing PlayerInputController system
            SendMoveInputToController(processedInput);
        }
        
        // Update hand controller positions if available
        UpdateHandControllers();
    }
    
    private Vector2 ApplyDeadzone(Vector2 input)
    {
        if (input.magnitude < thumbstickDeadzone)
        {
            return Vector2.zero;
        }
        
        // Normalize the input beyond the deadzone
        return input.normalized * ((input.magnitude - thumbstickDeadzone) / (1f - thumbstickDeadzone));
    }
    
    private void SendMoveInputToController(Vector2 moveInput)
    {
        // Create a mock InputValue to send to the PlayerInputController
        if (playerInputController != null)
        {
            // We'll use reflection or direct access to set the move input
            // For now, we'll use the public method if it exists, or access the private field
            var moveInputField = typeof(PlayerInputController).GetField("_moveInput", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (moveInputField != null)
            {
                moveInputField.SetValue(playerInputController, moveInput);
            }
        }
    }
    
    private void UpdateHandControllers()
    {
        // Update hand controller transforms if they exist
        if (leftHandController != null)
        {
            // Get left controller position and rotation from XR
            XRInputDevice leftDevice = XRInputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftDevice.isValid)
            {
                if (leftDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 leftPos))
                {
                    leftHandController.position = leftPos;
                }
                if (leftDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion leftRot))
                {
                    leftHandController.rotation = leftRot;
                }
            }
        }
        
        if (rightHandController != null)
        {
            // Get right controller position and rotation from XR
            XRInputDevice rightDevice = XRInputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightDevice.isValid)
            {
                if (rightDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 rightPos))
                {
                    rightHandController.position = rightPos;
                }
                if (rightDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion rightRot))
                {
                    rightHandController.rotation = rightRot;
                }
            }
        }
    }
    
    // Input callback methods
    private void OnLeftThumbstick(InputAction.CallbackContext context)
    {
        currentMoveInput = context.ReadValue<Vector2>();
    }
    
    private void OnRightThumbstick(InputAction.CallbackContext context)
    {
        Vector2 rightInput = context.ReadValue<Vector2>();
        // Right thumbstick can be used for camera rotation if needed
        // For now, we'll use it for looking around or other actions
    }
    
    private void OnLeftTrigger(InputAction.CallbackContext context)
    {
        leftTriggerPressed = true;
        // Trigger can be used for interact actions
        TriggerInteract();
    }
    
    private void OnLeftTriggerRelease(InputAction.CallbackContext context)
    {
        leftTriggerPressed = false;
    }
    
    private void OnRightTrigger(InputAction.CallbackContext context)
    {
        rightTriggerPressed = true;
        // Right trigger can also be used for interact
        TriggerInteract();
    }
    
    private void OnRightTriggerRelease(InputAction.CallbackContext context)
    {
        rightTriggerPressed = false;
    }
    
    private void OnLeftGrip(InputAction.CallbackContext context)
    {
        leftGripPressed = true;
        // Grip can be used for hold interactions
        TriggerInteractHold();
    }
    
    private void OnLeftGripRelease(InputAction.CallbackContext context)
    {
        leftGripPressed = false;
        TriggerInteractRelease();
    }
    
    private void OnRightGrip(InputAction.CallbackContext context)
    {
        rightGripPressed = true;
        TriggerInteractHold();
    }
    
    private void OnRightGripRelease(InputAction.CallbackContext context)
    {
        rightGripPressed = false;
        TriggerInteractRelease();
    }
    
    private void OnLeftPrimaryButton(InputAction.CallbackContext context)
    {
        // X button on left controller (Quest 3)
        TriggerAttack();
    }
    
    private void OnRightPrimaryButton(InputAction.CallbackContext context)
    {
        // A button on right controller (Quest 3)
        TriggerInteract();
    }
    
    private void OnLeftSecondaryButton(InputAction.CallbackContext context)
    {
        // Y button on left controller (Quest 3)
        // Could be used for menu or other actions
    }
    
    private void OnRightSecondaryButton(InputAction.CallbackContext context)
    {
        // B button on right controller (Quest 3)
        // Could be used for cancel or other actions
    }
    
    // Action methods that interface with the existing game systems
    private void TriggerInteract()
    {
        SendButtonAction("pick_up_drop");
    }
    
    private void TriggerInteractHold()
    {
        SendInteractAction("keydown");
    }
    
    private void TriggerInteractRelease()
    {
        SendInteractAction("keyup");
    }
    
    private void TriggerAttack()
    {
        // Use interact action for attack since they're similar in this game
        TriggerInteract();
    }
    
    // Send button actions directly to StudyClient (same as PlayerInputController does)
    private void SendButtonAction(string actionType)
    {
        if (studyClient == null) return;
        
        var action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = actionType,
            action_data = null,
            duration = 0.0f,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        Debug.Log($"VR Sent Button Action: {actionType}");
    }
    
    // Send interact actions directly to StudyClient (same as PlayerInputController does)
    private void SendInteractAction(string actionData)
    {
        if (studyClient == null) return;
        
        var action = new Action
        {
            player = studyClient.myPlayerId,
            action_type = "interact",
            action_data = actionData,
            duration = 0.0f,
            player_hash = studyClient.myPlayerHash
        };
        studyClient.SendAction(action);
        Debug.Log($"VR Sent Interact Action: {actionData}");
    }
    
    void OnDestroy()
    {
        DisableVRInputActions();
        
        // Dispose of input actions
        leftThumbstickAction?.Dispose();
        rightThumbstickAction?.Dispose();
        leftTriggerAction?.Dispose();
        rightTriggerAction?.Dispose();
        leftGripAction?.Dispose();
        rightGripAction?.Dispose();
        leftPrimaryButtonAction?.Dispose();
        rightPrimaryButtonAction?.Dispose();
        leftSecondaryButtonAction?.Dispose();
        rightSecondaryButtonAction?.Dispose();
    }
}
