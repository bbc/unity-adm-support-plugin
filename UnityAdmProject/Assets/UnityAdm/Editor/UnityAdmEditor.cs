using UnityEditor;
using UnityEngine;

public class UnityAdmFunctions : EditorWindow
{
    private bool togState = false;

    [MenuItem("Window/Unity ADM live controls")]
    public static void ShowWindow()
    {
        GetWindow<UnityAdmFunctions>("Unity ADM live controls");
    }

    private void OnGUI()
    {
        if (GUILayout.Toggle(togState, "Interface with SteamVR\n   (change will cause recompile)"))
        {
            if (!togState)
            {
                togState = true;
                Debug.LogWarning("Recompiling for SteamVR... Please be patient\n If an error is shown regarding a missing 'Valve' namespace, the SteamVR package can not be located.");
                EditorUtils.AddDefineIfNecessary("STEAMVR", BuildTargetGroup.Standalone);
            }
        }
        else
        {
            if (togState)
            {
                togState = false;
                Debug.LogWarning("Recompiling WITHOUT SteamVR integration... Please be patient");
                EditorUtils.RemoveDefineIfNecessary("STEAMVR", BuildTargetGroup.Standalone);
            }
        }
        if (GUILayout.Button("Start Playback"))
        {
            startPlayback();
        }
        if (GUILayout.Button("Stop Playback"))
        {
            stopPlayback();
        }
        if (GUILayout.Button("Apply new settings and Reinitialise"))
        {
            reinit();
        }
        if (GUILayout.Button("Recentre Listener"))
        {
            var script = FindObjectOfType<UnityAdm>();
            script.recentreListener();
        }
    }

    private void startPlayback()
    {
        if (!Application.isPlaying) return;

        var script = FindObjectOfType<UnityAdm>();
        script.startPlayback();
    }

    private void stopPlayback()
    {
        if (!Application.isPlaying) return;

        var script = FindObjectOfType<UnityAdm>();
        script.stopPlayback();
    }

    private void reinit()
    {
        if (!Application.isPlaying) return;

        var script = FindObjectOfType<UnityAdm>();
        script.stopPlayback();
        script.applySettings();
        script.initialise();
    }
}