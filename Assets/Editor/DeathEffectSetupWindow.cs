#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using System.IO;
using System.Collections.Generic;
using Zenject;

namespace BreakingHue.Editor
{
    /// <summary>
    /// Editor window for setting up the death effect system.
    /// Handles DeathEffectController, WhiteOutEffect, and explosion prefab assignment.
    /// Access via Window > Breaking Hue > Death Effect Setup
    /// </summary>
    public class DeathEffectSetupWindow : EditorWindow
    {
        private const string UIPath = "Assets/UI";
        private const string PrefabsPath = "Assets/Prefabs";
        private const string EffectsPrefabsPath = "Assets/Prefabs/Effects";
        private const string ScenesPath = "Assets/Scenes";
        
        private Vector2 _scrollPosition;
        private List<string> _logMessages = new List<string>();
        
        // References for display
        private GameObject _explosionPrefab;

        [MenuItem("Window/Breaking Hue/Death Effect Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<DeathEffectSetupWindow>("Death Effect Setup");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            // Try to load existing explosion prefab
            _explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EffectsPrefabsPath}/ExplosionPlaceholder.prefab");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawHeader();
            EditorGUILayout.Space(10);

            DrawStatusSection();
            EditorGUILayout.Space(10);

            DrawSetupSection();
            EditorGUILayout.Space(10);

            DrawExplosionPrefabSection();
            EditorGUILayout.Space(20);

            DrawLogSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Death Effect Setup Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This tool sets up the death effect system:\n\n" +
                "• DeathEffectController - Manages death sequence\n" +
                "• WhiteOutEffect - UI fade to/from white\n" +
                "• Explosion Prefab - Particle system placeholder\n" +
                "• Zenject Bindings - Dependency injection",
                MessageType.Info);
        }

        private void DrawStatusSection()
        {
            EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Check for DeathEffectController
            var deathController = FindObjectOfType<Effects.DeathEffectController>();
            DrawStatusRow("DeathEffectController", deathController != null);
            
            // Check for WhiteOutEffect
            var whiteOut = FindObjectOfType<UI.WhiteOutEffect>();
            DrawStatusRow("WhiteOutEffect", whiteOut != null);
            
            // Check for explosion prefab
            bool hasExplosionPrefab = _explosionPrefab != null;
            DrawStatusRow("Explosion Prefab", hasExplosionPrefab);
            
            // Check if explosion is assigned
            bool explosionAssigned = deathController != null && deathController.ExplosionPrefab != null;
            DrawStatusRow("Explosion Assigned", explosionAssigned);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusRow(string label, bool isSetup)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(180));
            
