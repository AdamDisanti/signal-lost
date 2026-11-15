using System;
using System.Collections.Generic;
using SignalLost.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// One Mudlands prop or building (<see cref="MudModels.Kind"/>). Its model is built from code whenever it is
    /// enabled, in the editor as well as in play, and never saved into the scene (so the scene stays small and a code
    /// change updates every copy). Its colliders, Destructible and quest scripts are ordinary saved components the
    /// level builder adds. Some kinds add lights, fire, signs and moving parts.
    /// </summary>
    [ExecuteAlways]
    public sealed class MudProp : MonoBehaviour
    {
        [SerializeField] private MudModels.Kind kind;
        [SerializeField] private int variant;
        [SerializeField] private float size = 1f;
        [Tooltip("Text on the kind's sign, if it has one (driving school, road signs, the roadhouse, the county line).")]
        [SerializeField] private string sign = "";

        private Transform model;

        public MudModels.Kind Kind => kind;
        /// <summary>A moving part made for this kind (the gong's disc), or null.</summary>
        public Transform Moving { get; private set; }

        public void Configure(MudModels.Kind propKind, int propVariant, float propSize = 1f, string signText = "")
        {
            kind = propKind;
            variant = propVariant;
            size = propSize;
            sign = signText;
            Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (model != null)
            {
                if (Application.isPlaying) Destroy(model.gameObject);
                else DestroyImmediate(model.gameObject);
            }
            model = null;
        }

        public void Rebuild()
        {
            OnDisable();
            model = new GameObject("Model") { hideFlags = HideFlags.DontSave }.transform;
            model.SetParent(transform, false);
            string key = $"prop_{kind}_{variant % 4}_{size:0.00}";
            MudArt.Part(model, "Mesh", Vector3.zero, Quaternion.identity, key, k => MudModels.Build(k, kind, variant % 4, size)).gameObject.hideFlags = HideFlags.DontSave;
            Extras();
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.hideFlags = HideFlags.DontSave;
        }

        private void Extras()
        {
            switch (kind)
            {
                case MudModels.Kind.StiltShack:
                    Lamp(new Vector3(0f, 3.8f, 2.3f), MudPalette.Pod, 7f, 1.6f); // the glow from its round windows and door
                    break;
                case MudModels.Kind.Cabin:
                    Lamp(new Vector3(0f, 1.4f, 3f), MudPalette.Pod, 6f, 1.4f);
                    break;
                case MudModels.Kind.LanternPost:
                    Lamp(new Vector3(0.55f, 2.7f, 0f), MudPalette.Lantern, 9f, 2.2f);
                    break;
                case MudModels.Kind.StoneLantern:
                    Lamp(new Vector3(0f, 1.2f, 0f), MudPalette.Lantern, 6f, 1.5f);
                    break;
                case MudModels.Kind.CursedLantern:
                    Lamp(new Vector3(0.55f, 3.8f, 0f), MudPalette.Curse, 9f, 2.5f);
                    break;
                case MudModels.Kind.Cauldron:
                    Lamp(new Vector3(0f, 2f, 0f), MudPalette.Curse, 10f, 2.5f);
                    break;
                case MudModels.Kind.TikiTorch:
                    if (Application.isPlaying) FireEffect.Spawn(transform.position + Vector3.up * 2.15f, 0.14f, 0.5f, 0f, model, null, 0.3f, 0.7f);
                    Lamp(new Vector3(0f, 2.4f, 0f), MudPalette.Fire, 8f, 2f);
                    break;
                case MudModels.Kind.MoonshineStill:
                    if (Application.isPlaying) FireEffect.Spawn(transform.TransformPoint(new Vector3(0f, 0.15f, 1.7f)), 0.3f, 0.5f, 0f, model, null, 0.6f, 0.6f);
                    break;
                case MudModels.Kind.Gong:
                    Moving = MudArt.Part(model, "Disc", new Vector3(0f, 2.95f, 0f), Quaternion.identity, "gong_disc", k =>
                    {
                        k.Prism(new Vector3(0f, -1.2f, -0.04f), Quaternion.Euler(90f, 0f, 0f), 0.9f, 0.9f, 0.08f, 16, new Color(0.72f, 0.55f, 0.25f));
                        k.Prism(new Vector3(0f, -1.2f, 0.045f), Quaternion.Euler(90f, 0f, 0f), 0.3f, 0.3f, 0.02f, 12, new Color(0.85f, 0.7f, 0.35f));
                        k.Rod(new Vector3(-0.6f, 0f, 0f), new Vector3(-0.5f, -0.5f, 0f), 0.02f, 4, MudPalette.Black);
                        k.Rod(new Vector3(0.6f, 0f, 0f), new Vector3(0.5f, -0.5f, 0f), 0.02f, 4, MudPalette.Black);
                    });
                    break;
            }
            if (!string.IsNullOrEmpty(sign)) Sign();
        }

        private void Lamp(Vector3 at, Color color, float range, float intensity)
        {
            var light = new GameObject("Lamp").AddComponent<Light>();
            light.transform.SetParent(model, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = color;
            light.range = range * Mathf.Max(1f, size);
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // Words on the kind's sign board.
        private void Sign()
        {
            (Vector3 at, float width, float fontSize, Color color) = kind switch
            {
                MudModels.Kind.DrivingSchool => (new Vector3(0f, 4.95f, 3.29f), 5.2f, 5f, new Color(0.2f, 0.12f, 0.08f)),
                MudModels.Kind.Roadhouse => (new Vector3(0f, 6.6f, 5.73f), 6.8f, 7f, new Color(1f, 0.35f, 0.6f)),
                MudModels.Kind.CountyLine => (new Vector3(0f, 4.6f, 0.13f), 9.8f, 6.5f, new Color(0.15f, 0.25f, 0.18f)),
                MudModels.Kind.Kiosk => (new Vector3(0f, 2.15f, 0.05f), 0.85f, 2.2f, new Color(0.2f, 0.12f, 0.08f)),
                MudModels.Kind.RoadSign => (new Vector3(0.5f, 2.2f, 0.04f), 1.3f, 1.6f, new Color(0.2f, 0.12f, 0.08f)),
                MudModels.Kind.Barricade => (new Vector3(0f, 1.75f, 0.09f), 1.7f, 1.8f, Color.white),
                _ => (new Vector3(0f, 2f, 0.1f), 2f, 2f, Color.black)
            };
            foreach (float side in new[] { 1f, -1f })
            {
                if (side < 0f && kind != MudModels.Kind.RoadSign && kind != MudModels.Kind.CountyLine) continue;
                var go = new GameObject("Sign Text");
                go.transform.SetParent(model, false);
                go.transform.localPosition = new Vector3(at.x, at.y, at.z * side);
                go.transform.localRotation = side > 0f ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                var text = go.AddComponent<TextMeshPro>();
                text.text = sign;
                text.fontSize = fontSize;
                text.fontStyle = FontStyles.Bold;
                text.alignment = TextAlignmentOptions.Center;
                text.overflowMode = TextOverflowModes.Overflow;
                text.color = color;
                text.enableAutoSizing = true;
                text.fontSizeMin = 0.5f;
                text.fontSizeMax = fontSize;
                text.rectTransform.sizeDelta = new Vector2(width, 1f);
                if (kind == MudModels.Kind.Roadhouse) text.fontMaterial.EnableKeyword("GLOW_ON");
            }
        }
    }
}
