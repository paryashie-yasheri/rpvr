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

/// <summary>Exercises trigger release after the controller manipulation modifier is released.</summary>
public static class RPVRSimulatorReleaseSmokeTest
{
    public const string ResultPath = "/tmp/rpvr-simulator-release.txt";

    [MenuItem("RPVR/Tests/Run Simulator Trigger Release Smoke Test")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        File.WriteAllText(ResultPath, "RUNNING");
        new GameObject("Simulator Release Smoke Test").AddComponent<RPVRSimulatorReleaseSmokeTestRunner>();
    }
}

public sealed class RPVRSimulatorReleaseSmokeTestRunner : MonoBehaviour
{
    private Keyboard keyboard;
    private Mouse mouse;
    private XRDeviceSimulator simulator;
    private Vector3 speeds;
    private XRDeviceSimulator.Space space;
    private Transform hand;
    private NearFarInteractor interactor;
    private XRGrabInteractable item;
    private bool trigger;
    private bool grip;
    private Key handKey = Key.Space;
    private bool configured;
    private readonly List<string> observations = new List<string>();

    private IEnumerator Start()
    {
        try { yield return Sequence(); }
        finally { Cleanup(); }
    }

    private void Input(params Key[] keys)
    {
        var heldKeys = new List<Key>(keys);
        if (grip) heldKeys.Add(Key.G);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys.ToArray()));
        var state = new MouseState();
        if (trigger) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
    }

    private IEnumerator Move(Vector3 target)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Vector3.Distance(hand.position, target) > 0.03f)
        {
            Require(Time.realtimeSinceStartup < deadline, "Controller movement timed out");
            var delta = hand.parent.InverseTransformDirection(target - hand.position);
            var keys = new List<Key> { handKey };
            if (delta.x > 0.015f) keys.Add(Key.D); else if (delta.x < -0.015f) keys.Add(Key.A);
            if (delta.y > 0.015f) keys.Add(Key.E); else if (delta.y < -0.015f) keys.Add(Key.Q);
            if (delta.z > 0.015f) keys.Add(Key.W); else if (delta.z < -0.015f) keys.Add(Key.S);
            simulator.keyboardXTranslateSpeed = simulator.keyboardYTranslateSpeed = simulator.keyboardZTranslateSpeed =
                Mathf.Clamp(Vector3.Distance(hand.position, target), 0.03f, 0.4f);
            Input(keys.ToArray());
            yield return null;
        }
        Input(handKey);
    }

    private IEnumerator Sequence()
    {
        simulator = FindAnyObjectByType<XRDeviceSimulator>();
        speeds = new Vector3(simulator.keyboardXTranslateSpeed, simulator.keyboardYTranslateSpeed, simulator.keyboardZTranslateSpeed);
        space = simulator.keyboardTranslateSpace;
        configured = true;
        simulator.keyboardTranslateSpace = XRDeviceSimulator.Space.Parent;
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        hand = GameObject.Find("Right Controller").transform;
        interactor = hand.GetComponentInChildren<NearFarInteractor>();
        item = GameObject.Find("Patty").GetComponent<XRGrabInteractable>();
        yield return new WaitForSeconds(1f);
        Input(Key.Escape, Key.Digit3);
        yield return new WaitForSeconds(0.2f);
        Input(handKey);
        yield return new WaitForSeconds(0.2f);
        var target = item.GetComponent<Collider>().bounds.center + Vector3.up * 0.07f;
        yield return Move(target + Vector3.up * 0.3f);
        yield return Move(target);
        trigger = true;
        Input(handKey);
        yield return new WaitForSeconds(0.4f);
        Require(interactor.interactablesSelected.Contains(item), "Trigger did not grab Patty");
        yield return Move(hand.position + Vector3.up * 0.25f);
        trigger = false;
        Input(handKey);
        yield return new WaitForSeconds(0.4f);
        Require(!item.isSelected && !item.GetComponent<Rigidbody>().isKinematic, "Normal trigger release failed");
        observations.Add("Normal release passed");

        target = item.GetComponent<Collider>().bounds.center + Vector3.up * 0.07f;
        yield return Move(target);
        trigger = true;
        Input(handKey);
        yield return new WaitForSeconds(0.4f);
        Require(interactor.interactablesSelected.Contains(item), "Second grab failed");
        yield return Move(hand.position + Vector3.up * 0.25f);
        Input(); // Stop manipulating the hand while the mouse button is still down.
        yield return new WaitForSeconds(0.2f);
        Require(!simulator.manipulatingRightController, "Right controller is still manipulated");
        trigger = false;
        Input();
        yield return new WaitForSeconds(0.4f);
        Require(!item.isSelected && !item.GetComponent<Rigidbody>().isKinematic,
            "Patty stayed selected after releasing Space before the mouse button");
        observations.Add("Release after manipulation modifier passed");
        handKey = Key.LeftShift;
        hand = GameObject.Find("Left Controller").transform;
        interactor = hand.GetComponentInChildren<NearFarInteractor>();
        target = item.GetComponent<Collider>().bounds.center + Vector3.up * 0.07f;
        yield return Move(target + Vector3.up * 0.3f);
        yield return Move(target);
        trigger = true;
        Input(handKey);
        yield return new WaitForSeconds(0.4f);
        Require(interactor.interactablesSelected.Contains(item), "Left trigger did not grab Patty");
        yield return Move(hand.position + Vector3.up * 0.25f);
        Input();
        yield return new WaitForSeconds(0.2f);
        Require(!simulator.manipulatingLeftController, "Left controller is still manipulated");
        trigger = false;
        Input();
        yield return new WaitForSeconds(0.4f);
        Require(!item.isSelected, "Left trigger stayed selected after modifier release");
        observations.Add("Left trigger release after manipulation modifier passed");

        target = item.GetComponent<Collider>().bounds.center + Vector3.up * 0.07f;
        yield return Move(target);
        grip = true;
        Input(handKey);
        yield return new WaitForSeconds(0.4f);
        Require(interactor.interactablesSelected.Contains(item), "Grip did not grab Patty");
        yield return Move(hand.position + Vector3.up * 0.25f);
        Input();
        yield return new WaitForSeconds(0.2f);
        grip = false;
        Input();
        yield return new WaitForSeconds(0.4f);
        Require(!item.isSelected && !item.GetComponent<Rigidbody>().isKinematic,
            "Grip stayed selected after manipulation modifier release");
        observations.Add("Grip release after manipulation modifier passed");
        File.WriteAllText(RPVRSimulatorReleaseSmokeTest.ResultPath, "PASS\n" + string.Join("\n", observations));
        Debug.Log("RPVR simulator trigger release smoke test passed.");
    }

    private void Require(bool condition, string message)
    {
        if (condition) return;
        File.WriteAllText(RPVRSimulatorReleaseSmokeTest.ResultPath, "FAIL: " + message + "\n" + string.Join("\n", observations));
        throw new InvalidOperationException(message);
    }

    private void OnDestroy() => Cleanup();
    private void Cleanup()
    {
        if (configured && simulator)
        {
            simulator.keyboardTranslateSpace = space;
            simulator.keyboardXTranslateSpeed = speeds.x;
            simulator.keyboardYTranslateSpeed = speeds.y;
            simulator.keyboardZTranslateSpeed = speeds.z;
        }
        configured = false;
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
        if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
    }
}
