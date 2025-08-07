using System;
using System.Collections.Generic;

[Serializable]
public class StateRepresentation
{
    public List<PlayerState> players;
    public List<CounterState> counters;
    public KitchenInfo kitchen;
    public float score;
    public List<OrderState> orders;
    public bool ended;
    public string env_time;
    public float remaining_time;
    public List<ViewRestriction> view_restrictions;
    public List<string[]> served_meals;
    public List<string[]> info_msg;
    public bool all_players_ready;
}

[Serializable]
public class PlayerState
{
    public string id;
    public List<float> pos;
    public List<int> facing_direction;
    public ItemState holding;
    public List<float> current_nearest_counter_pos;
    public string current_nearest_counter_id;
    public PlayerInfoData player_info;
}

[Serializable]
public class PlayerInfoData
{
}

[Serializable]
public class CounterState
{
    public string id;
    public string category;
    public string type;
    public List<float> pos;
    public List<int> orientation;
    public ItemState occupied_by;
    public List<EffectState> active_effects;
}

[Serializable]
public class ItemState
{
    public string id;
    public string category;
    public string type;
    public float progress_percentage;
    public bool inverse_progress;
    public List<EffectState> active_effects;
    public List<ItemState> content_list;
    public ItemState content_ready;
}

[Serializable]
public class EffectState
{
    public string id;
    public string type;
    public float progress_percentage;
    public bool inverse_progress;
}

[Serializable]
public class KitchenInfo
{
    public int width;
    public int height;
}

[Serializable]
public class OrderState
{
    public string id;
    public string category;
    public string meal;
    public string start_time;
    public float max_duration;
    public float score;
}

[Serializable]
public class ViewRestriction
{
    public List<float> direction;
    public List<float> position;
    public int angle;
    public List<bool> counter_mask;
    public float range;
}