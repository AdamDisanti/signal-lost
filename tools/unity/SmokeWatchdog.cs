// Copy into Assets/Editor/Smoke/ alongside the smokes: stops a batch-mode smoke that can never finish.
// Coroutine-based runners have no try/catch around their yields, so an exception in one
// used to leave the editor sitting in Play Mode until someone killed it.
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SmokeWatchdog
{
    private static readonly double Deadline;
    private static bool exiting;

    static SmokeWatchdog()
    {
        if (!Application.isBatchMode) return;
        double limit = double.TryParse(Environment.GetEnvironmentVariable("SMOKE_TIMEOUT"), out double s) ? s : 600.0;
        Deadline = EditorApplication.timeSinceStartup + limit;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (exiting || type != LogType.Exception || stack == null) return;
        // Only exceptions thrown by test code itself; game code that throws doesn't stall a runner.
        if (stack.IndexOf("Editor/Smoke/", StringComparison.Ordinal) < 0 && stack.IndexOf("Editor\\Smoke\\", StringComparison.Ordinal) < 0) return;
        Fail("unhandled exception in test code: " + message);
    }

    private static void Tick()
    {
        if (!exiting && EditorApplication.timeSinceStartup > Deadline)
            Fail($"timed out after {Deadline:F0} s (set SMOKE_TIMEOUT to change)");
    }

    private static void Fail(string why)
    {
        exiting = true;
        Debug.Log("SMOKE_WATCHDOG_FAIL " + why);
        EditorApplication.Exit(3);
    }
}
