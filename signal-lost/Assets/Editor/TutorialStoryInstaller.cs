using System.Linq;
using SignalLost.Enemies;
using SignalLost.Enemies.Meadow;
using SignalLost.Inventory;
using SignalLost.NPC;
using SignalLost.Player;
using SignalLost.Scanner;
using SignalLost.Story;
using UnityEditor;
using UnityEngine;

namespace SignalLost.Editor
{
    /// <summary>
    /// Puts the tutorial's story into the level TutorialLevelBuilder generates: Cavendish at his campfire, the
    /// mining laser wedged in the wreck's reactor bay and the scanner under a fallen hull panel by the tail (both
    /// glint only up close), the merchant crab and its ten cousins in the hidden hollow, the opening's camera marks
    /// (cockpit and two drag glimpses) and the <see cref="TutorialStory"/> that runs it all. The player starts
    /// empty-handed. Called by TutorialLevelBuilder.Build.
    /// </summary>
    internal static class TutorialStoryInstaller
    {
        private const string CavendishPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/CapybaraAstronaut.prefab";
        private const string FlaskPath = "Assets/Prefabs/Enviornment objects/GrayboxProps/AlcoholFlask.prefab";
        private const string LaserPath = "Assets/Prefabs/Enviornment objects/TutorialCrashSite/Upgrades/MiningLaser.prefab";
        private const string ScannerRigPath = "Assets/Prefabs/Player/Scanner/ScannerRig.prefab";
        private const string MerchantPath = "Assets/Prefabs/NPC/MerchantCrab.prefab";
        private const string FrogPath = "Assets/Prefabs/Enemies/MortarFrog.prefab";
        private const string ChiefToadPath = "Assets/Prefabs/Enemies/Meadow/ChiefToad.prefab";
        private static readonly Color LaserGlow = new Color(1f, 0.7f, 0.3f);
        private static readonly Color ScannerGlow = new Color(0.42f, 0.9f, 0.86f);
        private static readonly Color FlaskGlow = new Color(1f, 0.62f, 0.2f); // warm Scotch amber

        // Camp layout relative to the fire (x east, y north): the player wakes on the west side facing east,
        // Cavendish sits on the log across the fire, and the merchant settles south-east of it.
        private static readonly Vector2 WakeSpot = new Vector2(-2.2f, -0.3f);
        private static readonly Vector2 CavendishSpot = new Vector2(3.4f, 0.5f);
        private static readonly Vector2 MerchantSpot = new Vector2(1.8f, -3f);

