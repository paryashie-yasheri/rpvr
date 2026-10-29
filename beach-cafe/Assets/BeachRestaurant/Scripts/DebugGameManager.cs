using UnityEngine;

public class DebugGameManager : GameManager
{
    [Min(0.01f)] public float changeInterval = 1f;
    private int index;
    private float elapsed;
    private static readonly OrderItem[][] Pattern = {
        new[] { OrderItem.Burger, OrderItem.Fries, OrderItem.Soda },
        new[] { OrderItem.Fries },
        new[] { OrderItem.Soda, OrderItem.Burger }
    };

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        if (Orders.Count == 0)
        {
            foreach (var items in Pattern) EnqueueOrder(new Order(items));
        }
        else ReplaceCurrentOrder(new Order(Pattern[index]));
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float interval = Mathf.Max(0.01f, changeInterval);
        while (elapsed >= interval)
        {
            elapsed -= interval;
            index = (index + 1) % Pattern.Length;
            ReplaceCurrentOrder(new Order(Pattern[index]));
        }
    }
}
