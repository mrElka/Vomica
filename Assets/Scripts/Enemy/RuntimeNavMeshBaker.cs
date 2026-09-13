using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public class RuntimeNavMeshBaker : MonoBehaviour
{
    private void Awake()
    {
        NavMeshSurface surface = GetComponent<NavMeshSurface>();
        if (surface == null)
            surface = gameObject.AddComponent<NavMeshSurface>();

        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = 1 << gameObject.layer;
        surface.BuildNavMesh();
    }
}
