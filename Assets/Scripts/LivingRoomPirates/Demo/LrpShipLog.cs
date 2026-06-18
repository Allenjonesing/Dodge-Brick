using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tiny headset-visible log buffer for Living Room Pirates.
/// Captures Unity logs plus explicit game messages so the in-world debug sign
/// can show what happened on Quest without adb/logcat.
/// </summary>
public static class LrpShipLog
{
    private const int MaxLines = 14;
    private static readonly Queue<string> Lines = new Queue<string>(MaxLines);
    private static bool _installed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInstall()
    {
        Install();
    }

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        Application.logMessageReceived -= HandleUnityLog;
        Application.logMessageReceived += HandleUnityLog;
        Add("log capture online");
    }

    public static void Add(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        string time = Time.realtimeSinceStartup.ToString("000.0");
        string line = time + " " + Compact(message, 72);
        while (Lines.Count >= MaxLines) Lines.Dequeue();
        Lines.Enqueue(line);
    }

    public static string RecentString(int maxLines)
    {
        Install();
        if (maxLines <= 0) maxLines = 8;

        string[] arr = Lines.ToArray();
        int start = Mathf.Max(0, arr.Length - maxLines);
        System.Text.StringBuilder sb = new System.Text.StringBuilder(384);
        for (int i = start; i < arr.Length; i++)
        {
            sb.Append(arr[i]);
            if (i < arr.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) return;

        // Keep the board useful: capture LRP/boundary messages and all warnings/errors.
        bool important = condition.IndexOf("LRP", System.StringComparison.OrdinalIgnoreCase) >= 0
                      || condition.IndexOf("LivingRoom", System.StringComparison.OrdinalIgnoreCase) >= 0
                      || condition.IndexOf("Boundary", System.StringComparison.OrdinalIgnoreCase) >= 0
                      || condition.IndexOf("OVR", System.StringComparison.OrdinalIgnoreCase) >= 0
                      || type == LogType.Warning
                      || type == LogType.Error
                      || type == LogType.Exception;
        if (!important) return;

        string prefix = type == LogType.Error || type == LogType.Exception ? "ERR " : (type == LogType.Warning ? "WARN " : "INFO ");
        Add(prefix + condition);
    }

    private static string Compact(string value, int maxChars)
    {
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        while (value.IndexOf("  ") >= 0) value = value.Replace("  ", " ");
        if (value.Length <= maxChars) return value;
        return value.Substring(0, Mathf.Max(0, maxChars - 3)) + "...";
    }
}
