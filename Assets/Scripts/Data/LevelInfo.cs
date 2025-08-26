using System;
using System.Collections.Generic;

[Serializable]
public class LevelInfo
{
    public string name;
    public bool last_level;
    public List<object> recipe_graphs; // Using object for now, can be more specific if needed
    public int number_players;
    public List<float> kitchen_size;
}