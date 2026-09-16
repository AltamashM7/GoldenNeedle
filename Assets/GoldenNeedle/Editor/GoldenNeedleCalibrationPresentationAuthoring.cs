using System;
using System.Collections.Generic;
using GoldenNeedle.Gameplay.Calibration;
using GoldenNeedle.Gameplay.Flow;
using GoldenNeedle.Gameplay.Player;
using GoldenNeedle.Gameplay.Presentation;
using TMPro;
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
    /// Narrow, idempotent authoring for the scene-local Calibration world-space presentation.
    /// Existing creative transforms, styles, timings, and camera settings are preserved.
    /// </summary>
    public static class GoldenNeedleCalibrationPresentationAuthoring
    {
        private const string CalibrationScenePath = "Assets/Scenes/Caliberation.unity";
        private const string PlayerPrefabPath = "Assets/GoldenNeedle/Gameplay/Player/GoldenNeedlePlayer.prefab";
        private const string DefaultFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private static readonly Vector3 DefaultCharacterStagePosition = new Vector3(2f, 0f, 0f);
        private static readonly Vector3 DefaultLeftPresentationPosition = new Vector3(-2.25f, 2.1f, 0f);

        [MenuItem("Golden Needle/Gameplay/Author Calibration Presentation")]
        public static void AuthorCalibrationPresentation()
        {
            try
            {
                EnsureNoDirtyActiveScene();
                EnsureTmpResourcesExist();
                var scene = OpenCalibrationScene();
                AuthorScene(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("Golden Needle: Calibration world-space presentation authored successfully.");
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"Golden Needle: Calibration presentation authoring failed: {exception.Message}");
                throw;
            }
        }

        [CliCommand("goldenneedle_author_calibration_presentation", "Author the scene-local Calibration world-space presentation without changing Hub or Build Settings.", Tags = new[] { "scenes" })]
        public static string AuthorCalibrationPresentationCommand()
        {
            AuthorCalibrationPresentation();
            return "Golden Needle: Calibration world-space presentation authored.";
        }

        private static Scene OpenCalibrationScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.path == CalibrationScenePath)
            {
                return activeScene;
            }

            return EditorSceneManager.OpenScene(CalibrationScenePath, OpenSceneMode.Single);
        }

        private static void AuthorScene(Scene scene)
        {
            var calibrationRoot = FindSingleRoot(scene, "GoldenNeedle_Calibration");
            var mainCameraObject = FindSingleRoot(scene, "Main Camera");
            var player = FindSinglePlayer(scene);
            var controller = EnsureComponent<CalibrationSceneController>(calibrationRoot, out _);
            var facade = player.GetComponent<GoldenNeedlePlayerFacade>();
            if (facade == null)
            {
                throw new InvalidOperationException("The Calibration player is missing GoldenNeedlePlayerFacade.");
            }

            RemoveKnownTemporaryCanvas(calibrationRoot.transform);
            EnsureEventSystem(scene);

            var anchors = GetOrCreateChild(calibrationRoot.transform, "PresentationAnchors", out var anchorsCreated);
            if (anchorsCreated)
            {
                anchors.transform.localPosition = Vector3.zero;
                anchors.transform.localRotation = Quaternion.identity;
                anchors.transform.localScale = Vector3.one;
            }

            var characterStage = GetOrCreateChild(anchors.transform, "CharacterStageAnchor", out var characterStageCreated);
            if (characterStageCreated)
            {
                characterStage.transform.position = DefaultCharacterStagePosition;
                characterStage.transform.rotation = Quaternion.identity;
            }

            var cameraPose = GetOrCreateChild(anchors.transform, "CameraPoseAnchor", out var cameraPoseCreated);
            if (cameraPoseCreated)
            {
                cameraPose.transform.SetPositionAndRotation(
                    mainCameraObject.transform.position,
                    mainCameraObject.transform.rotation);
            }

            var leftPresentation = GetOrCreateChild(anchors.transform, "LeftPresentationAnchor", out var leftPresentationCreated);
            if (leftPresentationCreated)
            {
                leftPresentation.transform.position = DefaultLeftPresentationPosition;
                leftPresentation.transform.rotation = Quaternion.identity;
                leftPresentation.transform.localScale = Vector3.one;
            }

            var mainCamera = EnsureComponent<Camera>(mainCameraObject, out _);
            var cameraRig = EnsureComponent<PresentationCameraRig>(mainCameraObject, out var cameraRigCreated);
            SetObjectReferenceIfMissing(cameraRig, "targetCamera", mainCamera);
            SetObjectReferenceIfMissing(cameraRig, "poseAnchor", cameraPose.transform);
            if (cameraRigCreated)
            {
                SetFloat(cameraRig, "fieldOfView", mainCamera.fieldOfView);
                SetBool(cameraRig, "snapOnStart", true);
            }

            var intro = CreatePresentationGroup(
                leftPresentation.transform,
                "IntroPromptGroup",
                mainCamera,
                initiallyVisible: true,
                blocksRaycasts: true,
                out var introCreated);
            var promptText = CreateText(
                intro.transform,
                "PromptText",
                "Say 'Begin Calibration' to start.",
                42f,
                new Color(0.95f, 0.98f, 1f, 1f),
                out var promptCreated);
            if (promptCreated)
            {
                SetAnchors(promptText.rectTransform, new Vector2(0.06f, 0.38f), Vector2.one - new Vector2(0.06f, 0.06f), Vector2.zero, Vector2.zero);
                promptText.alignment = TextAlignmentOptions.Center;
            }

            var beginButton = CreateButton(
                intro.transform,
                "BeginCalibrationButton",
                "Begin Calibration",
                out _);
            AddPersistentListenerIfMissing(beginButton, controller, controller.OnBeginCalibrationPressed);

            var webcam = CreatePresentationGroup(
                leftPresentation.transform,
                "WebcamGroup",
                mainCamera,
                initiallyVisible: false,
                blocksRaycasts: false,
                out _);
            var webcamPanel = GetOrCreateChild(webcam.transform, "WebcamPanel", out var webcamPanelCreated);
            var webcamPanelRect = EnsureComponent<RectTransform>(webcamPanel, out _);
            if (webcamPanelCreated)
            {
                webcamPanelRect.sizeDelta = new Vector2(600f, 360f);
                webcamPanelRect.anchoredPosition = Vector2.zero;
            }

            var webcamPanelImage = EnsureComponent<Image>(webcamPanel, out var webcamPanelImageCreated);
            if (webcamPanelImageCreated)
            {
                webcamPanelImage.color = new Color(0.035f, 0.05f, 0.075f, 1f);
                webcamPanelImage.raycastTarget = false;
            }

            var cameraPreview = GetOrCreateChild(webcamPanel.transform, "CameraPreview", out var cameraPreviewCreated);
            var cameraPreviewRect = EnsureComponent<RectTransform>(cameraPreview, out _);
            if (cameraPreviewCreated)
            {
                SetFullStretch(cameraPreviewRect, new Vector2(18f, 18f));
            }

            var rawImage = EnsureComponent<RawImage>(cameraPreview, out var rawImageCreated);
            if (rawImageCreated)
            {
                rawImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);
                rawImage.raycastTarget = false;
            }

            var aspectFitter = EnsureComponent<AspectRatioFitter>(cameraPreview, out var aspectFitterCreated);
            if (aspectFitterCreated)
            {
                aspectFitter.enabled = false;
            }

            var cameraPreviewComponent = EnsureComponent<CalibrationCameraPreview>(cameraPreview, out _);
            SetObjectReferenceIfMissing(cameraPreviewComponent, "rawImage", rawImage);
            SetObjectReferenceIfMissing(cameraPreviewComponent, "aspectRatioFitter", aspectFitter);

            var success = CreatePresentationGroup(
                leftPresentation.transform,
                "SuccessGroup",
                mainCamera,
                initiallyVisible: false,
                blocksRaycasts: false,
                out _);
            var successText = CreateText(
                success.transform,
                "SuccessText",
                "Calibration Complete",
                52f,
                new Color(0.55f, 1f, 0.75f, 1f),
                out var successTextCreated);
            if (successTextCreated)
            {
                SetFullStretch(successText.rectTransform, new Vector2(20f, 20f));
                successText.alignment = TextAlignmentOptions.Center;
            }

            var failure = CreatePresentationGroup(
                leftPresentation.transform,
                "FailureGroup",
                mainCamera,
                initiallyVisible: false,
                blocksRaycasts: true,
                out _);
            var failureText = CreateText(
                failure.transform,
                "FailureText",
                "Unable to enter Hub. Try again.",
                34f,
                new Color(1f, 0.65f, 0.55f, 1f),
                out var failureTextCreated);
            if (failureTextCreated)
            {
                SetAnchors(failureText.rectTransform, new Vector2(0.06f, 0.38f), Vector2.one - new Vector2(0.06f, 0.06f), Vector2.zero, Vector2.zero);
                failureText.alignment = TextAlignmentOptions.Center;
            }

            var retryButton = CreateButton(
                failure.transform,
                "RetryTransitionButton",
                "Retry Hub Transition",
                out _);
            AddPersistentListenerIfMissing(retryButton, controller, controller.RetryHubTransition);

            var presentation = EnsureComponent<CalibrationPresentationController>(calibrationRoot, out _);
            SetObjectReferenceIfMissing(presentation, "flowController", controller);
            SetObjectReferenceIfMissing(presentation, "playerFacade", facade);
            SetObjectReferenceIfMissing(presentation, "characterStageAnchor", characterStage.transform);
            SetObjectReferenceIfMissing(presentation, "cameraRig", cameraRig);
            SetObjectReferenceIfMissing(presentation, "introPromptGroup", intro.GetComponent<WorldSpacePresentationGroup>());
            SetObjectReferenceIfMissing(presentation, "webcamGroup", webcam.GetComponent<WorldSpacePresentationGroup>());
            SetObjectReferenceIfMissing(presentation, "successGroup", success.GetComponent<WorldSpacePresentationGroup>());
            SetObjectReferenceIfMissing(presentation, "failureGroup", failure.GetComponent<WorldSpacePresentationGroup>());
            SetObjectReferenceIfMissing(presentation, "beginCalibrationButton", beginButton);
            SetObjectReferenceIfMissing(presentation, "retryTransitionButton", retryButton);

            var introGroup = intro.GetComponent<WorldSpacePresentationGroup>();
            var webcamGroup = webcam.GetComponent<WorldSpacePresentationGroup>();
            var successGroup = success.GetComponent<WorldSpacePresentationGroup>();
            var failureGroup = failure.GetComponent<WorldSpacePresentationGroup>();
            ConfigureGroupDefaults(introGroup, introCreated, true, true);
            ConfigureGroupDefaults(webcamGroup, false, false, false);
            ConfigureGroupDefaults(successGroup, false, false, false);
            ConfigureGroupDefaults(failureGroup, false, false, true);

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject CreatePresentationGroup(
            Transform parent,
            string name,
            Camera mainCamera,
            bool initiallyVisible,
            bool blocksRaycasts,
            out bool created)
        {
            var group = GetOrCreateChild(parent, name, out created);
            var canvas = EnsureComponent<Canvas>(group, out var canvasCreated);
            var rect = EnsureComponent<RectTransform>(group, out _);
            if (canvasCreated)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = mainCamera;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 20;
                rect.sizeDelta = new Vector2(640f, 420f);
                rect.localScale = Vector3.one * 0.01f;
                rect.localRotation = Quaternion.identity;
                rect.localPosition = Vector3.zero;
            }
            else
            {
                canvas.renderMode = RenderMode.WorldSpace;
                if (canvas.worldCamera == null)
                {
                    canvas.worldCamera = mainCamera;
                }
            }

            var canvasGroup = EnsureComponent<CanvasGroup>(group, out _);
            var presentationGroup = EnsureComponent<WorldSpacePresentationGroup>(group, out var presentationGroupCreated);
            SetObjectReferenceIfMissing(presentationGroup, "canvasGroup", canvasGroup);
            if (presentationGroupCreated)
            {
                SetBool(presentationGroup, "initiallyVisible", initiallyVisible);
                SetBool(presentationGroup, "blocksRaycastsWhenVisible", blocksRaycasts);
                SetFloat(presentationGroup, "showDuration", name == "WebcamGroup" ? 0.25f : 0.2f);
                SetFloat(presentationGroup, "hideDuration", 0.2f);
            }

            EnsureComponent<GraphicRaycaster>(group, out _);
            return group;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string defaultText,
            float defaultFontSize,
            Color defaultColor,
            out bool created)
        {
            var textObject = GetOrCreateChild(parent, name, out created);
            var text = EnsureComponent<TextMeshProUGUI>(textObject, out var textCreated);
            var font = LoadDefaultFont();
            if (textCreated)
            {
                text.fontSize = defaultFontSize;
                text.color = defaultColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.Normal;
            }

            if (text.font == null && font != null)
            {
                text.font = font;
            }

            if (string.IsNullOrEmpty(text.text))
            {
                text.text = defaultText;
            }

            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            out bool created)
        {
            var buttonObject = GetOrCreateChild(parent, name, out created);
            var rect = EnsureComponent<RectTransform>(buttonObject, out var rectCreated);
            if (rectCreated)
            {
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(360f, 80f);
                rect.anchoredPosition = new Vector2(0f, 28f);
            }

            var image = EnsureComponent<Image>(buttonObject, out var imageCreated);
            if (imageCreated)
            {
                image.color = new Color(0.12f, 0.42f, 0.7f, 1f);
                image.raycastTarget = true;
            }

            var button = EnsureComponent<Button>(buttonObject, out _);
            if (button.targetGraphic == null)
            {
                button.targetGraphic = image;
            }

            var labelObject = GetOrCreateChild(buttonObject.transform, "Label", out var labelCreated);
            var labelText = EnsureComponent<TextMeshProUGUI>(labelObject, out var labelTextCreated);
            if (labelCreated || labelTextCreated)
            {
                SetFullStretch(labelText.rectTransform, new Vector2(12f, 6f));
                labelText.fontSize = 28f;
                labelText.color = Color.white;
                labelText.alignment = TextAlignmentOptions.Center;
                labelText.raycastTarget = false;
            }

            var font = LoadDefaultFont();
            if (labelText.font == null && font != null)
            {
                labelText.font = font;
            }

            if (string.IsNullOrEmpty(labelText.text))
            {
                labelText.text = label;
            }

            return button;
        }

        private static void ConfigureGroupDefaults(
            WorldSpacePresentationGroup group,
            bool created,
            bool initiallyVisible,
            bool blocksRaycasts)
        {
            if (group == null || !created)
            {
                return;
            }

            SetBool(group, "initiallyVisible", initiallyVisible);
            SetBool(group, "blocksRaycastsWhenVisible", blocksRaycasts);
        }

        private static void AddPersistentListenerIfMissing(
            Button button,
            UnityEngine.Object target,
            UnityEngine.Events.UnityAction listener)
        {
            for (var index = 0; index < button.onClick.GetPersistentEventCount(); index++)
            {
                if (button.onClick.GetPersistentTarget(index) == target &&
                    button.onClick.GetPersistentMethodName(index) == listener.Method.Name)
                {
                    return;
                }
            }

            UnityEventTools.AddPersistentListener(button.onClick, listener);
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
                    EditorSceneManager.MarkSceneDirty(scene);
                }

                return;
            }

            throw new InvalidOperationException("Caliberation is missing its existing EventSystem.");
        }

        private static GameObject FindSinglePlayer(Scene scene)
        {
            var sessions = FindComponentsInScene<GoldenNeedlePlayerSession>(scene);
            if (sessions.Count != 1)
            {
                throw new InvalidOperationException($"Caliberation contains {sessions.Count} player sessions; expected exactly one.");
            }

            var root = PrefabUtility.GetNearestPrefabInstanceRoot(sessions[0].gameObject);
            var source = root == null ? null : PrefabUtility.GetCorrespondingObjectFromSource(root);
            var acceptedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (source != acceptedPrefab)
            {
                throw new InvalidOperationException("The existing Calibration player is not the accepted GoldenNeedlePlayer prefab instance.");
            }

            return root;
        }

        private static void RemoveKnownTemporaryCanvas(Transform calibrationRoot)
        {
            var matches = FindChildrenByName(calibrationRoot, "CalibrationCanvas");
            if (matches.Count > 1)
            {
                throw new InvalidOperationException("GoldenNeedle_Calibration contains multiple temporary CalibrationCanvas objects.");
            }

            if (matches.Count == 1)
            {
                Undo.DestroyObjectImmediate(matches[0].gameObject);
            }
        }

        private static GameObject FindSingleRoot(Scene scene, string name)
        {
            var roots = new List<GameObject>();
            var sceneRoots = scene.GetRootGameObjects();
            for (var index = 0; index < sceneRoots.Length; index++)
            {
                if (sceneRoots[index].name == name)
                {
                    roots.Add(sceneRoots[index]);
                }
            }

            if (roots.Count != 1)
            {
                throw new InvalidOperationException($"Scene '{scene.name}' must contain exactly one root named '{name}'.");
            }

            return roots[0];
        }

        private static GameObject GetOrCreateChild(Transform parent, string name, out bool created)
        {
            var matches = FindChildrenByName(parent, name);
            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"'{GetHierarchyPath(parent)}' contains multiple children named '{name}'.");
            }

            if (matches.Count == 1)
            {
                created = false;
                return matches[0].gameObject;
            }

            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, $"Create {name}");
            gameObject.transform.SetParent(parent, false);
            created = true;
            return gameObject;
        }

        private static T EnsureComponent<T>(GameObject gameObject, out bool created) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                created = false;
                return component;
            }

            created = true;
            return Undo.AddComponent<T>(gameObject);
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

        private static void SetObjectReferenceIfMissing(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null || value == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Could not find serialized field '{propertyName}' on '{target.GetType().Name}'.");
            }

            if (property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Could not find serialized field '{propertyName}'.");
            }

            property.floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string propertyName, bool value)
        {
            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Could not find serialized field '{propertyName}'.");
            }

            property.boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_FontAsset LoadDefaultFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFontPath);
            if (font != null)
            {
                return font;
            }

            var guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
            return guids.Length == 0
                ? null
                : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void SetFullStretch(RectTransform rect, Vector2 padding = default)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one, padding, -padding);
        }

        private static void SetAnchors(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void EnsureTmpResourcesExist()
        {
            if (LoadDefaultFont() == null)
            {
                throw new InvalidOperationException("TextMeshPro is available but no TMP_FontAsset is imported.");
            }
        }

        private static void EnsureNoDirtyActiveScene()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                throw new InvalidOperationException($"Active scene '{activeScene.path}' has unsaved changes. Save or discard them before running Calibration presentation authoring.");
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
