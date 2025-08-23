
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

[Serializable]
public class StateRepresentation : IEquatable<StateRepresentation>
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

    public bool Equals(StateRepresentation other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return players.SequenceEqual(other.players) &&
               counters.SequenceEqual(other.counters) &&
               kitchen.Equals(other.kitchen) &&
               score == other.score &&
               orders.SequenceEqual(other.orders) &&
               ended == other.ended &&
               remaining_time == other.remaining_time;
    }

    public override bool Equals(object obj) => Equals(obj as StateRepresentation);
    public override int GetHashCode() => base.GetHashCode();
}

[Serializable]
public class PlayerState : IEquatable<PlayerState>
{
    public string id;
    public List<float> pos;
    public List<float> facing_direction;
    [JsonConverter(typeof(JsonSubtypeConverter<ItemState>))]
    public ItemState holding;
    public List<float> current_nearest_counter_pos;
    public string current_nearest_counter_id;
    public PlayerInfoData player_info;

    public bool Equals(PlayerState other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return id == other.id &&
               pos.SequenceEqual(other.pos) &&
               facing_direction.SequenceEqual(other.facing_direction) &&
               object.Equals(holding, other.holding);
    }
    public override bool Equals(object obj) => Equals(obj as PlayerState);
    public override int GetHashCode() => id.GetHashCode();
}

[Serializable]
public class PlayerInfoData
{
}

[Serializable]
public class CounterState : IEquatable<CounterState>
{
    public string id;
    public string category;
    public string type;
    public List<float> pos;
    public List<int> orientation;
    [JsonConverter(typeof(SingleOrArrayOfSubtypeConverter<ItemState>))]
    public List<ItemState> occupied_by;
    public List<EffectState> active_effects;

    public bool Equals(CounterState other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return id == other.id && occupied_by.SequenceEqual(other.occupied_by);
    }
    public override bool Equals(object obj) => Equals(obj as CounterState);
    public override int GetHashCode() => id.GetHashCode();
}

[Serializable]
public class ItemState : IEquatable<ItemState>
{
    public string id;
    public string category;
    public string type;
    public float progress_percentage;
    public bool inverse_progress;
    public List<EffectState> active_effects;

    public bool Equals(ItemState other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return id == other.id && progress_percentage == other.progress_percentage;
    }

    public override bool Equals(object obj) => Equals(obj as ItemState);
    public override int GetHashCode() => id.GetHashCode();
}

[Serializable]
public class CookingEquipmentState : ItemState
{
    [JsonConverter(typeof(SingleOrArrayOfSubtypeConverter<ItemState>))]
    public List<ItemState> content_list;
    [JsonConverter(typeof(JsonSubtypeConverter<ItemState>))]
    public ItemState content_ready;
}

[Serializable]
public class EffectState : IEquatable<EffectState>
{
    public string id;
    public string type;
    public float progress_percentage;
    public bool inverse_progress;

    public bool Equals(EffectState other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return id == other.id && progress_percentage == other.progress_percentage;
    }

    public override bool Equals(object obj) => Equals(obj as EffectState);
    public override int GetHashCode() => id.GetHashCode();
}

[Serializable]
public class KitchenInfo : IEquatable<KitchenInfo>
{
    public int width;
    public int height;

    public bool Equals(KitchenInfo other)
    {
        if (other is null) return false;
        return width == other.width && height == other.height;
    }

    public override bool Equals(object obj) => Equals(obj as KitchenInfo);
    public override int GetHashCode() => (width, height).GetHashCode();
}

[Serializable]
public class OrderState : IEquatable<OrderState>
{
    public string id;
    public string category;
    public string meal;
    public string start_time;
    public float max_duration;
    public float score;

    public bool Equals(OrderState other)
    {
        if (other is null) return false;
        return id == other.id;
    }

    public override bool Equals(object obj) => Equals(obj as OrderState);
    public override int GetHashCode() => id.GetHashCode();
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
