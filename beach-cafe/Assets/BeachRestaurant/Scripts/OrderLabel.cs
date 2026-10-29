using System;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(TextMesh))]
public class OrderLabel : MonoBehaviour
{
    private GameManager manager;
    private TextMesh label;

    private void Awake() => label = GetComponent<TextMesh>();

    private void Start()
    {
        manager = GameManager.Instance;
        if (!manager)
        {
            Debug.LogError("OrderLabel requires a GameManager in the scene.", this);
            return;
        }
        manager.OrdersChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (manager) manager.OrdersChanged -= Refresh;
    }

    private void Refresh()
    {
        var order = manager.CurrentOrder;
        label.text = order == null || order.items.Count == 0
            ? "ЗАКАЗ:\nнет заказов"
            : "ЗАКАЗ:\n" + string.Join(" + ", order.items.Select(ItemName));
    }

    private static string ItemName(OrderItem item)
    {
        switch (item)
        {
            case OrderItem.Burger: return "бургер";
            case OrderItem.Fries: return "картошка";
            case OrderItem.Soda: return "напиток";
            default: return item.ToString();
        }
    }
}
