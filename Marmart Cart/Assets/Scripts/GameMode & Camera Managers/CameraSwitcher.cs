using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public static class CameraSwitcher
{
    static List<CinemachineCamera> cameras = new List<CinemachineCamera>();
    public static CinemachineCamera ActiveCamera = null;

    public static bool IsActiveCamera(CinemachineCamera camera)
    {
        return camera == ActiveCamera;
    }
    public static void SwitchCamera(CinemachineCamera camera)
    {
        camera.Priority = 20;
        ActiveCamera = camera;

        foreach (CinemachineCamera c in cameras)
        {
            if (c != camera && c.Priority != 10)
            {
                c.Priority = 10;
            }
        }
    }
    public static void Register(CinemachineCamera camera)
    {
        cameras.Add(camera);
    }

    public static void Unregister(CinemachineCamera camera)
    {
        cameras.Remove(camera);
    }

    public static void UpdateCameraFocus(CinemachineCamera camera, Transform targetTransform)
    {
        camera.Follow = targetTransform;
    }
}