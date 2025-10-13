using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// VR Main Menu that provides a UI for connecting to the server
/// This should be attached to a Canvas in World Space
/// </summary>
public class VRMainMenu : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField serverUrlInputField;
    public Button connectButton;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI titleText;
    public GameObject menuPanel;
    
    [Header("Connection Settings")]
    public string defaultServerUrl = "682e89727181.ngrok-free.app";
    
    private StudyClient studyClient;
    private bool isConnecting = false;
    
    void Start()
    {
        // Find the StudyClient in the scene
        studyClient = FindFirstObjectByType<StudyClient>();
        
        SetupUI();
        
        // Disable the StudyClient's automatic connection
        if (studyClient != null)
        {
            studyClient.enabled = false;
        }
    }
    
    void SetupUI()
    {
        // Set default server URL
        if (serverUrlInputField != null)
        {
            serverUrlInputField.text = defaultServerUrl;
        }
        
        // Setup button
        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        }
        
        // Set initial status
        if (statusText != null)
        {
            statusText.text = "Enter server URL and click Connect";
            statusText.color = Color.white;
        }
        
        // Set title
        if (titleText != null)
        {
            titleText.text = "Cooperative Cuisine VR";
        }
    }
    
    public void OnConnectButtonClicked()
    {
        if (isConnecting) return;
        
        string serverUrl = serverUrlInputField.text.Trim();
        
        if (string.IsNullOrEmpty(serverUrl))
        {
            UpdateStatus("Please enter a server URL", Color.red);
            return;
        }
        
        StartCoroutine(ConnectToServer(serverUrl));
    }
    
    private IEnumerator ConnectToServer(string serverUrl)
    {
        isConnecting = true;
        
        // Update UI
        UpdateStatus("Connecting to server...", Color.yellow);
        connectButton.interactable = false;
        
        // Update StudyClient with new server URL
        if (studyClient != null)
        {
            // Remove "https://" if present
            serverUrl = serverUrl.Replace("https://", "").Replace("http://", "");
            studyClient.studyHost = serverUrl;
            
            // Enable and start the StudyClient
            studyClient.enabled = true;
            
            // Wait a moment for initialization
            yield return new WaitForSeconds(1f);
            
            // Start the study connection
            StartCoroutine(studyClient.StartStudy());
            
            // Monitor connection status
            yield return StartCoroutine(MonitorConnection());
        }
        else
        {
            UpdateStatus("StudyClient not found!", Color.red);
            isConnecting = false;
            connectButton.interactable = true;
        }
    }
    
    private IEnumerator MonitorConnection()
    {
        float timeout = 10f; // 10 second timeout
        float elapsed = 0f;
        
        while (elapsed < timeout && isConnecting)
        {
            // Check if we have a websocket connection
            if (studyClient != null && !string.IsNullOrEmpty(studyClient.myPlayerHash))
            {
                // Connection successful
                UpdateStatus("Connected! Starting game...", Color.green);
                yield return new WaitForSeconds(2f);
                
                // Hide the menu
                if (menuPanel != null)
                {
                    menuPanel.SetActive(false);
                }
                else
                {
                    gameObject.SetActive(false);
                }
                
                isConnecting = false;
                yield break;
            }
            
            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
        
        // Timeout or error
        if (isConnecting)
        {
            UpdateStatus("Connection failed. Please try again.", Color.red);
            connectButton.interactable = true;
            isConnecting = false;
        }
    }
    
    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
        
        Debug.Log($"VR Menu Status: {message}");
    }
    
    public void OnServerUrlChanged()
    {
        // Reset status when URL is changed
        if (statusText != null && !isConnecting)
        {
            statusText.text = "Enter server URL and click Connect";
            statusText.color = Color.white;
        }
    }
}
