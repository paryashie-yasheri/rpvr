using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

[InitializeOnLoad]
public static class RPVRBellInputReplay
{
    public const string Output = "/tmp/rpvr-bell-replay";
    private const string Pending = "RPVRBellInputReplay.Pending";
    static RPVRBellInputReplay() => EditorApplication.playModeStateChanged += OnPlay;

    [MenuItem("RPVR/Tests/Run Bell Input Replay")]
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else EditorApplication.EnterPlaymode();
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () => EditorApplication.EnterPlaymode();
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        SessionState.SetBool(Pending, false);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING");
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.titleContent.text == "Project Settings") window.Close();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        new GameObject("Bell Input Replay").AddComponent<RPVRBellInputReplayRunner>();
    }
}

public sealed class RPVRBellInputReplayRunner : MonoBehaviour
{
    private Keyboard keyboard;
    private Mouse mouse;
    private XRDeviceSimulator simulator;
    private Vector3 oldSpeeds;
    private XRDeviceSimulator.Space oldKeyboardSpace;
    private bool configured;
    private Transform hand;
    private Key handKey = Key.Space;
    private bool trigger;
    private HoverPressButton button;
    private AudioSource bellAudio;
    private UnityEvent probeA;
    private UnityEvent probeB;
    private readonly List<int> callbacks = new List<int>();
    private readonly Dictionary<Canvas, bool> overlays = new Dictionary<Canvas, bool>();
    private bool oldBackground;
    private InputSettings.BackgroundBehavior oldInputBackground;
    private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;
    private bool inputConfigured;

    private void Start()
    {
        oldBackground = Application.runInBackground;
        oldInputBackground = InputSystem.settings.backgroundBehavior;
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

    private IEnumerator Move(Vector3 target)
    {
        var moving = hand;
        float deadline = Time.realtimeSinceStartup + 25f;
        while (Vector3.Distance(moving.position, target) > 0.03f)
        {
            Require(Time.realtimeSinceStartup < deadline, "Movement timed out: " + moving.position + " -> " + target);
            float speed = Mathf.Clamp(Vector3.Distance(moving.position, target) * 2f, 0.02f, 0.4f);
            simulator.keyboardXTranslateSpeed = simulator.keyboardYTranslateSpeed = simulator.keyboardZTranslateSpeed = speed;
            var delta = moving.parent.InverseTransformDirection(target - moving.position);
            var keys = new List<Key>();
            keys.Add(handKey);
            if (delta.x > 0.015f) keys.Add(Key.D); else if (delta.x < -0.015f) keys.Add(Key.A);
            if (delta.y > 0.015f) keys.Add(Key.E); else if (delta.y < -0.015f) keys.Add(Key.Q);
            if (delta.z > 0.015f) keys.Add(Key.W); else if (delta.z < -0.015f) keys.Add(Key.S);
            Input(keys.ToArray());
            yield return null;
        }
        Input(handKey);
    }

    private IEnumerator LookAtBell()
    {
        Input(Key.U);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        var camera = Camera.main;
        var target = button.pressVisual ? button.pressVisual.position : button.transform.position;
        var desired = Quaternion.LookRotation(target - camera.transform.position).eulerAngles;
        var current = camera.transform.eulerAngles;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(
            Mathf.DeltaAngle(current.y, desired.y) / simulator.mouseXRotateSensitivity,
            -Mathf.DeltaAngle(current.x, desired.x) / simulator.mouseYRotateSensitivity) });
        yield return new WaitForSeconds(0.3f);
        Input();
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator Click()
    {
        trigger = true;
        Input(handKey);
        yield return new WaitForSeconds(0.05f);
    }

    private IEnumerator Release()
    {
        trigger = false;
        Input(handKey);
        yield return new WaitForSeconds(0.2f);
    }

