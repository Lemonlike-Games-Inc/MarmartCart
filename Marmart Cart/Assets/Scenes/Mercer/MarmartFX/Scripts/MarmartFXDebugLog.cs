using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes Play-mode console output (MarmartFX messages, warnings, errors) to
/// Assets/Scenes/Mercer/MarmartFX/Logs/runtime_log.txt so problems can be diagnosed from the file.
/// Editor only; does nothing in builds.
/// </summary>
public static class MarmartFXDebugLog
{
    static readonly string LogPath = "Assets/Scenes/Mercer/MarmartFX/Logs/runtime_log.txt";
    static StringBuilder s_Buffer;
    static float s_LastFlush;
    static bool s_Hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
#if UNITY_EDITOR
        if (s_Hooked) return;
        s_Hooked = true;
        s_Buffer = new StringBuilder();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, $"=== Play started {System.DateTime.Now} scene ===\n");
        }
        catch { }
        Application.logMessageReceived += OnLog;
        Application.quitting += Flush;
#endif
    }

    public static void Write(string msg)
    {
#if UNITY_EDITOR
        if (s_Buffer == null) return;
        s_Buffer.Append($"[{Time.time:0.00}] {msg}\n");
        if (Time.realtimeSinceStartup - s_LastFlush > 1f) Flush();
#endif
    }

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log && !condition.Contains("MarmartFX")) return;
        string st = type == LogType.Exception || type == LogType.Error ? "\n" + stackTrace : "";
        Write($"{type}: {condition}{st}");
    }

    static void Flush()
    {
        if (s_Buffer == null || s_Buffer.Length == 0) return;
        try { File.AppendAllText(LogPath, s_Buffer.ToString()); } catch { }
        s_Buffer.Clear();
        s_LastFlush = Time.realtimeSinceStartup;
    }
}
