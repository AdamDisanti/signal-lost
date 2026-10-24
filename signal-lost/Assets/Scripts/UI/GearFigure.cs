using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Inventory;
using SignalLost.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SignalLost.UI
{
    /// <summary>
    /// The 3D view in the pockets menu's Gear tab (meeting notes 10-23): a blocky mechanic, Minecraft-inspired but in
    /// Signal Lost's low-poly style, holding the mining laser and the scanner with the flask on the belt, wearing every
    /// upgrade bought (a coil on the laser, an antenna on the scanner, padding...). Each tool also gets a close-up.
    /// Everything lives on a hidden stage far below the level on its own layer and renders into textures the menu
    /// shows; nothing is drawn while the menu is closed. Models come from the player's own gear and the ItemCatalog.
    /// </summary>
    public sealed class GearFigure : MonoBehaviour
    {
        public const int Layer = 30;
        private static readonly Vector3 StagePosition = new Vector3(0f, -3000f, 0f);

        public enum Shot { Figure, Laser, Scanner, Flask }

        private static readonly Color Suit = new Color(0.93f, 0.48f, 0.15f);
        private static readonly Color Cream = new Color(0.9f, 0.86f, 0.78f);
        private static readonly Color Glove = new Color(0.62f, 0.44f, 0.27f);
        private static readonly Color Skin = new Color(0.93f, 0.74f, 0.58f);
        private static readonly Color Boot = new Color(0.25f, 0.18f, 0.13f);
        private static readonly Color Belt = new Color(0.18f, 0.16f, 0.15f);
        private static readonly Color Teal = new Color(0.42f, 0.9f, 0.86f);

        private readonly Dictionary<Shot, Camera> cameras = new Dictionary<Shot, Camera>();
        private readonly Dictionary<Shot, RenderTexture> textures = new Dictionary<Shot, RenderTexture>();
        private readonly Dictionary<Shot, Transform> pivots = new Dictionary<Shot, Transform>();
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly List<GameObject> attachments = new List<GameObject>();
        private Transform figureLaser, figureScanner, figureFlask, torso, kneeL, kneeR;
        private Transform showLaser, showScanner, showFlask;
        private string builtFor = "\u0000";
        private float spin;

        public static GearFigure Create(PlayerGear gear)
        {
            var stage = new GameObject("Gear Figure Stage");
            stage.transform.position = StagePosition;
            var figure = stage.AddComponent<GearFigure>();
            figure.Build(gear);
            stage.SetActive(false);
            return figure;
        }

        public RenderTexture Texture(Shot shot) => textures[shot];

        /// <summary>Turns the mechanic (degrees), e.g. when the player drags the view.</summary>
        public void Turn(float degrees) => spin += degrees;

        public void Show(bool show) => gameObject.SetActive(show);

        /// <summary>Puts on the parts of every upgrade owned (rebuilt only when that changes).</summary>
        public void Refresh(PlayerInventory inventory)
        {
            string key = string.Join(",", inventory.Upgrades);
            if (key == builtFor) return;
            builtFor = key;
            foreach (GameObject part in attachments) Destroy(part);
            attachments.Clear();
            foreach (string id in inventory.Upgrades)
            {
                GameObject model = ItemCatalog.Model(id);
                if (model == null) continue;
                switch (id)
                {
                    case "overclocked_coil": OnTool(model, figureLaser, showLaser, b => new Vector3(b.center.x, b.center.y + b.extents.y * 0.25f, b.min.z + b.size.z * 0.78f), Vector3.zero); break;
                    case "quickcool_fins": OnTool(model, figureLaser, showLaser, b => new Vector3(b.center.x, b.max.y, b.center.z - b.size.z * 0.1f), Vector3.zero); break;
                    case "heavy_slam": OnTool(model, figureLaser, showLaser, b => new Vector3(b.center.x, b.center.y + b.extents.y * 0.25f, b.max.z + 0.01f), Vector3.zero); break;
                    case "longnose_antenna": OnTool(model, figureScanner, showScanner, b => new Vector3(b.max.x - b.size.x * 0.2f, b.max.y - 0.01f, b.center.z), Vector3.zero); break;
                    case "castiron_liver": OnTool(model, figureFlask, showFlask, b => b.center, Vector3.zero); break;
                    case "padded_overalls": Attach(model, torso, new Vector3(0f, 0.05f, 0.17f), Vector3.zero, 1f); break;
                    case "slick_kneepads":
                        Attach(model, kneeL, new Vector3(0f, 0f, 0.13f), Vector3.zero, 1f);
                        Attach(model, kneeR, new Vector3(0f, 0f, 0.13f), Vector3.zero, 1f);
                        break;
                }
            }
        }

        private void Update()
        {
            // Faces the viewer three-quarters on and sways gently; dragging turns it.
            pivots[Shot.Figure].localRotation = Quaternion.Euler(0f, 20f + spin + Mathf.Sin(Time.unscaledTime * 0.6f) * 18f, 0f);
            foreach (Shot tool in new[] { Shot.Laser, Shot.Scanner, Shot.Flask })
                pivots[tool].localRotation = Quaternion.Euler(0f, Time.unscaledTime * 25f, 0f);
        }

        private void OnDestroy()
        {
            foreach (RenderTexture texture in textures.Values) if (texture != null) texture.Release();
            foreach (Material material in materials.Values) Destroy(material);
        }

        // ---------- Building ----------

        private void Build(PlayerGear gear)
        {
            // The mechanic, built from boxes (~1.85 m tall), facing +Z.
            Transform figure = Pivot(Shot.Figure, Vector3.zero);
            var body = new GameObject("Mechanic").transform;
            body.SetParent(figure, false);
            Box(body, "Leg L", Suit, new Vector3(-0.13f, 0.42f, 0f), new Vector3(0.22f, 0.66f, 0.24f));
            Box(body, "Leg R", Suit, new Vector3(0.13f, 0.42f, 0f), new Vector3(0.22f, 0.66f, 0.24f));
            Box(body, "Boot L", Boot, new Vector3(-0.13f, 0.06f, 0.03f), new Vector3(0.24f, 0.14f, 0.3f));
            Box(body, "Boot R", Boot, new Vector3(0.13f, 0.06f, 0.03f), new Vector3(0.24f, 0.14f, 0.3f));
            kneeL = Empty(body, new Vector3(-0.13f, 0.45f, 0f));
            kneeR = Empty(body, new Vector3(0.13f, 0.45f, 0f));
            Box(body, "Torso", Suit, new Vector3(0f, 1.08f, 0f), new Vector3(0.52f, 0.62f, 0.3f));
            torso = Empty(body, new Vector3(0f, 1.08f, 0f));
            Box(body, "Stripe", Cream, new Vector3(0f, 0.86f, 0f), new Vector3(0.53f, 0.06f, 0.31f));
            Box(body, "Belt", Belt, new Vector3(0f, 0.78f, 0f), new Vector3(0.54f, 0.08f, 0.32f));
            Box(body, "Patch", Teal, new Vector3(-0.13f, 1.22f, 0.155f), new Vector3(0.1f, 0.1f, 0.01f));
            Box(body, "Patch Gear", Cream, new Vector3(-0.13f, 1.22f, 0.161f), new Vector3(0.05f, 0.05f, 0.005f));
            Transform head = Box(body, "Head", Skin, new Vector3(0f, 1.62f, 0f), new Vector3(0.38f, 0.38f, 0.36f));
            Box(head, "Cap", Suit, new Vector3(0f, 0.22f, 0f), new Vector3(1.04f, 0.3f, 1.04f));
            Box(head, "Peak", Suit * 0.85f, new Vector3(0f, 0.12f, 0.6f), new Vector3(1.04f, 0.08f, 0.35f));
            Box(head, "Badge", Cream, new Vector3(0f, 0.24f, 0.525f), new Vector3(0.2f, 0.16f, 0.02f));
            Box(head, "Eye L", new Color(0.08f, 0.07f, 0.07f), new Vector3(-0.2f, -0.02f, 0.505f), new Vector3(0.12f, 0.1f, 0.02f));
            Box(head, "Eye R", new Color(0.08f, 0.07f, 0.07f), new Vector3(0.2f, -0.02f, 0.505f), new Vector3(0.12f, 0.1f, 0.02f));
            Box(head, "Nose", Skin * 0.92f, new Vector3(0f, -0.14f, 0.53f), new Vector3(0.12f, 0.14f, 0.08f));
            Box(head, "Stubble", new Color(0.62f, 0.4f, 0.25f), new Vector3(0f, -0.38f, 0.51f), new Vector3(0.8f, 0.2f, 0.04f));

            // Arms: the right reaches forward holding the laser, the left holds the scanner low.
            Transform armR = Arm(body, "Arm R", new Vector3(0.36f, 1.32f, 0f), new Vector3(-62f, 0f, 6f));
            Transform armL = Arm(body, "Arm L", new Vector3(-0.36f, 1.32f, 0f), new Vector3(-25f, 0f, -10f));
            figureLaser = Holding(armR, LaserModel(gear), new Vector3(0f, -0.62f, 0.06f), new Vector3(62f, 0f, 0f), 0.75f);
            figureScanner = Holding(armL, ScannerModel(gear), new Vector3(0f, -0.62f, 0.08f), new Vector3(25f, 0f, 0f), 0.55f);
            figureFlask = Holding(body, FlaskModel(gear), new Vector3(0.3f, 0.7f, 0.06f), new Vector3(0f, 90f, 0f), 0.5f);

            // Close-ups of each tool, side on.
            showLaser = Holding(Pivot(Shot.Laser, new Vector3(20f, 0f, 0f)), LaserModel(gear), Vector3.zero, Vector3.zero, 1f);
            showScanner = Holding(Pivot(Shot.Scanner, new Vector3(40f, 0f, 0f)), ScannerModel(gear), Vector3.zero, Vector3.zero, 1f);
            showFlask = Holding(Pivot(Shot.Flask, new Vector3(60f, 0f, 0f)), FlaskModel(gear), Vector3.zero, Vector3.zero, 1f);
            foreach (Transform tool in new[] { showLaser, showScanner, showFlask }) Centre(tool);

            Lamp(new Vector3(35f, 150f, 0f), new Color(1f, 0.93f, 0.82f), 1.2f);
            Lamp(new Vector3(20f, -30f, 0f), new Color(0.55f, 0.75f, 1f), 0.6f);
            Lens(Shot.Figure, 512, 768, new Vector3(0f, 1.0f, 0f), 4.4f, 30f);
            foreach (Shot tool in new[] { Shot.Laser, Shot.Scanner, Shot.Flask })
                Lens(tool, 384, 256, pivots[tool].position - transform.position, Size(tool) * 1.9f, 30f);
            SetLayer(transform);
        }

        private float Size(Shot tool)
        {
            Transform model = tool == Shot.Laser ? showLaser : tool == Shot.Scanner ? showScanner : showFlask;
            return model != null ? Bounds(model).extents.magnitude : 0.3f;
        }

        private Transform Pivot(Shot shot, Vector3 offset)
        {
            var pivot = new GameObject(shot + " Pivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = offset;
            pivots[shot] = pivot;
            return pivot;
        }

        private void Lens(Shot shot, int width, int height, Vector3 target, float distance, float fov)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Gear " + shot };
            var lens = new GameObject(shot + " Camera").AddComponent<Camera>();
            lens.transform.SetParent(transform, false);
            lens.transform.localPosition = target + new Vector3(0f, distance * 0.12f, distance);
            lens.transform.LookAt(transform.position + target);
            lens.fieldOfView = fov;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = new Color(0f, 0f, 0f, 0f);
            lens.cullingMask = 1 << Layer;
            lens.nearClipPlane = 0.05f;
            lens.farClipPlane = distance * 3f + 5f;
            lens.targetTexture = texture;
            UniversalAdditionalCameraData data = lens.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            cameras[shot] = lens;
            textures[shot] = texture;
        }

        private void Lamp(Vector3 angles, Color color, float intensity)
        {
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.rotation = Quaternion.Euler(angles);
            lamp.type = LightType.Directional;
            lamp.color = color;
            lamp.intensity = intensity;
            lamp.cullingMask = 1 << Layer;
            lamp.shadows = LightShadows.None;
        }

        private Transform Arm(Transform body, string name, Vector3 shoulder, Vector3 angles)
        {
            Transform pivot = Empty(body, shoulder);
            pivot.name = name;
            pivot.localRotation = Quaternion.Euler(angles);
            Box(pivot, "Sleeve", Suit, new Vector3(0f, -0.25f, 0f), new Vector3(0.17f, 0.5f, 0.18f));
            Box(pivot, "Cuff", Cream, new Vector3(0f, -0.5f, 0f), new Vector3(0.18f, 0.05f, 0.19f));
            Box(pivot, "Glove", Glove, new Vector3(0f, -0.6f, 0f), new Vector3(0.17f, 0.16f, 0.18f));
            return pivot;
        }

        private Transform Holding(Transform parent, GameObject model, Vector3 position, Vector3 angles, float scale)
        {
            if (model == null) return null;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = position;
            model.transform.localRotation = Quaternion.Euler(angles);
            model.transform.localScale = Vector3.one * scale;
            return model.transform;
        }

        // Puts a tool's middle on its pivot so it turns in place.
        private static void Centre(Transform tool)
        {
            if (tool == null) return;
            tool.position -= Bounds(tool).center - tool.parent.position;
        }

        // Puts an upgrade part on both copies of a tool, placed from the tool's own (unrotated) bounds.
        private void OnTool(GameObject model, Transform onFigure, Transform onShowcase, System.Func<Bounds, Vector3> where, Vector3 angles)
        {
            foreach (Transform tool in new[] { onFigure, onShowcase })
            {
                if (tool == null) continue;
                Bounds local = LocalBounds(tool);
                GameObject part = Instantiate(model, tool, false);
                part.transform.localPosition = where(local);
                part.transform.localRotation = Quaternion.Euler(angles);
                part.transform.localScale = Vector3.one; // the parts are made to fit the tools at their own scale
                SetLayer(part.transform);
                attachments.Add(part);
            }
        }

        // Puts an upgrade part on an (unscaled) mount on the mechanic's body.
        private void Attach(GameObject model, Transform host, Vector3 position, Vector3 angles, float scale)
        {
            GameObject part = Instantiate(model, host, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(angles);
            part.transform.localScale = Vector3.one * scale;
            SetLayer(part.transform);
            attachments.Add(part);
        }

        // Children of a box (the head's cap, eyes...) are placed in the box's own scaled units.
        private Transform Box(Transform parent, string name, Color color, Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Destroy(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            var renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = Paint(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return box.transform;
        }

        private static Transform Empty(Transform parent, Vector3 position)
        {
            var empty = new GameObject("Mount").transform;
            empty.SetParent(parent, false);
            empty.localPosition = position;
            return empty;
        }

        private Material Paint(Color color)
        {
            string key = ColorUtility.ToHtmlStringRGB(color);
            if (materials.TryGetValue(key, out Material material)) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Gear Figure " + key };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            materials[key] = material;
            return material;
        }

        // ---------- The player's own tool models ----------

        private static GameObject LaserModel(PlayerGear gear)
        {
            MiningLaser laser = gear != null ? gear.Laser : null;
            return laser != null && laser.HeldModel != null ? Copy(laser.HeldModel) : null;
        }

        private static GameObject ScannerModel(PlayerGear gear)
        {
            GameObject visual = gear != null && gear.Scanner != null ? gear.Scanner.Visual : null;
            if (visual == null) return null;
            GameObject copy = Copy(visual);
            copy.transform.localPosition = Vector3.zero;
            return copy;
        }

        private static GameObject FlaskModel(PlayerGear gear)
        {
            var hands = gear != null ? gear.GetComponent<FirstPersonHands>() : null;
            return hands != null && hands.FlaskModel != null ? Copy(hands.FlaskModel) : null;
        }

        // A copy with only its looks: no colliders, scripts, lights or particles.
        private static GameObject Copy(GameObject source)
        {
            GameObject copy = Instantiate(source);
            copy.SetActive(true);
            foreach (Transform part in copy.GetComponentsInChildren<Transform>(true)) part.gameObject.SetActive(true);
            foreach (Component part in copy.GetComponentsInChildren<Component>(true))
                if (part is Collider || part is MonoBehaviour || part is Light || part is ParticleSystem || part is AudioSource) Destroy(part);
            foreach (Renderer part in copy.GetComponentsInChildren<Renderer>(true)) part.shadowCastingMode = ShadowCastingMode.Off;
            return copy;
        }

        private static Bounds Bounds(Transform root)
        {
            var bounds = new Bounds(root.position, Vector3.zero);
            bool first = true;
            foreach (Renderer part in root.GetComponentsInChildren<Renderer>(true))
            {
                if (part is ParticleSystemRenderer) continue;
                if (first) { bounds = part.bounds; first = false; }
                else bounds.Encapsulate(part.bounds);
            }
            return bounds;
        }

        // Bounds in the tool's own space (so placement does not depend on how the tool is turned).
        private static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (MeshFilter part in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (part.sharedMesh == null) continue;
                Matrix4x4 toRoot = root.worldToLocalMatrix * part.transform.localToWorldMatrix;
                Bounds mesh = part.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = toRoot.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else bounds.Encapsulate(point);
                }
            }
            return bounds;
        }

        private static void SetLayer(Transform root)
        {
            foreach (Transform part in root.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = Layer;
        }
    }
}
