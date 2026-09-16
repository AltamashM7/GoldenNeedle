using System;
using System.Collections.Generic;
using System.Text;
using GoldenNeedle.Gameplay.Calibration;
using GoldenNeedle.Gameplay.Commands;
using GoldenNeedle.Gameplay.Flow;
using GoldenNeedle.Gameplay.Hub;
using GoldenNeedle.Gameplay.Player;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using Unity.Pipeline.Commands;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GoldenNeedle.Editor.Gameplay
{
    /// <summary>
    /// One-shot, Editor-API-only authoring for the first production Calibration -> Hub flow.
    /// It is intentionally additive and refuses to overwrite dirty scenes or duplicate roots.
    /// </summary>
    public static class GoldenNeedleCalibrationHubAuthoring
    {
        private const string CalibrationScenePath = "Assets/Scenes/Caliberation.unity";
        private const string HubScenePath = "Assets/Scenes/GoldenNeedle_Hub.unity";
        private const string ObstacleScenePath = "Assets/Scenes/Obstacle Course.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string PlayerPrefabPath = "Assets/GoldenNeedle/Gameplay/Player/GoldenNeedlePlayer.prefab";

        // Sampled from the live GoldenNeedle_Island Terrain at normalized (0.85, 0.50).
        // This is clear of the two portals, bridge and poppy props while remaining well inside the terrain.
        private static readonly Vector3 HubEntryPosition = new Vector3(-130.46f, 3.252f, -9.052f);

        [MenuItem("Golden Needle/Gameplay/Inspect Calibration and Hub Scenes")]
        public static void InspectScenes()
        {
            InspectScene(CalibrationScenePath);
            InspectScene(HubScenePath);
        }

        [CliCommand("goldenneedle_inspect_hub_geometry", "Inspect GoldenNeedle_Hub roots and world-space renderer/collider bounds before authoring the HubEntry spawn.", Tags = new[] { "scenes" })]
        public static string InspectHubGeometryCommand()
        {
            EnsureNoDirtyActiveScene();
            var scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            var summary = new StringBuilder();
            summary.AppendLine($"scene={scene.path}");

            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                summary.AppendLine($"root[{rootIndex}] '{roots[rootIndex].name}' position={roots[rootIndex].transform.position}");
            }

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer.gameObject.scene != scene)
                {
                    continue;
                }

                summary.AppendLine($"renderer '{GetHierarchyPath(renderer.transform)}' bounds={renderer.bounds}");
            }

            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < colliders.Length; index++)
            {
                var collider = colliders[index];
                if (collider.gameObject.scene != scene)
                {
                    continue;
                }

                summary.AppendLine($"collider '{GetHierarchyPath(collider.transform)}' bounds={collider.bounds} trigger={collider.isTrigger}");
            }

            return summary.ToString();
        }

        [CliCommand("goldenneedle_sample_hub_terrain", "Sample the existing Hub Terrain at a small set of candidate points for safe spawn placement.", Tags = new[] { "scenes" })]
        public static string SampleHubTerrainCommand()
        {
            EnsureNoDirtyActiveScene();
            var scene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            var terrains = FindComponentsInScene<Terrain>(scene);
            if (terrains.Count != 1 || terrains[0].terrainData == null)
            {
                throw new InvalidOperationException("GoldenNeedle_Hub must contain exactly one Terrain with TerrainData for spawn inspection.");
            }

            var terrain = terrains[0];
            var data = terrain.terrainData;
            var summary = new StringBuilder();
            summary.AppendLine($"terrain='{GetHierarchyPath(terrain.transform)}' position={terrain.transform.position} size={data.size}");
            var normalizedSamples = new[]
            {
                new Vector2(0.15f, 0.15f),
                new Vector2(0.35f, 0.35f),
                new Vector2(0.50f, 0.50f),
                new Vector2(0.65f, 0.65f),
                new Vector2(0.85f, 0.85f),
                new Vector2(0.15f, 0.50f),
                new Vector2(0.50f, 0.15f),
                new Vector2(0.85f, 0.50f),
                new Vector2(0.50f, 0.85f),
            };
            for (var index = 0; index < normalizedSamples.Length; index++)
            {
                var normalized = normalizedSamples[index];
                var world = new Vector3(
                    terrain.transform.position.x + data.size.x * normalized.x,
                    terrain.transform.position.y + data.size.y + 10f,
                    terrain.transform.position.z + data.size.z * normalized.y);
                var terrainHeight = terrain.SampleHeight(world);
                summary.AppendLine($"sample[{index}] normalized={normalized} world=({world.x:0.###}, {terrainHeight:0.###}, {world.z:0.###})");
            }

            return summary.ToString();
        }

        [MenuItem("Golden Needle/Gameplay/Author Calibration to Hub Integration")]
        public static void AuthorProductionIntegration()
        {
            try
            {
                EnsureRequiredAssetsExist();
                EnsureNoDirtyActiveScene();

                var calibrationScene = EditorSceneManager.OpenScene(CalibrationScenePath, OpenSceneMode.Single);
                AuthorCalibrationScene(calibrationScene);
                EditorSceneManager.SaveScene(calibrationScene);

                var hubScene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
                AuthorHubScene(hubScene);
                EditorSceneManager.SaveScene(hubScene);

                ConfigureBuildSettings();
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("Golden Needle: Calibration -> Hub production integration authored successfully.");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"Golden Needle: Calibration -> Hub authoring failed: {exception.Message}");
                throw;
            }
        }

        [CliCommand("goldenneedle_author_calibration_hub", "Author the production Calibration -> Hub composition and final Build Settings through Unity Editor APIs.", Tags = new[] { "scenes", "build/settings" })]
        public static string AuthorProductionIntegrationCommand()
        {
            AuthorProductionIntegration();
            return "Golden Needle: Calibration -> Hub production integration authored.";
        }

        private static void InspectScene(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            UnityEngine.Debug.Log($"Golden Needle scene inspection: {scenePath} roots={roots.Length}");
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                UnityEngine.Debug.Log($"  root[{rootIndex}] '{roots[rootIndex].name}' position={roots[rootIndex].transform.position}");
            }

            if (scenePath != HubScenePath)
            {
                return;
            }

            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer.gameObject.scene != scene)
                {
                    continue;
                }

                UnityEngine.Debug.Log($"  renderer '{GetHierarchyPath(renderer.transform)}' bounds={renderer.bounds}");
            }

            var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var index = 0; index < colliders.Length; index++)
            {
                var collider = colliders[index];
                if (collider.gameObject.scene != scene)
                {
                    continue;
                }

                UnityEngine.Debug.Log($"  collider '{GetHierarchyPath(collider.transform)}' bounds={collider.bounds} trigger={collider.isTrigger}");
            }
        }

        private static void AuthorCalibrationScene(Scene scene)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = EnsureSinglePlayerInstance(scene, playerPrefab);
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;

            var services = GetOrCreateRoot(scene, "GoldenNeedle_GameServices");
            var flowManager = EnsureComponent<GameFlowManager>(services);
            EnsureComponent<GameplayCommandHost>(services);
            var fadeCanvas = GetOrCreateChild(services.transform, "FadeCanvas");
            var canvas = EnsureComponent<Canvas>(fadeCanvas);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var canvasGroup = EnsureComponent<CanvasGroup>(fadeCanvas);
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            var gameFlowFade = EnsureComponent<GameFlowFade>(fadeCanvas);
            SetObjectReference(gameFlowFade, "canvasGroup", canvasGroup);
            SetObjectReference(flowManager, "fade", gameFlowFade);

            var fadeImage = GetOrCreateChild(fadeCanvas.transform, "FadeImage");
            var fadeImageRect = EnsureComponent<RectTransform>(fadeImage);
            SetFullStretch(fadeImageRect);
            var image = EnsureComponent<Image>(fadeImage);
            image.color = Color.black;
            image.raycastTarget = false;

            var calibrationRoot = GetOrCreateRoot(scene, "GoldenNeedle_Calibration");
            var controller = EnsureComponent<CalibrationSceneController>(calibrationRoot);
            var calibrationCanvas = GetOrCreateChild(calibrationRoot.transform, "CalibrationCanvas");
            var calibrationCanvasComponent = EnsureComponent<Canvas>(calibrationCanvas);
            calibrationCanvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
            calibrationCanvasComponent.overrideSorting = true;
            calibrationCanvasComponent.sortingOrder = 10;
            var scaler = EnsureComponent<CanvasScaler>(calibrationCanvas);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            EnsureComponent<GraphicRaycaster>(calibrationCanvas);

            var previewPanel = GetOrCreateChild(calibrationCanvas.transform, "PreviewPanel");
            var previewPanelRect = EnsureComponent<RectTransform>(previewPanel);
            SetAnchors(previewPanelRect, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var previewPanelImage = EnsureComponent<Image>(previewPanel);
            previewPanelImage.color = new Color(0.04f, 0.05f, 0.07f, 1f);
            previewPanelImage.raycastTarget = false;

            var cameraPreview = GetOrCreateChild(previewPanel.transform, "CameraPreview");
            var cameraPreviewRect = EnsureComponent<RectTransform>(cameraPreview);
            SetFullStretch(cameraPreviewRect);
            var rawImage = EnsureComponent<RawImage>(cameraPreview);
            rawImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            rawImage.raycastTarget = false;
            var aspectRatioFitter = EnsureComponent<AspectRatioFitter>(cameraPreview);
            var previewComponent = EnsureComponent<CalibrationCameraPreview>(cameraPreview);
            SetObjectReference(previewComponent, "rawImage", rawImage);
            SetObjectReference(previewComponent, "aspectRatioFitter", aspectRatioFitter);

            var instruction = CreateText(calibrationCanvas.transform, "InstructionText", "Stand comfortably where your upper body is visible, then begin calibration.", 30, Color.white);
            SetAnchors(instruction.rectTransform, new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.98f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            instruction.alignment = TextAnchor.MiddleCenter;

            var status = CreateText(calibrationCanvas.transform, "StatusText", "Starting motion tracking…", 26, new Color(0.8f, 0.9f, 1f, 1f));
            SetAnchors(status.rectTransform, new Vector2(0.1f, 0.14f), new Vector2(0.9f, 0.21f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            status.alignment = TextAnchor.MiddleCenter;

            var beginButton = CreateButton(calibrationCanvas.transform, "BeginCalibrationButton", "Begin Calibration");
            SetAnchors(beginButton.GetComponent<RectTransform>(), new Vector2(0.35f, 0.035f), new Vector2(0.65f, 0.12f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));

            var retryButton = CreateButton(calibrationCanvas.transform, "RetryTransitionButton", "Retry Hub Transition");
            SetAnchors(retryButton.GetComponent<RectTransform>(), new Vector2(0.35f, 0.035f), new Vector2(0.65f, 0.12f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            retryButton.gameObject.SetActive(false);

            SetObjectReference(controller, "instructionText", instruction);
            SetObjectReference(controller, "statusText", status);
            SetObjectReference(controller, "beginCalibrationButton", beginButton);
            SetObjectReference(controller, "retryTransitionButton", retryButton);
            SetObjectReference(controller, "cameraPreview", previewComponent);
            AddPersistentListenerIfMissing(beginButton, controller.OnBeginCalibrationPressed);
            AddPersistentListenerIfMissing(retryButton, controller.RetryHubTransition);

            EnsureEventSystem(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            UnityEngine.Debug.Log($"Golden Needle: authored Caliberation hierarchy with player prefab '{PlayerPrefabPath}'.");
        }

        private static void AuthorHubScene(Scene scene)
        {
            var existingIntegrationRoots = FindRootObjects(scene, "GoldenNeedle_HubIntegration");
            if (existingIntegrationRoots.Count > 1)
            {
                throw new InvalidOperationException("GoldenNeedle_Hub contains multiple GoldenNeedle_HubIntegration roots.");
            }

            var integrationRoot = existingIntegrationRoots.Count == 1
                ? existingIntegrationRoots[0]
                : CreateRoot(scene, "GoldenNeedle_HubIntegration");
            var existingEntryRoots = FindChildrenByName(integrationRoot.transform, "HubEntry");
            if (existingEntryRoots.Count > 1)
            {
                throw new InvalidOperationException("GoldenNeedle_HubIntegration contains multiple HubEntry objects.");
            }

            var hubEntry = existingEntryRoots.Count == 1
                ? existingEntryRoots[0].gameObject
                : CreateChild(integrationRoot.transform, "HubEntry");
            hubEntry.transform.position = HubEntryPosition;
            hubEntry.transform.rotation = Quaternion.identity;
            var spawnPoint = EnsureComponent<PlayerSpawnPoint>(hubEntry);
            SetString(spawnPoint, "spawnId", CalibrationSceneController.HubSpawnId);

            var contextController = EnsureComponent<HubSceneContextController>(integrationRoot);
            UnityEngine.Debug.Log($"Golden Needle: HubEntry authored at world position {HubEntryPosition}; context={contextController.GetType().FullName}.");
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(CalibrationScenePath, true),
                new EditorBuildSettingsScene(HubScenePath, true),
                new EditorBuildSettingsScene(ObstacleScenePath, true),
                new EditorBuildSettingsScene(SampleScenePath, false),
            };
        }

        private static GameObject EnsureSinglePlayerInstance(Scene scene, GameObject prefab)
        {
            var sessions = FindComponentsInScene<GoldenNeedlePlayerSession>(scene);
            if (sessions.Count > 1)
            {
                throw new InvalidOperationException($"Caliberation contains {sessions.Count} GoldenNeedlePlayerSession components; expected exactly one.");
            }

            if (sessions.Count == 1)
            {
                var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sessions[0].gameObject);
                var source = instanceRoot == null ? null : PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                if (source != prefab)
                {
                    throw new InvalidOperationException("The existing Calibration player is not the accepted GoldenNeedlePlayer prefab instance.");
                }

                return instanceRoot;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            if (instance == null)
            {
                throw new InvalidOperationException("Unity could not instantiate the accepted GoldenNeedlePlayer prefab into Caliberation.");
            }

            return instance;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            var eventSystems = FindComponentsInScene<EventSystem>(scene);
            if (eventSystems.Count > 1)
            {
                throw new InvalidOperationException("Caliberation contains more than one EventSystem.");
            }

            if (eventSystems.Count == 1)
            {
                if (eventSystems[0].GetComponent<InputSystemUIInputModule>() == null)
                {
                    eventSystems[0].gameObject.AddComponent<InputSystemUIInputModule>();
                }

                return;
            }

            var eventSystemObject = CreateRoot(scene, "EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var buttonObject = GetOrCreateChild(parent, name);
            var rect = EnsureComponent<RectTransform>(buttonObject);
            var image = EnsureComponent<Image>(buttonObject);
            image.color = new Color(0.12f, 0.42f, 0.7f, 1f);
            image.raycastTarget = true;
            var button = EnsureComponent<Button>(buttonObject);
            button.targetGraphic = image;

            var labelObject = GetOrCreateChild(buttonObject.transform, "Label");
            var labelText = EnsureComponent<Text>(labelObject);
            SetFullStretch(labelText.rectTransform, new Vector2(12f, 6f));
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 26;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;
            labelText.raycastTarget = false;
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, Color color)
        {
            var textObject = GetOrCreateChild(parent, name);
            var text = EnsureComponent<Text>(textObject);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static void AddPersistentListenerIfMissing(Button button, UnityEngine.Events.UnityAction listener)
        {
            if (button.onClick.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(button.onClick, listener);
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component == null ? gameObject.AddComponent<T>() : component;
        }

        private static GameObject GetOrCreateRoot(Scene scene, string name)
        {
            var roots = FindRootObjects(scene, name);
            if (roots.Count > 1)
            {
                throw new InvalidOperationException($"Scene '{scene.name}' contains multiple roots named '{name}'.");
            }

            return roots.Count == 1 ? roots[0] : CreateRoot(scene, name);
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private static GameObject GetOrCreateChild(Transform parent, string name)
        {
            var matches = FindChildrenByName(parent, name);
            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"'{GetHierarchyPath(parent)}' contains multiple children named '{name}'.");
            }

            return matches.Count == 1 ? matches[0].gameObject : CreateChild(parent, name);
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static List<GameObject> FindRootObjects(Scene scene, string name)
        {
            var matches = new List<GameObject>();
            var roots = scene.GetRootGameObjects();
            for (var index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == name)
                {
                    matches.Add(roots[index]);
                }
            }

            return matches;
        }

        private static List<Transform> FindChildrenByName(Transform parent, string name)
        {
            var matches = new List<Transform>();
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == name)
                {
                    matches.Add(child);
                }
            }

            return matches;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            var matches = new List<T>();
            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                matches.AddRange(roots[rootIndex].GetComponentsInChildren<T>(true));
            }

            return matches;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Could not find serialized field '{propertyName}' on '{target.GetType().Name}'.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Could not find serialized field '{propertyName}' on '{target.GetType().Name}'.");
            }

            property.stringValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFullStretch(RectTransform rect, Vector2 padding = default)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one, padding, -padding, new Vector2(0.5f, 0.5f));
        }

        private static void SetAnchors(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax,
            Vector2 pivot)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = pivot;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void EnsureRequiredAssetsExist()
        {
            var requiredPaths = new[]
            {
                CalibrationScenePath,
                HubScenePath,
                ObstacleScenePath,
                SampleScenePath,
                PlayerPrefabPath,
            };
            for (var index = 0; index < requiredPaths.Length; index++)
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(requiredPaths[index]) == null)
                {
                    throw new InvalidOperationException($"Required asset is missing: {requiredPaths[index]}");
                }
            }
        }

        private static void EnsureNoDirtyActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException($"Active scene '{activeScene.path}' has unsaved changes. Save or discard them before running production authoring.");
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform == null ? string.Empty : transform.name;
            while (transform != null && transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
