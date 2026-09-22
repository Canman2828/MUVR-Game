using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Requests "Floor" (a.k.a. stage / room-scale) tracking so the player's real
/// physical floor maps to y = 0 in the scene. With this the player can put on
/// the Quest and physically walk around inside their Guardian boundary and have
/// it map 1:1 to the floor plane in this scene.
///
/// Attach this to the "XR Rig" root object (already done in SampleScene).
/// The Main Camera is a child of that rig; when an XR provider (OpenXR) is
/// active, the headset pose automatically drives the camera each frame.
/// </summary>
public class FloorTrackingOrigin : MonoBehaviour
{
    void Start()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (var subsystem in subsystems)
        {
            // Prefer room-scale (Floor). Fall back to Device-relative if the
            // runtime/headset doesn't expose a floor/stage boundary.
            if (!subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
            {
                subsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
            }

            // Center the play space on the rig's starting position.
            subsystem.TryRecenter();
        }
    }
}
