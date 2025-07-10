using UnityEngine;
using System.Collections.Generic;

public class PlayerSpawner : MonoBehaviour
{
    // List of player prefab names (should match FBX/prefab names in Resources/Players/)
    public List<string> playerTypes = new List<string> { "Player1", "Player2" };

    // Example spawn positions for each player
    public List<Vector3> spawnPositions = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(2, 0, 2) };

    void Start()
    {
        for (int i = 0; i < playerTypes.Count; i++)
        {
            string playerType = playerTypes[i];
            GameObject prefab = Resources.Load<GameObject>($"Players/{playerType}");
            if (prefab != null)
            {
                Vector3 spawnPos = (i < spawnPositions.Count) ? spawnPositions[i] : Vector3.zero;
                GameObject playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
                playerObj.name = $"Player_{playerType}";
                // Optional: scale players if needed
                playerObj.transform.localScale = playerObj.transform.localScale * 0.5f;
            }
            else
            {
                Debug.LogWarning($"Player prefab not found: Players/{playerType}");
            }
        }
    }
}
