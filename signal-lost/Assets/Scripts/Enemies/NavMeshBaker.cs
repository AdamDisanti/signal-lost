using Unity.AI.Navigation;
using UnityEngine;

namespace SignalLost.Enemies
{
    /// <summary>
    /// Builds the NavMesh for this object's NavMeshSurface when the scene starts, so enemies can find paths
    /// without a baked NavMesh asset being committed. Fine for small test arenas; bake in the editor for
    /// big levels. Runs before other scripts so enemies can be placed on the NavMesh in their Start.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(NavMeshSurface))]
    public sealed class NavMeshBaker : MonoBehaviour
    {
        private void Awake()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface.navMeshData == null) surface.BuildNavMesh();
        }
    }
}
