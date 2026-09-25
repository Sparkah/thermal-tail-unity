#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThermalTail.Prototype.Tests
{
    public class ContinuousLevelTests
    {
        const string ScenePath = "Assets/ThermalTail/Prototype/Scenes/ThermalTailLevel.unity";
        PrototypeSession session;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = Object.FindFirstObjectByType<PrototypeSession>();
            session.Player.GetComponent<PrototypeControls>().enabled = false;
            foreach (var sensor in Object.FindObjectsByType<VisionSensor>(FindObjectsSortMode.None)) sensor.enabled = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            var current = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Level test cleanup"));
            yield return SceneManager.UnloadSceneAsync(current);
        }

        [UnityTest]
        public IEnumerator AllPatrolLegsAreReachableAndSensorsUseTheLevelSession()
        {
            Assert.That(Object.FindObjectsByType<WardenBrain>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            Assert.That(Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None).Length, Is.EqualTo(16));
            foreach (var npc in Object.FindObjectsByType<NpcMotor>(FindObjectsSortMode.None))
            {
                Assert.That(npc.Agent.isOnNavMesh, Is.True, npc.name);
                var points = npc.Route.Points;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    var path = new NavMeshPath();
                    Assert.That(NavMesh.CalculatePath(points[i].position, points[i + 1].position, NavMesh.AllAreas, path), Is.True, npc.name);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), npc.name);
                }
            }
            foreach (var sensor in Object.FindObjectsByType<VisionSensor>(FindObjectsSortMode.None))
            {
                Assert.That(sensor.Session, Is.EqualTo(session));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LobbyAndVentEntranceConnectWithoutTeleporting()
        {
            session.Player.Warp(new Vector3(-2, .34f, 0), Quaternion.LookRotation(Vector3.right));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(2.5f);
            Assert.That(session.Player.transform.position.x, Is.GreaterThan(3.5f));
            Assert.That(session.Section, Is.EqualTo("Service ventilation"));
            session.RestoreCheckpoint();
            Assert.That(Vector3.Distance(session.Player.transform.position, new Vector3(1, .34f, 0)), Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator VentBaffleCanBeClimbedOverWithAutomaticCorners()
        {
            session.Player.Warp(new Vector3(33, .34f, 16), Quaternion.LookRotation(Vector3.right));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(7);
            Assert.That(session.Player.transform.position.x, Is.GreaterThan(38), session.Player.transform.position.ToString());
            Assert.That(session.Player.SurfaceNormal.y, Is.GreaterThan(.99f));
            Assert.That(session.Player.transform.position.y, Is.EqualTo(.34f).Within(.05f));
        }

        [UnityTest]
        public IEnumerator VaultRimAndBottomConnectAutomatically()
        {
            session.Player.Warp(new Vector3(62, .34f, -24), Quaternion.LookRotation(Vector3.right));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(8);
            Assert.That(session.Player.transform.position.y, Is.LessThan(-4), "Rim: " + session.Player.transform.position);
            Assert.That(session.Player.SurfaceNormal.x, Is.GreaterThan(.99f));
            Assert.That(session.Section, Is.EqualTo("Vault descent"));
            session.Player.Warp(new Vector3(70.64f, -22, -24), Quaternion.LookRotation(Vector3.down, Vector3.right));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(4);
            Assert.That(session.Player.SurfaceNormal.y, Is.GreaterThan(.99f), "Bottom: " + session.Player.transform.position);
            Assert.That(session.Player.transform.position.y, Is.EqualTo(-23.66f).Within(.05f));
            Assert.That(session.Player.transform.position.x, Is.GreaterThan(73));
        }

        [UnityTest]
        public IEnumerator OctagonalWallCornerRemainsTraversable()
        {
            session.Player.Warp(new Vector3(70.64f, -4, -21), Quaternion.LookRotation(Vector3.forward, Vector3.right));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(5);
            Assert.That(session.Player.transform.position.z, Is.GreaterThan(-16), session.Player.transform.position.ToString());
            Assert.That(session.Player.SurfaceNormal.x, Is.GreaterThan(.5f));
            Assert.That(session.Player.SurfaceNormal.z, Is.LessThan(-.5f));
            Assert.That(session.Player.transform.position.y, Is.EqualTo(-4).Within(.05f));
        }

        [UnityTest]
        public IEnumerator WallSheltersHeatAndCoolAtTheAmbientBoundaries()
        {
            foreach (string name in new[] { "Prepare for warm exhaust · 30", "Prepare for cold storage · 18" })
            {
                var source = GameObject.Find(name).GetComponent<ThermalVolume>();
                var t = source.transform;
                session.Player.Warp(t.position + t.up * .34f, t.rotation);
                session.Thermal.SetTemperature(22);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(2);
                Assert.That(session.Thermal.IsSafe, Is.True, name);
                Assert.That(session.Thermal.Temperature, Is.EqualTo(source.TargetTemperature).Within(.1f), name);
                Assert.That(session.Thermal.Ambient, Is.EqualTo(source.TargetTemperature), name);
            }
        }

        [UnityTest]
        public IEnumerator VentCorridorAllowsTheNormalCameraDistance()
        {
            session.Player.Warp(new Vector3(48, .34f, 16), Quaternion.LookRotation(Vector3.right));
            var camera = Object.FindFirstObjectByType<SurfaceCamera>();
            yield return new WaitForSeconds(1.5f);
            Assert.That(Vector3.Distance(camera.transform.position, session.Player.transform.position), Is.EqualTo(camera.Distance).Within(.05f));
            Assert.That(Physics.Raycast(session.Player.transform.position, Vector3.up, out var ceiling, 8, camera.ObstructionMask), Is.True);
            Assert.That(ceiling.distance, Is.GreaterThan(6));
        }

        [UnityTest]
        public IEnumerator FiveNotesPersistAndTheBottomKeypadCompletes()
        {
            var notes = Object.FindObjectsByType<NoteActor>(FindObjectsSortMode.None);
            Assert.That(notes.Length, Is.EqualTo(5));
            foreach (var note in notes)
            {
                Assert.That(note.Note.Text, Is.Not.Empty);
                session.Player.Warp(note.transform.position, Quaternion.identity);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                session.CloseScreen();
            }
            Assert.That(session.Notes.Count, Is.EqualTo(5));
            session.RestoreCheckpoint();
            Assert.That(session.Notes.Count, Is.EqualTo(5));
            var keypad = Object.FindFirstObjectByType<LockActor>();
            session.Player.Warp(new Vector3(84, -23.66f, -34.3f), Quaternion.LookRotation(Vector3.back));
            Physics.SyncTransforms();
            Assert.That(keypad.CanInteract(session.Player), Is.True);
            session.OpenLock(keypad);
            Assert.That(keypad.Submit(keypad.Passcode), Is.True);
            Assert.That(session.IsComplete, Is.True);
        }
    }
}
#endif
