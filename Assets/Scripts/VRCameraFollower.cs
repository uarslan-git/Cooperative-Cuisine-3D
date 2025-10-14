using UnityEngine;

/// <summary>
/// VR Camera Follower that positions the VR rig above the player character
/// </summary>
public class VRCameraFollower : MonoBehaviour
{
    [Header("Player Following Settings")]
    public bool followPlayer = false;
    public Vector3 offsetFromPlayer = new Vector3(0, 1.6f, 0); // Eye height offset
    public bool lockRotation = false;
    
    [Header("References")]
    public Transform playerTransform;
    
    private StudyClient studyClient;
    private GameManager gameManager;
    
    void Start()
    {
        // Find references
        studyClient = FindFirstObjectByType<StudyClient>();
        gameManager = FindFirstObjectByType<GameManager>();
        
        Debug.Log("VRCameraFollower initialized");
    }
    
    void Update()
    {
        if (followPlayer && playerTransform != null)
        {
            // Follow the player
            transform.position = playerTransform.position + offsetFromPlayer;
            
            if (!lockRotation)
            {
                // Optional: match player rotation (usually not needed for VR)
                transform.rotation = Quaternion.Euler(0, playerTransform.eulerAngles.y, 0);
            }
        }
        else if (followPlayer && playerTransform == null)
        {
            // Try to find the player
            FindPlayerCharacter();
        }
    }
    
    public void EnablePlayerFollowing()
    {
        followPlayer = true;
        FindPlayerCharacter();
        Debug.Log("VR Camera following enabled");
    }
    
    public void DisablePlayerFollowing()
    {
        followPlayer = false;
        Debug.Log("VR Camera following disabled");
    }
    
    private void FindPlayerCharacter()
    {
        // Method 1: Try to find by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
            Debug.Log($"Found player by tag: {playerObj.name}");
            return;
        }
        
        // Method 2: Look for PlayerInputController
        PlayerInputController playerController = FindFirstObjectByType<PlayerInputController>();
        if (playerController != null)
        {
            playerTransform = playerController.transform;
            Debug.Log($"Found player via PlayerInputController: {playerController.name}");
            return;
        }
        
        // Method 3: Look for GameObject with "Player" in name
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.ToLower().Contains("player") && obj.activeInHierarchy)
            {
                playerTransform = obj.transform;
                Debug.Log($"Found player by name: {obj.name}");
                return;
            }
        }
        
        // Method 4: Use GameManager if available
        if (gameManager != null && gameManager.transform != null)
        {
            // Check if GameManager has a reference to player
            playerTransform = gameManager.transform;
            Debug.Log($"Using GameManager as player reference: {gameManager.name}");
            return;
        }
        
        Debug.LogWarning("Could not find player character to follow");
    }
    
    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
        if (player != null)
        {
            Debug.Log($"VR Camera will follow: {player.name}");
        }
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offsetFromPlayer = newOffset;
    }
    
    void OnDrawGizmos()
    {
        // Draw connection line to player in Scene view
        if (followPlayer && playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, playerTransform.position);
            Gizmos.DrawWireSphere(playerTransform.position + offsetFromPlayer, 0.3f);
        }
    }
}