            GUI.color = isSetup ? Color.green : Color.red;
            EditorGUILayout.LabelField(isSetup ? "✓ Ready" : "✗ Not Setup", EditorStyles.boldLabel);
            GUI.color = Color.white;
            
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSetupSection()
        {
            EditorGUILayout.LabelField("Setup Actions", EditorStyles.boldLabel);

            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Run Full Setup (Recommended)", GUILayout.Height(35)))
            {
                RunFullSetup();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(5);
            
            EditorGUILayout.LabelField("Individual Steps:", EditorStyles.miniBoldLabel);

            if (GUILayout.Button("1. Setup DeathEffectController"))
            {
                SetupDeathEffectController();
            }

            if (GUILayout.Button("2. Setup WhiteOutEffect"))
            {
                SetupWhiteOutEffect();
            }

            if (GUILayout.Button("3. Create Explosion Placeholder Prefab"))
            {
                CreateExplosionPlaceholderPrefab();
            }

            if (GUILayout.Button("4. Connect Components"))
            {
                ConnectComponents();
            }
        }

        private void DrawExplosionPrefabSection()
        {
            EditorGUILayout.LabelField("Explosion Prefab", EditorStyles.boldLabel);
            
            EditorGUILayout.HelpBox(
                "Assign your custom explosion particle system here.\n" +
                "The placeholder prefab will be replaced with your custom one.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Explosion Prefab:", GUILayout.Width(120));
            _explosionPrefab = (GameObject)EditorGUILayout.ObjectField(_explosionPrefab, typeof(GameObject), false);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Assign Explosion to DeathEffectController"))
            {
                AssignExplosionPrefab();
            }
        }

        private void DrawLogSection()
        {
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(120));
            foreach (var msg in _logMessages)
            {
                EditorGUILayout.LabelField(msg, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Clear Log"))
            {
                _logMessages.Clear();
            }
        }

        private void Log(string message)
        {
            _logMessages.Add($"[{System.DateTime.Now:HH:mm:ss}] {message}");
            Debug.Log($"[DeathEffectSetup] {message}");
            Repaint();
        }

        // ==================== SETUP METHODS ====================

        private void RunFullSetup()
        {
            _logMessages.Clear();
            Log("Starting Full Death Effect Setup...");

            try
            {
                // Check if we're in the right scene
                string currentScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                if (!currentScenePath.Contains("World"))
                {
                    if (EditorUtility.DisplayDialog("Open World Scene?",
                        "The Death Effect system should be set up in the World scene.\n\nOpen World scene now?",
                        "Open World Scene", "Cancel"))
                    {
                        string worldPath = $"{ScenesPath}/World.unity";
                        if (File.Exists(worldPath))
                        {
                            EditorSceneManager.OpenScene(worldPath, OpenSceneMode.Single);
                        }
                        else
                        {
                            Log("ERROR: World scene not found!");
                            return;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                EditorUtility.DisplayProgressBar("Death Effect Setup", "Creating components...", 0.2f);
                SetupDeathEffectController();

                EditorUtility.DisplayProgressBar("Death Effect Setup", "Creating WhiteOutEffect...", 0.4f);
                SetupWhiteOutEffect();

                EditorUtility.DisplayProgressBar("Death Effect Setup", "Creating explosion prefab...", 0.6f);
                CreateExplosionPlaceholderPrefab();

                EditorUtility.DisplayProgressBar("Death Effect Setup", "Connecting components...", 0.8f);
                ConnectComponents();

                EditorUtility.DisplayProgressBar("Death Effect Setup", "Saving scene...", 0.95f);
                EditorSceneManager.SaveOpenScenes();

                Log("Full Setup Complete!");
                EditorUtility.DisplayDialog("Setup Complete",
                    "Death Effect System is now ready!\n\n" +
                    "Components added:\n" +
                    "• DeathEffectController\n" +
                    "• WhiteOutEffect with UIDocument\n" +
                    "• ExplosionPlaceholder prefab\n\n" +
                    "Next steps:\n" +
                    "1. Create your explosion particle system\n" +
                    "2. Assign it to DeathEffectController's Explosion Prefab field\n" +
                    "   (or use this tool's Explosion Prefab section)",
                    "OK");
            }
            catch (System.Exception ex)
            {
                Log($"ERROR: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void SetupDeathEffectController()
        {
            // Check if already exists
            var existingController = FindObjectOfType<Effects.DeathEffectController>();
            if (existingController != null)
            {
                Log("DeathEffectController already exists, skipping");
                return;
            }

            // Find or create Managers container
            var managersObj = GameObject.Find("Managers");
            if (managersObj == null)
            {
                managersObj = new GameObject("Managers");
                Log("Created Managers container");
            }

            // Create DeathEffectController
            var deathControllerObj = new GameObject("DeathEffectController");
            deathControllerObj.transform.SetParent(managersObj.transform);
            deathControllerObj.AddComponent<Effects.DeathEffectController>();
            
            Log("Added DeathEffectController");
            EditorUtility.SetDirty(deathControllerObj);
        }

        private void SetupWhiteOutEffect()
        {
            // Check if already exists
            var existingWhiteOut = FindObjectOfType<UI.WhiteOutEffect>();
            if (existingWhiteOut != null)
            {
                Log("WhiteOutEffect already exists, skipping");
                return;
            }

            // Find or create UI container
            var uiContainer = GameObject.Find("UI");
            if (uiContainer == null)
            {
                uiContainer = new GameObject("UI");
                Log("Created UI container");
            }

            // Create WhiteOutEffect with UIDocument
            var whiteOutObj = new GameObject("WhiteOutEffect");
            whiteOutObj.transform.SetParent(uiContainer.transform);

            // Add UIDocument
            var uiDoc = whiteOutObj.AddComponent<UIDocument>();
            
            // Try to assign panel settings
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>($"{UIPath}/PanelSettings.asset");
            if (panelSettings != null)
            {
                uiDoc.panelSettings = panelSettings;
            }
            
            // Try to assign UXML
            var whiteOutUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{UIPath}/WhiteOutOverlay.uxml");
            if (whiteOutUxml != null)
            {
                uiDoc.visualTreeAsset = whiteOutUxml;
            }

            // Add WhiteOutEffect component
            whiteOutObj.AddComponent<UI.WhiteOutEffect>();

            Log("Added WhiteOutEffect with UIDocument");
            EditorUtility.SetDirty(whiteOutObj);
        }

        private void CreateExplosionPlaceholderPrefab()
        {
            // Check if prefab already exists
            string prefabPath = $"{EffectsPrefabsPath}/ExplosionPlaceholder.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                Log("ExplosionPlaceholder prefab already exists");
                _explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                return;
            }

            // Create folder if needed
            if (!AssetDatabase.IsValidFolder(EffectsPrefabsPath))
            {
                AssetDatabase.CreateFolder(PrefabsPath, "Effects");
            }

            // Create the prefab
            var explosionObj = new GameObject("ExplosionPlaceholder");
            
            // Add a basic particle system
            var particleSystem = explosionObj.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.duration = 1f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 5f;
            main.startSize = 0.3f;
            main.startColor = new Color(1f, 0.5f, 0f, 1f); // Orange
            main.maxParticles = 50;
            main.playOnAwake = true;
            
            var emission = particleSystem.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 30, 50)
            });
            
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            
            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.8f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.3f, 0f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0.1f, 0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Save as prefab
            PrefabUtility.SaveAsPrefabAsset(explosionObj, prefabPath);
            DestroyImmediate(explosionObj);

            _explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            
            Log("Created ExplosionPlaceholder prefab");
            Log("NOTE: Replace this with your custom explosion particle system!");
        }

        private void ConnectComponents()
        {
            var deathController = FindObjectOfType<Effects.DeathEffectController>();
            var whiteOutEffect = FindObjectOfType<UI.WhiteOutEffect>();

            if (deathController == null)
            {
                Log("ERROR: DeathEffectController not found. Run setup first.");
                return;
            }

            // Connect WhiteOutEffect
            if (whiteOutEffect != null)
            {
                var so = new SerializedObject(deathController);
                var whiteOutProp = so.FindProperty("whiteOutEffect");
                if (whiteOutProp != null)
                {
                    whiteOutProp.objectReferenceValue = whiteOutEffect;
                    so.ApplyModifiedProperties();
                    Log("Connected WhiteOutEffect to DeathEffectController");
                }
            }
            else
            {
                Log("WARNING: WhiteOutEffect not found");
            }

            // Connect explosion prefab if available
            if (_explosionPrefab != null)
            {
                var so = new SerializedObject(deathController);
                var explosionProp = so.FindProperty("explosionPrefab");
                if (explosionProp != null)
                {
                    explosionProp.objectReferenceValue = _explosionPrefab;
                    so.ApplyModifiedProperties();
                    Log("Connected Explosion Prefab to DeathEffectController");
                }
            }

            EditorUtility.SetDirty(deathController);
        }

        private void AssignExplosionPrefab()
        {
            var deathController = FindObjectOfType<Effects.DeathEffectController>();
            
            if (deathController == null)
            {
                Log("ERROR: DeathEffectController not found. Run setup first.");
                EditorUtility.DisplayDialog("Error", "DeathEffectController not found in scene.\n\nRun 'Full Setup' first.", "OK");
                return;
            }

            if (_explosionPrefab == null)
            {
                Log("ERROR: No explosion prefab selected");
                EditorUtility.DisplayDialog("Error", "Please assign an explosion prefab first.", "OK");
                return;
            }

            var so = new SerializedObject(deathController);
            var explosionProp = so.FindProperty("explosionPrefab");
            if (explosionProp != null)
            {
                explosionProp.objectReferenceValue = _explosionPrefab;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(deathController);
                
                Log($"Assigned explosion prefab: {_explosionPrefab.name}");
                EditorUtility.DisplayDialog("Success", $"Assigned '{_explosionPrefab.name}' to DeathEffectController.", "OK");
            }
        }
    }
}
#endif
