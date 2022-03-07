using UnityEngine;
using System.Collections;

public static class OpenVrWrapper
{
    private static int hmdIndex = -1;
#if STEAMVR
    private static Valve.VR.TrackedDevicePose_t[] trackedDevicePose;
#endif

    private static bool checkDeviceIndexIsConnectedHmd(int devIndex)
    {
#if STEAMVR
        if (devIndex >= 0 && devIndex < Valve.VR.OpenVR.k_unMaxTrackedDeviceCount)
        {
            if (Valve.VR.OpenVR.System.IsTrackedDeviceConnected((uint)devIndex))
            {
                if (Valve.VR.OpenVR.System.GetTrackedDeviceClass((uint)devIndex) == Valve.VR.ETrackedDeviceClass.HMD)
                {
                    return true;
                }
            }
        }
#endif
        return false;
    }

    public static bool updateHmdIndex()
    {
#if STEAMVR
        if (IsRunning())
        {
            if (checkDeviceIndexIsConnectedHmd(hmdIndex))
            {
                return true;
            }

            for (uint unDevice = 0; unDevice < Valve.VR.OpenVR.k_unMaxTrackedDeviceCount; unDevice++)
            {
                if (checkDeviceIndexIsConnectedHmd((int)unDevice))
                {
                    hmdIndex = (int)unDevice;
                    trackedDevicePose = new Valve.VR.TrackedDevicePose_t[unDevice + 1];
                    return true;
                }
            }
        }
#endif
        hmdIndex = -1;
        return false;
    }

    public static bool getHmdPosAndRot(out Vector3 position, out Quaternion rotation)
    {
#if STEAMVR
        if (IsRunning() || checkDeviceIndexIsConnectedHmd(hmdIndex))
        {
            Valve.VR.OpenVR.System.GetDeviceToAbsoluteTrackingPose(Valve.VR.ETrackingUniverseOrigin.TrackingUniverseSeated, 0.015f, trackedDevicePose);
            SteamVR_Utils.RigidTransform rigidTransform = new SteamVR_Utils.RigidTransform(trackedDevicePose[hmdIndex].mDeviceToAbsoluteTracking);
            position = rigidTransform.pos;
            rotation = rigidTransform.rot;
            return true;
        }
#endif
        rotation = new Quaternion();
        position = new Vector3();
        return false;
    }

    public static void recentreListener()
    {
#if STEAMVR
        if (IsRunning()) {
            Valve.VR.OpenVR.System.ResetSeatedZeroPose();
            Valve.VR.OpenVR.Compositor.SetTrackingSpace(Valve.VR.ETrackingUniverseOrigin.TrackingUniverseSeated);
        }
#endif
    }
    public static bool IsRunning()
    {
#if STEAMVR
        return Valve.VR.OpenVR.System != null;
#endif
        return false;
    }
}
