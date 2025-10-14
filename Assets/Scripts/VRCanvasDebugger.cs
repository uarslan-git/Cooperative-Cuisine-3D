using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Debug script to help make VR canvas visible
/// Attach this to your Main Menu Canvas
/// </summary>
public class VRCanvasDebugger : MonoBehaviour
{
    [Header("Canvas Debug")]
    public bool autoPositionCanvas = true;
    public bool addBackgroundPanel = true;
    public Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    
    [Header("Manual Canvas Settings")]
    public Vector3 canvasPosition = new Vector3(0, 1.5f, 2f);
    public Vector3 canvasScale = new Vector3(0.01f, 0.01f, 0.01f); // Larger scale for better visibility
    
    void Start()
    {
        Debug.Log("=== VR CANVAS DEBUG ===");
        
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("No Canvas component found!");
            return;
        }
        
        // Check canvas settings
        Debug.Log($"Canvas Render Mode: {canvas.renderMode}");
        Debug.Log($"Canvas Position: {transform.position}");
        Debug.Log($"Canvas Scale: {transform.localScale}");
        Debug.Log($"Canvas Active: {gameObject.activeInHierarchy}");
        
        // Auto-fix canvas settings for VR (only if enabled)
        if (autoPositionCanvas)
        {
            FixCanvasForVR(canvas);
        }
        else
        {
            Debug.Log("Auto-positioning disabled - keeping manual Inspector values");
        }
        
        // Add visible background
        if (addBackgroundPanel)
        {
            AddVisibleBackground();
        }
        
        // Check for required components
        CheckComponents();
    }
    
    void FixCanvasForVR(Canvas canvas)
    {
        Debug.Log("Auto-fixing canvas for VR...");
        
        // Set to World Space
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Position in front of player
        transform.position = canvasPosition;
        transform.rotation = Quaternion.identity;
        
        // Scale for VR (adjustable)
        transform.localScale = canvasScale;
        
        // Ensure it's active
        gameObject.SetActive(true);
        
        Debug.Log("Canvas fixed for VR positioning");
    }
    
    void AddVisibleBackground()
    {
        // Check if we already have a background panel
        Transform bgPanel = transform.Find("Background Panel");
        if (bgPanel != null) return;
        
        // Create background panel
        GameObject panel = new GameObject("Background Panel");
        panel.transform.SetParent(transform, false);
        
        // Add Image component
        Image image = panel.AddComponent<Image>();
        image.color = backgroundColor;
        
        // Set to full size
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        // Move to back
        panel.transform.SetAsFirstSibling();
        
        Debug.Log("Added visible background panel");
    }
    
    void CheckComponents()
    {
        // Check for GraphicRaycaster
        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("Added missing GraphicRaycaster");
        }
        
        // Check for Canvas Scaler
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
            Debug.Log("Added CanvasScaler with ConstantPhysicalSize");
        }
    }
    
    void Update()
    {
        // Manual positioning with keyboard for testing
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKey(KeyCode.W)) transform.Translate(0, 0, 0.01f);
            if (Input.GetKey(KeyCode.S)) transform.Translate(0, 0, -0.01f);
            if (Input.GetKey(KeyCode.A)) transform.Translate(-0.01f, 0, 0);
            if (Input.GetKey(KeyCode.D)) transform.Translate(0.01f, 0, 0);
            if (Input.GetKey(KeyCode.Q)) transform.Translate(0, -0.01f, 0);
            if (Input.GetKey(KeyCode.E)) transform.Translate(0, 0.01f, 0);
        }
        
        // Log position when moved
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log($"Canvas Position: {transform.position}, Scale: {transform.localScale}");
        }
    }
}
