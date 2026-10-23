using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SignalLost.Combat;
using SignalLost.Player;
using UnityEngine;

namespace SignalLost.Enemies.Desert
{
    /// <summary>
    /// A test recorder for the Core Worm fight. It is off unless the file Logs/coreworm-diagnostics.on exists in the
    /// project folder (Editor only). When on, it walks the player to the stage exit to start the ambush, keeps the player
    /// alive, writes one line per frame to Logs/coreworm-diagnostics.csv (frame time, what the worm is doing, where its
    /// head and body are, what laying out the body cost, garbage collections), and stops Play mode after
    /// <see cref="RunSeconds"/> seconds.
    /// </summary>
    [DefaultExecutionOrder(10000)] // after the worm has moved and laid out its body this frame
    public sealed class CorewormDiagnostics : MonoBehaviour
    {
        private const float RunSeconds = 90f;
        private static readonly float[] BodySamples = { 0f, 4f, 8f, 12f, 16f, 20f, 24f, 28f };

        private Coreworm worm;
        private Transform player;
        private Health playerHealth;
        private Camera view;
        private StreamWriter writer;
        private readonly StringBuilder line = new StringBuilder(512);
        private float started;
        private bool walked;
        private int lastGc;

        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void MaybeStart()
        {
            if (!Application.isEditor || !File.Exists(Path.Combine(Folder, "coreworm-diagnostics.on"))) return;
            if (FindAnyObjectByType<Coreworm>() == null) return;
            new GameObject("Coreworm Diagnostics").AddComponent<CorewormDiagnostics>();
        }

        private void Start()
        {
            worm = FindAnyObjectByType<Coreworm>();
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null)
            {
                player = controller.transform;
                playerHealth = controller.GetComponent<Health>();
                view = controller.GetComponentInChildren<Camera>();
            }
            Directory.CreateDirectory(Folder);
            writer = new StreamWriter(Path.Combine(Folder, "coreworm-diagnostics.csv"), false, Encoding.UTF8, 1 << 16);
            var header = new StringBuilder("frame,time,unscaled_dt,dt,fps_cap,attack,move,fighting,speed,trail,body_ms,gc0,mono_mb,head_x,head_y,head_z,fwd_x,fwd_y,fwd_z,cam_x,cam_y,cam_z");
            foreach (float d in BodySamples) header.Append($",b{d:0}_x,b{d:0}_y,b{d:0}_z");
            writer.WriteLine(header.ToString());
            started = Time.realtimeSinceStartup;
            lastGc = System.GC.CollectionCount(0);
            Debug.Log("[Coreworm Diagnostics] Recording to " + Path.Combine(Folder, "coreworm-diagnostics.csv"));
        }

        private void Update()
        {
            // Start the ambush by putting the player at the exit, once.
            if (!walked && Time.timeSinceLevelLoad > 1f && player != null)
            {
                var exit = FindAnyObjectByType<DesertStageExit>();
                if (exit != null)
                {
                    var body = player.GetComponent<CharacterController>();
                    if (body != null) body.enabled = false;
                    player.position = exit.transform.position + Vector3.up * 0.1f;
                    if (body != null) body.enabled = true;
                }
                walked = true;
            }
            if (playerHealth != null && playerHealth.Normalized < 0.5f) playerHealth.Restore();
        }

        private void LateUpdate()
        {
            if (writer == null || worm == null) return;
            var c = CultureInfo.InvariantCulture;
            line.Clear();
            int gc = System.GC.CollectionCount(0);
            line.Append(Time.frameCount).Append(',')
                .Append(Time.time.ToString("F4", c)).Append(',')
                .Append((Time.unscaledDeltaTime * 1000f).ToString("F2", c)).Append(',')
                .Append((Time.deltaTime * 1000f).ToString("F2", c)).Append(',')
                .Append(Application.targetFrameRate).Append(',')
                .Append(worm.AttackName).Append(',').Append(worm.MoveName).Append(',')
                .Append(worm.Fighting ? 1 : 0).Append(',')
                .Append(worm.CurrentSpeed.ToString("F2", c)).Append(',')
                .Append(worm.TrailCount).Append(',')
                .Append(Coreworm.LastBodyMs.ToString("F3", c)).Append(',')
                .Append(gc - lastGc).Append(',')
                .Append((UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576.0).ToString("F1", c));
            lastGc = gc;
            Vector3 h = worm.Head != null ? worm.Head.position : Vector3.zero;
            Vector3 f = worm.Head != null ? worm.Head.forward : Vector3.zero;
            Vector3 cam = view != null ? view.transform.position : Vector3.zero;
            Append(h, c); Append(f, c); Append(cam, c);
            foreach (float d in BodySamples)
                Append(worm.Body != null ? worm.Body.PointAt(d, out _) : Vector3.zero, c);
            writer.WriteLine(line.ToString());

            if (Time.realtimeSinceStartup - started > RunSeconds) Finish();
        }

        private void Append(Vector3 v, CultureInfo c)
        {
            line.Append(',').Append(v.x.ToString("F3", c)).Append(',').Append(v.y.ToString("F3", c)).Append(',').Append(v.z.ToString("F3", c));
        }

        private void Finish()
        {
            Close();
            Debug.Log("[Coreworm Diagnostics] Done.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void Close()
        {
            if (writer == null) return;
            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        private void OnDisable() => Close();
        private void OnApplicationQuit() => Close();
    }
}
