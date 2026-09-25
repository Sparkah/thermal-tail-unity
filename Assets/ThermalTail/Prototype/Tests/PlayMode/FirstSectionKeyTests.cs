#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ThermalTail.Prototype.Tests
{
    public class FirstSectionKeyTests
    {
        PrototypeSession session;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/ThermalTail/Prototype/Scenes/ThermalTailLevel.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = Object.FindFirstObjectByType<PrototypeSession>();
            session.Player.GetComponent<PrototypeControls>().enabled = false;
            foreach (var sensor in Object.FindObjectsByType<VisionSensor>(FindObjectsSortMode.None)) sensor.enabled = false;
            foreach (var civilian in Object.FindObjectsByType<CivilianSuspicion>(FindObjectsSortMode.None)) civilian.enabled = false;
            foreach (var warden in Object.FindObjectsByType<WardenBrain>(FindObjectsSortMode.None)) warden.enabled = false;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Key test cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator EightKeysHaveUniqueIdsAndClearReachablePlacementsInSectionOne()
        {
            var keys = Object.FindObjectsByType<KeyCollectible>(FindObjectsSortMode.None);
            Assert.That(keys.Length, Is.EqualTo(8));
            Assert.That(keys.Select(k => k.CollectionId).Distinct().Count(), Is.EqualTo(8));
            int world = 1 << LayerMask.NameToLayer("TT World");
            foreach (var key in keys)
            {
                Assert.That(key.transform.root.name, Does.StartWith("01"));
                Assert.That(key.Session, Is.EqualTo(session));
                Assert.That(Physics.CheckSphere(key.transform.position, .3f, world, QueryTriggerInteraction.Ignore), Is.False, key.name);
                Assert.That(Physics.Raycast(key.transform.position, Vector3.down, out var hit, 1, world), Is.True, key.name);
                Assert.That(hit.collider.name, Is.EqualTo("Concourse deck"));
                Assert.That(UnityEngine.AI.NavMesh.SamplePosition(hit.point, out _, .5f, UnityEngine.AI.NavMesh.AllAreas), Is.True);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoorBlocksUntilAllEightKeysAndProgressSurvivesReset()
        {
            var door = Object.FindFirstObjectByType<KeyDoor>();
            var keys = Object.FindObjectsByType<KeyCollectible>(FindObjectsSortMode.None).OrderBy(k => k.name).ToArray();
            Assert.That(door.IsOpen, Is.False);
            Assert.That(door.RequiredKeys, Is.EqualTo(8));
            session.Player.Warp(door.transform.position - door.transform.forward * 1.5f + Vector3.up * .34f,
                Quaternion.LookRotation(door.transform.forward));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(1);
            Assert.That(Vector3.Dot(session.Player.transform.position - door.transform.position, door.transform.forward), Is.LessThan(-.3f));
            session.Player.MoveInput = Vector2.zero;
            foreach (var npc in Object.FindObjectsByType<NpcMotor>(FindObjectsSortMode.None))
            { if (npc.Agent.isOnNavMesh) npc.Agent.isStopped = true; npc.enabled = false; }
            for (int i = 0; i < keys.Length; i++)
            {
                session.Player.Warp(keys[i].transform.position, Quaternion.identity); Physics.SyncTransforms();
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(session.KeysCollected, Is.EqualTo(i + 1));
                Assert.That(keys[i].Collect(), Is.False, "Each key counts once.");
                Assert.That(door.IsOpen, Is.EqualTo(i == 7));
            }
            Assert.That(door.Barrier.activeSelf, Is.False);
            Assert.That(door.StatusText.text, Is.EqualTo("ACCESS GRANTED"));
            session.RestoreCheckpoint();
            Assert.That(session.KeysCollected, Is.EqualTo(8));
            Assert.That(door.IsOpen, Is.True);
            Assert.That(keys.All(k => !k.gameObject.activeSelf), Is.True);
            session.Player.Warp(door.transform.position - door.transform.forward * 1.5f + Vector3.up * .34f,
                Quaternion.LookRotation(door.transform.forward));
            session.Player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(1.2f);
            Assert.That(Vector3.Dot(session.Player.transform.position - door.transform.position, door.transform.forward), Is.GreaterThan(.3f));
        }

        [UnityTest]
        public IEnumerator AddedWardenStartsOppositeAndMatchesCivilianPatrol()
        {
            var warden = GameObject.Find("Warden · opposite square civilian").GetComponent<NpcMotor>();
            var civilian = Object.FindObjectsByType<NpcMotor>(FindObjectsSortMode.None).First(n =>
                n.GetComponent<WardenBrain>() == null && n.Route != null && n.Route.Loop && n.Route.Points.Length == 4);
            Assert.That(warden.Agent.isOnNavMesh && civilian.Agent.isOnNavMesh, Is.True);
            Assert.That(warden.PatrolSpeed, Is.EqualTo(civilian.PatrolSpeed));
            Assert.That(warden.Route.WaitSeconds, Is.EqualTo(civilian.Route.WaitSeconds));
            for (int i = 0; i < 4; i++) Assert.That(warden.Route.Points[i], Is.EqualTo(civilian.Route.Points[(i + 2) % 4]));
            Vector3 wardenStart = warden.transform.position, civilianStart = civilian.transform.position;
            yield return new WaitForSeconds(3);
            Vector3 wardenDelta = warden.transform.position - wardenStart, civilianDelta = civilian.transform.position - civilianStart;
            Assert.That(wardenDelta.magnitude, Is.GreaterThan(.7f));
            Assert.That(wardenDelta.magnitude, Is.EqualTo(civilianDelta.magnitude).Within(.2f));
            Assert.That(Vector3.Dot(wardenDelta.normalized, (warden.Route.Points[1].position - warden.Route.Points[0].position).normalized), Is.GreaterThan(.95f));
            Assert.That(Vector3.Dot(civilianDelta.normalized, (civilian.Route.Points[1].position - civilian.Route.Points[0].position).normalized), Is.GreaterThan(.95f));
        }
    }
}
#endif
