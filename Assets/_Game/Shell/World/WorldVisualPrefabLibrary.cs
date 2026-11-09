using System;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using UnityEngine;

namespace Ggj2026.Game.Shell.World
{
    [CreateAssetMenu(fileName = "WorldVisualPrefabLibrary", menuName = "GGJ2026/World/World Visual Prefab Library")]
    public sealed class WorldVisualPrefabLibrary : ScriptableObject
    {
        [Serializable]
        public struct VisualPrefabDefinition
        {
            public GameObject prefab;
            public Vector3 localOffset;
            public Vector3 localEuler;
            public Vector3 localScale;
            public bool useCustomScale;
            public bool tintWithStateColor;
        }

        [Header("Units")]
        [SerializeField] private VisualPrefabDefinition defaultUnit;
        [SerializeField] private VisualPrefabDefinition workerUnit;
        [SerializeField] private VisualPrefabDefinition builderUnit;
        [SerializeField] private VisualPrefabDefinition haulerUnit;
        [SerializeField] private VisualPrefabDefinition crafterUnit;
        [SerializeField] private VisualPrefabDefinition soldierUnit;

        [Header("Edges")]
        [SerializeField] private VisualPrefabDefinition wallEdge;
        [SerializeField] private VisualPrefabDefinition doorEdge;
        [SerializeField] private VisualPrefabDefinition gateEdge;

        [Header("Features")]
        [SerializeField] private VisualPrefabDefinition defaultModule;
        [SerializeField] private VisualPrefabDefinition defaultItemStack;
        [SerializeField] private VisualPrefabDefinition defaultResourceNode;
        [SerializeField] private VisualPrefabDefinition defaultLandmark;

        public VisualPrefabDefinition ResolveUnit(UnitRole role)
        {
            return role switch
            {
                UnitRole.Worker => ResolveOrDefault(workerUnit, defaultUnit),
                UnitRole.Builder => ResolveOrDefault(builderUnit, defaultUnit),
                UnitRole.Hauler => ResolveOrDefault(haulerUnit, defaultUnit),
                UnitRole.Crafter => ResolveOrDefault(crafterUnit, defaultUnit),
                UnitRole.Soldier => ResolveOrDefault(soldierUnit, defaultUnit),
                _ => defaultUnit
            };
        }

        public VisualPrefabDefinition ResolveEdge(EdgeFeatureKind featureKind)
        {
            return featureKind switch
            {
                EdgeFeatureKind.Door => ResolveOrDefault(doorEdge, wallEdge),
                EdgeFeatureKind.Gate => ResolveOrDefault(gateEdge, wallEdge),
                EdgeFeatureKind.Wall => wallEdge,
                _ => default
            };
        }

        public VisualPrefabDefinition ResolveModule(ModuleKind _)
        {
            return defaultModule;
        }

        public VisualPrefabDefinition ResolveItemStack(ResourceType _)
        {
            return defaultItemStack;
        }

        public VisualPrefabDefinition ResolveResourceNode(ResourceNodeKind _)
        {
            return defaultResourceNode;
        }

        public VisualPrefabDefinition ResolveLandmark(LandmarkKind _)
        {
            return defaultLandmark;
        }

        public bool HasAnyPrefab()
        {
            return HasPrefab(defaultUnit) ||
                   HasPrefab(workerUnit) ||
                   HasPrefab(builderUnit) ||
                   HasPrefab(haulerUnit) ||
                   HasPrefab(crafterUnit) ||
                   HasPrefab(soldierUnit) ||
                   HasPrefab(wallEdge) ||
                   HasPrefab(doorEdge) ||
                   HasPrefab(gateEdge) ||
                   HasPrefab(defaultModule) ||
                   HasPrefab(defaultItemStack) ||
                   HasPrefab(defaultResourceNode) ||
                   HasPrefab(defaultLandmark);
        }

        private static VisualPrefabDefinition ResolveOrDefault(VisualPrefabDefinition definition, VisualPrefabDefinition fallback)
        {
            return HasPrefab(definition) ? definition : fallback;
        }

        private static bool HasPrefab(VisualPrefabDefinition definition)
        {
            return definition.prefab != null;
        }
    }
}