        public static void Install(Transform level, Transform gameplay, Transform crash, Transform hollow)
        {
            var story = new GameObject("Tutorial Story").transform;
            story.SetParent(gameplay, false);
            Vector2 camp = TutorialLevelBuilder.Camp;

            // The player wakes empty-handed; the spawn point (respawns too) is beside the fire.
            PlayerLife player = Object.FindObjectsByType<PlayerLife>(FindObjectsInactive.Include).First();
            var gear = new SerializedObject(player.GetComponent<PlayerGear>());
            gear.FindProperty("startingItems").arraySize = 0;
            gear.ApplyModifiedPropertiesWithoutUndo();
            Vector3 wake = TutorialLevelBuilder.Ground(camp + WakeSpot);
            Quaternion facingFire = Facing(camp + WakeSpot, camp);
            PlayerSpawnPoint spawn = Object.FindObjectsByType<PlayerSpawnPoint>().First();
            spawn.transform.SetPositionAndRotation(wake, facingFire);
            player.transform.SetPositionAndRotation(wake + Vector3.up * 0.05f, facingFire);

            NpcActor cavendish = BuildCavendish(story, camp);
            Transform cockpit = CockpitView(crash);
            Transform window = CockpitWindow(crash);
            Transform[] glimpses = DragGlimpses(story);
            ItemPickup laser = BuildLaser(crash);
            ItemPickup scanner = BuildScanner(crash);
            MerchantCrab merchant = BuildMerchant(story, hollow, camp);
            FrogWaves frogs = BuildPondFrogs(story);
            ChiefToadEncounter toad = BuildChiefToad(story, frogs);

            var flow = new SerializedObject(story.gameObject.AddComponent<TutorialStory>());
            flow.FindProperty("cavendish").objectReferenceValue = cavendish;
            flow.FindProperty("merchant").objectReferenceValue = merchant;
            flow.FindProperty("player").objectReferenceValue = player;
            flow.FindProperty("cockpitView").objectReferenceValue = cockpit;
            flow.FindProperty("cockpitWindow").objectReferenceValue = window;
            SerializedProperty list = flow.FindProperty("dragGlimpses");
            list.arraySize = glimpses.Length;
            for (int i = 0; i < glimpses.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = glimpses[i];
            flow.FindProperty("laserPickup").objectReferenceValue = laser;
            flow.FindProperty("scannerPickup").objectReferenceValue = scanner;
            flow.FindProperty("frogs").objectReferenceValue = frogs;
            flow.FindProperty("cavendishSitDrop").floatValue = SitDrop;
            flow.FindProperty("toad").objectReferenceValue = toad;
            flow.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Cast ----------

        private static NpcActor BuildCavendish(Transform story, Vector2 camp)
        {
            GameObject cavendish = Instance(CavendishPath, story);
            cavendish.name = "Cavendish";
            // Sitting on his log across the fire: the log top is his seat, and his (unrigged) model sinks onto it.
            Transform log = CampLog(story.root, "Cavendish's Log");
            Vector3 seat = log != null ? LogTop(log) : TutorialLevelBuilder.Ground(camp + CavendishSpot);
            cavendish.transform.SetPositionAndRotation(seat, Facing(new Vector2(seat.x, seat.z), camp));
            Transform visual = cavendish.transform.Find("Visual");
            if (log != null && visual != null) visual.localPosition += Vector3.down * SitDrop;
            var actor = cavendish.AddComponent<NpcActor>();
            cavendish.AddComponent<QuestSignalSource>();
            var voice = new GameObject("Cavendish Dialogue");
            voice.transform.SetParent(cavendish.transform, false);
            voice.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            var dialogue = new SerializedObject(voice.AddComponent<NpcDialogue>());
            dialogue.FindProperty("speaker").stringValue = "Cavendish";
            dialogue.ApplyModifiedPropertiesWithoutUndo();
            return actor;
        }

        // The pond's frog colony: nothing until Cavendish's quest, then 20 frogs in three waves from around the bank.
        private static FrogWaves BuildPondFrogs(Transform story)
        {
            Vector2 pond = TutorialLevelBuilder.Pond, radii = TutorialLevelBuilder.PondRadii;
            var colony = new GameObject("Pond Frogs").transform;
            colony.SetParent(story, false);
            colony.position = TutorialLevelBuilder.Ground(pond);
            var zone = colony.gameObject.AddComponent<CombatZone>();
            zone.Radius = Mathf.Max(radii.x, radii.y) + 12f;
            var points = new Transform[12];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / points.Length;
                Vector2 at = pond + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y) * 1.35f;
                points[i] = new GameObject("Bank " + (i + 1)).transform;
                points[i].SetParent(colony, false);
                points[i].position = TutorialLevelBuilder.Ground(at);
            }
            var waves = new SerializedObject(colony.gameObject.AddComponent<FrogWaves>());
            waves.FindProperty("frogPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MortarFrog>(FrogPath);
            SerializedProperty list = waves.FindProperty("spawnPoints");
            list.arraySize = points.Length;
            for (int i = 0; i < points.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            waves.ApplyModifiedPropertiesWithoutUndo();
            return colony.GetComponent<FrogWaves>();
        }

        // The Chief Toad, switched off in the middle of the pond until Cavendish's last quest arms the encounter. His
        // arena is widened to cover our pond (50 x 34 m; his test pond is much smaller).
        private static ChiefToadEncounter BuildChiefToad(Transform story, FrogWaves frogs)
        {
            Vector2 pond = TutorialLevelBuilder.Pond, radii = TutorialLevelBuilder.PondRadii;
            var encounter = new GameObject("Chief Toad Encounter").transform;
            encounter.SetParent(story, false);
            var centre = new GameObject("Pond Centre").transform;
            centre.SetParent(encounter, false);
            centre.position = new Vector3(pond.x, TutorialLevelBuilder.PondWater, pond.y);
            centre.gameObject.AddComponent<QuestSignalSource>().enabled = false;

            GameObject toad = Instance(ChiefToadPath, encounter);
            toad.name = "Chief Toad";
            toad.transform.position = centre.position;
            var boss = new SerializedObject(toad.GetComponent<ChiefToad>());
            boss.FindProperty("arenaRadius").floatValue = Mathf.Max(radii.x, radii.y) + 15f;
            boss.ApplyModifiedPropertiesWithoutUndo();
            toad.SetActive(false);

            var data = new SerializedObject(encounter.gameObject.AddComponent<ChiefToadEncounter>());
            data.FindProperty("toad").objectReferenceValue = toad.GetComponent<ChiefToad>();
            data.FindProperty("frogs").objectReferenceValue = frogs;
            data.FindProperty("pondCentre").objectReferenceValue = centre;
            data.FindProperty("triggerRadius").floatValue = Mathf.Max(radii.x, radii.y) + 15f;
            data.ApplyModifiedPropertiesWithoutUndo();
            return encounter.GetComponent<ChiefToadEncounter>();
        }

        private static MerchantCrab BuildMerchant(Transform story, Transform hollow, Vector2 camp)
        {
            CombatZone zone = hollow.GetComponent<CombatZone>();
            Vector2 centre = TutorialLevelBuilder.Hollow;

            // Where it settles at camp, and the flask it drops when it gives up.
            var spot = new GameObject("Merchant Spot At Camp").transform;
            spot.SetParent(story, false);
            spot.SetPositionAndRotation(TutorialLevelBuilder.Ground(camp + MerchantSpot), Facing(camp + MerchantSpot, camp));
            // Once home it hops up onto the other log by the fire.
            var seat = new GameObject("Merchant Seat").transform;
            seat.SetParent(story, false);
            Transform otherLog = CampLog(story.root, "LogSeat");
            Vector3 top = otherLog != null ? LogTop(otherLog) : spot.position;
            seat.SetPositionAndRotation(top, Facing(new Vector2(top.x, top.z), camp));
            GameObject flask = Instance(FlaskPath, story);
            flask.name = "Scotch Flask";
            SetEnum(flask.AddComponent<ItemPickup>(), "item", (int)ItemPickup.Item.Flask);
            // Glints like the laser and scanner, but from any distance: it is dropped mid-brawl and should stand out.
            Glint(flask.transform, new Vector3(0f, 0.4f, 0f), FlaskGlow, null, 0f);
            flask.SetActive(false);

            GameObject merchant = Instance(MerchantPath, hollow);
            merchant.name = "Eugene"; // also its name in the HUD target readout
            merchant.transform.SetPositionAndRotation(TutorialLevelBuilder.Ground(centre + new Vector2(1.5f, 1.5f)), Facing(centre + new Vector2(1.5f, 1.5f), centre + new Vector2(10f, -3f)));
            if (merchant.GetComponent<QuestSignalSource>() == null) merchant.AddComponent<QuestSignalSource>();
            SetZone(merchant.GetComponent<HermitCrab>(), zone);

            // The ten cousins loaf around the hollow until the quest starts.
            var cousins = new CrabCousin[CrabCousinBuilder.PrefabPaths.Count()];
            int i = 0;
            foreach (string path in CrabCousinBuilder.PrefabPaths)
            {
                float angle = (i + 0.3f) / cousins.Length * Mathf.PI * 2f;
                Vector2 at = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (i % 2 == 0 ? 5.5f : 8.5f);
                GameObject cousin = Instance(path, hollow);
                cousin.transform.SetPositionAndRotation(TutorialLevelBuilder.Ground(at), Facing(at, centre) * Quaternion.Euler(0f, 160f + 40f * (i % 3), 0f));
                SetZone(cousin.GetComponent<HermitCrab>(), zone);
                cousins[i++] = cousin.GetComponent<CrabCousin>();
            }

            var data = new SerializedObject(merchant.GetComponent<MerchantCrab>());
            data.FindProperty("returnPoint").objectReferenceValue = spot;
            data.FindProperty("seat").objectReferenceValue = seat;
            data.FindProperty("flaskDrop").objectReferenceValue = flask.GetComponent<ItemPickup>();
            data.FindProperty("startFriendly").boolValue = false;
            SerializedProperty list = data.FindProperty("cousins");
            list.arraySize = cousins.Length;
            for (int c = 0; c < cousins.Length; c++) list.GetArrayElementAtIndex(c).objectReferenceValue = cousins[c];
            data.ApplyModifiedPropertiesWithoutUndo();
            // Nobody is in the hollow until Cavendish's big-crab quest (TutorialStory switches them on).
            merchant.SetActive(false);
            foreach (CrabCousin cousin in cousins) cousin.gameObject.SetActive(false);
            return merchant.GetComponent<MerchantCrab>();
        }

        private static void SetZone(HermitCrab crab, CombatZone zone)
        {
            var data = new SerializedObject(crab);
            data.FindProperty("territory.zone").objectReferenceValue = zone;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- The hidden gear ----------

        // Wedged at the far end of the open reactor bay, under a rib, beside the empty cradle.
        private static ItemPickup BuildLaser(Transform crash)
        {
            Transform hull = Hull(crash);
            GameObject laser = Instance(LaserPath, crash);
            laser.name = "Mining Laser Pickup";
            laser.transform.SetPositionAndRotation(hull.TransformPoint(new Vector3(-1.7f, 1.42f, -4.3f)), hull.rotation * Quaternion.Euler(-8f, 115f, -28f));
            var pickup = laser.AddComponent<ItemPickup>();
            SetEnum(pickup, "item", (int)ItemPickup.Item.Laser);
            laser.AddComponent<QuestSignalSource>().enabled = false;
            Glint(laser.transform, new Vector3(0f, 0.5f, 0f), LaserGlow, null);
            return pickup;
        }

        // Under a hull panel that fell off by the tail, on the port side of the trench.
        private static ItemPickup BuildScanner(Transform crash)
        {
            Vector2 forward = TutorialLevelBuilder.ShipForward, port = new Vector2(-forward.y, forward.x);
            Vector2 spot = TutorialLevelBuilder.Ship - forward * 15f + port * 5f;
            Vector3 ground = TutorialLevelBuilder.Ground(spot);

            var root = new GameObject("Scanner Pickup");
            root.transform.SetParent(crash, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(new Vector3(-port.x, 0f, -port.y)) * Quaternion.Euler(0f, 25f, 0f));
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(ScannerRigPath);
            GameObject model = Object.Instantiate(rig.transform.Find("ScannerVisualRoot/ScannerModel").gameObject, root.transform);
            model.name = "Scanner Model";
            model.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            model.transform.localRotation = Quaternion.Euler(-75f, 0f, 20f); // lying on its back
            model.transform.localScale = Vector3.one * 0.6f;
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = Vector3.Max(bounds.size + Vector3.one * 0.15f, Vector3.one * 0.5f);
            var pickup = root.AddComponent<ItemPickup>();
            SetEnum(pickup, "item", (int)ItemPickup.Item.Scanner);
            Glint(root.transform, new Vector3(0f, 0.4f, 0f), ScannerGlow, null);

            // The panel leans over it like a lean-to: its low edge on the ground to port, the high edge open toward
            // the trench, so the scanner (and its glint) shows from the trench side.
            GameObject panel = Instance(CrashSiteWreckBuilder.DebrisPath("HullPanel"), crash);
            panel.name = "Fallen Hull Panel";
            var along = new Vector3(forward.x, 0f, forward.y);
            Vector3 starboard = Vector3.Cross(Vector3.up, along);
            const float tilt = 35f;
            panel.transform.SetPositionAndRotation(ground + starboard * 0.35f + Vector3.up * (1.1f * Mathf.Sin(tilt * Mathf.Deg2Rad) - 0.1f),
                Quaternion.LookRotation(along) * Quaternion.Euler(0f, 0f, tilt));
            return pickup;
        }

        private static void Glint(Transform pickup, Vector3 at, Color color, Transform model, float visibleDistance = 8f)
        {
            var light = new GameObject("Glint").AddComponent<Light>();
            light.transform.SetParent(pickup, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.range = 3.5f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            var glint = new SerializedObject(pickup.gameObject.AddComponent<PickupGlint>());
            glint.FindProperty("glow").objectReferenceValue = light;
            glint.FindProperty("model").objectReferenceValue = model;
            glint.FindProperty("visibleDistance").floatValue = visibleDistance;
            glint.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Opening camera marks ----------

        // The pilot seat: slumped forward over the console, head tilted.
        private static Transform CockpitView(Transform crash)
        {
            Transform hull = Hull(crash);
            var view = new GameObject("Opening - Cockpit View").transform;
            view.SetParent(hull, false);
            view.localPosition = new Vector3(0.15f, 3.7f, 6.7f);
            view.localRotation = Quaternion.Euler(-4f, -5f, 10f); // the console, its plaque and the sparks ahead
            return view;
        }

        // The smashed starboard cockpit window Cavendish pulls the mechanic out through, facing out of the ship.
        private static Transform CockpitWindow(Transform crash)
        {
            Transform hull = Hull(crash);
            Pose pose = CrashSiteWreckBuilder.CockpitWindow();
            var window = new GameObject("Opening - Cockpit Window").transform;
            window.SetParent(hull, false);
            window.SetLocalPositionAndRotation(pose.position, pose.rotation);
            return window;
        }

        // Two glimpses on the way from the wreck to camp, each facing back toward the wreck.
        private static Transform[] DragGlimpses(Transform story)
        {
            Vector2 ship = TutorialLevelBuilder.Ship;
            Vector2[] points = TutorialLevelBuilder.DragPoints;
            var marks = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var mark = new GameObject("Opening - Drag Glimpse " + (i + 1)).transform;
                mark.SetParent(story, false);
                Vector3 toShip = TutorialLevelBuilder.Ground(ship) - TutorialLevelBuilder.Ground(points[i]);
                toShip.y = 0f;
                mark.SetPositionAndRotation(TutorialLevelBuilder.Ground(points[i]) + Vector3.up * 0.55f,
                    Quaternion.LookRotation(toShip) * Quaternion.Euler(-6f, 0f, 0f));
                marks[i] = mark;
            }
            return marks;
        }

        // ---------- Helpers ----------

        // How far Cavendish's model sinks so he looks seated on his log.
        private const float SitDrop = 0.55f;

        private static Transform CampLog(Transform root, string name)
        {
            var camp = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "Campsite");
            return camp == null ? null : camp.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        // The middle of a log's top surface.
        private static Vector3 LogTop(Transform log)
        {
            var bounds = new Bounds(log.position, Vector3.zero);
            foreach (Renderer part in log.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        private static Transform Hull(Transform crash) => crash.GetComponentsInChildren<Transform>(true).First(t => t.name == "Hull");

        private static GameObject Instance(string path, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new System.InvalidOperationException("Missing prefab " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        private static Quaternion Facing(Vector2 from, Vector2 to) => Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.y - from.y));

        private static void SetEnum(Object target, string field, int value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).enumValueIndex = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
