using System;
using System.Collections.Generic;
using UnityEngine;

public class StoveCookable : MonoBehaviour
{
    [Min(0.01f)] public float secondsToCook = 10f;
    [Min(0f)] public float cookProgress;

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
        if (previous < 1f && cookProgress >= 1f) Ready?.Invoke();
    }
}
