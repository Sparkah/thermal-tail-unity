using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ThermalTail.Prototype.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThermalTail.Prototype.Tests
{
    public class PrefabAuthoringTests
    {
        [SetUp]
        public void SetUp() { EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath); }

        [TearDown]
        public void TearDown() { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }

        static GameObject[] Actors() => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject)
            .Where(go => PrototypePrefabMigration.PrefabName(go) != null).ToArray();

        [Test]
        public void ExampleActorsAreConnectedWithoutDuplicatedComponents()
        {
            var actors = Actors();
            Assert.That(actors.Length, Is.EqualTo(15));
            foreach (var actor in actors)
            {
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(actor), Is.EqualTo(PrefabInstanceStatus.Connected), actor.name);
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor),
                    Is.EqualTo(PrototypePrefabMigration.PrefabPath(PrototypePrefabMigration.PrefabName(actor))), actor.name);
                Assert.That(PrefabUtility.GetAddedComponents(actor), Is.Empty, actor.name);
                Assert.That(PrefabUtility.GetAddedGameObjects(actor), Is.Empty, actor.name);
            }
            var session = Object.FindFirstObjectByType<PrototypeSession>();
            Assert.That(session.Player, Is.Not.Null);
            Assert.That(session.Thermal, Is.EqualTo(session.Player.GetComponent<PlayerThermal>()));
            Assert.That(Object.FindFirstObjectByType<SurfaceCamera>().Player, Is.EqualTo(session.Player));
            Assert.That(session.Player.GetComponent<PrototypeControls>().CameraRig, Is.Not.Null);
            foreach (var npc in Object.FindObjectsByType<NpcMotor>(FindObjectsSortMode.None))
            {
                Assert.That(npc.Session, Is.EqualTo(session));
                Assert.That(npc.Route, Is.Not.Null);
            }
            foreach (var sensor in Object.FindObjectsByType<VisionSensor>(FindObjectsSortMode.None))
            {
                Assert.That(sensor.Session, Is.EqualTo(session));
                Assert.That(sensor.Origin.IsChildOf(sensor.transform), Is.True);
                Assert.That(sensor.GetComponent<StealthVisuals>().Sensor, Is.EqualTo(sensor));
            }
            Assert.That(Object.FindObjectsByType<NoteActor>(FindObjectsSortMode.None).Select(n => n.Note.Id).Distinct().Count(), Is.EqualTo(2));
            Assert.That(Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None).Select(c => c.PhaseOffset).OrderBy(x => x),
                Is.EqualTo(new[] { 0f, 0.2f, 0.4f, 0.6f }));
        }

        [TestCase("Warden", "SuspiciousTurnSpeed")]
        [TestCase("Warden", "CloseRange")]
        [TestCase("Warden", "CloseFieldOfView")]
        [TestCase("SecurityCamera", "Range")]
        [TestCase("SecurityCamera", "BaseDetectionTime")]
        [TestCase("SecurityCamera", "TemperatureSensitivity")]
        [TestCase("SecurityCamera", "SuspicionRecoveryTime")]
        [TestCase("Player3D", "HorizontalSensitivity")]
        public void SharedTuningChangesPropagateFromPrefab(string prefabName, string property)
        {
            string path = PrototypePrefabMigration.PrefabPath(prefabName);
            byte[] original = File.ReadAllBytes(path);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var component = TuningComponent(prefab, prefabName, property);
                var serialized = new SerializedObject(component);
                float expected = serialized.FindProperty(property).floatValue + 0.123f;
                serialized.FindProperty(property).floatValue = expected;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(prefab);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
                foreach (var actor in Actors().Where(go => PrototypePrefabMigration.PrefabName(go) == prefabName))
                {
                    var value = new SerializedObject(TuningComponent(actor, prefabName, property)).FindProperty(property);
                    Assert.That(value.prefabOverride, Is.False, actor.name + " shadows shared tuning");
                    Assert.That(value.floatValue, Is.EqualTo(expected).Within(0.00001f), actor.name);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.WriteAllBytes(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        static Component TuningComponent(GameObject go, string name, string property) => name == "Warden" && property == "SuspiciousTurnSpeed"
            ? (Component)go.GetComponent<WardenBrain>() : name == "SecurityCamera" && property != "Range"
            ? go.GetComponent<SecurityCamera>() : name == "Warden" || name == "SecurityCamera"
            ? go.GetComponent<VisionSensor>() : go.GetComponent<PrototypeControls>();

        [Test]
        public void RebuildingExampleKeepsExistingPrefabAndMaterialTuning()
        {
            // The builder owns its scene and navigation; restore test output after verifying the rebuild.
            var files = new Dictionary<string, byte[]>();
            foreach (string directory in new[] { "Scenes", "Prefabs", "Data", "Materials" })
                foreach (string path in Directory.GetFiles(PrototypeSceneBuilder.Root + "/" + directory, "*", SearchOption.AllDirectories))
                    files[path] = File.ReadAllBytes(path);
            try
            {
                PrototypeSceneBuilder.Build();
                foreach (var file in files.Where(f => f.Key.Contains("/Prefabs/") || f.Key.Contains("/Materials/")))
                    Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key + " was overwritten");
                ExampleActorsAreConnectedWithoutDuplicatedComponents();
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (var file in files) File.WriteAllBytes(file.Key, file.Value);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void ConnectionMigrationIsIdempotent()
        {
            byte[] before = File.ReadAllBytes(PrototypeSceneBuilder.ScenePath);
            PrototypePrefabMigration.Connect();
            Assert.That(File.ReadAllBytes(PrototypeSceneBuilder.ScenePath), Is.EqualTo(before));
        }
    }
}
