using System;
using System.Collections.Generic;
using System.IO;
using Ggj2026.Game.Shell.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ggj2026.Game.Authoring.Editor
{
    public sealed class PolygonAssetIntegrationWindow : EditorWindow
    {
        private const string DefaultSourceRoot = "/Users/doronnacash/polygon assets";
        private const string VisualLibraryDirectory = "Assets/_Game/Art/WorldVisuals";
        private const string VisualLibraryAssetPath = VisualLibraryDirectory + "/WorldVisualPrefabLibrary.asset";

        private readonly List<PackageEntry> _packages = new List<PackageEntry>();

        private TextField _sourceRootField;
        private ScrollView _packagesScrollView;
        private Label _statusLabel;

        private sealed class PackageEntry
        {
            public string path;
            public bool selected;
        }

        [Serializable]
        public sealed class PolygonMaintenanceReport
        {
            public bool success = true;
            public string summary = string.Empty;
            public string sourceRoot = string.Empty;
            public int importedPackageCount;
            public int scannedPackageCount;
            public int scannedMaterialCount;
            public int repairedMaterialCount;
            public string repairShader = string.Empty;
            public bool refreshedVisualLibrary;
            public int assignedRendererCount;
            public string visualLibraryAssetPath = VisualLibraryAssetPath;
            public List<string> warnings = new List<string>();
        }

        private struct MaterialRepairResult
        {
            public int scanned;
            public int repaired;
            public string shaderName;
            public bool success;
            public string warning;
        }

        private struct VisualLibraryRefreshResult
        {
            public bool refreshed;
            public string warning;
        }

        private struct RendererAssignmentResult
        {
            public int assigned;
            public string warning;
        }

        public static PolygonMaintenanceReport RunMaintenanceNonInteractive(
            bool importPackages,
            bool repairMaterials,
            bool refreshVisualLibrary,
            bool assignLibraryToRenderers,
            string sourceRoot = DefaultSourceRoot)
        {
            var report = new PolygonMaintenanceReport
            {
                sourceRoot = string.IsNullOrWhiteSpace(sourceRoot) ? DefaultSourceRoot : sourceRoot.Trim()
            };

            List<string> packagePaths = CollectPackagePaths(report.sourceRoot);
            report.scannedPackageCount = packagePaths.Count;

            if (importPackages)
            {
                report.importedPackageCount = ImportSelectedPackages(packagePaths);
            }

            if (repairMaterials)
            {
                MaterialRepairResult materialResult = RepairPolygonMaterialsInternal();
                report.success &= materialResult.success;
                report.scannedMaterialCount = materialResult.scanned;
                report.repairedMaterialCount = materialResult.repaired;
                report.repairShader = materialResult.shaderName ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(materialResult.warning))
                {
                    report.warnings.Add(materialResult.warning);
                }
            }

            if (refreshVisualLibrary)
            {
                VisualLibraryRefreshResult refreshResult = BuildOrRefreshVisualLibraryInternal();
                report.refreshedVisualLibrary = refreshResult.refreshed;
                if (!string.IsNullOrWhiteSpace(refreshResult.warning))
                {
                    report.success = false;
                    report.warnings.Add(refreshResult.warning);
                }
            }

            AppendMissingVisualMappingWarnings(report.warnings);

            if (assignLibraryToRenderers)
            {
                RendererAssignmentResult assignmentResult = AssignLibraryToLoadedRenderersInternal();
                report.assignedRendererCount = assignmentResult.assigned;
                if (!string.IsNullOrWhiteSpace(assignmentResult.warning))
                {
                    report.warnings.Add(assignmentResult.warning);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report.summary =
                $"Polygon maintenance finished. Imported {report.importedPackageCount} package(s), repaired {report.repairedMaterialCount}/{report.scannedMaterialCount} material(s), refreshedLibrary={report.refreshedVisualLibrary}, assignedRenderers={report.assignedRendererCount}.";
            return report;
        }

        [MenuItem("Window/GGJ2026/Art/Polygon Asset Integrator")]
        public static void ShowWindow()
        {
            var window = GetWindow<PolygonAssetIntegrationWindow>("Polygon Integrator");
            window.minSize = new Vector2(780f, 520f);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            var container = new VisualElement();
            container.style.flexGrow = 1f;
            container.style.paddingTop = 10f;
            container.style.paddingBottom = 10f;
            container.style.paddingLeft = 10f;
            container.style.paddingRight = 10f;
            rootVisualElement.Add(container);

            var titleLabel = new Label("Import Polygon packages and wire a world visual prefab library.");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8f;
            container.Add(titleLabel);

            _sourceRootField = new TextField("Polygon Source Folder")
            {
                value = DefaultSourceRoot
            };
            container.Add(_sourceRootField);

            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.flexWrap = Wrap.Wrap;
            actionRow.style.marginBottom = 8f;
            actionRow.Add(CreateButton("Scan Packages", ScanPackages));
            actionRow.Add(CreateButton("Import Selected", ImportSelectedPackages));
            actionRow.Add(CreateButton("Repair Polygon Materials", RepairPolygonMaterials));
            actionRow.Add(CreateButton("Build/Refresh Visual Library", BuildOrRefreshVisualLibrary));
            actionRow.Add(CreateButton("Assign Library To Loaded Renderer", AssignLibraryToLoadedRenderers));
            actionRow.Add(CreateButton("Open Visual Library Asset", OpenVisualLibraryAsset));
            container.Add(actionRow);

            _packagesScrollView = new ScrollView(ScrollViewMode.Vertical);
            _packagesScrollView.style.flexGrow = 1f;
            _packagesScrollView.style.borderTopWidth = 1f;
            _packagesScrollView.style.borderBottomWidth = 1f;
            _packagesScrollView.style.borderLeftWidth = 1f;
            _packagesScrollView.style.borderRightWidth = 1f;
            _packagesScrollView.style.marginBottom = 8f;
            container.Add(_packagesScrollView);

            _statusLabel = new Label("Click Scan Packages to load available .unitypackage files.");
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.minHeight = 40f;
            container.Add(_statusLabel);

            ScanPackages();
        }

        private static Button CreateButton(string text, Action action)
        {
            var button = new Button(action) { text = text };
            button.style.marginRight = 6f;
            button.style.marginBottom = 6f;
            return button;
        }

        private void ScanPackages()
        {
            _packages.Clear();
            _packagesScrollView.Clear();

            string sourceRoot = _sourceRootField?.value?.Trim();
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                SetStatus($"Source directory not found: {sourceRoot}");
                return;
            }

            List<string> packagePaths = CollectPackagePaths(sourceRoot);
            for (int i = 0; i < packagePaths.Count; i++)
            {
                var entry = new PackageEntry
                {
                    path = packagePaths[i],
                    selected = packagePaths[i].IndexOf("survival", StringComparison.OrdinalIgnoreCase) >= 0
                };
                _packages.Add(entry);

                var toggle = new Toggle(Path.GetFileName(packagePaths[i])) { value = entry.selected };
                toggle.tooltip = packagePaths[i];
                int index = i;
                toggle.RegisterValueChangedCallback(evt => _packages[index].selected = evt.newValue);
                _packagesScrollView.Add(toggle);
            }

            SetStatus($"Found {_packages.Count} package(s).");
        }

        private void ImportSelectedPackages()
        {
            var selectedPaths = new List<string>();
            for (int i = 0; i < _packages.Count; i++)
            {
                if (!_packages[i].selected || string.IsNullOrWhiteSpace(_packages[i].path) || !File.Exists(_packages[i].path))
                {
                    continue;
                }

                selectedPaths.Add(_packages[i].path);
            }

            int imported = ImportSelectedPackages(selectedPaths);
            RepairPolygonMaterials();
            SetStatus($"Imported {imported} package(s) and repaired incompatible Polygon materials.");
        }

        private void BuildOrRefreshVisualLibrary()
        {
            VisualLibraryRefreshResult result = BuildOrRefreshVisualLibraryInternal();
            if (!string.IsNullOrWhiteSpace(result.warning))
            {
                SetStatus(result.warning);
                return;
            }

            SetStatus("World visual prefab library refreshed.");
        }

        private static void PopulateVisualLibraryMapping(WorldVisualPrefabLibrary library)
        {
            List<string> prefabPaths = CollectCandidatePrefabPaths();
            var serializedLibrary = new SerializedObject(library);

            AssignDefinition(serializedLibrary, "defaultUnit", FindPrefabByCategory(prefabPaths, new[] { "worker", "survivor", "character", "human" }, new[] { "zombie", "tree", "rock", "wall", "door", "weapon", "ui" }), false);
            AssignDefinition(serializedLibrary, "workerUnit", FindPrefabByCategory(prefabPaths, new[] { "worker", "survivor", "character" }, new[] { "zombie", "weapon", "wall", "door", "vehicle" }), false);
            AssignDefinition(serializedLibrary, "builderUnit", FindPrefabByCategory(prefabPaths, new[] { "builder", "worker", "character" }, new[] { "zombie", "weapon", "wall", "door", "vehicle" }), false);
            AssignDefinition(serializedLibrary, "haulerUnit", FindPrefabByCategory(prefabPaths, new[] { "hauler", "worker", "survivor" }, new[] { "zombie", "weapon", "wall", "door", "vehicle" }), false);
            AssignDefinition(serializedLibrary, "crafterUnit", FindPrefabByCategory(prefabPaths, new[] { "worker", "survivor", "character" }, new[] { "zombie", "weapon", "wall", "door", "vehicle" }), false);
            AssignDefinition(serializedLibrary, "soldierUnit", FindPrefabByCategory(prefabPaths, new[] { "soldier", "military", "character" }, new[] { "zombie", "weapon", "tree", "rock", "wall", "door" }), false);

            AssignDefinition(serializedLibrary, "wallEdge", FindPrefabByCategory(prefabPaths, new[] { "wall", "fence", "barrier" }, new[] { "weapon", "ui", "character" }), false);
            AssignDefinition(serializedLibrary, "doorEdge", FindPrefabByCategory(prefabPaths, new[] { "door", "gate" }, new[] { "weapon", "ui", "character", "window" }), false);
            AssignDefinition(serializedLibrary, "gateEdge", FindPrefabByCategory(prefabPaths, new[] { "gatehouse", "gate" }, new[] { "weapon", "ui", "character", "window" }), false);

            AssignDefinition(serializedLibrary, "defaultModule", FindPrefabByCategory(prefabPaths, new[] { "bench", "table", "station", "work" }, new[] { "weapon", "ui", "character", "tree", "rock" }), false);
            AssignDefinition(serializedLibrary, "defaultItemStack", FindPrefabByCategory(prefabPaths, new[] { "crate", "box", "barrel", "bag" }, new[] { "ui", "character", "tree", "rock" }), false);
            AssignDefinition(serializedLibrary, "defaultResourceNode", FindPrefabByCategory(prefabPaths, new[] { "tree", "rock", "ore", "boulder", "log" }, new[] { "ui", "character", "weapon", "wall", "door" }), false);
            AssignDefinition(serializedLibrary, "defaultLandmark", FindPrefabByCategory(prefabPaths, new[] { "tower", "base", "headquarter", "town", "building" }, new[] { "ui", "character", "weapon" }), false);

            GameObject wallPrefab = GetAssignedPrefab(serializedLibrary, "wallEdge");
            if (GetAssignedPrefab(serializedLibrary, "doorEdge") == null && wallPrefab != null)
            {
                AssignDefinition(serializedLibrary, "doorEdge", wallPrefab, true);
            }

            if (GetAssignedPrefab(serializedLibrary, "gateEdge") == null && wallPrefab != null)
            {
                AssignDefinition(serializedLibrary, "gateEdge", wallPrefab, true);
            }

            ConfigureDefinition(serializedLibrary, "wallEdge", tintWithStateColor: false, localEuler: new Vector3(0f, 90f, 0f));
            ConfigureDefinition(serializedLibrary, "doorEdge", tintWithStateColor: true, localEuler: new Vector3(0f, 90f, 0f));
            ConfigureDefinition(serializedLibrary, "gateEdge", tintWithStateColor: true, localEuler: new Vector3(0f, 90f, 0f));

            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<string> CollectCandidatePrefabPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var results = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string lower = path.ToLowerInvariant();
                if (!IsPolygonAssetPath(lower) ||
                    lower.Contains("/ui/") ||
                    lower.Contains("post-apocalyptic survival ui") ||
                    lower.Contains("/demo") ||
                    lower.Contains("/examples") ||
                    lower.Contains("/sample"))
                {
                    continue;
                }

                results.Add(path);
            }

            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        private static GameObject FindPrefabByCategory(List<string> prefabPaths, string[] includeKeywords, string[] excludeKeywords)
        {
            if (prefabPaths == null || includeKeywords == null || includeKeywords.Length == 0)
            {
                return null;
            }

            string bestPath = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < prefabPaths.Count; i++)
            {
                string path = prefabPaths[i];
                string lowerPath = path.ToLowerInvariant();
                if (excludeKeywords != null)
                {
                    bool rejected = false;
                    for (int ex = 0; ex < excludeKeywords.Length; ex++)
                    {
                        string exclude = excludeKeywords[ex]?.Trim().ToLowerInvariant();
                        if (!string.IsNullOrWhiteSpace(exclude) && lowerPath.Contains(exclude))
                        {
                            rejected = true;
                            break;
                        }
                    }

                    if (rejected)
                    {
                        continue;
                    }
                }

                string fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                int score = 0;
                for (int inc = 0; inc < includeKeywords.Length; inc++)
                {
                    string include = includeKeywords[inc]?.Trim().ToLowerInvariant();
                    if (string.IsNullOrWhiteSpace(include))
                    {
                        continue;
                    }

                    if (fileName.Contains(include))
                    {
                        score += 7;
                    }
                    else if (lowerPath.Contains(include))
                    {
                        score += 2;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                }
            }

            return bestScore > 0 && !string.IsNullOrWhiteSpace(bestPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(bestPath)
                : null;
        }

        private static void AssignDefinition(SerializedObject serializedLibrary, string propertyName, GameObject prefab, bool tintWithStateColor)
        {
            SerializedProperty definition = serializedLibrary.FindProperty(propertyName);
            if (definition == null)
            {
                return;
            }

            definition.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            definition.FindPropertyRelative("localOffset").vector3Value = Vector3.zero;
            definition.FindPropertyRelative("localEuler").vector3Value = Vector3.zero;
            definition.FindPropertyRelative("localScale").vector3Value = Vector3.one;
            definition.FindPropertyRelative("useCustomScale").boolValue = false;
            definition.FindPropertyRelative("tintWithStateColor").boolValue = tintWithStateColor;
        }

        private static GameObject GetAssignedPrefab(SerializedObject serializedLibrary, string propertyName)
        {
            SerializedProperty definition = serializedLibrary.FindProperty(propertyName);
            if (definition == null)
            {
                return null;
            }

            return definition.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
        }

        private static void ConfigureDefinition(SerializedObject serializedLibrary, string propertyName, bool tintWithStateColor, Vector3 localEuler)
        {
            SerializedProperty definition = serializedLibrary.FindProperty(propertyName);
            if (definition == null)
            {
                return;
            }

            definition.FindPropertyRelative("localEuler").vector3Value = localEuler;
            definition.FindPropertyRelative("tintWithStateColor").boolValue = tintWithStateColor;
        }

        private static bool IsPolygonAssetPath(string lowerAssetPath)
        {
            return lowerAssetPath.Contains("/polygon-") || lowerAssetPath.Contains("/poly ");
        }

        private void RepairPolygonMaterials()
        {
            MaterialRepairResult result = RepairPolygonMaterialsInternal();
            if (!result.success)
            {
                SetStatus(result.warning);
                return;
            }

            SetStatus($"Material repair completed. Scanned {result.scanned} Polygon material(s), repaired {result.repaired}.");
        }

        private static bool IsUnsupportedShader(string shaderName)
        {
            return string.IsNullOrWhiteSpace(shaderName) ||
                   shaderName == "Hidden/InternalErrorShader" ||
                   shaderName == "Standard" ||
                   shaderName.StartsWith("Toon/") ||
                   shaderName.StartsWith("Legacy Shaders/") ||
                   shaderName.StartsWith("Particles/") ||
                   shaderName.Contains("Projector") ||
                   shaderName.Contains("Autodesk");
        }

        private void AssignLibraryToLoadedRenderers()
        {
            RendererAssignmentResult result = AssignLibraryToLoadedRenderersInternal();
            if (!string.IsNullOrWhiteSpace(result.warning))
            {
                SetStatus(result.warning);
                return;
            }

            SetStatus(result.assigned > 0
                ? $"Assigned library to {result.assigned} loaded LocalWorldDebugRenderer component(s)."
                : "No loaded LocalWorldDebugRenderer found in the current editor scene set.");
        }

        private void OpenVisualLibraryAsset()
        {
            WorldVisualPrefabLibrary library = AssetDatabase.LoadAssetAtPath<WorldVisualPrefabLibrary>(VisualLibraryAssetPath);
            if (library == null)
            {
                SetStatus("World visual library not found. Run Build/Refresh Visual Library first.");
                return;
            }

            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            SetStatus($"Opened {VisualLibraryAssetPath}.");
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = message;
            }

            Debug.Log($"[PolygonAssetIntegrationWindow] {message}");
        }

        private static List<string> CollectPackagePaths(string sourceRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                return new List<string>();
            }

            string[] packagePaths = Directory.GetFiles(sourceRoot, "*.unitypackage", SearchOption.AllDirectories);
            Array.Sort(packagePaths, StringComparer.OrdinalIgnoreCase);
            return new List<string>(packagePaths);
        }

        private static int ImportSelectedPackages(List<string> packagePaths)
        {
            if (packagePaths == null || packagePaths.Count == 0)
            {
                return 0;
            }

            int imported = 0;
            for (int i = 0; i < packagePaths.Count; i++)
            {
                string packagePath = packagePaths[i];
                if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                {
                    continue;
                }

                AssetDatabase.ImportPackage(packagePath, false);
                imported++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return imported;
        }

        private static VisualLibraryRefreshResult BuildOrRefreshVisualLibraryInternal()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Art"))
            {
                AssetDatabase.CreateFolder("Assets/_Game", "Art");
            }

            if (!AssetDatabase.IsValidFolder(VisualLibraryDirectory))
            {
                AssetDatabase.CreateFolder("Assets/_Game/Art", "WorldVisuals");
            }

            WorldVisualPrefabLibrary library = AssetDatabase.LoadAssetAtPath<WorldVisualPrefabLibrary>(VisualLibraryAssetPath);
            if (library == null)
            {
                library = CreateInstance<WorldVisualPrefabLibrary>();
                AssetDatabase.CreateAsset(library, VisualLibraryAssetPath);
            }

            PopulateVisualLibraryMapping(library);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new VisualLibraryRefreshResult
            {
                refreshed = true,
                warning = string.Empty
            };
        }

        private static MaterialRepairResult RepairPolygonMaterialsInternal()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (urpLit == null)
            {
                return new MaterialRepairResult
                {
                    success = false,
                    warning = "Could not find a compatible lit shader for material repair."
                };
            }

            string[] materialGuids = AssetDatabase.FindAssets("t:Material");
            int scanned = 0;
            int repaired = 0;
            for (int i = 0; i < materialGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                string lower = path.ToLowerInvariant();
                if (!IsPolygonAssetPath(lower))
                {
                    continue;
                }

                scanned++;
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                string shaderName = material.shader != null ? material.shader.name : string.Empty;
                if (!IsUnsupportedShader(shaderName))
                {
                    continue;
                }

                Texture mainTexture = material.HasProperty("_BaseMap")
                    ? material.GetTexture("_BaseMap")
                    : material.HasProperty("_MainTex")
                        ? material.GetTexture("_MainTex")
                        : null;
                Color color = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.HasProperty("_Color")
                        ? material.GetColor("_Color")
                        : Color.white;

                material.shader = urpLit;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", mainTexture);
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                EditorUtility.SetDirty(material);
                repaired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new MaterialRepairResult
            {
                scanned = scanned,
                repaired = repaired,
                shaderName = urpLit.name,
                success = true,
                warning = string.Empty
            };
        }

        private static RendererAssignmentResult AssignLibraryToLoadedRenderersInternal()
        {
            WorldVisualPrefabLibrary library = AssetDatabase.LoadAssetAtPath<WorldVisualPrefabLibrary>(VisualLibraryAssetPath);
            if (library == null)
            {
                return new RendererAssignmentResult
                {
                    assigned = 0,
                    warning = "World visual library not found. Run Build/Refresh Visual Library first."
                };
            }

            LocalWorldDebugRenderer[] renderers = UnityEngine.Object.FindObjectsByType<LocalWorldDebugRenderer>(FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Undo.RecordObject(renderers[i], "Assign World Visual Prefab Library");
                renderers[i].SetVisualPrefabLibrary(library);
                EditorUtility.SetDirty(renderers[i]);
            }

            return new RendererAssignmentResult
            {
                assigned = renderers.Length,
                warning = string.Empty
            };
        }

        private static void AppendMissingVisualMappingWarnings(List<string> warnings)
        {
            WorldVisualPrefabLibrary library = AssetDatabase.LoadAssetAtPath<WorldVisualPrefabLibrary>(VisualLibraryAssetPath);
            if (library == null)
            {
                return;
            }

            var serialized = new SerializedObject(library);
            string[] criticalProperties =
            {
                "defaultUnit",
                "wallEdge",
                "doorEdge",
                "defaultModule",
                "defaultItemStack",
                "defaultResourceNode",
                "defaultLandmark"
            };

            for (int i = 0; i < criticalProperties.Length; i++)
            {
                string propertyName = criticalProperties[i];
                GameObject prefab = GetAssignedPrefab(serialized, propertyName);
                if (prefab != null)
                {
                    continue;
                }

                warnings.Add($"Visual mapping '{propertyName}' is unassigned; runtime fallback visuals will be used.");
            }
        }
    }
}
