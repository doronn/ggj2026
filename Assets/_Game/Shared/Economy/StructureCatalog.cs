using System.Collections.Generic;
using Ggj2026.Game.Shared.Hex;
using Ggj2026.Game.Shared.Logistics;
using UnityEngine;

namespace Ggj2026.Game.Shared.Economy
{
    public static class SimulationCatalog
    {
        private static readonly Dictionary<FloorPurposeKind, ConstructionBlueprint> FloorBlueprints = new Dictionary<FloorPurposeKind, ConstructionBlueprint>
        {
            {
                FloorPurposeKind.Residential,
                CreateFloorBlueprint(FloorPurposeKind.Residential, FloorMaterialKind.Timber, 8f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 4 })
            },
            {
                FloorPurposeKind.Storage,
                CreateFloorBlueprint(FloorPurposeKind.Storage, FloorMaterialKind.Timber, 7f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 3 })
            },
            {
                FloorPurposeKind.Workshop,
                CreateFloorBlueprint(FloorPurposeKind.Workshop, FloorMaterialKind.Timber, 9f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 4 },
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 1 })
            },
            {
                FloorPurposeKind.Weaponry,
                CreateFloorBlueprint(FloorPurposeKind.Weaponry, FloorMaterialKind.Stone, 10f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 3 },
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 2 })
            },
            {
                FloorPurposeKind.Barracks,
                CreateFloorBlueprint(FloorPurposeKind.Barracks, FloorMaterialKind.Timber, 9f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 4 },
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 1 })
            },
            {
                FloorPurposeKind.Roadway,
                CreateFloorBlueprint(FloorPurposeKind.Roadway, FloorMaterialKind.Stone, 5f,
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 2 })
            },
            {
                FloorPurposeKind.Farmland,
                CreateFloorBlueprint(FloorPurposeKind.Farmland, FloorMaterialKind.PackedEarth, 4f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 1 })
            },
            {
                FloorPurposeKind.Utility,
                CreateFloorBlueprint(FloorPurposeKind.Utility, FloorMaterialKind.Stone, 9f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 2 },
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 2 })
            }
        };

        private static readonly Dictionary<ModuleKind, ModuleDefinition> ModuleDefinitions = new Dictionary<ModuleKind, ModuleDefinition>
        {
            {
                ModuleKind.Bed,
                CreateModuleDefinition(ModuleKind.Bed, "Bed", FloorPurposeKind.Residential, true, 2, 0, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 6 })
            },
            {
                ModuleKind.Rack,
                CreateModuleDefinition(ModuleKind.Rack, "Rack", FloorPurposeKind.Storage, false, 0, 24, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 4 })
            },
            {
                ModuleKind.Bin,
                CreateModuleDefinition(ModuleKind.Bin, "Bin", FloorPurposeKind.Storage, false, 0, 18, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 3 })
            },
            {
                ModuleKind.Pallet,
                CreateModuleDefinition(ModuleKind.Pallet, "Pallet", FloorPurposeKind.Storage, false, 0, 12, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 2 })
            },
            {
                ModuleKind.Workbench,
                CreateModuleDefinition(ModuleKind.Workbench, "Workbench", FloorPurposeKind.Workshop, true, 0, 0,
                    CreateProductionRecipe(
                        "workbench-smelt-metal",
                        new ResourceStack { resourceType = ResourceType.Ore, amount = 2 },
                        new ResourceStack { resourceType = ResourceType.Wood, amount = 1 },
                        new ResourceStack { resourceType = ResourceType.Metal, amount = 2 },
                        8f),
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 8 },
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 2 })
            },
            {
                ModuleKind.WeaponBench,
                CreateModuleDefinition(ModuleKind.WeaponBench, "Weapon Bench", FloorPurposeKind.Weaponry, true, 0, 0,
                    CreateProductionRecipe(
                        "weapon-bench-craft-weapons",
                        new ResourceStack { resourceType = ResourceType.Metal, amount = 2 },
                        new ResourceStack { resourceType = ResourceType.Wood, amount = 1 },
                        new ResourceStack { resourceType = ResourceType.Weapons, amount = 1 },
                        10f),
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 8 },
                    new ResourceStack { resourceType = ResourceType.Metal, amount = 4 })
            },
            {
                ModuleKind.Bunk,
                CreateModuleDefinition(ModuleKind.Bunk, "Bunk", FloorPurposeKind.Barracks, true, 3, 0, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 7 })
            },
            {
                ModuleKind.CommandPost,
                CreateModuleDefinition(ModuleKind.CommandPost, "Command Post", FloorPurposeKind.Barracks, true, 0, 0, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 6 },
                    new ResourceStack { resourceType = ResourceType.Stone, amount = 2 })
            },
            {
                ModuleKind.DrillPost,
                CreateModuleDefinition(ModuleKind.DrillPost, "Drill Post", FloorPurposeKind.Barracks, false, 0, 0, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 4 })
            }
        };

        private static readonly Dictionary<LandmarkKind, LandmarkDefinition> LandmarkDefinitions = new Dictionary<LandmarkKind, LandmarkDefinition>
        {
            {
                LandmarkKind.TownCenterCore,
                new LandmarkDefinition
                {
                    landmarkKind = LandmarkKind.TownCenterCore,
                    displayName = "Town Center Core",
                    maxHitPoints = 1200,
                    commandRadius = 12,
                    initialStorageBonus = 40,
                    buildCost = new List<ResourceStack>
                    {
                        new ResourceStack { resourceType = ResourceType.Wood, amount = 20 },
                        new ResourceStack { resourceType = ResourceType.Stone, amount = 15 }
                    },
                    footprint = new LandmarkFootprint
                    {
                        occupiedOffsets = new List<HexCoord>
                        {
                            new HexCoord(0, 0),
                            new HexCoord(1, 0)
                        }
                    }
                }
            },
            {
                LandmarkKind.Tower,
                new LandmarkDefinition
                {
                    landmarkKind = LandmarkKind.Tower,
                    displayName = "Tower",
                    maxHitPoints = 900,
                    commandRadius = 4,
                    buildCost = new List<ResourceStack>
                    {
                        new ResourceStack { resourceType = ResourceType.Wood, amount = 15 },
                        new ResourceStack { resourceType = ResourceType.Stone, amount = 20 }
                    },
                    footprint = new LandmarkFootprint
                    {
                        occupiedOffsets = new List<HexCoord> { HexCoord.Zero }
                    }
                }
            },
            {
                LandmarkKind.Gatehouse,
                new LandmarkDefinition
                {
                    landmarkKind = LandmarkKind.Gatehouse,
                    displayName = "Gatehouse",
                    maxHitPoints = 1000,
                    commandRadius = 3,
                    buildCost = new List<ResourceStack>
                    {
                        new ResourceStack { resourceType = ResourceType.Wood, amount = 12 },
                        new ResourceStack { resourceType = ResourceType.Stone, amount = 15 }
                    },
                    footprint = new LandmarkFootprint
                    {
                        occupiedOffsets = new List<HexCoord>
                        {
                            new HexCoord(0, 0),
                            new HexCoord(1, 0)
                        }
                    },
                    placementRule = new LandmarkPlacementRule
                    {
                        requiresClearCells = true,
                        mayConnectToWalls = true,
                        blocksOpenPassage = false
                    }
                }
            }
        };

        public static ConstructionBlueprint GetFloorBlueprint(FloorPurposeKind purposeKind)
        {
            return FloorBlueprints.TryGetValue(purposeKind, out ConstructionBlueprint blueprint)
                ? CloneBlueprint(blueprint)
                : CreateFloorBlueprint(FloorPurposeKind.Storage, FloorMaterialKind.Timber, 6f,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 2 });
        }

        public static ConstructionBlueprint GetEdgeBlueprint(EdgeFeatureKind featureKind)
        {
            return featureKind switch
            {
                EdgeFeatureKind.Wall => new ConstructionBlueprint
                {
                    featureKind = ConstructionFeatureKind.Edge,
                    edgeFeatureKind = EdgeFeatureKind.Wall,
                    buildCost = new List<ResourceStack> { new ResourceStack { resourceType = ResourceType.Wood, amount = 5 } },
                    requiredWork = 10f
                },
                EdgeFeatureKind.Door => new ConstructionBlueprint
                {
                    featureKind = ConstructionFeatureKind.Edge,
                    edgeFeatureKind = EdgeFeatureKind.Door,
                    buildCost = new List<ResourceStack> { new ResourceStack { resourceType = ResourceType.Wood, amount = 4 } },
                    requiredWork = 8f
                },
                _ => new ConstructionBlueprint
                {
                    featureKind = ConstructionFeatureKind.Edge,
                    edgeFeatureKind = featureKind,
                    buildCost = new List<ResourceStack> { new ResourceStack { resourceType = ResourceType.Wood, amount = 3 } },
                    requiredWork = 8f
                }
            };
        }

        public static ModuleDefinition GetModuleDefinition(ModuleKind moduleKind)
        {
            return ModuleDefinitions.TryGetValue(moduleKind, out ModuleDefinition definition)
                ? CloneModuleDefinition(definition)
                : CreateModuleDefinition(ModuleKind.Rack, "Rack", FloorPurposeKind.Storage, false, 0, 12, null,
                    new ResourceStack { resourceType = ResourceType.Wood, amount = 2 });
        }

        public static ConstructionBlueprint GetModuleBlueprint(ModuleKind moduleKind)
        {
            ModuleDefinition definition = GetModuleDefinition(moduleKind);
            return new ConstructionBlueprint
            {
                featureKind = ConstructionFeatureKind.Module,
                moduleKind = moduleKind,
                buildCost = new List<ResourceStack>(definition.buildCost),
                requiredWork = 8f
            };
        }

        public static LandmarkDefinition GetLandmarkDefinition(LandmarkKind landmarkKind)
        {
            return LandmarkDefinitions.TryGetValue(landmarkKind, out LandmarkDefinition definition)
                ? CloneLandmarkDefinition(definition)
                : CloneLandmarkDefinition(LandmarkDefinitions[LandmarkKind.Tower]);
        }

        public static ConstructionBlueprint GetLandmarkBlueprint(LandmarkKind landmarkKind)
        {
            LandmarkDefinition definition = GetLandmarkDefinition(landmarkKind);
            return new ConstructionBlueprint
            {
                featureKind = ConstructionFeatureKind.Landmark,
                landmarkKind = landmarkKind,
                buildCost = new List<ResourceStack>(definition.buildCost),
                requiredWork = landmarkKind == LandmarkKind.TownCenterCore ? 18f : 14f
            };
        }

        public static TileCapacityProfile GetDefaultCapacityProfile(FloorState floorState, bool hasModule, bool hasLandmark, bool hasLargePile)
        {
            int baseGroundCapacity = floorState != null && floorState.isBuilt ? 4 : 3;
            if (hasModule)
            {
                baseGroundCapacity = Mathf.Max(1, baseGroundCapacity - 1);
            }

            if (hasLandmark)
            {
                baseGroundCapacity = Mathf.Max(0, baseGroundCapacity - 3);
            }

            if (hasLargePile)
            {
                baseGroundCapacity = 0;
            }

            return new TileCapacityProfile
            {
                groundSmallCapacity = baseGroundCapacity,
                groundLargeCapacity = hasLandmark ? 0 : 1,
                machineCapacity = hasLandmark ? 0 : 1,
                airCapacity = 99,
                congestionThreshold = Mathf.Max(1, baseGroundCapacity - 1),
                congestionMovePenalty = 0.45f,
                roadMoveBonus = floorState != null && floorState.purposeKind == FloorPurposeKind.Roadway ? 0.35f : 0f
            };
        }

        private static ConstructionBlueprint CreateFloorBlueprint(FloorPurposeKind purposeKind, FloorMaterialKind materialKind, float work, params ResourceStack[] cost)
        {
            var blueprint = new ConstructionBlueprint
            {
                featureKind = ConstructionFeatureKind.Floor,
                floorPurposeKind = purposeKind,
                floorMaterialKind = materialKind,
                requiredWork = work,
                buildCost = new List<ResourceStack>()
            };

            for (int i = 0; i < cost.Length; i++)
            {
                blueprint.buildCost.Add(cost[i]);
            }

            return blueprint;
        }

        private static ModuleDefinition CreateModuleDefinition(
            ModuleKind moduleKind,
            string displayName,
            FloorPurposeKind requiredPurposeKind,
            bool requiresShelter,
            int housingProvided,
            int storageCapacity,
            ProductionRecipeDefinition productionRecipe,
            params ResourceStack[] cost)
        {
            var definition = new ModuleDefinition
            {
                moduleKind = moduleKind,
                displayName = displayName,
                requiredPurposeKind = requiredPurposeKind,
                requiresShelter = requiresShelter,
                housingProvided = housingProvided,
                storageCapacity = storageCapacity,
                buildCost = new List<ResourceStack>(),
                productionRecipe = CloneProductionRecipe(productionRecipe)
            };

            for (int i = 0; i < cost.Length; i++)
            {
                definition.buildCost.Add(cost[i]);
            }

            if (definition.productionRecipe != null)
            {
                for (int i = 0; i < definition.productionRecipe.inputCosts.Count; i++)
                {
                    ResourceStack input = definition.productionRecipe.inputCosts[i];
                    definition.inputSockets.Add(new ModuleSocketDefinition
                    {
                        socketId = $"{moduleKind}-input-{i}",
                        acceptedResourceType = input.resourceType,
                        maxAmount = input.amount
                    });
                }
            }

            return definition;
        }

        private static ProductionRecipeDefinition CreateProductionRecipe(
            string recipeId,
            ResourceStack inputA,
            ResourceStack inputB,
            ResourceStack output,
            float requiredWork)
        {
            return new ProductionRecipeDefinition
            {
                recipeId = recipeId,
                inputCosts = new List<ResourceStack> { inputA, inputB },
                output = output,
                requiredWork = requiredWork
            };
        }

        private static ConstructionBlueprint CloneBlueprint(ConstructionBlueprint source)
        {
            return new ConstructionBlueprint
            {
                featureKind = source.featureKind,
                floorPurposeKind = source.floorPurposeKind,
                floorMaterialKind = source.floorMaterialKind,
                edgeFeatureKind = source.edgeFeatureKind,
                moduleKind = source.moduleKind,
                landmarkKind = source.landmarkKind,
                requiredWork = source.requiredWork,
                buildCost = new List<ResourceStack>(source.buildCost)
            };
        }

        private static ModuleDefinition CloneModuleDefinition(ModuleDefinition source)
        {
            return new ModuleDefinition
            {
                moduleKind = source.moduleKind,
                displayName = source.displayName,
                requiredPurposeKind = source.requiredPurposeKind,
                requiresShelter = source.requiresShelter,
                housingProvided = source.housingProvided,
                storageCapacity = source.storageCapacity,
                buildCost = new List<ResourceStack>(source.buildCost),
                inputSockets = new List<ModuleSocketDefinition>(source.inputSockets),
                productionRecipe = CloneProductionRecipe(source.productionRecipe)
            };
        }

        private static ProductionRecipeDefinition CloneProductionRecipe(ProductionRecipeDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return new ProductionRecipeDefinition
            {
                recipeId = source.recipeId,
                inputCosts = new List<ResourceStack>(source.inputCosts),
                output = source.output,
                requiredWork = source.requiredWork
            };
        }

        private static LandmarkDefinition CloneLandmarkDefinition(LandmarkDefinition source)
        {
            return new LandmarkDefinition
            {
                landmarkKind = source.landmarkKind,
                displayName = source.displayName,
                maxHitPoints = source.maxHitPoints,
                commandRadius = source.commandRadius,
                initialStorageBonus = source.initialStorageBonus,
                buildCost = new List<ResourceStack>(source.buildCost),
                footprint = new LandmarkFootprint
                {
                    occupiedOffsets = new List<HexCoord>(source.footprint.occupiedOffsets)
                },
                placementRule = new LandmarkPlacementRule
                {
                    requiresClearCells = source.placementRule.requiresClearCells,
                    mayConnectToWalls = source.placementRule.mayConnectToWalls,
                    blocksOpenPassage = source.placementRule.blocksOpenPassage
                }
            };
        }
    }
}
