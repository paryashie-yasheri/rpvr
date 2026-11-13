using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

/// <summary>Release simulated buttons even after their hand's manipulation modifier is released.</summary>
public sealed class CafeXRDeviceSimulator : XRDeviceSimulator
{
#if ENABLE_VR || UNITY_GAMECORE
    protected override void ProcessAnalogButtonControlInput(ref XRSimulatedControllerState controllerState)
    {
        base.ProcessAnalogButtonControlInput(ref controllerState);
        // XRI otherwise preserves button state on a hand that is no longer being
        // manipulated. Releasing Space before the mouse would leave Select pressed.
        if (triggerAction != null && triggerAction.action != null && triggerAction.action.WasReleasedThisFrame())
        {
            controllerState.trigger = 0f;
            controllerState.WithButton(ControllerButton.TriggerButton, false);
        }
        if (gripAction != null && gripAction.action != null && gripAction.action.WasReleasedThisFrame())
        {
            controllerState.grip = 0f;
            controllerState.WithButton(ControllerButton.GripButton, false);
        }
    }
#endif
}
