using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Editor
{
    /// <summary>
    /// Renders a prefab to a square, transparent PNG and imports it as a UI sprite: the icons for scrap, supplies and
    /// upgrades in the HUD, pockets menu and shop. Lit by a warm key light and a cool rim so it reads on dark panels.
    /// </summary>
    internal static class ItemIconRenderer
    {
        private const int Layer = 31; // an otherwise unused layer, so nothing else in the open scene shows up

        public static Sprite Render(GameObject prefab, string pngPath, Vector3 viewAngles, int size = 256, float padding = 1.15f)
        {
            var root = new GameObject("Icon Stage") { hideFlags = HideFlags.HideAndDontSave };
            var model = (GameObject)Object.Instantiate(prefab, root.transform);
            model.transform.localRotation = Quaternion.Euler(viewAngles);
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = Layer;
            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer part in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(part.bounds);

            var cameraObject = new GameObject("Icon Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.cullingMask = 1 << Layer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * padding;
            camera.transform.position = bounds.center + Vector3.back * (bounds.extents.magnitude + 2f);
            camera.transform.rotation = Quaternion.identity;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = bounds.extents.magnitude * 2f + 4f;

            Light key = Lamp(new Color(1f, 0.92f, 0.8f), 1.3f, new Vector3(35f, -35f, 0f));
            Light rim = Lamp(new Color(0.55f, 0.75f, 1f), 0.8f, new Vector3(-20f, 150f, 0f));
            AmbientMode ambientMode = RenderSettings.ambientMode;
            Color ambient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, image.EncodeToPNG());
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambient;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(key.gameObject);
            Object.DestroyImmediate(rim.gameObject);
            Object.DestroyImmediate(root);

            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(pngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
        }

        private static Light Lamp(Color color, float intensity, Vector3 angles)
        {
            var light = new GameObject("Icon Light") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << Layer;
            light.transform.rotation = Quaternion.Euler(angles);
            return light;
        }
    }
}
