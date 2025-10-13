using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.UI;

/// <summary>
/// Simple VR pointer for interacting with UI elements
/// Attach this to your VR controller or hand
/// </summary>
public class VRUIPointer : MonoBehaviour
{
    [Header("Pointer Settings")]
    public Transform pointer;
    public LineRenderer lineRenderer;
    public float maxDistance = 10f;
    public LayerMask uiLayerMask = -1;
    
    [Header("Visual Settings")]
    public Color normalColor = Color.blue;
    public Color hoverColor = Color.green;
    public Color clickColor = Color.red;
    
    private Camera vrCamera;
    private GraphicRaycaster graphicRaycaster;
    private EventSystem eventSystem;
    private GameObject currentTarget;
    
    void Start()
    {
        SetupComponents();
    }
    
    void SetupComponents()
    {
        // Find VR camera
        vrCamera = Camera.main;
        if (vrCamera == null)
        {
            vrCamera = FindFirstObjectByType<Camera>();
        }
        
        // Setup line renderer if not assigned
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startWidth = 0.01f;
            lineRenderer.endWidth = 0.01f;
            lineRenderer.positionCount = 2;
        }
        
        // Find EventSystem
        eventSystem = FindFirstObjectByType<EventSystem>();
        
        Debug.Log("VR UI Pointer initialized");
    }
    
    void Update()
    {
        HandlePointing();
        HandleInput();
    }
    
    void HandlePointing()
    {
        if (pointer == null) return;
        
        // Cast ray from pointer
        Ray ray = new Ray(pointer.position, pointer.forward);
        RaycastHit hit;
        
        bool hitUI = false;
        
        // Check for UI hits
        if (Physics.Raycast(ray, out hit, maxDistance, uiLayerMask))
        {
            // Check if we hit a canvas
            Canvas canvas = hit.collider.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                hitUI = true;
                currentTarget = hit.collider.gameObject;
                
                // Update line renderer
                lineRenderer.SetPosition(0, pointer.position);
                lineRenderer.SetPosition(1, hit.point);
                lineRenderer.material.color = hoverColor;
            }
        }
        
        if (!hitUI)
        {
            // No UI hit - draw line to max distance
            lineRenderer.SetPosition(0, pointer.position);
            lineRenderer.SetPosition(1, pointer.position + pointer.forward * maxDistance);
            lineRenderer.material.color = normalColor;
            currentTarget = null;
        }
    }
    
    void HandleInput()
    {
        // Check for trigger press (VR controller input)
        bool triggerPressed = false;
        
        // Try different input methods
        if (Input.GetButtonDown("Fire1")) // Mouse click for testing
        {
            triggerPressed = true;
        }
        
        // XR Input (if available)
        InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerValue))
            {
                if (triggerValue)
                {
                    triggerPressed = true;
                }
            }
        }
        
        // Handle click
        if (triggerPressed && currentTarget != null)
        {
            // Try to click UI element
            var button = currentTarget.GetComponent<UnityEngine.UI.Button>();
            if (button != null && button.interactable)
            {
                button.onClick.Invoke();
                lineRenderer.material.color = clickColor;
                Debug.Log($"VR UI: Clicked button {button.name}");
            }
        }
    }
}
