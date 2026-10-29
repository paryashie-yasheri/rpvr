using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>Runs the actual simulator keyboard/mouse controls and records the observed result.</summary>
[InitializeOnLoad]
public static class RPVRBurgerInputReplay
{
    public const string OutputDirectory = "/tmp/rpvr-burger-replay";
    public const string TracePath = "Tools/burger-input-replay.json";
    private const string PendingKey = "RPVRBurgerInputReplay.Pending";
    private const string RecordKey = "RPVRBurgerInputReplay.Record";

    static RPVRBurgerInputReplay()
    {
        EditorApplication.playModeStateChanged += StartWhenPlaying;
    }

    [MenuItem("RPVR/Tests/Run Burger Input Replay")]
    public static void Run()
    {
        Launch(false);
    }

    [MenuItem("RPVR/Tests/Record Burger Input Replay")]
    public static void Record() => Launch(true);

    private static void Launch(bool record)
    {
        SessionState.SetBool(RecordKey, record);
        SessionState.SetBool(PendingKey, true);
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        else EditorApplication.isPlaying = true;
    }

    private static void StartWhenPlaying(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            return;
        }
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        SessionState.SetBool(PendingKey, false);
        StartReplay();
    }

    private static void StartReplay()
    {
        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllText(Path.Combine(OutputDirectory, "result.txt"), "RUNNING\n");
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.titleContent.text == "Project Settings") window.Close();
        var game = Type.GetType("UnityEditor.GameView,UnityEditor");
        EditorWindow.GetWindow(game).Focus();
        var runner = new GameObject("Burger Input Replay").AddComponent<RPVRBurgerInputReplayRunner>();
        runner.record = SessionState.GetBool(RecordKey, false);
    }
}

public sealed class RPVRBurgerInputReplayRunner : MonoBehaviour
{
    [Serializable] private class ReplayEvent { public float time; public string keys; public bool trigger; public string checkpoint; }
    [Serializable] private class ReplayTrace { public List<ReplayEvent> events = new List<ReplayEvent>(); }
    public bool record;
    private ReplayTrace trace = new ReplayTrace();
    private float startedAt;
    private string previousInput;
    private bool previousTrigger;
    private float nextCapture;
    private int frameNumber;
    private bool finished;
    private Keyboard keyboard;
    private Mouse mouse;
    private Transform hand;
    private StoveCookable patty;
    private XRGrabInteractable grab;
    private NearFarInteractor interactor;
    private bool trigger;
    private readonly List<string> observations = new List<string>();

    private void Start() => StartCoroutine(Replay());

