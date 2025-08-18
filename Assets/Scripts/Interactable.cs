using UnityEngine;

// A base class for any object that the player can interact with.
// Counters, cutting boards, etc., should inherit from this class.
public class Interactable : MonoBehaviour
{
    // Called when the player presses the "PickUp" key (E) while this is the nearest interactable.
    public virtual void OnPickUp(PlayerController player)
    {
        // Base implementation does nothing. Override in child classes.
        Debug.Log(gameObject.name + " was picked up by " + player.name);
    }

    // Called when the player presses the "Interact" key (F) while this is the nearest interactable.
    public virtual void OnInteract(PlayerController player)
    {
        // Base implementation does nothing. Override in child classes.
        Debug.Log(gameObject.name + " was interacted with by " + player.name);
    }
}
