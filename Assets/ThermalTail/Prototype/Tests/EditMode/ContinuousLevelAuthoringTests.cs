using System.Linq;
using NUnit.Framework;
using ThermalTail.Prototype.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.Prototype.Tests
{
    public class ContinuousLevelAuthoringTests
    {
        [SetUp]
        public void OpenLevel() => EditorSceneManager.OpenScene(ContinuousLevelBuilder.ScenePath);

        [TearDown]
        public void CloseLevel() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void EveryReusableActorIsAConnectedPrefabWithResolvedReferences()
        {
            var session = Object.FindFirstObjectByType<PrototypeSession>();
            Assert.That(session.Settings, Is.Not.Null);
            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(behaviour is NpcMotor || behaviour is SecurityCamera || behaviour is NoteActor ||
                      behaviour is LockActor || behaviour is SurfaceMotor || behaviour is SafeZone)) continue;
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(behaviour), Is.EqualTo(PrefabInstanceStatus.Connected), behaviour.name);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(behaviour), Is.Not.Null, behaviour.name);
                Assert.That(behaviour.transform.localScale, Is.EqualTo(Vector3.one), behaviour.name);
            }
            var notes = Object.FindObjectsByType<NoteActor>(FindObjectsSortMode.None);
            Assert.That(notes.Length, Is.EqualTo(5));
            Assert.That(notes.Select(n => n.Note.Id).Distinct().Count(), Is.EqualTo(5));
            foreach (var note in notes) Assert.That(note.Session, Is.EqualTo(session));
            foreach (var npc in Object.FindObjectsByType<NpcMotor>(FindObjectsSortMode.None))
            {
                Assert.That(npc.Session, Is.EqualTo(session));
                Assert.That(npc.Route.Points.All(p => p != null), Is.True);
                if (npc.GetComponent<WardenBrain>() == null)
                {
                    var suspicion = npc.GetComponent<CivilianSuspicion>();
                    Assert.That(suspicion, Is.Not.Null);
                    Assert.That(suspicion.Zone, Is.EqualTo(npc.GetComponentInChildren<CrowdZone>()));
                    Assert.That(suspicion.ObstructionMask.value, Is.EqualTo(1 << LayerMask.NameToLayer("TT World")));
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(suspicion), Is.Not.Null);
                }
            }
            foreach (var camera in Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(camera.gameObject);
                Assert.That(AssetDatabase.GetAssetPath(source), Does.Contain("Prefabs/ContinuousLevel/"));
                var sensor = new SerializedObject(camera.GetComponent<VisionSensor>());
                Assert.That(sensor.FindProperty("Range").prefabOverride, Is.False, "Tune range on the camera variant.");
                Assert.That(sensor.FindProperty("FieldOfView").prefabOverride, Is.False);
                Assert.That(new SerializedObject(camera).FindProperty("Period").prefabOverride, Is.False);
                foreach (string field in new[] { "BaseDetectionTime", "TemperatureSensitivity", "SuspicionRecoveryTime" })
                {
                    var value = new SerializedObject(camera).FindProperty(field);
                    Assert.That(value.prefabOverride, Is.False, field + " should inherit through the camera variant.");
                    Assert.That(value.floatValue, Is.EqualTo(new SerializedObject(source.GetComponent<SecurityCamera>())
                        .FindProperty(field).floatValue));
                }
            }
            Assert.That(session.Player.GetComponent<PrototypeControls>().CameraRig.Player, Is.EqualTo(session.Player));
            foreach (var checkpoint in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
            {
                var bounds = checkpoint.GetComponent<BoxCollider>().bounds;
                Assert.That(bounds.min.y, Is.LessThanOrEqualTo(0));
                Assert.That(bounds.max.y, Is.GreaterThanOrEqualTo(7), "Ceiling travel must activate checkpoints.");
            }
        }

        [Test]
        public void VentNetworkIsConnectedAndOffersCameraHeadroom()
        {
            var tiles = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Duct floor ")).ToArray();
            var reached = new System.Collections.Generic.HashSet<Transform> { tiles[0] };
            bool added;
            do
            {
                added = false;
                foreach (var tile in tiles)
                    if (!reached.Contains(tile) && reached.Any(other => Vector3.Distance(other.position, tile.position) < 8.1f))
                    { reached.Add(tile); added = true; }
            } while (added);
            Assert.That(reached.Count, Is.EqualTo(tiles.Length));
            foreach (var tile in tiles)
            {
                Assert.That(tile.localScale.x, Is.GreaterThanOrEqualTo(8));
                var ceiling = GameObject.Find(tile.name.Replace("floor", "ceiling"));
                Assert.That(ceiling.transform.position.y - ceiling.transform.localScale.y / 2, Is.EqualTo(7).Within(.01f));
            }
            Assert.That(Object.FindObjectsByType<SurfaceTransition>(FindObjectsSortMode.None), Is.Empty,
                "The level's adjacent surfaces should use automatic corner traversal.");
        }
    }
}
