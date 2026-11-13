using UnityEngine;

public sealed class ServingItem : MonoBehaviour
{
    public OrderItem itemType;

    public bool CanPlaceOnTray
    {
        get
        {
            if (itemType != OrderItem.Burger) return true;
            var assembly = GetComponentInChildren<BurgerAssembly>();
            return assembly && assembly.IsComplete;
        }
    }
}
