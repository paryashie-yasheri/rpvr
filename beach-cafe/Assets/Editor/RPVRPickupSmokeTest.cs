using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>Checks the XRI pickup/release physics path in the currently running scene.</summary>
public static class RPVRPickupSmokeTest
{
    [MenuItem("RPVR/Tests/Run Pickup Grab-Release Smoke Test")]
    public static void Run()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before running the pickup smoke test.");

        var rightHand = UnityEngine.Object.FindObjectsByType<NearFarInteractor>()
            .FirstOrDefault(interactor => interactor.transform.parent != null &&
                                         interactor.transform.parent.name == "Right Controller");
        var item = UnityEngine.Object.FindObjectsByType<XRGrabInteractable>()
            .FirstOrDefault(interactable => interactable.name == "Patty");

        if (rightHand == null || item == null)
            throw new InvalidOperationException("Could not find Right Controller/Near-Far Interactor and Patty in the active scene.");

        var leftHand = UnityEngine.Object.FindObjectsByType<NearFarInteractor>()
            .FirstOrDefault(interactor => interactor.transform.parent != null &&
                                         interactor.transform.parent.name == "Left Controller");
        if (leftHand == null || !HasTriggerSelectionBinding(leftHand, "LeftHand") ||
            !HasTriggerSelectionBinding(rightHand, "RightHand"))
            throw new InvalidOperationException("Both controller Select actions must be bound to their hand's XR trigger button.");

        var body = item.GetComponent<Rigidbody>();
        if (body == null)
            throw new InvalidOperationException("Patty needs a Rigidbody for pickup.");

        if (item.movementType != XRBaseInteractable.MovementType.VelocityTracking ||
            body.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
            throw new InvalidOperationException("Patty must use velocity-tracked movement and continuous collision detection.");

        var startPosition = body.position;
        var startRotation = body.rotation;
        var startVelocity = body.linearVelocity;
        var startAngularVelocity = body.angularVelocity;
        var startIsKinematic = body.isKinematic;
        var startUseGravity = body.useGravity;
        var started = false;

        try
        {
            rightHand.StartManualInteraction((IXRSelectInteractable)item);
            started = rightHand.interactablesSelected.Contains(item);

            if (!started || body.isKinematic)
                throw new InvalidOperationException("Pickup failed: Patty was not selected as a dynamic Rigidbody.");

            rightHand.EndManualInteraction();
            started = false;

            if (rightHand.interactablesSelected.Contains(item) || body.isKinematic || !body.useGravity)
                throw new InvalidOperationException("Release failed: Patty remained selected or gravity was not restored.");

            Debug.Log("RPVR pickup smoke test passed: Patty stayed dynamic while held and released back to gravity.");
        }
        finally
        {
            if (started)
                rightHand.EndManualInteraction();

            body.position = startPosition;
            body.rotation = startRotation;
            body.linearVelocity = startVelocity;
            body.angularVelocity = startAngularVelocity;
            body.isKinematic = startIsKinematic;
            body.useGravity = startUseGravity;
            Physics.SyncTransforms();
        }
    }

    static bool HasTriggerSelectionBinding(NearFarInteractor interactor, string hand)
    {
        var selectAction = interactor.selectInput.inputActionReferencePerformed?.action;
        var selectValueAction = interactor.selectInput.inputActionReferenceValue?.action;
        var triggerButton = $"<XRController>{{{hand}}}/{{TriggerButton}}";
        var triggerValue = $"<XRController>{{{hand}}}/{{Trigger}}";

        return selectAction != null && selectAction.bindings.Any(binding => binding.path == triggerButton) &&
               selectValueAction != null && selectValueAction.bindings.Any(binding => binding.path == triggerValue);
    }
}
