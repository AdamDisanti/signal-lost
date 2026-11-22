// Copy-only smoke (never in the real project). Batch: -executeMethod MudBuildingsSmoke.Start
// Builds the Mudlands, validates the buildings and their quest hooks, scans the whole level for z-fighting
// (coplanar or nearly coplanar overlapping faces of different materials), and renders every building from several
// sides, from below the eaves too, into contact sheets. Env: MUD_SMOKE_OUT (folder for the report and pictures),
// MUD_SMOKE_SKIP_BUILD=1 (use the saved scene), MUD_SMOKE_RENDER=0 (no pictures).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SignalLost.Editor;
using SignalLost.Mudlands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MudBuildingsSmoke
{
    private static readonly StringBuilder Report = new StringBuilder();
    private static int failures;
    private static readonly List<string> Errors = new List<string>();

    // Every renderer in the open scene, the models MudProp builds with HideFlags.DontSave included (Find skips those).
    private static List<T> SceneAll<T>() where T : Component =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToList();

    private static void Note(string line) { Report.AppendLine(line); Debug.Log("MUDSMOKE " + line); }
    private static void Fail(string line) { failures++; Note("FAIL " + line); }

    public static void Start()
    {
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        string temporary = Path.GetFullPath(Path.GetTempPath()).Replace('\\', '/').TrimEnd('/') + "/";
        if (!Application.isBatchMode || !project.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use only a disposable batch-mode project under the system temporary directory.");
        string output = Environment.GetEnvironmentVariable("MUD_SMOKE_OUT") ?? Path.Combine(project, "SmokeOut");
        Directory.CreateDirectory(output);
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Errors.Add(message);
        };
        int code = 0;
        try
        {
            if (Environment.GetEnvironmentVariable("MUD_SMOKE_SKIP_BUILD") != "1")
            {
                float t = Time.realtimeSinceStartup;
                MudlandsBuilder.Build();
                Note($"built in {Time.realtimeSinceStartup - t:0.0} s, {Errors.Count} errors while building");
                foreach (string e in Errors.Take(10)) Fail("build error: " + e);
            }
            else EditorSceneManager.OpenScene(MudlandsBuilder.ScenePath);
            Validate();
            ScanZFighting(output);
            if (Environment.GetEnvironmentVariable("MUD_SMOKE_RENDER") != "0") RenderBuildings(output);
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
        Note(failures == 0 ? "MUD_BUILDINGS_SMOKE_PASS" : $"MUD_BUILDINGS_SMOKE_FAIL ({failures})");
        File.WriteAllText(Path.Combine(output, "report.txt"), Report.ToString());
        code = failures == 0 ? 0 : 1;
        EditorApplication.Exit(code);
    }

    // ---------------------------------------------------------------- validation
    private static readonly MudModels.Kind[] BuildingKinds =
    {
        MudModels.Kind.StiltShack, MudModels.Kind.Cabin, MudModels.Kind.DrivingSchool, MudModels.Kind.Kiosk, MudModels.Kind.Sawmill,
        MudModels.Kind.Outhouse, MudModels.Kind.WitchHut, MudModels.Kind.Roadhouse, MudModels.Kind.Windmill, MudModels.Kind.Grandstand,
        MudModels.Kind.BananaStand, MudModels.Kind.DojoGate, MudModels.Kind.GasPump
    };

    private static IEnumerable<MudProp> Buildings(bool includeInactive = true) =>
        Object.FindObjectsByType<MudProp>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(p => BuildingKinds.Contains(p.Kind));

    private static void Validate()
    {
        var props = Buildings().ToList();
        Note("buildings: " + string.Join(", ", props.GroupBy(p => p.Kind).Select(g => g.Key + " x" + g.Count())));
        foreach (MudProp prop in props)
        {
            var filter = prop.transform.Find("Model/Mesh")?.GetComponent<MeshFilter>();
            var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("Mud")) { Fail($"{prop.name}: no Blender model ({filter?.sharedMesh?.name})"); continue; }
            foreach (Material m in renderer.sharedMaterials)
                if (m == null || m.GetColor("_BaseColor") == Color.magenta || !AssetDatabase.Contains(m)) Fail($"{prop.name}: material {m?.name} missing or unknown");
            if (prop.GetComponentsInChildren<BoxCollider>(true).Length == 0) Fail($"{prop.name}: no colliders");
        }
        // Quest hooks the buildings carry.
        Require(Object.FindAnyObjectByType<DrivingTestKiosk>(FindObjectsInactive.Include) != null, "the driving test kiosk");
        Require(Object.FindAnyObjectByType<RoadhouseGate>(FindObjectsInactive.Include) != null, "the roadhouse gate");
        Require(GameObject.Find("Neon") != null, "the roadhouse neon");
        // Hex lanterns stand on their roofs: a ray down from above the lantern meets the building's model at its base.
        var lanterns = Object.FindObjectsByType<CursedLantern>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.name.StartsWith("Hex Lantern")).ToList();
        Require(lanterns.Count == 3, "three hex lanterns, found " + lanterns.Count);
        var temporary = new List<MeshCollider>();
        foreach (MudProp prop in props)
        {
            var filter = prop.transform.Find("Model/Mesh")?.GetComponent<MeshFilter>();
            if (filter == null) continue;
            var go = new GameObject("probe");
            go.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
            go.transform.localScale = filter.transform.lossyScale;
            go.layer = 31;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = filter.sharedMesh;
            temporary.Add(mc);
        }
        Physics.SyncTransforms();
        foreach (CursedLantern lantern in lanterns)
        {
            Vector3 p = lantern.transform.position;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, 1 << 31))
            {
                float gap = p.y - hit.point.y;
                if (Mathf.Abs(gap) > 0.15f) Fail($"{lantern.name} base is {gap:0.00} m off its roof");
                else Note($"{lantern.name} sits on its roof ({gap:+0.00;-0.00} m)");
            }
            else Fail($"{lantern.name}: no roof under it");
        }
        foreach (MeshCollider mc in temporary) Object.DestroyImmediate(mc.gameObject);
    }

    private static void Require(bool ok, string what) { if (!ok) Fail("missing: " + what); else Note("ok: " + what); }

    // ---------------------------------------------------------------- z-fighting
    private struct Tri
    {
        public Vector3 a, b, c, n, centre;
        public float d, area;
        public int owner;   // renderer index
        public int look;    // material instance id
    }

    private static void ScanZFighting(string output)
    {
        var renderers = SceneAll<MeshRenderer>()
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponent<TMPro.TMP_Text>() == null && r.GetComponent<MeshFilter>()?.sharedMesh != null)
            .ToList();
        var tris = new List<Tri>();
        var looks = new Dictionary<Material, int>();
        var lookList = new List<Material>();
        for (int ri = 0; ri < renderers.Count; ri++)
        {
            MeshRenderer r = renderers[ri];
            Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
            if (!mesh.isReadable) { var copy = Object.Instantiate(mesh); mesh = copy; }
            Vector3[] v = mesh.vertices;
            Matrix4x4 m = r.transform.localToWorldMatrix;
            for (int s = 0; s < mesh.subMeshCount && s < r.sharedMaterials.Length; s++)
            {
                Material mat = r.sharedMaterials[s];
                if (mat == null) continue;
                if (!looks.TryGetValue(mat, out int look)) { look = lookList.Count; looks[mat] = look; lookList.Add(mat); }
                int[] t = mesh.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = m.MultiplyPoint3x4(v[t[i]]), b = m.MultiplyPoint3x4(v[t[i + 1]]), c = m.MultiplyPoint3x4(v[t[i + 2]]);
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    float area = cross.magnitude * 0.5f;
                    if (area < 1e-4f) continue;
                    Vector3 n = cross / (area * 2f);
                    tris.Add(new Tri { a = a, b = b, c = c, n = n, d = Vector3.Dot(n, a), area = area, owner = ri, look = look, centre = (a + b + c) / 3f });
                }
            }
        }
        Note($"z-fight scan: {renderers.Count} renderers, {tris.Count} triangles");
        // Buckets: normal (0.04 steps), plane offset (3 cm), 8 m grid of the plane. Each triangle goes into every bucket
        // its tolerance reaches; a query looks only at its own bucket.
        const float nq = 0.04f, dq = 0.03f, gq = 8f;
        var buckets = new Dictionary<(int, int, int, int, int, int), List<int>>();
        IEnumerable<int> Range(float value, float tolerance, float step)
        {
            int lo = Mathf.FloorToInt((value - tolerance) / step), hi = Mathf.FloorToInt((value + tolerance) / step);
            for (int i = lo; i <= hi; i++) yield return i;
        }
        for (int i = 0; i < tris.Count; i++)
        {
            Tri t = tris[i];
            Vector3 lo = Vector3.Min(t.a, Vector3.Min(t.b, t.c)), hi = Vector3.Max(t.a, Vector3.Max(t.b, t.c));
            foreach (int x in Range(t.n.x, 0.012f, nq))
            foreach (int y in Range(t.n.y, 0.012f, nq))
            foreach (int z in Range(t.n.z, 0.012f, nq))
            foreach (int d in Range(t.d, 0.02f, dq))
                for (int gx = Mathf.FloorToInt(lo.x / gq); gx <= Mathf.FloorToInt(hi.x / gq); gx++)
                for (int gz = Mathf.FloorToInt(lo.z / gq); gz <= Mathf.FloorToInt(hi.z / gq); gz++)
                {
                    var key = (x, y, z, d, gx, gz);
                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<int>();
                    list.Add(i);
                }
        }
        var found = new Dictionary<(int, int, int, int), (float area, float separation, Vector3 at)>();
        var seen = new HashSet<long>();
        foreach (var bucket in buckets.Values)
        {
            if (bucket.Count < 2) continue;
            for (int p = 0; p < bucket.Count; p++)
                for (int q = p + 1; q < bucket.Count; q++)
                {
                    int i = bucket[p], j = bucket[q];
                    Tri A = tris[i], B = tris[j];
                    if (A.look == B.look) continue; // the same material in the same plane looks the same: no flicker
                    if (Vector3.Dot(A.n, B.n) < 0.9994f) continue; // within 2 degrees, facing the same way
                    long pair = i < j ? (long)i * tris.Count + j : (long)j * tris.Count + i;
                    if (!seen.Add(pair)) continue;
                    float sep = Mathf.Max(Mathf.Abs(Vector3.Dot(A.n, B.a) - A.d), Mathf.Abs(Vector3.Dot(A.n, B.b) - A.d), Mathf.Abs(Vector3.Dot(A.n, B.c) - A.d));
                    if (sep > 0.02f) continue;
                    float g0 = Vector3.Dot(A.n, B.a) - A.d, g1 = Vector3.Dot(A.n, B.b) - A.d, g2 = Vector3.Dot(A.n, B.c) - A.d;
                    if (Mathf.Min(g0, Mathf.Min(g1, g2)) < -0.002f && Mathf.Max(g0, Mathf.Max(g1, g2)) > 0.002f) continue; // crossing faces
                    float area = Overlap(A, B);
                    if (area < 1e-4f) continue;
                    var key = A.owner < B.owner ? (A.owner, A.look, B.owner, B.look) : (B.owner, B.look, A.owner, A.look);
                    found.TryGetValue(key, out var acc);
                    found[key] = (acc.area + area, acc.area == 0f ? sep : Mathf.Min(acc.separation, sep), acc.area == 0f ? A.centre : acc.at);
                }
        }
        string Path(int owner)
        {
            Transform t = renderers[owner].transform;
            string s = t.name;
            for (int k = 0; k < 3 && t.parent != null; k++) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
        string Mat(int owner, int look) => lookList[look].name;
        var lines = found.OrderByDescending(f => f.Value.area)
            .Select(f => $"{f.Value.area * 1e4f,8:0} cm2  sep {f.Value.separation * 1000f,5:0.0} mm  at {f.Value.at.x:0.0},{f.Value.at.y:0.00},{f.Value.at.z:0.0}  {Path(f.Key.Item1)} [{Mat(f.Key.Item1, f.Key.Item2)}]  x  {Path(f.Key.Item3)} [{Mat(f.Key.Item3, f.Key.Item4)}]")
            .ToList();
        File.WriteAllLines(System.IO.Path.Combine(output, "zfight.txt"), lines);
        int severe = found.Count(f => f.Value.separation < 0.006f);
        Note($"z-fight candidates: {found.Count} pairs ({severe} closer than 6 mm); details in zfight.txt");
        foreach (string line in lines.Take(40)) Note("  " + line);
        if (severe > 0) Fail($"{severe} coplanar overlaps of different materials");
    }

    private static float Overlap(Tri A, Tri B)
    {
        Vector3 u = Vector3.Cross(A.n, Mathf.Abs(A.n.y) < 0.9f ? Vector3.up : Vector3.right).normalized, w = Vector3.Cross(A.n, u);
        List<Vector2> P(Tri t) => Ccw(new List<Vector2> { new Vector2(Vector3.Dot(t.a, u), Vector3.Dot(t.a, w)), new Vector2(Vector3.Dot(t.b, u), Vector3.Dot(t.b, w)), new Vector2(Vector3.Dot(t.c, u), Vector3.Dot(t.c, w)) });
        List<Vector2> poly = P(A), clipper = P(B);
        for (int i = 0; i < clipper.Count && poly.Count > 0; i++)
        {
            Vector2 a = clipper[i], b = clipper[(i + 1) % clipper.Count];
            var input = poly;
            poly = new List<Vector2>();
            for (int j = 0; j < input.Count; j++)
            {
                Vector2 p = input[j], q = input[(j + 1) % input.Count];
                bool pin = Side(a, b, p) >= -1e-7f, qin = Side(a, b, q) >= -1e-7f;
                if (qin) { if (!pin) poly.Add(Cut(p, q, a, b)); poly.Add(q); }
                else if (pin) poly.Add(Cut(p, q, a, b));
            }
        }
        float area = 0f;
        for (int i = 0; i < poly.Count; i++) area += poly[i].x * poly[(i + 1) % poly.Count].y - poly[(i + 1) % poly.Count].x * poly[i].y;
        return Mathf.Abs(area) * 0.5f;
    }

    private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    private static Vector2 Cut(Vector2 p, Vector2 q, Vector2 a, Vector2 b)
    {
        float sp = Side(a, b, p), sq = Side(a, b, q);
        return Vector2.Lerp(p, q, sp / (sp - sq));
    }
    private static List<Vector2> Ccw(List<Vector2> pts)
    {
        float s = 0f;
        for (int i = 0; i < pts.Count; i++) s += pts[i].x * pts[(i + 1) % pts.Count].y - pts[(i + 1) % pts.Count].x * pts[i].y;
        if (s < 0f) pts.Reverse();
        return pts;
    }

    // ---------------------------------------------------------------- pictures
    private static void RenderBuildings(string output)
    {
        string folder = System.IO.Path.Combine(output, "buildings");
        Directory.CreateDirectory(folder);
        var all = SceneAll<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
        var camera = new GameObject("Smoke Camera").AddComponent<Camera>();
        camera.fieldOfView = 55f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 600f;
        const int w = 480, h = 360;
        var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = target;
        // Inspection light: no fog, a brighter flat ambient and a key light, so gaps and seams show.
        bool fog = RenderSettings.fog;
        var ambientMode = RenderSettings.ambientMode;
        Color ambient = RenderSettings.ambientLight;
        var key = new GameObject("Smoke Key").AddComponent<Light>();
        key.type = LightType.Directional;
        key.intensity = 1.1f;
        key.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        int index = 0;
        foreach (MudProp prop in Buildings(false).OrderBy(p => p.Kind).ThenBy(p => p.name))
        {
            var model = prop.transform.Find("Model/Mesh")?.GetComponent<MeshRenderer>();
            if (model == null) continue;
            Bounds b = model.bounds;
            // Show only this building and the ground, water and roads around it.
            var keep = new HashSet<Renderer>(prop.GetComponentsInChildren<Renderer>());
            foreach (Renderer r in all)
                if (!keep.Contains(r) && !(r.name.Contains("Terrain") || r.name.Contains("Water") || r.name.Contains("Road") || r.name.Contains("Ground"))) r.enabled = false;
            var sheet = new Texture2D(w * 4, h * 2, TextureFormat.RGB24, false);
            float size = Mathf.Max(b.extents.x, b.extents.z) * 2f;
            Vector3 f = prop.transform.forward, rgt = prop.transform.right, ground = new Vector3(b.center.x, prop.transform.position.y, b.center.z);
            var views = new List<(Vector3 eye, Vector3 look, bool game)>();
            float dist = size * 1.1f + 3f;
            foreach (Vector3 dir in new[] { f, -f, rgt, -rgt })
                views.Add((b.center + dir * dist + Vector3.up * dist * 0.35f, b.center, false));
            views.Add((b.center + (f - rgt).normalized * dist + Vector3.up * dist * 0.9f, b.center, false));
            // Below the eaves: a player's eye (1.6 m) 2.5 m outside the wall on the right side and at the back, looking up
            // at where the walls meet the roof.
            Bounds local = model.GetComponent<MeshFilter>().sharedMesh.bounds;
            float eave = prop.transform.position.y + b.size.y * 0.6f;
            foreach ((Vector3 dir, float reach) in new[] { (rgt, local.extents.x), (-f, local.extents.z) })
            {
                Vector3 eye = ground + dir * (reach + 2.5f) + Vector3.up * 1.6f;
                views.Add((eye, ground + dir * (reach * 0.8f) + Vector3.up * (eave - prop.transform.position.y), false));
            }
            views.Add((b.center + (f + rgt * 0.6f).normalized * dist + Vector3.up * dist * 0.25f, b.center, true));
            for (int v = 0; v < views.Count; v++)
            {
                (Vector3 eye, Vector3 look, bool game) = views[v];
                RenderSettings.fog = game && fog;
                RenderSettings.ambientMode = game ? ambientMode : UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = game ? ambient : new Color(0.55f, 0.55f, 0.6f);
                key.enabled = !game;
                camera.transform.position = eye;
                camera.transform.LookAt(look);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = game ? new Color(0.25f, 0.33f, 0.36f) : new Color(0.62f, 0.7f, 0.78f);
                camera.Render();
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, w, h), (v % 4) * w, (1 - v / 4) * h);
                RenderTexture.active = null;
            }
            sheet.Apply();
            string file = $"{index++:00}_{prop.Kind}_{prop.name.Replace(' ', '_').Replace("(", "").Replace(")", "")}.png";
            File.WriteAllBytes(System.IO.Path.Combine(folder, file), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            foreach (Renderer r in all) if (r != null) r.enabled = true;
        }
        RenderSettings.fog = fog;
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambient;
        Object.DestroyImmediate(key.gameObject);
        Object.DestroyImmediate(camera.gameObject);
        Note($"rendered {index} buildings to {folder}");
    }
}
