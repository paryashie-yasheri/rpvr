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
public static class RPVRStoveInputReplay
{
    public const string OutputDirectory = "/tmp/rpvr-stove-replay";
    private const string PendingKey = "RPVRStoveInputReplay.Pending";

    static RPVRStoveInputReplay()
    {
        EditorApplication.playModeStateChanged += StartWhenPlaying;
    }

    [MenuItem("RPVR/Tests/Run Stove Input Replay")]
    public static void Run()
    {
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
        new GameObject("Stove Input Replay").AddComponent<RPVRStoveInputReplayRunner>();
    }
}

public sealed class RPVRStoveInputReplayRunner : MonoBehaviour
{
    private Keyboard keyboard;
    private Mouse mouse;
    private Transform hand;
    private StoveCookable patty;
    private XRGrabInteractable grab;
    private NearFarInteractor interactor;
    private bool trigger;
    private bool ready;
    private bool heardReady;
    private int readyCount;
    private AudioSource stoveAudio;
    private readonly List<string> observations = new List<string>();

    private void Start() => StartCoroutine(Replay());

    private void Input(params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        var state = new MouseState();
        if (trigger) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
    }

    private IEnumerator Move(Vector3 target, float timeout = 20f)
    {
        float deadline = Time.time + timeout;
        while (Vector3.Distance(hand.position, target) > 0.035f)
        {
            if (Time.time > deadline)
                Require(false, $"Movement timed out: hand={hand.position}, target={target}");
            Vector3 delta = target - hand.position;
            // The simulator's Local keyboard translation follows the controller rotation.
            delta = hand.InverseTransformDirection(delta);
            var keys = new List<Key> { Key.Space };
            if (delta.x > 0.02f) keys.Add(Key.D); else if (delta.x < -0.02f) keys.Add(Key.A);
            if (delta.y > 0.02f) keys.Add(Key.E); else if (delta.y < -0.02f) keys.Add(Key.Q);
            if (delta.z > 0.02f) keys.Add(Key.W); else if (delta.z < -0.02f) keys.Add(Key.S);
            Input(keys.ToArray());
            yield return null;
        }
        Input(Key.Space);
    }

    // Use the simulator's FPS-mode mouse input to turn the player's actual view toward the grill.
    private IEnumerator LookAt(Vector3 target)
    {
        Input(Key.U);
        yield return new WaitForSeconds(0.2f);
        Input();
        yield return new WaitForSeconds(0.2f);
        var camera = Camera.main;
        var simulator = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
        var desired = Quaternion.LookRotation(target - camera.transform.position).eulerAngles;
        var current = camera.transform.eulerAngles;
        var delta = new Vector2(Mathf.DeltaAngle(current.y, desired.y) / simulator.mouseXRotateSensitivity,
            -Mathf.DeltaAngle(current.x, desired.x) / simulator.mouseYRotateSensitivity);
        InputSystem.QueueStateEvent(mouse, new MouseState { delta = delta });
        yield return new WaitForSeconds(0.3f);
        Input();
        yield return new WaitForSeconds(0.2f);
        var viewport = camera.WorldToViewportPoint(target);
        Require(viewport.z > 0f && viewport.x > 0.1f && viewport.x < 0.9f && viewport.y > 0.1f && viewport.y < 0.9f,
            $"Player is not looking at the grill: viewport={viewport}");
    }

