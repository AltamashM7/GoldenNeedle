using System;
using System.Collections.Generic;
using GoldenNeedle.Core.Motion.Locomotion;
using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using GoldenNeedle.Core.Motion.Retargeting;
using GoldenNeedle.Core.Motion.Runtime;
using GoldenNeedle.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace GoldenNeedle.Editor.Gameplay
{
    public readonly struct GameplayPlayerRequiredComponentCounts
    {
        public GameplayPlayerRequiredComponentCounts(
            int providers,
            int canonicalSources,
            int runtimes,
            int bindings,
            int retargeters,
            int locomotionControllers,
            int facades,
            int healthComponents,
            int bodyAnchors,
            int sessions)
        {
            Providers = providers;
            CanonicalSources = canonicalSources;
            Runtimes = runtimes;
            Bindings = bindings;
            Retargeters = retargeters;
            LocomotionControllers = locomotionControllers;
            Facades = facades;
            HealthComponents = healthComponents;
            BodyAnchors = bodyAnchors;
            Sessions = sessions;
        }

        public int Providers { get; }
        public int CanonicalSources { get; }
        public int Runtimes { get; }
        public int Bindings { get; }
        public int Retargeters { get; }
        public int LocomotionControllers { get; }
        public int Facades { get; }
        public int HealthComponents { get; }
        public int BodyAnchors { get; }
        public int Sessions { get; }
    }

    public static class GameplayPlayerPrefabBuilder
    {
        public const string MenuPath = "Golden Needle/Gameplay/Build GoldenNeedlePlayer Prefab From Selected Accepted Root";
        public const string PrefabPath = "Assets/GoldenNeedle/Gameplay/Player/GoldenNeedlePlayer.prefab";

        private const string PoseTrackingSpikeDebugNamespacePrefix = "GoldenNeedle.Debug.PoseTrackingSpike.";

        private static readonly HashSet<string> KnownDebugComponentTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "GoldenNeedle.Debug.PoseTrackingSpike.PoseTrackingSpikePresenter",
            "GoldenNeedle.Debug.PoseTrackingSpike.ProceduralDebugHumanoidRig",
            "GoldenNeedle.Debug.PoseTrackingSpike.ProceduralDebugRigView",
            "GoldenNeedle.Debug.PoseTrackingSpike.LocomotionPrototypeView",
            "GoldenNeedle.Debug.PoseTrackingSpike.ThirdPersonLabCamera",
            "GoldenNeedle.Debug.PoseTrackingSpike.PoseAcquisitionLabDiagnostics",
            "GoldenNeedle.Debug.PoseTrackingSpike.WindowsKeywordSpeechProvider",
        };

        [MenuItem(MenuPath)]
        private static void BuildGoldenNeedlePlayerPrefab()
        {
            var source = Selection.activeGameObject;
            if (!ValidateAcceptedSource(source, out var sourceError))
            {
                ReportFailure("GoldenNeedlePlayer prefab build refused", sourceError);
                return;
            }

            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existingPrefab != null && !EditorUtility.DisplayDialog(
                    "Overwrite GoldenNeedlePlayer prefab?",
                    $"A prefab already exists at:\n\n{PrefabPath}\n\nThe selected accepted root will be duplicated and serialized over it. The source scene will not be saved or modified.",
                    "Overwrite",
                    "Cancel"))
            {
                return;
            }

            var sourceScene = source.scene;
            var sourceSceneWasDirty = sourceScene.IsValid() && sourceScene.isDirty;
            var previousActiveScene = SceneManager.GetActiveScene();
            var previewScene = EditorSceneManager.NewPreviewScene();
            GameObject duplicate = null;

            try
            {
                if (!SceneManager.SetActiveScene(previewScene))
                {
                    throw new InvalidOperationException("Could not activate the temporary preview scene used for safe prefab assembly.");
                }

                duplicate = UnityEngine.Object.Instantiate(source);
                duplicate.name = "GoldenNeedlePlayer";
                duplicate.hideFlags = HideFlags.HideAndDontSave;

                StripKnownDebugComponents(duplicate);

                if (!EnsureRequiredProductionAndGameplayComponents(duplicate, out var ensureError))
                {
                    throw new InvalidOperationException(ensureError);
                }

                if (!ValidateConstructedPlayer(duplicate, out var constructedError))
                {
                    throw new InvalidOperationException(constructedError);
                }

                duplicate.hideFlags = HideFlags.None;
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(duplicate, PrefabPath, out var saveSucceeded);
                if (!saveSucceeded || savedPrefab == null)
                {
                    throw new InvalidOperationException("Unity PrefabUtility did not successfully save the production player prefab.");
                }

                AssetDatabase.SaveAssets();
                Selection.activeObject = savedPrefab;
                EditorGUIUtility.PingObject(savedPrefab);
                UnityEngine.Debug.Log($"Golden Needle: built production player prefab at '{PrefabPath}' from accepted source root '{source.name}'.");
            }
            catch (Exception exception)
            {
                ReportFailure("GoldenNeedlePlayer prefab build failed", exception.Message);
            }
            finally
            {
                if (previousActiveScene.IsValid())
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }

                if (duplicate != null)
                {
                    UnityEngine.Object.DestroyImmediate(duplicate);
                }

                if (previewScene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(previewScene);
                }

                if (sourceScene.IsValid() && !sourceSceneWasDirty && sourceScene.isDirty)
                {
                    UnityEngine.Debug.LogError("Golden Needle: the source scene became dirty during prefab assembly. The utility did not save it; review the scene before saving anything.");
                }
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateBuildMenu()
        {
            return Selection.activeGameObject != null && !Application.isPlaying;
        }

        public static bool ValidateAcceptedSource(GameObject source, out string error)
        {
            var errors = new List<string>();
            if (source == null)
            {
                error = "Select the accepted PoseTrackingSpike root GameObject in the open scene first.";
                return false;
            }

            if (EditorUtility.IsPersistent(source) || !source.scene.IsValid())
            {
                error = "The selected object must be a scene instance of the accepted PoseTrackingSpike root, not a prefab asset or project asset.";
                return false;
            }

            ValidateExactlyOneRootAndHierarchy<MediaPipePoseProvider>(source, "MediaPipePoseProvider", errors);
            ValidateExactlyOneRootAndHierarchy<HumanoidRigBinding>(source, "HumanoidRigBinding", errors);
            ValidateExactlyOneRootAndHierarchy<HumanoidRetargeter>(source, "HumanoidRetargeter", errors);
            ValidateExactlyOneRootAndHierarchy<EmbodiedLocomotionController>(source, "EmbodiedLocomotionController", errors);
            ValidateAtMostOneRootAndHierarchy<MediaPipeCanonicalPoseSource>(source, "MediaPipeCanonicalPoseSource", errors);
            ValidateAtMostOneRootAndHierarchy<MotionEngineRuntime>(source, "MotionEngineRuntime", errors);
            ValidateAtMostOneRootAndHierarchy<GoldenNeedlePlayerFacade>(source, "GoldenNeedlePlayerFacade", errors);
            ValidateAtMostOneRootAndHierarchy<PlayerHealth>(source, "PlayerHealth", errors);
            ValidateAtMostOneRootAndHierarchy<GoldenNeedleBodyAnchors>(source, "GoldenNeedleBodyAnchors", errors);
            ValidateAtMostOneRootAndHierarchy<GoldenNeedlePlayerSession>(source, "GoldenNeedlePlayerSession", errors);

            if (CountMissingScripts(source) > 0)
            {
                errors.Add("The selected hierarchy contains one or more missing MonoBehaviour scripts.");
            }

            var binding = source.GetComponent<HumanoidRigBinding>();
            if (binding != null)
            {
                ValidateAcceptedAnimatorHierarchy(source, binding, errors);

                var retargeter = source.GetComponent<HumanoidRetargeter>();
                var locomotion = source.GetComponent<EmbodiedLocomotionController>();
                ValidateSerializedReference(retargeter, "binding", binding, "HumanoidRetargeter.binding", errors);
                ValidateSerializedReference(locomotion, "binding", binding, "EmbodiedLocomotionController.binding", errors);
            }

            error = JoinErrors(errors);
            return errors.Count == 0;
        }

        public static bool ValidateConstructedPlayer(GameObject root, out string error)
        {
            var errors = new List<string>();
            if (root == null)
            {
                error = "Constructed player root is null.";
                return false;
            }

            var counts = GetRequiredComponentCounts(root);
            if (!ValidateExactRequiredComponentCounts(counts, out var countError))
            {
                errors.Add(countError);
            }

            ValidateRequiredComponentsAreOnRoot<MediaPipePoseProvider>(root, "MediaPipePoseProvider", errors);
            ValidateRequiredComponentsAreOnRoot<MediaPipeCanonicalPoseSource>(root, "MediaPipeCanonicalPoseSource", errors);
            ValidateRequiredComponentsAreOnRoot<MotionEngineRuntime>(root, "MotionEngineRuntime", errors);
            ValidateRequiredComponentsAreOnRoot<HumanoidRigBinding>(root, "HumanoidRigBinding", errors);
            ValidateRequiredComponentsAreOnRoot<HumanoidRetargeter>(root, "HumanoidRetargeter", errors);
            ValidateRequiredComponentsAreOnRoot<EmbodiedLocomotionController>(root, "EmbodiedLocomotionController", errors);
            ValidateRequiredComponentsAreOnRoot<GoldenNeedlePlayerFacade>(root, "GoldenNeedlePlayerFacade", errors);
            ValidateRequiredComponentsAreOnRoot<PlayerHealth>(root, "PlayerHealth", errors);
            ValidateRequiredComponentsAreOnRoot<GoldenNeedleBodyAnchors>(root, "GoldenNeedleBodyAnchors", errors);
            ValidateRequiredComponentsAreOnRoot<GoldenNeedlePlayerSession>(root, "GoldenNeedlePlayerSession", errors);

            if (CountMissingScripts(root) > 0)
            {
                errors.Add("The constructed player contains one or more missing MonoBehaviour scripts.");
            }

            var binding = root.GetComponent<HumanoidRigBinding>();
            if (binding != null)
            {
                ValidateAcceptedAnimatorHierarchy(root, binding, errors);
            }

            var remainingBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < remainingBehaviours.Length; i++)
            {
                var behaviour = remainingBehaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                var fullName = behaviour.GetType().FullName;
                if (IsPoseTrackingSpikeDebugTypeName(fullName))
                {
                    errors.Add($"Forbidden PoseTrackingSpike debug component remains: {fullName} on '{behaviour.gameObject.name}'.");
                }
            }

            error = JoinErrors(errors);
            return errors.Count == 0;
        }

        public static bool ValidateExactRequiredComponentCounts(
            GameplayPlayerRequiredComponentCounts counts,
            out string error)
        {
            var errors = new List<string>();
            RequireExactlyOne(counts.Providers, "MediaPipePoseProvider", errors);
            RequireExactlyOne(counts.CanonicalSources, "MediaPipeCanonicalPoseSource", errors);
            RequireExactlyOne(counts.Runtimes, "MotionEngineRuntime", errors);
            RequireExactlyOne(counts.Bindings, "HumanoidRigBinding", errors);
            RequireExactlyOne(counts.Retargeters, "HumanoidRetargeter", errors);
            RequireExactlyOne(counts.LocomotionControllers, "EmbodiedLocomotionController", errors);
            RequireExactlyOne(counts.Facades, "GoldenNeedlePlayerFacade", errors);
            RequireExactlyOne(counts.HealthComponents, "PlayerHealth", errors);
            RequireExactlyOne(counts.BodyAnchors, "GoldenNeedleBodyAnchors", errors);
            RequireExactlyOne(counts.Sessions, "GoldenNeedlePlayerSession", errors);
            error = JoinErrors(errors);
            return errors.Count == 0;
        }

        public static bool IsKnownDebugComponentTypeName(string fullName)
        {
            return !string.IsNullOrEmpty(fullName) && KnownDebugComponentTypes.Contains(fullName);
        }

        public static bool IsPoseTrackingSpikeDebugTypeName(string fullName)
        {
            return !string.IsNullOrEmpty(fullName) &&
                fullName.StartsWith(PoseTrackingSpikeDebugNamespacePrefix, StringComparison.Ordinal);
        }

        private static bool EnsureRequiredProductionAndGameplayComponents(GameObject root, out string error)
        {
            var errors = new List<string>();

            EnsureExactlyOneRootComponent<MediaPipePoseProvider>(root, false, "MediaPipePoseProvider", errors);
            EnsureExactlyOneRootComponent<HumanoidRigBinding>(root, false, "HumanoidRigBinding", errors);
            EnsureExactlyOneRootComponent<HumanoidRetargeter>(root, false, "HumanoidRetargeter", errors);
            EnsureExactlyOneRootComponent<EmbodiedLocomotionController>(root, false, "EmbodiedLocomotionController", errors);

            EnsureExactlyOneRootComponent<MediaPipeCanonicalPoseSource>(root, true, "MediaPipeCanonicalPoseSource", errors);
            EnsureExactlyOneRootComponent<MotionEngineRuntime>(root, true, "MotionEngineRuntime", errors);
            EnsureExactlyOneRootComponent<PlayerHealth>(root, true, "PlayerHealth", errors);
            EnsureExactlyOneRootComponent<GoldenNeedleBodyAnchors>(root, true, "GoldenNeedleBodyAnchors", errors);
            EnsureExactlyOneRootComponent<GoldenNeedlePlayerFacade>(root, true, "GoldenNeedlePlayerFacade", errors);
            EnsureExactlyOneRootComponent<GoldenNeedlePlayerSession>(root, true, "GoldenNeedlePlayerSession", errors);

            error = JoinErrors(errors);
            return errors.Count == 0;
        }

        private static void StripKnownDebugComponents(GameObject root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                {
                    continue;
                }

                var fullName = behaviour.GetType().FullName;
                if (IsKnownDebugComponentTypeName(fullName))
                {
                    UnityEngine.Object.DestroyImmediate(behaviour);
                }
            }
        }

        private static GameplayPlayerRequiredComponentCounts GetRequiredComponentCounts(GameObject root)
        {
            return new GameplayPlayerRequiredComponentCounts(
                root.GetComponentsInChildren<MediaPipePoseProvider>(true).Length,
                root.GetComponentsInChildren<MediaPipeCanonicalPoseSource>(true).Length,
                root.GetComponentsInChildren<MotionEngineRuntime>(true).Length,
                root.GetComponentsInChildren<HumanoidRigBinding>(true).Length,
                root.GetComponentsInChildren<HumanoidRetargeter>(true).Length,
                root.GetComponentsInChildren<EmbodiedLocomotionController>(true).Length,
                root.GetComponentsInChildren<GoldenNeedlePlayerFacade>(true).Length,
                root.GetComponentsInChildren<PlayerHealth>(true).Length,
                root.GetComponentsInChildren<GoldenNeedleBodyAnchors>(true).Length,
                root.GetComponentsInChildren<GoldenNeedlePlayerSession>(true).Length);
        }

        private static void ValidateExactlyOneRootAndHierarchy<T>(GameObject root, string label, List<string> errors)
            where T : Component
        {
            var rootCount = root.GetComponents<T>().Length;
            var hierarchyCount = root.GetComponentsInChildren<T>(true).Length;
            if (rootCount != 1 || hierarchyCount != 1)
            {
                errors.Add($"Expected exactly one {label} on the selected root and nowhere else in its hierarchy; found root={rootCount}, hierarchy={hierarchyCount}.");
            }
        }

        private static void ValidateAtMostOneRootAndHierarchy<T>(GameObject root, string label, List<string> errors)
            where T : Component
        {
            var rootCount = root.GetComponents<T>().Length;
            var hierarchyCount = root.GetComponentsInChildren<T>(true).Length;
            if (rootCount > 1 || hierarchyCount > 1 || hierarchyCount != rootCount)
            {
                errors.Add($"Expected zero or one {label}, and only on the selected root; found root={rootCount}, hierarchy={hierarchyCount}.");
            }
        }

        private static void ValidateRequiredComponentsAreOnRoot<T>(GameObject root, string label, List<string> errors)
            where T : Component
        {
            var rootCount = root.GetComponents<T>().Length;
            var hierarchyCount = root.GetComponentsInChildren<T>(true).Length;
            if (rootCount != 1 || hierarchyCount != 1)
            {
                errors.Add($"Constructed player requires exactly one {label} on the player root; found root={rootCount}, hierarchy={hierarchyCount}.");
            }
        }

        private static void EnsureExactlyOneRootComponent<T>(
            GameObject root,
            bool allowAdd,
            string label,
            List<string> errors)
            where T : Component
        {
            var rootComponents = root.GetComponents<T>();
            var hierarchyComponents = root.GetComponentsInChildren<T>(true);
            if (rootComponents.Length > 1 || hierarchyComponents.Length > 1 || hierarchyComponents.Length != rootComponents.Length)
            {
                errors.Add($"Cannot safely ensure {label}: found root={rootComponents.Length}, hierarchy={hierarchyComponents.Length}.");
                return;
            }

            if (rootComponents.Length == 1)
            {
                return;
            }

            if (!allowAdd)
            {
                errors.Add($"Accepted source copy is missing required existing production component {label}; the builder will not reconstruct it.");
                return;
            }

            root.AddComponent<T>();
        }

        private static void ValidateAcceptedAnimatorHierarchy(
            GameObject root,
            HumanoidRigBinding binding,
            List<string> errors)
        {
            var serializedBinding = new SerializedObject(binding);
            var animatorProperty = serializedBinding.FindProperty("animator");
            var preferAnimatorProperty = serializedBinding.FindProperty("preferAnimatorHumanoid");
            var avatarRootProperty = serializedBinding.FindProperty("avatarRoot");

            if (animatorProperty == null || preferAnimatorProperty == null || avatarRootProperty == null)
            {
                errors.Add("HumanoidRigBinding serialized Animator/avatar configuration could not be inspected.");
                return;
            }

            if (!preferAnimatorProperty.boolValue)
            {
                errors.Add("HumanoidRigBinding is not configured to prefer the accepted Animator Humanoid binding.");
            }

            var animator = animatorProperty.objectReferenceValue as Animator;
            if (animator == null)
            {
                var animators = root.GetComponentsInChildren<Animator>(true);
                if (animators.Length == 1)
                {
                    animator = animators[0];
                }
                else
                {
                    errors.Add($"Could not resolve one authoritative Animator from HumanoidRigBinding; found {animators.Length} Animator components in the selected hierarchy.");
                    return;
                }
            }

            if (!IsSameOrChild(root.transform, animator.transform))
            {
                errors.Add("HumanoidRigBinding.Animator points outside the selected player hierarchy.");
            }

            if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                errors.Add("The accepted Animator does not have a valid Humanoid Avatar.");
            }

            var avatarRoot = avatarRootProperty.objectReferenceValue as Transform;
            if (avatarRoot == null)
            {
                errors.Add("HumanoidRigBinding.avatarRoot is not explicitly assigned on the accepted source.");
            }
            else
            {
                if (!IsSameOrChild(root.transform, avatarRoot))
                {
                    errors.Add("HumanoidRigBinding.avatarRoot points outside the selected player hierarchy.");
                }

                if (!IsSameOrChild(avatarRoot, animator.transform))
                {
                    errors.Add("The accepted Animator is not inside the configured HumanoidRigBinding.avatarRoot hierarchy.");
                }
            }
        }

        private static void ValidateSerializedReference(
            Component owner,
            string propertyName,
            UnityEngine.Object expected,
            string label,
            List<string> errors)
        {
            if (owner == null)
            {
                return;
            }

            var serializedObject = new SerializedObject(owner);
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                errors.Add($"Could not inspect serialized reference {label}.");
                return;
            }

            if (property.objectReferenceValue != null && property.objectReferenceValue != expected)
            {
                errors.Add($"{label} does not reference the selected root's authoritative HumanoidRigBinding.");
            }
        }

        private static int CountMissingScripts(GameObject root)
        {
            var missing = 0;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[i].gameObject);
            }
            return missing;
        }

        private static bool IsSameOrChild(Transform root, Transform candidate)
        {
            return root != null && candidate != null && (candidate == root || candidate.IsChildOf(root));
        }

        private static void RequireExactlyOne(int count, string label, List<string> errors)
        {
            if (count != 1)
            {
                errors.Add($"Expected exactly one {label}; found {count}.");
            }
        }

        private static string JoinErrors(List<string> errors)
        {
            return errors == null || errors.Count == 0
                ? string.Empty
                : "• " + string.Join("\n• ", errors);
        }

        private static void ReportFailure(string title, string message)
        {
            UnityEngine.Debug.LogError($"Golden Needle: {title}.\n{message}");
            EditorUtility.DisplayDialog(title, message, "OK");
        }
    }
}
