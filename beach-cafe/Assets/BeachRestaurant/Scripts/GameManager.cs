using System;
using System.Collections.Generic;
using UnityEngine;

public enum OrderItem { Burger, Fries, Soda }

[Serializable]
public class Order
{
    public List<OrderItem> items = new List<OrderItem>();

    public Order(params OrderItem[] items) => this.items = new List<OrderItem>(items);
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private List<Order> initialOrders = new List<Order>();
    private readonly Queue<Order> orders = new Queue<Order>();
    public IReadOnlyCollection<Order> Orders => orders;
    public Order CurrentOrder => orders.Count > 0 ? orders.Peek() : null;
    public event Action OrdersChanged;

    protected virtual void Awake()
    {
        if (Instance && Instance != this)
        {
            Debug.LogError("Only one GameManager can be active.", this);
            enabled = false;
            return;
        }
        Instance = this;
        foreach (var order in initialOrders) if (order != null) orders.Enqueue(order);
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void EnqueueOrder(Order order)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
        orders.Enqueue(order);
        OrdersChanged?.Invoke();
    }

    public Order CompleteCurrentOrder()
    {
        if (orders.Count == 0) return null;
        var order = orders.Dequeue();
        OrdersChanged?.Invoke();
        return order;
    }

    protected void ReplaceCurrentOrder(Order order)
    {
        if (order == null) throw new ArgumentNullException(nameof(order));
        var remaining = orders.ToArray();
        orders.Clear();
        orders.Enqueue(order);
        for (int i = 1; i < remaining.Length; i++) orders.Enqueue(remaining[i]);
        OrdersChanged?.Invoke();
    }
}
