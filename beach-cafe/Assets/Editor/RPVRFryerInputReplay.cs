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
public static class RPVRFryerInputReplay
{
    public const string Output = "/tmp/rpvr-fryer-replay";
    private const string Pending = "RPVRFryerInputReplay.Pending";

    static RPVRFryerInputReplay() => EditorApplication.playModeStateChanged += OnPlayMode;

    [MenuItem("RPVR/Tests/Run Fryer Input Replay")]
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else EditorApplication.EnterPlaymode();
    }

    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        SessionState.SetBool(Pending, false);
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "result.txt"), "RUNNING");
        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            if (window.titleContent.text == "Project Settings") window.Close();
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
        new GameObject("Fryer Input Replay").AddComponent<RPVRFryerInputReplayRunner>();
    }
}

// Uses real simulator keyboard/mouse events, not transform changes or forced XRI selection.
public sealed class RPVRFryerInputReplayRunner : MonoBehaviour
{
    private Keyboard keyboard;
    private Mouse mouse;
    private XRDeviceSimulator simulator;
    private Vector3 originalSpeeds;
    private XRDeviceSimulator.Space originalKeyboardSpace;
    private Transform hand;
    private Transform playerRoot;
    private Vector3 playerStartPosition;
    private Key handKey = Key.Space;
    private bool trigger;
    private FryerBasket basket;
    private FryerCookable fries;
    private XRGrabInteractable grab;
    private readonly Dictionary<Canvas, bool> overlayStates = new Dictionary<Canvas, bool>();
    private readonly List<string> observations = new List<string>();
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

    private IEnumerator Move(Vector3 target, float tolerance = 0.03f)
    {
        float deadline = Time.realtimeSinceStartup + 25f;
        string latestInput = "no input sample";
        while (Vector3.Distance(hand.position, target) > tolerance)
        {
            Require(Time.realtimeSinceStartup < deadline,
                "Hand movement timed out: " + hand.position + " -> " + target + "; " + latestInput);
            float speed = Mathf.Clamp(Vector3.Distance(hand.position, target) * 2f, 0.02f, 0.45f);
            simulator.keyboardXTranslateSpeed = simulator.keyboardYTranslateSpeed = simulator.keyboardZTranslateSpeed = speed;
            var delta = target - hand.position;
            var keys = new List<Key> { handKey };
            if (delta.x > 0.015f) keys.Add(Key.D); else if (delta.x < -0.015f) keys.Add(Key.A);
            if (delta.y > 0.015f) keys.Add(Key.E); else if (delta.y < -0.015f) keys.Add(Key.Q);
            if (delta.z > 0.015f) keys.Add(Key.W); else if (delta.z < -0.015f) keys.Add(Key.S);
            Input(keys.ToArray());
            yield return null;
            latestInput = "keys=" + string.Join(",", keys) + " right=" + simulator.manipulatingRightController
                + " x=" + simulator.keyboardXTranslateAction.action.ReadValue<float>()
                + " y=" + simulator.keyboardYTranslateAction.action.ReadValue<float>()
                + " z=" + simulator.keyboardZTranslateAction.action.ReadValue<float>()
                + " targets=" + simulator.axis2DTargets;
        }
        Input(handKey);
    }