    private IEnumerator Replay()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            yield return RunSequence();
        }
        finally
        {
            if (patty) patty.Ready -= OnReady;
            CleanupDevices();
        }
    }

    private void OnDestroy() => CleanupDevices();

    private void CleanupDevices()
    {
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
    }

    private IEnumerator RunSequence()
    {
        yield return new WaitForSeconds(1f);
        hand = GameObject.Find("Right Controller").transform;
        patty = GameObject.Find("Patty").GetComponent<StoveCookable>();
        grab = patty.GetComponent<XRGrabInteractable>();
        interactor = hand.GetComponentInChildren<NearFarInteractor>();
        patty.Ready += OnReady;
        var surface = GameObject.Find("Grill Cooking Surface").GetComponent<Collider>();
        stoveAudio = surface.GetComponent<AudioSource>();
        Record("start");
        Require(patty.cookProgress == 0f && ColorNear(new Color(0.85f, 0.42f, 0.43f)), "Patty did not start raw and pink");
        // Holding Space selects the right simulated controller, without changing scene transforms.
        Input(Key.Space, Key.Digit3);
        yield return new WaitForSeconds(0.2f);
        Input(Key.Space);
        yield return new WaitForSeconds(0.2f);
        yield return Move(patty.transform.position + Vector3.up * 0.04f);
        yield return new WaitForSeconds(0.4f);
        trigger = true;
        Input(Key.Space);
        yield return new WaitForSeconds(0.5f);
        Require(interactor.interactablesSelected.Contains(grab), "Mouse trigger did not select Patty");
        Record("grabbed");
        Vector3 offset = hand.position - patty.transform.position;
        yield return Move(hand.position + Vector3.up * 0.45f);
        var destination = new Vector3(surface.bounds.center.x, surface.bounds.max.y + 0.25f, surface.bounds.center.z);
        yield return Move(destination + offset);
        yield return Move(new Vector3(hand.position.x, surface.bounds.max.y + 0.10f + offset.y, hand.position.z));
        trigger = false;
        Input(Key.Space);
        yield return new WaitForSeconds(0.3f);
        yield return LookAt(surface.bounds.center);
        yield return new WaitForSeconds(1f);
        Require(!interactor.interactablesSelected.Contains(grab), "Patty did not release");
        Record("released");
        float deadline = Time.time + 20f;
        while (patty.cookProgress < 1.55f && Time.time < deadline)
        {
            if (ready && stoveAudio.isPlaying) heardReady = true;
            if (patty.cookProgress >= 1f && !observations.Any(x => x.StartsWith("ready ")))
            {
                Require(ColorNear(new Color(0.36f, 0.15f, 0.07f)), "Ready Patty did not turn brown");
                Record("ready");
            }
            yield return null;
        }
        Require(ready, "Ready event did not fire");
        Require(heardReady, "The stove did not play the ready tone");
        Require(readyCount == 1, "The ready signal did not fire exactly once");
        Require(patty.cookProgress >= 1.5f, "Patty did not continue cooking past ready");
        Require(ColorNear(new Color(0.025f, 0.02f, 0.015f)), "Overcooked Patty did not turn black");
        Record("overcooked");
        yield return Move(patty.transform.position + Vector3.up * 0.03f);
        yield return new WaitForSeconds(0.3f);
        trigger = true;
        Input(Key.Space);
        yield return new WaitForSeconds(0.5f);
        Require(interactor.interactablesSelected.Contains(grab), "Could not pick up cooked Patty");
        yield return Move(hand.position + Vector3.up * 0.5f);
        yield return new WaitForSeconds(0.5f);
        float removedProgress = patty.cookProgress;
        yield return new WaitForSeconds(1f);
        Require(Mathf.Abs(patty.cookProgress - removedProgress) < 0.001f, "Patty continued cooking after leaving the stove");
        Record("removed");
        File.WriteAllText(Path.Combine(RPVRStoveInputReplay.OutputDirectory, "result.txt"), "PASS\n" + string.Join("\n", observations));
        Debug.Log("RPVR stove input replay passed: " + RPVRStoveInputReplay.OutputDirectory);
    }

    private void OnReady()
    {
        ready = true;
        readyCount++;
    }

    private bool ColorNear(Color expected)
    {
        var block = new MaterialPropertyBlock();
        patty.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
        var actual = block.GetColor("_BaseColor");
        return Mathf.Abs(actual.r - expected.r) < 0.03f && Mathf.Abs(actual.g - expected.g) < 0.03f && Mathf.Abs(actual.b - expected.b) < 0.03f;
    }

    private void Record(string stage)
    {
        var block = new MaterialPropertyBlock();
        patty.GetComponentInChildren<Renderer>().GetPropertyBlock(block);
        var simulator = FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
        observations.Add($"{stage} time={Time.time:F2} hand={hand.position} patty={patty.transform.position} progress={patty.cookProgress:F3} color={block.GetColor("_BaseColor")} ready={ready} soundPlaying={heardReady} hovered={interactor.interactablesHovered.Count} selected={interactor.interactablesSelected.Count} right={simulator.manipulatingRightController} mouse={mouse.leftButton.ReadValue()} simTrigger={simulator.triggerAction.action.ReadValue<float>()} select={interactor.selectInput.inputActionReferencePerformed.action.ReadValue<float>()}");
        ScreenCapture.CaptureScreenshot(Path.Combine(RPVRStoveInputReplay.OutputDirectory, stage + ".png"));
        CaptureGrill(stage);
    }

    private void CaptureGrill(string stage)
    {
        var center = GameObject.Find("Grill Cooking Surface").transform.position;
        var cameraObject = new GameObject("Replay evidence camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.transform.position = center + new Vector3(0.45f, 0.8f, 0.65f);
        camera.transform.LookAt(center);
        camera.fieldOfView = 45f;
        var render = RenderTexture.GetTemporary(800, 600, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = render;
        camera.Render();
        RenderTexture.active = render;
        var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.Combine(RPVRStoveInputReplay.OutputDirectory, stage + "-grill.png"), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(render);
        Destroy(image);
        Destroy(cameraObject);
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        Record("failure");
        File.WriteAllText(Path.Combine(RPVRStoveInputReplay.OutputDirectory, "result.txt"), "FAIL: " + message + "\n" + string.Join("\n", observations));
        CleanupDevices();
        throw new InvalidOperationException(message);
    }
}
