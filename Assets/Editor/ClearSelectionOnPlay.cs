using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ClearSelectionOnPlay
{
    static ClearSelectionOnPlay()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        // 进入播放前/进入播放时都清空一次，最稳
        if (change == PlayModeStateChange.ExitingEditMode ||
            change == PlayModeStateChange.EnteredPlayMode)
        {
            Selection.objects = System.Array.Empty<Object>();
        }
    }
}