    private IEnumerator Replay()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        try { yield return Sequence(); }
        finally { Cleanup(); }
    }

    private IEnumerator Sequence()
    {
        yield return new WaitForSeconds(1f);
        simulator = FindAnyObjectByType<XRDeviceSimulator>();
        oldSpeeds = new Vector3(simulator.keyboardXTranslateSpeed, simulator.keyboardYTranslateSpeed, simulator.keyboardZTranslateSpeed);
        oldKeyboardSpace = simulator.keyboardTranslateSpace;
        simulator.keyboardTranslateSpace = XRDeviceSimulator.Space.Parent;
        configured = true;
        simulator.keyboardXTranslateSpeed = simulator.keyboardYTranslateSpeed = simulator.keyboardZTranslateSpeed = 0.4f;
        foreach (var canvas in simulator.GetComponentsInChildren<Canvas>()) { overlays.Add(canvas, canvas.enabled); canvas.enabled = false; }
        button = GameObject.Find("Table Bell").GetComponent<HoverPressButton>();
        Require(button != null, "Bell interaction is not configured");
        bellAudio = button.GetComponent<AudioSource>();
        Require(bellAudio != null, "Bell AudioSource is missing");
        Require(bellAudio.clip && !bellAudio.playOnAwake && !bellAudio.isPlaying, "Bell audio is missing or plays automatically");
        Require(button.onPressed.Count > 0 && button.onPressed[0].GetPersistentEventCount() > 0 && button.onPressed[0].GetPersistentTarget(0) == bellAudio && button.onPressed[0].GetPersistentMethodName(0) == "Play", "AudioSource.Play is not assigned to the UnityEvent list");
        probeA = new UnityEvent();
        probeB = new UnityEvent();
        probeA.AddListener(() => { Require(bellAudio.isPlaying, "Sound did not play before the next event"); callbacks.Add(1); });
        probeB.AddListener(() => callbacks.Add(2));
        button.onPressed.Add(probeA);
        button.onPressed.Add(probeB);
        yield return LookAtBell();
        Input(Key.Escape);
        yield return new WaitForSeconds(0.2f);
        Input(Key.Y);
        yield return new WaitForSeconds(0.2f);
        hand = GameObject.Find("Right Controller").transform;
        Input(Key.Space, Key.Digit3);
        yield return new WaitForSeconds(0.2f);
        yield return Move(button.transform.position + new Vector3(-0.115f, 0.12f, -0.015f));
        yield return new WaitForSeconds(0.2f);
        Require(button.IsHighlighted, "Right hand did not highlight the bell");
        var outline = button.GetComponent<HoverOutline>();
        Require(outline && outline.IsVisible && outline.RendererCount > 0 && outline.outlineMaterial.shader.isSupported,
            "Hover outline is missing or its shader is unsupported");
        Record("01-right-hover");
        Vector3 rest = button.pressVisual.position;
        yield return Click();
        Require(callbacks.Count == 2 && callbacks[0] == 1 && callbacks[1] == 2, "Press event list was not invoked in order");
        Require(Vector3.Dot(rest - button.pressVisual.position, button.transform.up) > 0.001f, "Bell plunger did not depress");
        Record("02-ringing");
        yield return new WaitForSeconds(0.6f);
        Require(callbacks.Count == 2, "Holding the trigger repeatedly rang the bell");
        yield return Release();
        yield return Click();
        Require(callbacks.Count == 4, "Second press did not ring again");
        yield return Release();

        Input(Key.Escape);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        handKey = Key.LeftShift;
        hand = GameObject.Find("Left Controller").transform;
        yield return Move(button.transform.position + new Vector3(0.115f, 0.12f, -0.015f));
        yield return new WaitForSeconds(0.2f);
        Require(button.Interaction.interactorsHovering.Count >= 2, "Both hands could not hover the bell");
        handKey = Key.Space;
        hand = GameObject.Find("Right Controller").transform;
        yield return Move(hand.position + Vector3.up * 0.4f);
        Require(button.IsHighlighted, "Highlight disappeared while the left hand still hovered");
        handKey = Key.LeftShift;
        hand = GameObject.Find("Left Controller").transform;
        yield return Click();
        Require(callbacks.Count == 6 && bellAudio.isPlaying, "Left hand did not ring the bell");
        Record("03-left-ringing");
        yield return Release();
        yield return Move(hand.position + Vector3.up * 0.4f);
        yield return new WaitForSeconds(0.2f);
        Require(!button.IsHighlighted, "Highlight stayed on after both hands left");
        Require(!outline.IsVisible, "Outline stayed visible after both hands left");
        Record("04-hover-cleared");
        yield return new WaitForEndOfFrame();
        File.WriteAllText(Path.Combine(RPVRBellInputReplay.Output, "result.txt"), "PASS: both-hand hover, press/release, one invocation per press, ordered UnityEvents, assigned audio playback, plunger animation.\n");
        Debug.Log("RPVR bell input replay passed.");
    }

    private void Record(string stage)
    {
        var target = button.pressVisual ? button.pressVisual.position : button.transform.position;
        var camera = Camera.main;
        var p = camera.WorldToViewportPoint(target);
        Require(p.z > 0 && p.x > 0.1f && p.x < 0.9f && p.y > 0.1f && p.y < 0.9f, "Player cannot see the bell");
        Vector3 direction = target - camera.transform.position;
        if (Physics.Raycast(camera.transform.position, direction.normalized, out var hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
            Require(hit.transform.IsChildOf(button.transform), "Bell is occluded by " + hit.collider.name);
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRBellInputReplay.Output, stage + ".png"));
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        File.WriteAllText(Path.Combine(RPVRBellInputReplay.Output, "result.txt"), "FAIL: " + message);
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRBellInputReplay.Output, "failure.png"));
        throw new InvalidOperationException(message);
    }

    private void OnDestroy() => Cleanup();
    private void Cleanup()
    {
        if (button) { button.onPressed.Remove(probeA); button.onPressed.Remove(probeB); }
        foreach (var entry in overlays) if (entry.Key) entry.Key.enabled = entry.Value;
        overlays.Clear();
        if (configured && simulator)
        {
            simulator.keyboardXTranslateSpeed = oldSpeeds.x;
            simulator.keyboardYTranslateSpeed = oldSpeeds.y;
            simulator.keyboardZTranslateSpeed = oldSpeeds.z;
            simulator.keyboardTranslateSpace = oldKeyboardSpace;
        }
        configured = false;
        if (inputConfigured)
        {
            Application.runInBackground = oldBackground;
            InputSystem.settings.backgroundBehavior = oldInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            inputConfigured = false;
        }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
    }
}