    private IEnumerator LookAtFryer()
    {
        Input(Key.U);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        var camera = Camera.main;
        var desired = Quaternion.LookRotation(basket.transform.position - camera.transform.position).eulerAngles;
        var current = camera.transform.eulerAngles;
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(
            Mathf.DeltaAngle(current.y, desired.y) / simulator.mouseXRotateSensitivity,
            -Mathf.DeltaAngle(current.x, desired.x) / simulator.mouseYRotateSensitivity) });
        yield return new WaitForSeconds(0.3f);
        Input();
        yield return new WaitForSeconds(0.2f);
        var viewport = camera.WorldToViewportPoint(basket.transform.position);
        Require(viewport.z > 0f && viewport.x > 0.05f && viewport.x < 0.95f && viewport.y > 0.05f && viewport.y < 0.95f,
            "Player could not look at the fryer while standing in place: " + viewport);
    }

    private IEnumerator Click()
    {
        trigger = true;
        Input(handKey);
        yield return new WaitForSeconds(0.35f);
    }

    private IEnumerator Replay()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        simulator = FindAnyObjectByType<XRDeviceSimulator>();
        originalSpeeds = new Vector3(simulator.keyboardXTranslateSpeed, simulator.keyboardYTranslateSpeed, simulator.keyboardZTranslateSpeed);
        originalKeyboardSpace = simulator.keyboardTranslateSpace;
        simulator.keyboardXTranslateSpeed = simulator.keyboardYTranslateSpeed = simulator.keyboardZTranslateSpeed = 0.2f;
        simulator.keyboardTranslateSpace = XRDeviceSimulator.Space.Parent;
        try { yield return Sequence(); }
        finally { Cleanup(); }
    }

    private IEnumerator Sequence()
    {
        yield return new WaitForSeconds(1f);
        playerRoot = GameObject.Find("XR Origin (XR Rig)").transform;
        playerStartPosition = playerRoot.position;
        basket = FindAnyObjectByType<FryerBasket>();
        fries = GameObject.Find("Fries").GetComponent<FryerCookable>();
        grab = fries.GetComponent<XRGrabInteractable>();
        Vector3 freeReleasePoint = fries.transform.position + Vector3.up * 0.25f;
        foreach (var canvas in simulator.GetComponentsInChildren<Canvas>())
        {
            overlayStates.Add(canvas, canvas.enabled);
            canvas.enabled = false;
        }
        Require(GameObject.Find("Liquid Oil Surface").GetComponent<Collider>() == null, "Oil has a solid collider");
        yield return LookAtFryer();
        Input(Key.Escape);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        Input(Key.Y);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        Require(simulator.manipulatingRightController, "Right controller was not selected after turning to the fryer.");
        hand = GameObject.Find("Right Controller").transform;
        Input(Key.Space, Key.Digit3);
        yield return new WaitForSeconds(0.3f);
        // Approach from above, then lower the hand onto the item. A diagonal
        // descent can be blocked by the counter lip when the cook stands inside the ring.
        yield return Move(fries.transform.position + Vector3.up * 0.32f);
        yield return Move(fries.transform.position + Vector3.up * 0.08f);
        yield return Click();
        Require(IsHeld(), "Right trigger could not grab fries");
        yield return DropIntoOil();
        Record("01-cooking");

        var hover = basket.transform.Find("Fryer Basket Handle").position + Vector3.up * 0.04f;
        yield return Move(hover);
        yield return new WaitForSeconds(0.3f);
        Require(basket.IsHighlighted, "Right hand did not highlight the basket");
        var outline = basket.GetComponent<HoverOutline>();
        Require(outline && outline.IsVisible && outline.RendererCount > 0, "Fryer hover outline is not visible");
        Record("02-right-hover");
        yield return Click();
        Require(IsHeld(), "Basket click did not transfer fries to right hand");
        Require(!fries.IsFrying, "Retrieved fries are still cooking");
        Require(!fries.transform.Find("Cooking Progress").gameObject.activeSelf, "Progress stayed visible after retrieval");
        Require(Vector3.Distance(fries.transform.position, hand.position) < 0.25f, "Fries were not teleported to the hand");
        Record("03-right-retrieved");

        yield return DropIntoOil();
        yield return Move(hover);
        Input(Key.Escape);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        handKey = Key.LeftShift;
        hand = GameObject.Find("Left Controller").transform;
        yield return Move(hover + Vector3.right * 0.03f);
        yield return new WaitForSeconds(0.3f);
        Require(basket.Interaction.interactorsHovering.Count >= 2, "Both hands did not hover the basket");
        Record("04-both-hover");
        handKey = Key.Space;
        hand = GameObject.Find("Right Controller").transform;
        yield return Move(hover + Vector3.up * 0.4f);
        Require(basket.IsHighlighted, "Highlight disappeared while left hand was still hovering");
        Require(outline.IsVisible, "Outline disappeared while the left hand was still hovering");
        handKey = Key.LeftShift;
        hand = GameObject.Find("Left Controller").transform;
        yield return Click();
        Require(IsHeld() && !fries.IsFrying, "Left hand did not retrieve fries");
        Record("05-left-retrieved");
        // Release over the supply counter; dropping onto the tray correctly
        // socket-selects fries and would not exercise a free physics release.
        yield return Move(freeReleasePoint);
        trigger = false;
        Input(handKey);
        yield return new WaitForSeconds(0.8f);
        Require(!grab.isSelected && !grab.GetComponent<Rigidbody>().isKinematic, "Retrieved fries did not release dynamically");
        Require(!basket.IsHighlighted, "Highlight stayed on after both hands left");
        Require(Vector3.Distance(playerRoot.position, playerStartPosition) < 0.01f,
            "Player moved away from the center during the fryer replay.");
        Require(!outline.IsVisible, "Outline stayed on after both hands left");
        Record("06-released");
        yield return new WaitForEndOfFrame();
        File.WriteAllText(Path.Combine(RPVRFryerInputReplay.Output, "result.txt"), "PASS\n" + string.Join("\n", observations));
        Debug.Log("RPVR fryer input replay passed: drop in oil, both-hand hover, retrieve to either hand, release.");
    }

    private IEnumerator DropIntoOil()
    {
        yield return Move(hand.position + Vector3.up * 0.25f);
        Vector3 offset = hand.position - fries.transform.position;
        var oil = GameObject.Find("Fryer Cooking Zone").GetComponent<Collider>().bounds;
        Vector3 dropPoint = oil.center + Vector3.up * 0.34f;
        yield return Move(dropPoint + offset);
        // Let grab velocity settle so release drops the portion instead of throwing it.
        yield return new WaitForSeconds(0.5f);
        trigger = false;
        Input(handKey);
        yield return new WaitForSeconds(0.25f);
        yield return Move(hand.position + Vector3.up * 0.2f);
        yield return new WaitForSeconds(0.8f);
        Require(fries.IsFrying && fries.fryProgress > 0f, "Dropped fries did not enter the oil");
        Require(fries.transform.position.y < 1.19f, "Fries did not sink below the oil surface");
    }

    private bool IsHeld() => hand.GetComponentInChildren<NearFarInteractor>().interactablesSelected.Contains(grab);

    private void Record(string stage)
    {
        var point = Camera.main.WorldToViewportPoint(basket.transform.position);
        Require(point.z > 0 && point.x > 0 && point.x < 1 && point.y > 0 && point.y < 1, "Fryer is outside the player's view");
        observations.Add(stage + " progress=" + fries.fryProgress + " hand=" + hand.position + " fries=" + fries.transform.position);
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRFryerInputReplay.Output, stage + ".png"));
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        File.WriteAllText(Path.Combine(RPVRFryerInputReplay.Output, "result.txt"), "FAIL: " + message + "\n" + string.Join("\n", observations));
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRFryerInputReplay.Output, "failure.png"));
        throw new InvalidOperationException(message);
    }

    private void OnDestroy() => Cleanup();
    private void Cleanup()
    {
        if (inputConfigured)
        {
            Application.runInBackground = oldBackground;
            InputSystem.settings.backgroundBehavior = oldInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorInput;
            inputConfigured = false;
        }
        foreach (var entry in overlayStates) if (entry.Key) entry.Key.enabled = entry.Value;
        overlayStates.Clear();
        if (simulator)
        {
            simulator.keyboardXTranslateSpeed = originalSpeeds.x;
            simulator.keyboardYTranslateSpeed = originalSpeeds.y;
            simulator.keyboardZTranslateSpeed = originalSpeeds.z;
            simulator.keyboardTranslateSpace = originalKeyboardSpace;
        }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
    }
}
