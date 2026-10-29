using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

public static class RPVROrderSmokeTest
{
    public const string DirectoryPath = "/tmp/rpvr-orders";

    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "result.txt"), "RUNNING\n");
        new GameObject("Order Smoke Test").AddComponent<RPVROrderSmokeTestRunner>();
    }
}

public class RPVROrderSmokeTestRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        var manager = GameManager.Instance;
        var label = FindAnyObjectByType<OrderLabel>().GetComponent<TextMesh>();
        var tail = manager.Orders.Skip(1).ToArray();
        string previous = "";
        int changes = 0;
        float deadline = Time.time + 6f;
        var log = new System.Collections.Generic.List<string>();
        while (changes < 4 && Time.time < deadline)
        {
            string text = label.text;
            if (text != previous)
            {
                string expected = "ЗАКАЗ:\n" + string.Join(" + ", manager.CurrentOrder.items.Select(Name));
                if (text != expected || !manager.Orders.Skip(1).SequenceEqual(tail))
                {
                    File.WriteAllText(Path.Combine(RPVROrderSmokeTest.DirectoryPath, "result.txt"), "FAIL: Label or queue tail did not match the manager.\n" + string.Join("\n", log));
                    Destroy(gameObject);
                    yield break;
                }
                log.Add($"{Time.time:F2}: {text.Replace("\n", " ")}");
                ScreenCapture.CaptureScreenshot(Path.Combine(RPVROrderSmokeTest.DirectoryPath, $"order-{changes}.png"));
                previous = text;
                changes++;
            }
            yield return null;
        }
        File.WriteAllText(Path.Combine(RPVROrderSmokeTest.DirectoryPath, "result.txt"), (changes == 4 ? "PASS\n" : "FAIL: Expected four distinct transitions.\n") + string.Join("\n", log));
        Destroy(gameObject);
    }

    private static string Name(OrderItem item)
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
