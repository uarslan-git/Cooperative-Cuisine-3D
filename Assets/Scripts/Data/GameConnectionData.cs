using System.Collections.Generic;

[System.Serializable]
public class GameConnectionData
{
    public Dictionary<string, PlayerInfo> player_info;
    public LevelInfo level_info;
}
