using System;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// A stepping stone in the bog that sinks into the mud if you stand on it for 1.5 s, then floats back up.
    /// Part of the Three Gongs route (and anywhere else stones are placed).
    /// </summary>
    public sealed class SinkingStone : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float holdSeconds = 1.5f;
        private Vector3 rest;
        private float stoodFor, sunkUntil;
        private Transform player;

        private void Start()
        {
            rest = transform.position;
            var controller = FindAnyObjectByType<FirstPersonController>();
            player = controller != null ? controller.transform : null;
        }

        private void Update()
        {
            if (player == null) return;
            Vector3 offset = player.position - transform.position;
            bool on = offset.Flatten().magnitude < 1.1f && offset.y > -0.2f && offset.y < 1.2f;
            if (Time.time < sunkUntil)
            {
                transform.position = Vector3.MoveTowards(transform.position, rest + Vector3.down * 1.4f, Time.deltaTime * 2.5f);
                return;
            }
            stoodFor = on ? stoodFor + Time.deltaTime : Mathf.Max(0f, stoodFor - Time.deltaTime);
            float wobble = stoodFor / holdSeconds;
            transform.position = Vector3.MoveTowards(transform.position, rest + Vector3.down * 0.25f * wobble, Time.deltaTime * 2f);
            transform.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 25f) * 4f * wobble, 0f, Mathf.Cos(Time.time * 21f) * 4f * wobble);
            if (stoodFor < holdSeconds) return;
            stoodFor = 0f;
            sunkUntil = Time.time + 2.5f;
            Sfx.PlayAt("mud_splat", transform.position, 0.8f, 0.7f, 30f);
            ParticleBurst.Spawn(ParticleBurst.Kind.Water, transform.position, Vector3.up, 20, 0.3f, 3f, 60f, new Color(0.35f, 0.28f, 0.2f, 0.8f));
        }
    }
}
