using SignalLost.Inventory;
using UnityEngine;

namespace SignalLost.UI
{
    /// <summary>Put one in a level to stock Eugene's shop with that level's upgrades (<see cref="MerchantMenuUi.Region"/>).</summary>
    public sealed class ShopRegion : MonoBehaviour
    {
        [SerializeField] private UpgradeRegion region = UpgradeRegion.Tutorial;

        public UpgradeRegion Region => region;

        private void Start() => MerchantMenuUi.Instance.Region = region;
    }
}
