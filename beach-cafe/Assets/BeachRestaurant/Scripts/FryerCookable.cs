using System;
using System.Collections.Generic;
using UnityEngine;

public class FryerCookable : MonoBehaviour
{
    [Min(0.01f)] public float secondsToFry = 30f;
    [Min(0f)] public float readyStart = 0.8f;
    [Min(0f)] public float readyEnd = 1.2f;
    [Min(0f)] public float fryProgress;

    public bool IsFrying => fryers.Count > 0;
    public bool IsReady => fryProgress >= readyStart && fryProgress <= readyEnd;
    public bool IsOvercooked => fryProgress > readyEnd;

    public event Action<float> ProgressChanged;
    public event Action Ready;

    private readonly HashSet<Fryer> fryers = new HashSet<Fryer>();

    internal void EnterFryer(Fryer fryer) => fryers.Add(fryer);
    internal void ExitFryer(Fryer fryer) => fryers.Remove(fryer);

    private void Update()
    {
        if (!IsFrying || secondsToFry <= 0f) return;

        float previous = fryProgress;
        fryProgress += Time.deltaTime / secondsToFry;
        ProgressChanged?.Invoke(fryProgress);

        bool crossedReadyStart = previous < readyStart && fryProgress >= readyStart;
        bool beganAtZeroReadyStart = readyStart <= 0f && previous <= 0f && fryProgress > 0f;
        if (crossedReadyStart || beganAtZeroReadyStart) Ready?.Invoke();
    }
}
