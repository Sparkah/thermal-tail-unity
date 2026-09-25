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
    public class ExampleSceneTests
    {
        PrototypeSession session;
        const string ScenePath = "Assets/ThermalTail/Prototype/Scenes/PrototypeExample.unity";

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(System.IO.File.Exists(ScenePath), Is.True, "Build the prototype example before running scene tests.");
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            session = Object.FindFirstObjectByType<PrototypeSession>();
            session.Player.GetComponent<PrototypeControls>().enabled = false;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene current = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Empty test cleanup"));
            yield return SceneManager.UnloadSceneAsync(current);
        }

        [UnityTest]
        public IEnumerator ExampleHasWorkingGroundPatrolAndIndependentCivilian()
        {
            var civilian = Object.FindObjectsByType<NpcMotor>(FindObjectsSortMode.None).First(n => n.GetComponent<WardenBrain>() == null);
            Assert.That(civilian.Agent.isOnNavMesh, Is.True);
            Assert.That(civilian.GetComponent<VisionSensor>(), Is.Null);
            Vector3 start = civilian.transform.position;
            yield return new WaitForSeconds(2.3f);
            Assert.That(Vector3.Distance(start, civilian.transform.position), Is.GreaterThan(0.2f));
        }

        [UnityTest]
        public IEnumerator AllAuthoredClimbLinksHaveBodyClearance()
        {
            foreach (var sensor in Object.FindObjectsByType<VisionSensor>(FindObjectsSortMode.None)) sensor.enabled = false;
            foreach (var link in Object.FindObjectsByType<SurfaceTransition>(FindObjectsSortMode.None))
            {
                session.Player.Warp(link.Entry.position, link.Entry.rotation);
                session.CloseScreen();
                Assert.That(session.Player.BeginTransition(link), Is.True, link.name);
                yield return new WaitForSeconds(link.Duration + 0.1f);
                Assert.That(Vector3.Distance(session.Player.transform.position, link.Exit.position), Is.LessThan(0.1f),
                    link.name + " at " + session.Player.transform.position + " — " + session.Message);
                Assert.That(session.Player.InTransition, Is.False, link.name);
            }
        }

        [UnityTest]
        public IEnumerator SuspicionPausesNavigationAndReleasesItForPatrolAndChase()
        {
            yield return new WaitForSeconds(session.Settings.RespawnGrace + 0.1f);
            var warden = Object.FindFirstObjectByType<WardenBrain>();
            var npc = warden.GetComponent<NpcMotor>();
            bool originalRotation = npc.Agent.updateRotation;
            session.Player.Warp(warden.transform.position + warden.transform.forward * 4f + Vector3.right + Vector3.up * 0.34f,
                Quaternion.identity);
            session.Thermal.SetTemperature(25f); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.That(warden.Mode, Is.EqualTo(WardenMode.Suspicious));
            Assert.That(npc.Agent.isStopped, Is.True);
            Assert.That(npc.Agent.updateRotation, Is.False);
            Vector3 stopped = warden.transform.position;
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector3.Distance(stopped, warden.transform.position), Is.LessThan(0.03f));
            session.Thermal.SetTemperature(22f);
            yield return new WaitForSeconds(1f);
            Assert.That(warden.Mode, Is.EqualTo(WardenMode.Patrol));
            Assert.That(npc.Agent.isStopped, Is.False);
            Assert.That(npc.Agent.updateRotation, Is.EqualTo(originalRotation));
            Vector3 resumed = warden.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.That(Vector3.Distance(resumed, warden.transform.position), Is.GreaterThan(0.1f));

            session.Player.Warp(warden.transform.position + warden.transform.forward * 4f + Vector3.up * 0.34f, Quaternion.identity);
            session.Thermal.SetTemperature(40f); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.That(warden.Mode, Is.EqualTo(WardenMode.Suspicious));
            yield return new WaitForSeconds(0.7f);
            Assert.That(warden.Mode, Is.EqualTo(WardenMode.Chase));
            Assert.That(npc.Agent.isStopped, Is.False);
            Assert.That(npc.Agent.updateRotation, Is.EqualTo(originalRotation));
            Assert.That(npc.Agent.speed, Is.EqualTo(npc.ChaseSpeed * warden.RunSpeedMultiplier).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator WardenPrefabTakedownDisablesNavigationAndRestoresPoseOnReset()
        {
            var takedown = Object.FindFirstObjectByType<WardenTakedown>();
            Assert.That(takedown, Is.Not.Null, "The shared Warden prefab must supply takedowns.");
            var npc = takedown.GetComponent<NpcMotor>();
            var view = takedown.GetComponent<WardenTakedownView>();
            Vector3 position = view.BodyVisual.localPosition;
            Quaternion rotation = view.BodyVisual.localRotation;
            session.Player.Warp(takedown.transform.position - takedown.transform.forward * 1.1f + Vector3.up * 0.34f,
                Quaternion.identity);
            Physics.SyncTransforms();
            Assert.That(takedown.TryTakedown(session.Player), Is.True);
            yield return null;
            Assert.That(npc.Agent.enabled, Is.False);
            Assert.That(Quaternion.Angle(rotation, view.BodyVisual.localRotation), Is.GreaterThan(80f));
            Assert.That(view.BodyVisual.localPosition.y, Is.LessThan(position.y));
            Assert.That(takedown.GetComponentsInChildren<LineRenderer>().All(line => !line.enabled), Is.True);
            session.RestoreCheckpoint();
            yield return null;
            Assert.That(npc.Agent.enabled, Is.True);
            Assert.That(npc.Agent.isOnNavMesh, Is.True);
            Assert.That(view.BodyVisual.localPosition, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(rotation, view.BodyVisual.localRotation), Is.LessThan(0.01f));
            Assert.That(takedown.GetComponent<StealthVisuals>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator WardenRunsOnAlarmAndRestoresPatrolSpeed()
        {
            foreach (var sensor in Object.FindObjectsByType<VisionSensor>(FindObjectsSortMode.None)) sensor.enabled = false;
            yield return new WaitForSeconds(session.Settings.RespawnGrace + 0.1f);
            var warden = Object.FindFirstObjectByType<WardenBrain>();
            var npc = warden.GetComponent<NpcMotor>();
            session.ReportCivilianContact(warden.transform.position + warden.transform.forward * 4f);
            yield return null;
            Assert.That(warden.Mode, Is.EqualTo(WardenMode.Chase));
            Assert.That(npc.Agent.speed, Is.EqualTo(npc.ChaseSpeed * warden.RunSpeedMultiplier).Within(0.001f));
            Assert.That(npc.Agent.speed, Is.GreaterThan(session.Settings.MoveSpeed));
            warden.RunSpeedMultiplier = 2f;
            yield return null;
            Assert.That(npc.Agent.speed, Is.EqualTo(npc.ChaseSpeed * 2f).Within(0.001f));
            yield return new WaitForSeconds(warden.LostSightGrace + session.Settings.InvestigationSeconds + 0.1f);
            Assert.That(warden.Mode, Is.EqualTo(WardenMode.Patrol));
            Assert.That(npc.Agent.speed, Is.EqualTo(npc.PatrolSpeed).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator WardenContactCapturesWithoutRequiringSuspicion()
        {
            yield return new WaitForSeconds(session.Settings.RespawnGrace + 0.1f);
            var warden = Object.FindFirstObjectByType<WardenBrain>();
            var npc = warden.GetComponent<NpcMotor>(); npc.Agent.isStopped = true; npc.enabled = false;
            warden.enabled = false;
            session.Player.Warp(warden.transform.position + Vector3.up * 0.34f + Vector3.back * 0.65f, Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.True);
        }
    }
}
#endif
