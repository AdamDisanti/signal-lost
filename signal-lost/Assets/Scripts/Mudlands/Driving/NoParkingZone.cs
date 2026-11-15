using System;
using System.Collections;
using System.Collections.Generic;
using SignalLost.Audio;
using SignalLost.Combat;
using SignalLost.Effects;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace SignalLost.Mudlands
{
    /// <summary>
    /// Areas where the car can't be called (the base camp: it is where everyone sleeps). A sphere on its transform.
    /// </summary>
    public sealed class NoParkingZone : MonoBehaviour
    {
        private static readonly List<NoParkingZone> Active = new List<NoParkingZone>();
        [SerializeField, Min(1f)] private float radius = 14f;
        [SerializeField] private string reason = "No parking at camp.";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();

        public void Configure(float r, string why)
        {
            radius = r;
            reason = why;
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public static bool Contains(Vector3 point) => Find(point) != null;
        public static string ReasonAt(Vector3 point) => Find(point)?.reason ?? "";

        private static NoParkingZone Find(Vector3 point)
        {
            foreach (NoParkingZone zone in Active)
                if (zone != null && (zone.transform.position - point).sqrMagnitude <= zone.radius * zone.radius) return zone;
            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
