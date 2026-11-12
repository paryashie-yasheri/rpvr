using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[InitializeOnLoad]
public static class RPVRTrayInputReplay
{
    public const string Output = "/tmp/rpvr-tray-replay";
    private const string Pending = "RPVRTrayInputReplay.Pending";
    static RPVRTrayInputReplay() => EditorApplication.playModeStateChanged += OnPlay;

    [MenuItem("RPVR/Tests/Run Tray Input Replay")]
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        RPVRBurgerInputReplay.Run();
        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING: assembling burger with recorded input");
        new GameObject("Tray Input Replay").AddComponent<RPVRTrayInputReplayRunner>();
    }
}

public sealed class RPVRTrayInputReplayRunner : MonoBehaviour
{
    private Keyboard keyboard;
    private Mouse mouse;
    private XRDeviceSimulator simulator;
    private Vector3 oldSpeeds;
    private bool configured;
    private bool oldBackground;
    private bool inputConfigured;
    private InputSettings.BackgroundBehavior oldBackgroundInput;
    private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    private Transform hand;
    private NearFarInteractor interactor;
    private bool trigger;
    private TrayContents contents;
    private readonly List<string> observations = new List<string>();
    private readonly Dictionary<Canvas, bool> overlayStates = new Dictionary<Canvas, bool>();
    private int changes;

    private void Start()
    {
        oldBackground = Application.runInBackground;
        oldBackgroundInput = InputSystem.settings.backgroundBehavior;
        oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        inputConfigured = true;
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        StartCoroutine(Replay());
    }

    private void Input(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        var state = new MouseState();
        if (trigger) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
    }

    private IEnumerator Move(Vector3 target, bool head = false)
    {
        var moving = head ? Camera.main.transform : hand;
        float deadline = Time.realtimeSinceStartup + 25f;
        while (Vector3.Distance(moving.position, target) > 0.03f)
        {
            Require(Time.realtimeSinceStartup < deadline, "Movement timed out: " + moving.position + " -> " + target);
            var delta = moving.InverseTransformDirection(target - moving.position);
            var keys = new List<Key>();
            if (!head) keys.Add(Key.Space);
            if (delta.x > 0.015f) keys.Add(Key.D); else if (delta.x < -0.015f) keys.Add(Key.A);
            if (delta.y > 0.015f) keys.Add(Key.E); else if (delta.y < -0.015f) keys.Add(Key.Q);
            if (delta.z > 0.015f) keys.Add(Key.W); else if (delta.z < -0.015f) keys.Add(Key.S);
            Input(keys.ToArray());
            yield return null;
        }
        if (head) Input(); else Input(Key.Space);
    }

