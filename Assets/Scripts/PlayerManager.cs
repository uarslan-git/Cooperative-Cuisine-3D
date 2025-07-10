using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json.Linq; // Or use UnityEngine.JsonUtility if you prefer

public class PlayerManager : MonoBehaviour
{
    public TextAsset serverJson; // Assign the JSON string from the server here for testing

    private Dictionary<string, GameObject> playerObjects = new Dictionary<string, GameObject>();

    void Start()
    {
        // Parse JSON (replace with actual server data in production)
        var data = JArray.Parse(serverJson.text);
        foreach (var recipe in data)
        {
            var layout = recipe["layout"];
            foreach (var kv in layout)
            {
                string objName = kv.Path;
                JArray posArr = (JArray)kv.First;
                Vector3 pos = new Vector3((float)posArr[0], 0, (float)posArr[1]);

                if (objName.StartsWith("Player"))
                {
                    GameObject prefab = Resources.Load<GameObject>($"Players/{objName.Split('_')[0]}");
                    if (prefab == null)
                        prefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);

                    GameObject playerObj = Instantiate(prefab, pos, Quaternion.identity);
                    playerObj.name = objName;
                    playerObj.AddComponent<PlayerController>(); // Add movement script
                    playerObjects[objName] = playerObj;
                }
            }
        }
    }
}
