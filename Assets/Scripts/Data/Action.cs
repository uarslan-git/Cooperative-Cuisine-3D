using Newtonsoft.Json;

[System.Serializable]
public class Action
{
    public string player;
    [JsonProperty("action_type")]
    public string action_type;
    public object action_data;
    public float duration;
    public string player_hash;
}

