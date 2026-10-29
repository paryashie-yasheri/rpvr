using System;
using System.Collections.Generic;
using UnityEngine;

public class StoveCookable : MonoBehaviour
{
    [Min(0f)] public float readyStart = 0.8f;
    [Min(0f)] public float readyEnd = 1.2f;
    [Min(0f)] public float smokeStartProgress = 1f;
    [Min(0.01f)] public float secondsToCook = 30f;
    [Min(0f)] public float cookProgress;
    public bool IsCooking => stoves.Count > 0;
    public bool IsReady => cookProgress >= readyStart && cookProgress <= readyEnd;
    public bool IsOvercooked => cookProgress > readyEnd;

    public event Action<float> ProgressChanged;
    public event Action Ready;

    private readonly HashSet<Stove> stoves = new HashSet<Stove>();

    internal void EnterStove(Stove stove) => stoves.Add(stove);
    internal void ExitStove(Stove stove) => stoves.Remove(stove);

    private void Update()
    {
        if (stoves.Count == 0 || secondsToCook <= 0f) return;
        float previous = cookProgress;
        cookProgress += Time.deltaTime / secondsToCook;
        ProgressChanged?.Invoke(cookProgress);
        bool crossedReadyStart = previous < readyStart && cookProgress >= readyStart;
        bool beganCookingAtZeroReadyStart = readyStart <= 0f && previous <= 0f && cookProgress > 0f;
        if (crossedReadyStart || beganCookingAtZeroReadyStart) Ready?.Invoke();
    }
}
