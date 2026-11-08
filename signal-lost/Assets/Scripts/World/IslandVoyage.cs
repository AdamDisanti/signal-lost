using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Core;
using SignalLost.Enemies;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SignalLost.World
{
    /// <summary>
    /// The boat trip between two islands (Lawson, 11-07; the crew came along on 11-08). Started from a departure
    /// <see cref="IslandDock"/>'s boat:
    /// <list type="number">
    /// <item>the view leaves the player's eyes for a shot down the dock: the mechanic (their own body, dressed for
    /// the island) steps aboard and takes the tiller, and Cavendish and Eugene, if they are with the player, come down
    /// the dock from camp and climb in;</item>
    /// <item>the boat casts off with all of them and sails out to sea while the camera cranes up and back into a wide
    /// aerial shot, the island falling behind and the next island on the horizon ahead;</item>
    /// <item>the screen fades to black ("Sailing to ...") and the next stage's scene loads;</item>
    /// <item>on the new island it fades in on a wide shot of the boat (the crew now dressed for this island) sailing up
    /// to the arrival dock and follows it in. Everyone steps ashore; then a cut to the island's
    /// <see cref="IslandCamp"/>, where Cavendish and Eugene put the camp up piece by piece and sit down by the fire;
    /// then back to the mechanic on the dock and down into the player's eyes, and control returns.</item>
    /// </list>
    /// Press Jump to skip ahead. The voyage object survives the scene load; it switches off the player's controls,
    /// hides their hands and the HUD, and pushes the fog back so the far island shows, restoring all of it at the end.
    /// </summary>
    public sealed class IslandVoyage : MonoBehaviour
    {
        public const float BoardSeconds = 1.6f, SailSeconds = 8f, FadeOutSeconds = 1.3f;
        public const float FadeInSeconds = 1.2f, ApproachSeconds = 7f, ZoomSeconds = 1.8f;
        public const float CampSeconds = 3.2f, CutSeconds = 0.4f;
        private const float SailDistance = 150f, ApproachDistance = 110f, MaxBoardingSeconds = 7f;

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

            // Everyone steps aboard: the mechanic from where the player stood, then Cavendish and Eugene (when they are
            // with the player) coming down the dock from camp.
            int boarding = 0;
            IEnumerator Count(IEnumerator step) { boarding++; yield return step; boarding--; }
            if (rig.Controller != null)
            {
                Passenger me = Passenger.Mechanic(rig.Controller.transform.position, Flat(rig.Controller.transform.forward, sea));
                StartCoroutine(Count(Board(me, dock, boatControl != null ? boatControl.Helm : boat, 0.3f, 2.6f)));
            }
            float behind = 9f;
            foreach (NpcActor actor in Companions())
            {
                Passenger friend = Passenger.For(actor);
                friend.Place(dock.Landing.position - sea * behind, Quaternion.LookRotation(sea));
                Transform seat = boatControl == null ? boat : friend.Who == Passenger.Kind.Cavendish ? boatControl.CavendishSeat : boatControl.MerchantSeat;
                StartCoroutine(Count(Board(friend, dock, seat, 0f, friend.Who == Passenger.Kind.Merchant ? 3.75f : 3f)));
                behind += 3.5f;
            }

            // The view leaves the player's eyes for a shot back down the dock from out on the water, on the other side
            // from the boat, and holds there while they come aboard.
            Vector3 landing = dock.Landing.position;
            Vector3 boardAt = landing + sea * 10f - side * 7f + Vector3.up * 4.5f;
            Vector3 boardLook = landing + side * 2f - sea * 3f + Vector3.up * 0.8f;
            Pose eye = new Pose(shot.transform.position, shot.transform.rotation);
            for (float t = 0f; (t < BoardSeconds || boarding > 0) && t < MaxBoardingSeconds && !skip; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / BoardSeconds));
                shot.transform.SetPositionAndRotation(Vector3.Lerp(eye.position, boardAt, u), Level(eye.rotation * Vector3.forward, boardLook - boardAt, u));
                yield return null;
            }
            while (boarding > 0) yield return null; // (skipping hurries everyone into their seats)
            for (float t = 0f; t < 0.4f && !skip; t += Time.deltaTime) yield return null;
            shot.transform.SetPositionAndRotation(boardAt, Quaternion.LookRotation(boardLook - boardAt));

            Departed?.Invoke(dock.DestinationScene);
            TrailRenderer wake = boatControl != null ? boatControl.Wake : null;
            if (wake != null) { wake.Clear(); wake.emitting = true; }
            ScreenFade fade = ScreenFade.Instance;
            bool fading = false;
            float total = SailSeconds + FadeOutSeconds;
            Vector3 boardOffset = boardAt - start, lookOffset = boardLook - start;
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
                Vector3 look = Vector3.Lerp(position + lookOffset, position + sea * 90f, crane * 0.8f);
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

        // Down the dock (from camp), out to its edge beside their place on the boat, and a hop aboard.
        private IEnumerator Board(Passenger who, IslandDock dock, Transform seat, float delay, float speed)
        {
            for (float t = 0f; t < delay && !skip; t += Time.deltaTime) yield return null;
            if (who.Who != Passenger.Kind.Mechanic) yield return who.WalkTo(dock.Landing.position, speed, () => skip);
            yield return who.WalkTo(dock.StepFor(seat.position), speed, () => skip);
            yield return who.HopTo(seat.position, seat.rotation, 0.65f, 0.8f, () => skip);
            who.Board(seat);
        }

        /// <summary>Cavendish and Eugene, when they are with the player: Eugene only once he is a friendly trader.</summary>
        private static IEnumerable<NpcActor> Companions()
        {
            NpcActor cavendish = null, merchant = null;
            foreach (NpcActor actor in FindObjectsByType<NpcActor>(FindObjectsInactive.Exclude))
            {
                if (actor.Kind == NpcActor.Role.Cavendish) { if (cavendish == null) cavendish = actor; continue; }
                var crab = actor.GetComponent<MerchantCrab>();
                if (merchant == null && crab != null && crab.IsDefeated && !crab.AmbushActive) merchant = actor;
            }
            if (cavendish != null) yield return cavendish;
            if (merchant != null) yield return merchant;
        }

        // ---------- Arriving ----------

        private IEnumerator Arrive(IslandDock dock, ScreenFade fade)
        {
            IslandCamp camp = IslandCamp.Current;
            if (dock == null || dock.Boat == null)
            {
                // No arrival dock on this island: just fade in where the scene put the player.
                camp?.SetUpNow();
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

            // The crew aboard, dressed for this island now: the mechanic at the tiller, and this island's own Cavendish
            // and Eugene (their camp is still packed away).
            Passenger me = Passenger.Mechanic(boat.position, boat.rotation);
            me.Board(boatControl != null ? boatControl.Helm : boat);
            Passenger cavendish = camp != null && camp.Cavendish != null ? Passenger.For(camp.Cavendish) : null;
            Passenger merchant = camp != null && camp.Merchant != null ? Passenger.For(camp.Merchant) : null;
            cavendish?.Board(boatControl != null ? boatControl.CavendishSeat : boat);
            merchant?.Board(boatControl != null ? boatControl.MerchantSeat : boat);

            fade.SetCaption(dock.IslandName, "");
            Vector3 wideOffset = sea * 70f + side * 55f + Vector3.up * 50f, closeOffset = sea * 15f + side * 11f + Vector3.up * 6f;
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

            yield return Disembark(dock, me, cavendish, merchant);
            if (camp != null && (cavendish != null || merchant != null)) yield return PitchCamp(dock, camp, cavendish, merchant, fade);
            else camp?.SetUpNow();
            yield return BackToEyes(me, fade);
        }

        // Everyone hops onto the dock; Cavendish and Eugene set off up it toward the beach.
        private IEnumerator Disembark(IslandDock dock, Passenger me, Passenger cavendish, Passenger merchant)
        {
            Vector3 sea = dock.Seaward;
            Quaternion ashore = Quaternion.LookRotation(-sea);
            int pending = 0;
            IEnumerator Off(Passenger who, Vector3 to, float delay, float speed)
            {
                pending++;
                for (float t = 0f; t < delay && !skip; t += Time.deltaTime) yield return null;
                who.Ashore();
                yield return who.HopTo(to, ashore, 0.55f, 0.8f, () => skip);
                if (speed > 0f) yield return who.WalkTo(to - sea * 6f, speed, () => skip);
                pending--;
            }
            StartCoroutine(Off(me, dock.Landing.position, 0.15f, 0f));
            if (cavendish != null) StartCoroutine(Off(cavendish, dock.StepFor(cavendish.Root.position), 0.45f, 3f));
            if (merchant != null) StartCoroutine(Off(merchant, dock.StepFor(merchant.Root.position), 0.75f, 3.75f));
            yield return null;
            for (float t = 0f; pending > 0 && t < 4f && !skip; t += Time.deltaTime) yield return null;
        }

        // A cut to the camp site: Cavendish and Eugene unpack and the camp goes up around them, then they sit down.
        private IEnumerator PitchCamp(IslandDock dock, IslandCamp camp, Passenger cavendish, Passenger merchant, ScreenFade fade)
        {
            if (!skip) yield return fade.FadeTo(1f, CutSeconds);
            Vector3 centre = camp.Centre;
            Vector3 toDock = dock.transform.position - centre;
            toDock.y = 0f;
            toDock = toDock.sqrMagnitude > 0.01f ? toDock.normalized : -camp.transform.forward;
            Vector3 across = Vector3.Cross(Vector3.up, toDock);
            cavendish?.Place(camp.CavendishStand.position, Face(camp.CavendishStand.position, centre));
            merchant?.Place(camp.MerchantStand.position, Face(camp.MerchantStand.position, centre));
            cavendish?.Sit(false);
            Vector3 cameraAt = centre + toDock * 8.5f + across * 3f + Vector3.up * 4f;
            shot.transform.SetPositionAndRotation(cameraAt, Quaternion.LookRotation(centre + Vector3.up * 0.6f - cameraAt));

            if (skip) camp.SetUpNow();
            else
            {
                StartCoroutine(fade.FadeTo(0f, CutSeconds));
                StartCoroutine(camp.SetUp(CampSeconds));
                yield return null;
                // Busy pitching camp: Cavendish bobs about, Eugene scuttles on the spot.
                for (float t = 0f; camp.IsPacked && !skip; t += Time.deltaTime)
                {
                    cavendish?.Busy(t);
                    merchant?.Busy(t);
                    yield return null;
                }
                if (skip) camp.SetUpNow();
                fade.Set(0f);
            }
            cavendish?.Idle();
            merchant?.Idle();

            // Then they sit down by the fire.
            var hops = new List<Coroutine>();
            if (cavendish != null && camp.CavendishSeat != null) hops.Add(StartCoroutine(cavendish.HopTo(camp.CavendishSeat.position, camp.CavendishSeat.rotation, 0.5f, 0.6f, () => skip)));
            if (merchant != null && camp.MerchantSeat != null) hops.Add(StartCoroutine(merchant.HopTo(camp.MerchantSeat.position, camp.MerchantSeat.rotation, 0.5f, 0.6f, () => skip)));
            foreach (Coroutine hop in hops) yield return hop;
            camp.SeatResidents();
            for (float t = 0f; t < 0.7f && !skip; t += Time.deltaTime) yield return null;
        }

        // Back to the mechanic on the dock, looking up the beach, then down into the player's eyes.
        private IEnumerator BackToEyes(Passenger me, ScreenFade fade)
        {
            Transform body = me.Root;
            Vector3 facing = body.forward;
            Vector3 over = body.position - facing * 3.2f + Vector3.up * 2.6f;
            if (!skip) yield return fade.FadeTo(1f, CutSeconds);
            shot.transform.SetPositionAndRotation(over, Quaternion.LookRotation(body.position + facing * 8f + Vector3.up * 1.2f - over));
            if (!skip) StartCoroutine(fade.FadeTo(0f, CutSeconds));
            else fade.Set(0f);

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
            fade.Set(0f);
            Destroy(body.gameObject);
        }

        // ---------- Helpers ----------

        // Turns from one view direction to another without rolling the horizon (a plain rotation slerp can tilt it).
        private static Quaternion Level(Vector3 from, Vector3 to, float u) => Quaternion.LookRotation(Vector3.Slerp(from.normalized, to.normalized, u));

        private static Quaternion Flat(Vector3 forward, Vector3 fallback)
        {
            forward.y = 0f;
            return Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : fallback);
        }

        private static Quaternion Face(Vector3 from, Vector3 to) => Flat(to - from, Vector3.forward);

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

        /// <summary>
        /// Someone the cutscene moves about by hand: the mechanic's stand-in body, or Cavendish or Eugene (their
        /// NavMesh agents are switched off while it does). Walks with a little animation, hops on and off the boat,
        /// and rides along in their place on it.
        /// </summary>
        private sealed class Passenger
        {
            public enum Kind { Mechanic, Cavendish, Merchant }

            public readonly Transform Root;
            public readonly Kind Who;
            private readonly MechanicModel.Figure body;
            private readonly Transform bootL, bootR;
            private readonly HermitCrabRig crab;
            private readonly Transform home;
            private float phase, drop;

            private Passenger(Transform root, Kind who, MechanicModel.Figure body)
            {
                Root = root;
                Who = who;
                this.body = body;
                if (body != null)
                {
                    bootL = body.Body.Find("Boot L");
                    bootR = body.Body.Find("Boot R");
                }
                home = root.parent;
                crab = root.GetComponent<HermitCrabRig>();
                var agent = root.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
                if (who == Kind.Cavendish)
                {
                    Transform visual = root.Find("Visual");
                    drop = visual != null ? -visual.localPosition.y : 0f;
                }
            }

            /// <summary>The player's own body, dressed for this island, standing at <paramref name="at"/>.</summary>
            public static Passenger Mechanic(Vector3 at, Quaternion facing)
            {
                var root = new GameObject("Mechanic (voyage)").transform;
                root.SetPositionAndRotation(at, facing);
                var holder = new GameObject("Mechanic").transform;
                holder.SetParent(root, false);
                MechanicModel.Figure figure = MechanicModel.BuildFigure(holder, Outfits.Paint, new Vector3(0f, 0f, 8f), new Vector3(0f, 0f, -8f),
                    0.6f, 0.6f, Outfits.Mechanic(StageTheme.Current));
                // (The pockets figure is lit on its own stage; out here in the world it casts and catches shadows.)
                foreach (Renderer part in holder.GetComponentsInChildren<Renderer>(true))
                {
                    part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    part.receiveShadows = true;
                }
                return new Passenger(root, Kind.Mechanic, figure);
            }

            public static Passenger For(NpcActor actor) =>
                new Passenger(actor.transform, actor.Kind == NpcActor.Role.Cavendish ? Kind.Cavendish : Kind.Merchant, null);

            public void Place(Vector3 position, Quaternion facing) => Root.SetPositionAndRotation(position, facing);

            /// <summary>Cavendish's unrigged model sinks onto a seat; standing, it does not.</summary>
            public void Sit(bool seated)
            {
                if (Who != Kind.Cavendish) return;
                drop = seated ? IslandCamp.CavendishSitDrop : 0f;
                IslandCamp.SetCavendishDrop(Root, drop);
            }

            /// <summary>Takes their place on the boat (Cavendish sits on the thwart) and rides along with it.</summary>
            public void Board(Transform seat)
            {
                Root.SetParent(seat, true);
                Root.SetPositionAndRotation(seat.position, seat.rotation);
                Sit(Who == Kind.Cavendish);
                Idle();
            }

            public void Ashore()
            {
                Root.SetParent(home, true);
                Sit(false);
            }

            public IEnumerator WalkTo(Vector3 target, float speed, Func<bool> hurry)
            {
                Sit(false);
                while (!hurry())
                {
                    Vector3 to = target - Root.position;
                    to.y = 0f;
                    float left = to.magnitude;
                    if (left < 0.06f) break;
                    float step = Mathf.Min(left, speed * Time.deltaTime);
                    Vector3 position = Root.position + to / left * step;
                    position.y = Mathf.Lerp(Root.position.y, target.y, step / left);
                    Root.SetPositionAndRotation(position, Quaternion.RotateTowards(Root.rotation, Quaternion.LookRotation(to), 540f * Time.deltaTime));
                    Animate(speed);
                    yield return null;
                }
                Idle();
            }

            public IEnumerator HopTo(Vector3 position, Quaternion facing, float seconds, float height, Func<bool> hurry)
            {
                Vector3 from = Root.position;
                Quaternion turn = Root.rotation;
                for (float t = 0f; t < seconds && !hurry(); t += Time.deltaTime)
                {
                    float u = t / seconds, k = Mathf.SmoothStep(0f, 1f, u);
                    Root.SetPositionAndRotation(Vector3.Lerp(from, position, k) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * height), Quaternion.Slerp(turn, facing, k));
                    yield return null;
                }
                Root.SetPositionAndRotation(position, facing);
            }

            /// <summary>Working on the spot (pitching camp).</summary>
            public void Busy(float t)
            {
                switch (Who)
                {
                    case Kind.Cavendish: IslandCamp.SetCavendishDrop(Root, drop - Mathf.Abs(Mathf.Sin(t * 7f)) * 0.07f); break;
                    case Kind.Merchant: if (crab != null) crab.MoveSpeed = 1.2f + Mathf.Sin(t * 3f); break;
                    default: Animate(1f); break;
                }
            }

            public void Idle() => Animate(0f);

            // A walking animation at speed (m/s): the mechanic swings his arms and legs, Cavendish slides along with a
            // bob as in the opening, and Eugene's legs scuttle.
            private void Animate(float speed)
            {
                bool moving = speed > 0.05f;
                phase += Time.deltaTime * (2.5f + speed * 2.2f);
                switch (Who)
                {
                    case Kind.Mechanic:
                        // The suit is one seamless piece, so he waddles: a sway and bob, swinging arms, boots stepping.
                        float step = moving ? Mathf.Sin(phase) : 0f;
                        body.ArmR.localRotation = Quaternion.Euler(step * 25f, 0f, 8f);
                        body.ArmL.localRotation = Quaternion.Euler(-step * 25f, 0f, -8f);
                        body.Body.localPosition = Vector3.up * (moving ? Mathf.Abs(step) * 0.05f : 0f);
                        body.Body.localRotation = Quaternion.Euler(0f, 0f, step * 4f);
                        if (bootL != null) bootL.localPosition = new Vector3(bootL.localPosition.x, 0.065f + Mathf.Max(0f, step) * 0.07f, 0.04f + step * 0.06f);
                        if (bootR != null) bootR.localPosition = new Vector3(bootR.localPosition.x, 0.065f + Mathf.Max(0f, -step) * 0.07f, 0.04f - step * 0.06f);
                        break;
                    case Kind.Cavendish:
                        IslandCamp.SetCavendishDrop(Root, drop - (moving ? Mathf.Abs(Mathf.Sin(phase)) * 0.08f : 0f));
                        break;
                    default:
                        if (crab != null) crab.MoveSpeed = speed;
                        break;
                }
            }
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
