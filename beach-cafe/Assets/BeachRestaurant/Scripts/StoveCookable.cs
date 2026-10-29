using System;
using System.Collections.Generic;
using UnityEngine;

public class StoveCookable : MonoBehaviour
{
    public const float ReadyStart = 0.8f;
    public const float ReadyEnd = 1.2f;
    [Min(0.01f)] public float secondsToCook = 30f;
    [Min(0f)] public float cookProgress;
    public bool IsCooking => stoves.Count > 0;
    public bool IsReady => cookProgress >= ReadyStart && cookProgress <= ReadyEnd;
    public bool IsOvercooked => cookProgress > ReadyEnd;

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
        if (previous < ReadyStart && cookProgress >= ReadyStart) Ready?.Invoke();
    }
}
