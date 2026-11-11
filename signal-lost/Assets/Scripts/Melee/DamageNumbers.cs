using System.Collections.Generic;
using SignalLost.Combat;
using SignalLost.Player;
using TMPro;
using UnityEngine;

namespace SignalLost.Melee
{
    /// <summary>
    /// Testing aid for the melee arena: every hit the player lands floats up as a number over what it hit, so rage
    /// scaling can be seen. White normally, gauss-blue while rage is boosting it, bigger and gold for heavy hits.
    /// Faces the camera and fades as it rises. Pooled.
    /// </summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        private const int Pool = 48;
        private const float Life = 0.9f;

        private sealed class Number
        {
            public TextMeshPro Text;
            public Vector3 At, Drift;
            public float Age = Life;
        }

        private readonly List<Number> numbers = new List<Number>();
        private int next;
        private Transform player;

        private void Awake()
        {
            for (int i = 0; i < Pool; i++)
            {
                var text = new GameObject("Damage Number").AddComponent<TextMeshPro>();
                text.transform.SetParent(transform, false);
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = FontStyles.Bold;
                text.outlineWidth = 0.25f;
                text.outlineColor = new Color32(10, 14, 30, 255);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.gameObject.SetActive(false);
                numbers.Add(new Number { Text = text });
            }
        }

        private void OnEnable() => Health.AnyDamaged += OnDamaged;
        private void OnDisable() => Health.AnyDamaged -= OnDamaged;

        private void OnDamaged(Health target, float amount, GameObject source)
        {
            if (player == null)
            {
                var life = FindAnyObjectByType<PlayerLife>();
                if (life != null) player = life.transform;
            }
            if (player == null || source == null || !source.transform.IsChildOf(player) || target.transform.IsChildOf(player)) return;
            Number number = numbers[next];
            next = (next + 1) % numbers.Count;
            bool heavy = amount >= 25f;
            bool raging = RageMeter.Instance != null && RageMeter.Instance.Stacks > 0;
            number.At = MeleeTargets.Centre(target) + Vector3.up * 0.6f + Random.insideUnitSphere * 0.3f;
            number.Drift = new Vector3(Random.Range(-0.4f, 0.4f), 1.6f, Random.Range(-0.4f, 0.4f));
            number.Age = 0f;
            number.Text.text = Mathf.RoundToInt(amount).ToString();
            number.Text.fontSize = heavy ? 3.2f : 2.3f;
            number.Text.color = heavy ? new Color(1f, 0.82f, 0.3f) : raging ? GaussFx.Energy : Color.white;
            number.Text.gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            Camera view = Camera.main;
            foreach (Number number in numbers)
            {
                if (number.Age >= Life) continue;
                number.Age += Time.deltaTime;
                if (number.Age >= Life)
                {
                    number.Text.gameObject.SetActive(false);
                    continue;
                }
                float k = number.Age / Life;
                Transform t = number.Text.transform;
                t.position = number.At + number.Drift * (k * (1f - 0.4f * k));
                if (view != null) t.rotation = Quaternion.LookRotation(t.position - view.transform.position);
                t.localScale = Vector3.one * (k < 0.1f ? Mathf.Lerp(1.6f, 1f, k / 0.1f) : 1f);
                Color c = number.Text.color;
                c.a = 1f - k * k;
                number.Text.color = c;
            }
        }
    }
}
