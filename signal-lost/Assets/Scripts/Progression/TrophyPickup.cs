using System;
using SignalLost.Inventory;
using SignalLost.Player;
using SignalLost.UI;
using UnityEngine;

namespace SignalLost.Progression
{
    /// <summary>
    /// A boss trophy lying in the loot a boss leaves behind (meeting notes 10-23). E picks it up into the pockets (as a
    /// key item); the player then sets it on a <see cref="TrophyStand"/> at camp.
    /// </summary>
    public sealed class TrophyPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string trophyId;

        public Trophy Trophy => Trophy.Find(trophyId);
        public string Prompt => "Pick up " + (Trophy != null ? Trophy.Name : "trophy");

        /// <summary>Raised when any trophy is picked up, with its Id.</summary>
        public static event Action<string> PickedUp;

        /// <summary>Lays a trophy on the ground at <paramref name="position"/>, glinting so it stands out from the loot.</summary>
        public static TrophyPickup Create(string trophyId, Vector3 position)
        {
            Trophy trophy = Trophy.Find(trophyId);
            var root = new GameObject(trophy != null ? trophy.Name : trophyId);
            root.transform.position = position;
            GameObject model = trophy != null ? trophy.LoadModel() : null;
            Transform visual = model != null ? Instantiate(model, root.transform).transform : GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, 0.35f, 0f);
            foreach (Collider part in visual.GetComponentsInChildren<Collider>()) Destroy(part);
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f);
            box.size = new Vector3(1f, 0.9f, 1f);
            var glow = new GameObject("Glint").AddComponent<Light>();
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.85f, 0.35f);
            glow.range = 5f;
            glow.intensity = 3f;
            var pickup = root.AddComponent<TrophyPickup>();
            pickup.trophyId = trophyId;
            root.AddComponent<PickupSpin>().Model = visual;
            return pickup;
        }

        public bool CanInteract(PlayerInteractor player) => isActiveAndEnabled && player.Gear != null;

        public bool Interact(PlayerInteractor player)
        {
            PlayerInventory inventory = player.Gear.Inventory;
            if (inventory == null) return false;
            inventory.AddKeyItem(trophyId);
            GameplayOverlay.Instance.ShowItemCard(Trophy != null ? Trophy.Name : trophyId,
                "A trophy for the camp. Take it home and set it on a trophy stand by the tent; it stays there for good.",
                new Color(1f, 0.85f, 0.35f), "BOSS TROPHY");
            PickedUp?.Invoke(trophyId);
            Destroy(gameObject);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PickedUp = null;
    }

    /// <summary>Turns and bobs a pickup's model so it catches the eye.</summary>
    public sealed class PickupSpin : MonoBehaviour
    {
        public Transform Model;
        private Vector3 rest;

        private void Start() { if (Model != null) rest = Model.localPosition; }

        private void Update()
        {
            if (Model == null) return;
            Model.localPosition = rest + Vector3.up * (0.08f * Mathf.Sin(Time.time * 2f));
            Model.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
        }
    }
}
