using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SignalLost.World
{
    /// <summary>
    /// The boat trip between two islands (Lawson, 11-07). Started from a departure <see cref="IslandDock"/>'s boat:
    /// <list type="number">
    /// <item>the view leaves the player's eyes for a shot of the boat at the dock;</item>
    /// <item>the boat casts off and sails out to sea while the camera cranes up and back into a wide aerial shot, the
    /// island falling behind and the next island on the horizon ahead;</item>
    /// <item>the screen fades to black ("Sailing to ...") and the next stage's scene loads;</item>
    /// <item>on the new island it fades in on a wide shot of the boat sailing up to the arrival dock, follows it in,
    /// then zooms back down into the player's eyes standing on the dock, and hands control back.</item>
    /// </list>
    /// Press Jump to skip ahead. The voyage object survives the scene load; it switches off the player's controls,
    /// hides their hands and the HUD, and pushes the fog back so the far island shows, restoring all of it at the end.
    /// </summary>
    public sealed class IslandVoyage : MonoBehaviour
    {
        public const float BoardSeconds = 1.6f, SailSeconds = 8f, FadeOutSeconds = 1.3f;
        public const float FadeInSeconds = 1.2f, ApproachSeconds = 7f, ZoomSeconds = 1.8f;
        private const float SailDistance = 150f, ApproachDistance = 110f;

        private static IslandVoyage current;

        public static bool InProgress => current != null;
        /// <summary>Raised as the boat casts off. Argument: the destination scene.</summary>
        public static event Action<string> Departed;
        /// <summary>Raised when the player has control again on the new island. Argument: the island's name.</summary>
        public static event Action<string> Arrived;

        private Camera shot;
        private PlayerRig rig;
        private float fogStart, fogEnd;
        private bool skip;

        /// <summary>Starts the voyage from <paramref name="dock"/>. False if one is already under way or the boat can't sail.</summary>
        public static bool Begin(IslandDock dock)
        {
            if (current != null || dock == null || dock.Boat == null || !dock.IsOpen || !dock.DestinationExists) return false;
            current = new GameObject("Island Voyage").AddComponent<IslandVoyage>();
            DontDestroyOnLoad(current.gameObject);
            current.StartCoroutine(current.Run(dock));
            return true;
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        private void Update()
        {
            if (KeyBindings.WasPressed("jump")) skip = true;
        }

        private IEnumerator Run(IslandDock from)
        {
            string destination = from.DestinationScene, destinationName = from.DestinationName;
            rig = PlayerRig.Grab();
            shot = MakeCamera(rig.Camera);
            ExtendFog();
            yield return Depart(from);

            ScreenFade fade = ScreenFade.Instance;
            fade.SetCaption("Sailing to " + destinationName, "");
            AsyncOperation load = SceneManager.LoadSceneAsync(destination);
            while (!load.isDone) yield return null;
            yield return null; // let the new scene's Start methods run
            yield return null;

            rig = PlayerRig.Grab();
            ExtendFog();
            yield return Arrive(IslandDock.Find(IslandDock.Role.Arrival), fade);
            rig.Release();
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            fade.SetCaption("");
            fade.Set(0f);
            Arrived?.Invoke(IslandDock.Find(IslandDock.Role.Arrival)?.IslandName ?? "");
            Destroy(gameObject);
        }

        // ---------- Leaving ----------

        private IEnumerator Depart(IslandDock dock)
        {
            Transform boat = dock.Boat;
            var boatControl = boat.GetComponent<DockBoat>();
            Vector3 sea = dock.Seaward, side = Vector3.Cross(Vector3.up, sea);
            Vector3 start = boat.position;
            Quaternion heading = Quaternion.LookRotation(sea);
            if (boatControl != null) boatControl.Sailing = true;

            // The view leaves the player's eyes for the boat at the dock.
            Pose eye = new Pose(shot.transform.position, shot.transform.rotation);
            Vector3 boardOffset = -sea * 9f + side * 3f + Vector3.up * 3.5f;
            for (float t = 0f; t < BoardSeconds && !skip; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / BoardSeconds);
                Vector3 at = start + boardOffset;
                shot.transform.SetPositionAndRotation(Vector3.Lerp(eye.position, at, u),
                    Level(eye.rotation * Vector3.forward, start + Vector3.up - at, u));
                yield return null;
            }

            Departed?.Invoke(dock.DestinationScene);
            TrailRenderer wake = boatControl != null ? boatControl.Wake : null;
            if (wake != null) { wake.Clear(); wake.emitting = true; }
            ScreenFade fade = ScreenFade.Instance;
            bool fading = false;
            float total = SailSeconds + FadeOutSeconds;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (skip && !fading) t = Mathf.Max(t, SailSeconds); // jump straight to the fade
                float u = Mathf.Clamp01(t / total);
                Vector3 position = start + sea * (SailDistance * Mathf.Pow(u, 1.6f));
                position.y = start.y + (boatControl != null ? boatControl.Bob(Time.time) : 0f);
                boat.SetPositionAndRotation(position, heading * (boatControl != null ? boatControl.Rock(Time.time) : Quaternion.identity));

                // Crane up and back into a wide aerial shot looking out past the boat toward the next island.
                float crane = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / SailSeconds));
                Vector3 offset = Vector3.Lerp(boardOffset, -sea * 46f + side * 38f + Vector3.up * 44f, crane);
                Vector3 look = Vector3.Lerp(position + Vector3.up, position + sea * 90f, crane * 0.8f);
                shot.transform.SetPositionAndRotation(position + offset, Quaternion.LookRotation(look - (position + offset)));

                if (!fading && t >= SailSeconds)
                {
                    fading = true;
                    StartCoroutine(fade.FadeTo(1f, FadeOutSeconds));
                }
                yield return null;
            }
            fade.Set(1f);
            skip = false;
        }

        // ---------- Arriving ----------

        private IEnumerator Arrive(IslandDock dock, ScreenFade fade)
        {
            if (dock == null || dock.Boat == null)
            {
                // No arrival dock on this island: just fade in where the scene put the player.
                yield return fade.FadeTo(0f, FadeInSeconds);
                yield break;
            }
            Transform boat = dock.Boat;
            var boatControl = boat.GetComponent<DockBoat>();
            Vector3 sea = dock.Seaward, side = Vector3.Cross(Vector3.up, sea);
            Vector3 moor = dock.Mooring.position;
            Quaternion rest = boat.rotation, inbound = Quaternion.LookRotation(-sea);
            if (boatControl != null) boatControl.Sailing = true;
            rig.PlaceAt(dock.Landing.position, -sea);

            fade.SetCaption(dock.IslandName, "");
            Vector3 wideOffset = sea * 70f + side * 55f + Vector3.up * 50f, closeOffset = sea * 13f + side * 9f + Vector3.up * 5f;
            Coroutine fadeIn = StartCoroutine(fade.FadeTo(0f, FadeInSeconds));
            TrailRenderer wake = boatControl != null ? boatControl.Wake : null;
            if (wake != null) { wake.Clear(); wake.emitting = true; }
            for (float t = 0f; t < ApproachSeconds && !skip; t += Time.deltaTime)
            {
                float u = t / ApproachSeconds, ease = 1f - Mathf.Pow(1f - u, 2.2f);
                Vector3 position = moor + sea * (ApproachDistance * (1f - ease));
                position.y = moor.y + (boatControl != null ? boatControl.Bob(Time.time) : 0f);
                Quaternion turn = Quaternion.Slerp(inbound, rest, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, u)));
                boat.SetPositionAndRotation(position, turn * (boatControl != null ? boatControl.Rock(Time.time) : Quaternion.identity));

                float close = Mathf.SmoothStep(0f, 1f, u);
                Vector3 camera = Vector3.Lerp(position + wideOffset, moor + closeOffset, close);
                Vector3 look = Vector3.Lerp(moor - sea * 25f, position + Vector3.up, close);
                shot.transform.SetPositionAndRotation(camera, Quaternion.LookRotation(look - camera));
                yield return null;
            }
            yield return fadeIn;
            fade.SetCaption("");
            fade.Set(0f);
            boat.SetPositionAndRotation(moor, rest);
            if (wake != null) wake.emitting = false;
            if (boatControl != null) boatControl.Sailing = false;

            // Back down into the player's eyes.
            Pose from = new Pose(shot.transform.position, shot.transform.rotation);
            Transform eye = rig.Camera != null ? rig.Camera.transform : null;
            float fovFrom = shot.fieldOfView, fovTo = rig.Camera != null ? rig.Camera.fieldOfView : shot.fieldOfView;
            for (float t = 0f; t < ZoomSeconds && eye != null; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / ZoomSeconds);
                shot.transform.SetPositionAndRotation(Vector3.Lerp(from.position, eye.position, u), Level(from.forward, eye.forward, u));
                shot.fieldOfView = Mathf.Lerp(fovFrom, fovTo, u);
                yield return null;
            }
        }

        // ---------- Helpers ----------

        // Turns from one view direction to another without rolling the horizon (a plain rotation slerp can tilt it).
        private static Quaternion Level(Vector3 from, Vector3 to, float u) => Quaternion.LookRotation(Vector3.Slerp(from.normalized, to.normalized, u));

        private Camera MakeCamera(Camera source)
        {
            var camera = new GameObject("Voyage Camera").AddComponent<Camera>();
            camera.transform.SetParent(transform, false);
            if (source != null)
            {
                camera.CopyFrom(source);
                camera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                var from = source.GetUniversalAdditionalCameraData();
                var to = camera.GetUniversalAdditionalCameraData();
                to.renderPostProcessing = from.renderPostProcessing;
                to.antialiasing = from.antialiasing;
            }
            camera.depth = 50f;
            camera.farClipPlane = 3000f;
            camera.targetTexture = null;
            return camera;
        }

        // Pushes the haze back so the far island shows on the horizon.
        private void ExtendFog()
        {
            fogStart = RenderSettings.fogStartDistance;
            fogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fogStartDistance = fogStart * 2.5f;
            RenderSettings.fogEndDistance = Mathf.Max(fogEnd * 5f, 900f);
        }

        /// <summary>The player's controls, eyes, hands and HUD, switched off for the cutscene and back on after.</summary>
        private sealed class PlayerRig
        {
            public FirstPersonController Controller;
            public Camera Camera;
            private PlayerGear gear;
            private readonly List<Behaviour> controls = new List<Behaviour>();
            private readonly List<Renderer> renderers = new List<Renderer>();
            private readonly List<Light> lights = new List<Light>();
            private readonly List<Canvas> canvases = new List<Canvas>();

            public static PlayerRig Grab()
            {
                var rig = new PlayerRig { Controller = FindAnyObjectByType<FirstPersonController>() };
                if (rig.Controller == null) return rig;
                GameObject player = rig.Controller.gameObject;
                rig.Camera = player.GetComponentInChildren<Camera>();
                rig.gear = player.GetComponent<PlayerGear>();
                rig.gear?.Holster(rig);
                foreach (Behaviour behaviour in new Behaviour[]
                         {
                             rig.Controller, player.GetComponent<PlayerFlask>(), player.GetComponent<PlayerInteractor>(),
                             rig.gear == null ? player.GetComponent<MiningLaser>() : null,
                             rig.gear == null ? player.GetComponent<ScannerLaserSwap>() : null
                         })
                    if (behaviour != null && behaviour.enabled) { behaviour.enabled = false; rig.controls.Add(behaviour); }
                foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled) { renderer.enabled = false; rig.renderers.Add(renderer); }
                foreach (Light light in player.GetComponentsInChildren<Light>())
                    if (light.enabled) { light.enabled = false; rig.lights.Add(light); }
                if (rig.Camera != null) rig.Camera.enabled = false;
                foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                    if (canvas.isRootCanvas && canvas.enabled && canvas.GetComponent<ScreenFade>() == null) { canvas.enabled = false; rig.canvases.Add(canvas); }
                return rig;
            }

            public void PlaceAt(Vector3 position, Vector3 facing)
            {
                if (Controller == null) return;
                var body = Controller.GetComponent<CharacterController>();
                facing.y = 0f;
                if (body != null) body.enabled = false;
                Controller.transform.SetPositionAndRotation(position + Vector3.up * 0.05f,
                    facing.sqrMagnitude > 0.001f ? Quaternion.LookRotation(facing) : Controller.transform.rotation);
                if (body != null) body.enabled = true;
                Controller.ResetMotion();
            }

            public void Release()
            {
                if (Camera != null) Camera.enabled = true;
                foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = true;
                foreach (Light light in lights) if (light != null) light.enabled = true;
                foreach (Canvas canvas in canvases) if (canvas != null) canvas.enabled = true;
                foreach (Behaviour behaviour in controls) if (behaviour != null) behaviour.enabled = true;
                gear?.Unholster(this);
            }
        }
    }
}
