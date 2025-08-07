[System.Serializable]
public class WebsocketMessage
{
    public string type;
    public string player_hash;
    public Action action;
    public StateRepresentation state;
    public UpdatePlayerInfo player_info_update;
}

[System.Serializable]
public class UpdatePlayerInfo
{
    public string name;
}
