using System;
using System.Collections.Generic;
using GoldenNeedle.Gameplay.Flow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoldenNeedle.Tests.Editor
{
    public sealed class GameplayFlowFoundationTests
    {
        private readonly List<Scene> _createdScenes = new List<Scene>();

        [TearDown]
        public void TearDown()
        {
            for (var index = _createdScenes.Count - 1; index >= 0; index--)
            {
                var scene = _createdScenes[index];
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            _createdScenes.Clear();
        }

        [Test]
        public void SpawnResolution_IsScopedToDestinationScene()
        {
            var destination = CreateScene();
            var unrelated = CreateScene();
            var expected = CreateSpawn(destination, "Entry", new Vector3(4f, 2f, -3f));
            CreateSpawn(unrelated, "Entry", Vector3.zero);

            var resolved = PlayerSpawnPointResolver.TryResolve(
                destination,
                "Entry",
                out var spawn,
                out var error);

            Assert.That(resolved, Is.True, error);
            Assert.That(spawn, Is.SameAs(expected));
            Assert.That(spawn.PlacementPose.position, Is.EqualTo(new Vector3(4f, 2f, -3f)));
        }

        [Test]
        public void SpawnResolution_MissingRequestedIdFailsExplicitly()
        {
            var destination = CreateScene();
            CreateSpawn(destination, "Other", Vector3.zero);

            var resolved = PlayerSpawnPointResolver.TryResolve(
                destination,
                "Missing",
                out var spawn,
                out var error);

            Assert.That(resolved, Is.False);
            Assert.That(spawn, Is.Null);
            StringAssert.Contains("not found", error);
        }

        [Test]
        public void SpawnResolution_DuplicateRequestedIdFailsExplicitly()
        {
            var destination = CreateScene();
            CreateSpawn(destination, "Entry", Vector3.zero);
            CreateSpawn(destination, "Entry", Vector3.one);

            var resolved = PlayerSpawnPointResolver.TryResolve(
                destination,
                "Entry",
                out var spawn,
                out var error);

            Assert.That(resolved, Is.False);
            Assert.That(spawn, Is.Null);
            StringAssert.Contains("duplicated 2 times", error);
        }

        [Test]
        public void SpawnResolution_EmptyRequestedIdFailsExplicitly()
        {
            var destination = CreateScene();

            var resolved = PlayerSpawnPointResolver.TryResolve(
                destination,
                "   ",
                out var spawn,
                out var error);

            Assert.That(resolved, Is.False);
            Assert.That(spawn, Is.Null);
            StringAssert.Contains("non-empty spawn ID", error);
        }

        private Scene CreateScene()
        {
            var scene = SceneManager.CreateScene($"GameplayFlowTest_{Guid.NewGuid():N}");
            _createdScenes.Add(scene);
            return scene;
        }

        private static PlayerSpawnPoint CreateSpawn(Scene scene, string id, Vector3 position)
        {
            var gameObject = new GameObject($"Spawn_{id}");
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            gameObject.transform.position = position;
            var spawn = gameObject.AddComponent<PlayerSpawnPoint>();
            var serializedSpawn = new SerializedObject(spawn);
            serializedSpawn.FindProperty("spawnId").stringValue = id;
            serializedSpawn.ApplyModifiedPropertiesWithoutUndo();
            return spawn;
        }
    }
}