    private IEnumerator LookAtTray()
    {
        Input(Key.U);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        yield return Move(new Vector3(-0.1f, 1.8f, 0.05f), true);
        var camera = Camera.main;
        var desired = Quaternion.LookRotation(contents.transform.position - camera.transform.position).eulerAngles;
        var current = camera.transform.eulerAngles;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(
            Mathf.DeltaAngle(current.y, desired.y) / simulator.mouseXRotateSensitivity,
            -Mathf.DeltaAngle(current.x, desired.x) / simulator.mouseYRotateSensitivity) });
        yield return new WaitForSeconds(0.3f);
        Input();
        yield return new WaitForSeconds(0.2f);
        Input(Key.Space);
    }

    private IEnumerator Grab(XRGrabInteractable item)
    {
        yield return Move(item.transform.position + Vector3.up * 0.04f);
        yield return new WaitForSeconds(0.25f);
        trigger = true;
        Input(Key.Space);
        yield return new WaitForSeconds(0.4f);
        Require(interactor.interactablesSelected.Contains(item), "Could not grab " + item.name);
        Require(!item.GetComponent<Rigidbody>().isKinematic, "Held item is not dynamic");
        yield return Move(hand.position + Vector3.up * 0.25f);
    }

    private IEnumerator Place(XRGrabInteractable item, TraySlot slot, string stage)
    {
        Vector3 offset = hand.position - item.transform.position;
        yield return Move(slot.GetAttachTransform(item).position + offset + Vector3.up * 0.065f);
        yield return new WaitForSeconds(0.5f);
        Require(slot.interactablesHovered.Contains(item), "Correct slot did not offer a snap preview");
        Require(slot.Item == null, "Socket stole the food before release");
        Record(stage + "-preview");
        trigger = false;
        Input(Key.Space);
        yield return new WaitForSeconds(0.8f);
        Require(slot.Item == item.GetComponent<ServingItem>(), "Released food did not snap to " + slot.name);
        Require(Vector3.Distance(item.transform.position, slot.GetAttachTransform(item).position) < 0.01f, "Snapped food is not aligned to its slot");
        Record(stage + "-snapped");
        yield return Move(hand.position + Vector3.up * 0.25f);
    }

    private IEnumerator Replay()
    {
        try { yield return Sequence(); }
        finally { Cleanup(); }
    }

    private IEnumerator Sequence()
    {
        yield return new WaitForSeconds(1f);
        float deadline = Time.realtimeSinceStartup + 100f;
        while (true)
        {
            Require(Time.realtimeSinceStartup < deadline, "Burger input replay did not finish");
            string path = Path.Combine(RPVRBurgerInputReplay.OutputDirectory, "result.txt");
            string result = File.Exists(path) ? File.ReadAllText(path) : "";
            Require(!result.StartsWith("FAIL"), "Burger assembly replay failed: " + result);
            if (result.StartsWith("PASS")) break;
            yield return new WaitForSeconds(0.2f);
        }
        yield return null;
        simulator = FindAnyObjectByType<XRDeviceSimulator>();
        oldSpeeds = new Vector3(simulator.keyboardXTranslateSpeed, simulator.keyboardYTranslateSpeed, simulator.keyboardZTranslateSpeed);
        configured = true;
        simulator.keyboardXTranslateSpeed = simulator.keyboardYTranslateSpeed = simulator.keyboardZTranslateSpeed = 0.35f;
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hand = GameObject.Find("Right Controller").transform;
        interactor = hand.GetComponentInChildren<NearFarInteractor>();
        contents = GameObject.Find("Serving Tray").GetComponent<TrayContents>();
        contents.ContentsChanged += OnContentsChanged;
        foreach (var canvas in simulator.GetComponentsInChildren<Canvas>()) { overlayStates.Add(canvas, canvas.enabled); canvas.enabled = false; }
        var burger = GameObject.Find("Bottom Bun").GetComponent<BurgerAssembly>();
        Require(burger.IsComplete, "Burger is not complete");
        var burgerGrab = burger.AssemblyBody.GetComponent<XRGrabInteractable>();
        var friesGrab = GameObject.Find("Fries").GetComponent<XRGrabInteractable>();
        var burgerSlot = contents.slots[0];
        var friesSlot = contents.slots[1];
        Require(!burgerSlot.CanHover((IXRHoverInteractable)friesGrab) && !friesSlot.CanHover((IXRHoverInteractable)burgerGrab), "Slots accept the wrong food type");
        var cup = GameObject.Find("Cup").GetComponent<XRGrabInteractable>();
        Require(!burgerSlot.CanHover((IXRHoverInteractable)cup) && !friesSlot.CanHover((IXRHoverInteractable)cup), "Burger/fries slots accept soda");
        yield return LookAtTray();
        Record("01-empty");
        yield return Grab(burgerGrab);
        yield return Place(burgerGrab, burgerSlot, "02-burger");
        Require(contents.HasBurger && !contents.HasFries && contents.Items.Count == 1, "Tray did not track placed burger");
        yield return Grab(burgerGrab);
        Require(!contents.HasBurger && contents.Items.Count == 0, "Tray did not remove burger from contents on pickup");
        Record("03-burger-removed");
        yield return new WaitForSeconds(0.8f);
        yield return Place(burgerGrab, burgerSlot, "04-burger-returned");
        yield return Grab(friesGrab);
        yield return Place(friesGrab, friesSlot, "05-fries");
        Require(contents.HasBurger && contents.HasFries && contents.Items.Count == 2, "Tray did not track both foods");
        yield return Grab(friesGrab);
        Require(!contents.HasFries && contents.HasBurger && contents.Items.Count == 1, "Removing fries changed the wrong slot");
        Record("06-fries-removed");
        yield return new WaitForSeconds(0.8f);
        yield return Place(friesGrab, friesSlot, "07-complete");
        Require(contents.Count(OrderItem.Burger) == 1 && contents.Count(OrderItem.Fries) == 1 && changes >= 6, "Tray contents/count events are incorrect");
        yield return new WaitForEndOfFrame();
        File.WriteAllText(Path.Combine(RPVRTrayInputReplay.Output, "result.txt"), "PASS\n" + string.Join("\n", observations));
        Debug.Log("RPVR tray input replay passed: typed slots, preview, snap, remove, replace, live tray contents.");
    }

    private void OnContentsChanged() => changes++;
    private void Record(string stage)
    {
        var point = Camera.main.WorldToViewportPoint(contents.transform.position);
        Require(point.z > 0 && point.x > 0.1f && point.x < 0.9f && point.y > 0.1f && point.y < 0.9f, "Player cannot see the tray");
        observations.Add(stage + " burger=" + contents.HasBurger + " fries=" + contents.HasFries + " count=" + contents.Items.Count);
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRTrayInputReplay.Output, stage + ".png"));
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        File.WriteAllText(Path.Combine(RPVRTrayInputReplay.Output, "result.txt"), "FAIL: " + message + "\n" + string.Join("\n", observations));
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRTrayInputReplay.Output, "failure.png"));
        throw new InvalidOperationException(message);
    }

    private void OnDestroy() => Cleanup();
    private void Cleanup()
    {
        if (inputConfigured)
        {
            Application.runInBackground = oldBackground;
            InputSystem.settings.backgroundBehavior = oldBackgroundInput;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            inputConfigured = false;
        }
        if (contents) contents.ContentsChanged -= OnContentsChanged;
        foreach (var entry in overlayStates) if (entry.Key) entry.Key.enabled = entry.Value;
        overlayStates.Clear();
        if (simulator && configured)
        {
            simulator.keyboardXTranslateSpeed = oldSpeeds.x;
            simulator.keyboardYTranslateSpeed = oldSpeeds.y;
            simulator.keyboardZTranslateSpeed = oldSpeeds.z;
        }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
    }
}
