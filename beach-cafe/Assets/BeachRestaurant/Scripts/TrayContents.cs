using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public sealed class TrayContents : MonoBehaviour
{
    public TraySlot[] slots = Array.Empty<TraySlot>();
    [SerializeField] private List<ServingItem> items = new List<ServingItem>();
    public IReadOnlyList<ServingItem> Items => items;
    public bool HasBurger => Count(OrderItem.Burger) > 0;
    public bool HasFries => Count(OrderItem.Fries) > 0;
    public event Action ContentsChanged;

    private void Awake()
    {
        if (slots.Length == 0) slots = GetComponentsInChildren<TraySlot>(true);
        items.Clear();
    }

    private void OnEnable()
    {
        foreach (var slot in slots)
        {
            if (!slot) continue;
            slot.selectEntered.AddListener(OnPlaced);
            slot.selectExited.AddListener(OnRemoved);
        }
        Refresh();
    }

    public int Count(OrderItem type)
    {
        int count = 0;
        foreach (var item in items) if (item && item.itemType == type) count++;
        return count;
    }

    private void OnPlaced(SelectEnterEventArgs args) => Refresh();
    private void OnRemoved(SelectExitEventArgs args) => Refresh();

    private void Refresh()
    {
        items.Clear();
        foreach (var slot in slots)
            if (slot && slot.Item) items.Add(slot.Item);
        ContentsChanged?.Invoke();
    }

    private void OnDisable()
    {
        foreach (var slot in slots)
        {
            if (!slot) continue;
            slot.selectEntered.RemoveListener(OnPlaced);
            slot.selectExited.RemoveListener(OnRemoved);
        }
        items.Clear();
        ContentsChanged?.Invoke();
    }
}
