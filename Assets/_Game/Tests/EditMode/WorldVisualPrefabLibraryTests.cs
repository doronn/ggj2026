using System.Reflection;
using Ggj2026.Game.Shared.Economy;
using Ggj2026.Game.Shared.Units;
using Ggj2026.Game.Shared.World;
using Ggj2026.Game.Shell.World;
using NUnit.Framework;
using UnityEngine;

namespace Ggj2026.Game.Tests.EditMode
{
    public class WorldVisualPrefabLibraryTests
    {
        [Test]
        public void ResolveUnit_FallsBackToDefaultWhenRoleSpecificPrefabMissing()
        {
            var library = ScriptableObject.CreateInstance<WorldVisualPrefabLibrary>();
            var defaultPrefab = new GameObject("DefaultUnitPrefab");
            var defaultDefinition = new WorldVisualPrefabLibrary.VisualPrefabDefinition
            {
                prefab = defaultPrefab
            };

            SetPrivateField(library, "defaultUnit", defaultDefinition);

            WorldVisualPrefabLibrary.VisualPrefabDefinition resolved = library.ResolveUnit(UnitRole.Worker);
            Assert.That(resolved.prefab, Is.EqualTo(defaultPrefab));

            Object.DestroyImmediate(defaultPrefab);
            Object.DestroyImmediate(library);
        }

        [Test]
        public void ResolveEdge_UsesDoorOverrideWhenAvailable()
        {
            var library = ScriptableObject.CreateInstance<WorldVisualPrefabLibrary>();
            var wallPrefab = new GameObject("WallPrefab");
            var doorPrefab = new GameObject("DoorPrefab");

            SetPrivateField(library, "wallEdge", new WorldVisualPrefabLibrary.VisualPrefabDefinition { prefab = wallPrefab });
            SetPrivateField(library, "doorEdge", new WorldVisualPrefabLibrary.VisualPrefabDefinition { prefab = doorPrefab });

            WorldVisualPrefabLibrary.VisualPrefabDefinition resolvedDoor = library.ResolveEdge(EdgeFeatureKind.Door);
            WorldVisualPrefabLibrary.VisualPrefabDefinition resolvedWall = library.ResolveEdge(EdgeFeatureKind.Wall);

            Assert.That(resolvedDoor.prefab, Is.EqualTo(doorPrefab));
            Assert.That(resolvedWall.prefab, Is.EqualTo(wallPrefab));

            Object.DestroyImmediate(wallPrefab);
            Object.DestroyImmediate(doorPrefab);
            Object.DestroyImmediate(library);
        }

        private static void SetPrivateField<T>(WorldVisualPrefabLibrary library, string fieldName, T value)
        {
            FieldInfo field = typeof(WorldVisualPrefabLibrary).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Expected private field '{fieldName}' on {nameof(WorldVisualPrefabLibrary)}.");
            field.SetValue(library, value);
        }
    }
}