    private void Input(params Key[] keys)
    {
        string keyText = string.Join(",", keys.Select(key => ((int)key).ToString()));
        if (record && (keyText != previousInput || trigger != previousTrigger))
        {
            trace.events.Add(new ReplayEvent { time = Time.time - startedAt, keys = keyText, trigger = trigger });
            previousInput = keyText;
            previousTrigger = trigger;
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        var state = new MouseState();
        if (trigger) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
    }

    private IEnumerator Move(Vector3 target, float timeout = 20f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (Vector3.Distance(hand.position, target) > 0.035f)
        {
            if (Time.realtimeSinceStartup > deadline)
                Require(false, $"Movement timed out: hand={hand.position}, target={target}");
            Vector3 delta = target - hand.position;
            var parent = Camera.main.transform.parent;
            if (parent) delta = parent.InverseTransformDirection(delta);
            var keys = new List<Key> { Key.Space };
            if (delta.x > 0.02f) keys.Add(Key.D); else if (delta.x < -0.02f) keys.Add(Key.A);
            if (delta.y > 0.02f) keys.Add(Key.E); else if (delta.y < -0.02f) keys.Add(Key.Q);
            if (delta.z > 0.02f) keys.Add(Key.W); else if (delta.z < -0.02f) keys.Add(Key.S);
            Input(keys.ToArray());
            yield return null;
        }
        Input(Key.Space);
    }

    private IEnumerator Replay()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            startedAt = Time.time;
            if (record) yield return RunSequence();
            else yield return PlayTrace();
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (mouse != null) InputSystem.RemoveDevice(mouse);
        }
    }

    private void Update()
    {
        if (record || finished || !Application.isPlaying || Time.time < nextCapture) return;
        nextCapture = Time.time + 0.2f;
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, $"frame-{frameNumber++:D4}.png"));
    }

    private IEnumerator PlayTrace()
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, RPVRBurgerInputReplay.TracePath);
        if (!File.Exists(path))
        {
            File.WriteAllText(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, "result.txt"), "FAIL: replay trace missing: " + path);
            yield break;
        }
        trace = JsonUtility.FromJson<ReplayTrace>(File.ReadAllText(path));
        hand = GameObject.Find("Right Controller").transform;
        patty = GameObject.Find("Patty").GetComponent<StoveCookable>();
        grab = patty.GetComponent<XRGrabInteractable>();
        interactor = hand.GetComponentInChildren<NearFarInteractor>();
        var bun = GameObject.Find("Bottom Bun");
        var assembly = bun.GetComponent<BurgerAssembly>();
        foreach (var e in trace.events)
        {
            while (Time.time - startedAt < e.time) yield return null;
            if (!string.IsNullOrEmpty(e.checkpoint))
            {
                if (e.checkpoint == "preview") Require(assembly.PreviewObject && assembly.PreviewIngredient == patty.GetComponent<BurgerIngredient>(), "Patty preview missing during replay");
                if (e.checkpoint == "snapped") Require(assembly.IngredientCount == 2 && patty.GetComponentInParent<Rigidbody>() == assembly.AssemblyBody, "Patty did not snap during replay");
                if (e.checkpoint == "moved") Require(interactor.interactablesSelected.Contains(assembly.AssemblyBody.GetComponent<XRGrabInteractable>()), "Burger was not held during replay");
                if (e.checkpoint == "top-preview") Require(assembly.PreviewObject && assembly.PreviewIngredient == GameObject.Find("Top Bun").GetComponent<BurgerIngredient>(), "Top bun preview missing during replay");
                if (e.checkpoint == "complete") Require(assembly.IngredientCount == 3 && assembly.AssemblyBody.GetComponentsInChildren<Rigidbody>().Length == 1, "Burger did not assemble into one body during replay");
                Record(e.checkpoint);
            }
            else
            {
                trigger = e.trigger;
                var keys = string.IsNullOrEmpty(e.keys) ? Array.Empty<Key>() : e.keys.Split(',').Select(value => (Key)int.Parse(value)).ToArray();
                Input(keys);
            }
        }
        finished = true;
        File.WriteAllText(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, "result.txt"), "PASS (recorded keyboard and mouse events replayed)\n" + string.Join("\n", observations));
    }

    private IEnumerator RunSequence()
    {
        yield return new WaitForSeconds(1f);
        hand = GameObject.Find("Right Controller").transform;
        patty = GameObject.Find("Patty").GetComponent<StoveCookable>();
        grab = patty.GetComponent<XRGrabInteractable>();
        interactor = hand.GetComponentInChildren<NearFarInteractor>();
        var bun = GameObject.Find("Bottom Bun");
        var assembly = bun.GetComponent<BurgerAssembly>();
        Input(Key.Space, Key.Digit3);
        yield return new WaitForSeconds(0.2f);
        Input(Key.Space);
        yield return new WaitForSeconds(0.2f);
        yield return Move(patty.transform.position + Vector3.up * 0.03f);
        yield return new WaitForSeconds(0.3f);
        trigger = true;
        Input(Key.Space);
        yield return new WaitForSeconds(0.5f);
        Require(interactor.interactablesSelected.Contains(grab), "Could not grab the patty with the trigger");
        Vector3 offset = hand.position - patty.transform.position;
        yield return Move(hand.position + Vector3.up * 0.3f);
        var destination = bun.GetComponent<Collider>().bounds.center + Vector3.up * (bun.GetComponent<Collider>().bounds.extents.y + patty.GetComponent<Collider>().bounds.extents.y + 0.05f);
        yield return Move(destination + offset);
        yield return new WaitForSeconds(0.5f);
        Require(assembly.PreviewIngredient == patty.GetComponent<BurgerIngredient>() && assembly.PreviewObject != null, "Snap preview did not appear");
        Require(assembly.PreviewObject.GetComponentsInChildren<Collider>().Length == 0, "Preview has a collider");
        Record("preview");
        float progress = patty.cookProgress;
        trigger = false;
        Input(Key.Space);
        yield return new WaitForSeconds(1f);
        Require(assembly.IngredientCount == 2, "Patty did not snap to bun");
        Require(patty.cookProgress == progress, "Patty cooking progress changed during snapping");
        Require(assembly.AssemblyBody && patty.GetComponentInParent<Rigidbody>() == assembly.AssemblyBody, "Patty is not part of the assembly body");
        Require(assembly.AssemblyBody.GetComponentsInChildren<Rigidbody>().Length == 1, "Assembly has more than one Rigidbody");
        Require(!assembly.AssemblyBody.isKinematic && assembly.AssemblyBody.detectCollisions, "Assembly body is not dynamic with collisions enabled");
        Require(assembly.AssemblyBody.GetComponent<XRGrabInteractable>().colliders.Contains(patty.GetComponent<Collider>()), "Patty collider is not grabbable on the assembly");
        Record("snapped");
        yield return Move(patty.transform.position + Vector3.up * 0.03f);
        yield return new WaitForSeconds(0.3f);
        trigger = true;
        Input(Key.Space);
        yield return new WaitForSeconds(0.5f);
        Require(interactor.interactablesSelected.Contains(assembly.AssemblyBody.GetComponent<XRGrabInteractable>()), "Could not grab the assembled burger by the patty");
        Vector3 startBun = bun.transform.position;
        Vector3 startPatty = patty.transform.position;
        yield return Move(hand.position + Vector3.up * 0.35f);
        yield return new WaitForSeconds(0.5f);
        Require(Vector3.Distance(startBun, bun.transform.position) > 0.15f, "Bun did not move when grabbed");
        Require(Vector3.Distance(startPatty, patty.transform.position) > 0.15f, "Patty did not move with bun");
        Require(Vector3.Distance(patty.transform.position - bun.transform.position, startPatty - startBun) < 0.06f, "Burger parts moved independently");
        Record("moved");
        trigger = false;
        Input(Key.Space);
        yield return new WaitForSeconds(1f);
        Require(Mathf.Abs(bun.transform.position.y - startBun.y) < 0.12f, "Released burger did not settle on the counter");
        var top = GameObject.Find("Top Bun");
        var topGrab = top.GetComponent<XRGrabInteractable>();
        yield return Move(top.transform.position + Vector3.up * 0.03f);
        yield return new WaitForSeconds(0.3f);
        trigger = true;
        Input(Key.Space);
        yield return new WaitForSeconds(0.5f);
        Require(interactor.interactablesSelected.Contains(topGrab), "Could not grab top bun");
        Vector3 topOffset = hand.position - top.transform.position;
        yield return Move(hand.position + Vector3.up * 0.3f);
        var pattyCollider = patty.GetComponent<Collider>();
        var topCollider = top.GetComponent<Collider>();
        var topDestination = pattyCollider.bounds.center + Vector3.up * (pattyCollider.bounds.extents.y + topCollider.bounds.extents.y + 0.05f);
        yield return Move(topDestination + topOffset);
        yield return new WaitForSeconds(0.3f);
        Require(assembly.PreviewIngredient == top.GetComponent<BurgerIngredient>(), "Top bun preview did not appear");
        Record("top-preview");
        trigger = false;
        Input(Key.Space);
        yield return new WaitForSeconds(1f);
        Require(assembly.IngredientCount == 3, "Top bun did not attach");
        Require(assembly.AssemblyBody.GetComponentsInChildren<Rigidbody>().Length == 1, "Completed burger has more than one Rigidbody");
        Require(top.GetComponentInParent<Rigidbody>() == assembly.AssemblyBody, "Top bun is not part of the assembly body");
        Record("complete");
        File.WriteAllText(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, "recording.json"), JsonUtility.ToJson(trace, true));
        File.WriteAllText(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, "result.txt"), "RECORDED\n" + string.Join("\n", observations));
    }

    private void Record(string stage)
    {
        if (record && stage != "failure") trace.events.Add(new ReplayEvent { time = Time.time - startedAt, checkpoint = stage });
        observations.Add($"{stage} time={Time.time:F2} hand={hand.position} patty={patty.transform.position} progress={patty.cookProgress:F3}");
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, stage + ".png"));
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        finished = true;
        Record("failure");
        File.WriteAllText(Path.Combine(RPVRBurgerInputReplay.OutputDirectory, "result.txt"), "FAIL: " + message + "\n" + string.Join("\n", observations));
        throw new InvalidOperationException(message);
    }
}
