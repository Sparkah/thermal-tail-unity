using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ThermalTail.Prototype.Tests
{
    public class PrototypeGameplayTests
    {
        readonly List<Object> owned = new List<Object>();
        PrototypeSession session;
        SurfaceMotor player;
        PlayerThermal thermal;
        const int WorldLayer = 28;

        GameObject ObjectAt(string name, Vector3 position)
        {
            var go = new GameObject(name); go.SetActive(false); go.transform.position = position; owned.Add(go); return go;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var settings = ScriptableObject.CreateInstance<PrototypeSettings>(); owned.Add(settings);
            settings.RespawnGrace = 0f;
            var root = ObjectAt("Session", Vector3.zero);
            session = root.AddComponent<PrototypeSession>(); session.Settings = settings;
            var actor = ObjectAt("Player", new Vector3(0, 0.34f, 0));
            actor.AddComponent<SphereCollider>().radius = 0.32f;
            player = actor.AddComponent<SurfaceMotor>(); player.Session = session; player.SolidMask = 1 << WorldLayer;
            thermal = actor.AddComponent<PlayerThermal>(); thermal.Session = session;
            session.Player = player; session.Thermal = thermal;
            actor.SetActive(true); root.SetActive(true);
            Solid("Floor", new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
            yield return null;
            Physics.SyncTransforms();
        }

        GameObject Solid(string name, Vector3 position, Vector3 size, bool climbable = true)
        {
            var go = ObjectAt(name, position); go.layer = WorldLayer;
            go.AddComponent<BoxCollider>().size = size;
            if (climbable) go.AddComponent<ClimbableSurface>();
            go.SetActive(true); return go;
        }

        CrowdZone Crowd(Vector3 center, float target = 36)
        {
            var go = ObjectAt("Crowd", center);
            var crowd = go.AddComponent<CrowdZone>(); crowd.Radius = 2; crowd.TransitionBand = 1;
            crowd.UseSessionTemperature = false; crowd.Temperature = target;
            go.SetActive(true); return crowd;
        }

        VisionSensor SensorAt(Vector3 position, float closeRange = 0f)
        {
            var go = ObjectAt("Sensor", position);
            var sensor = go.AddComponent<VisionSensor>(); sensor.Session = session;
            sensor.CloseRange = closeRange; sensor.ObstructionMask = 1 << WorldLayer;
            go.SetActive(true); return sensor;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var obj in owned) if (obj != null) Object.Destroy(obj);
            owned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SlightlyEmbeddedPlacementRecoversSupportAndCanMove()
        {
            player.Warp(new Vector3(0, .22f, 0), Quaternion.identity);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.GetComponent<Rigidbody>().position.y, Is.EqualTo(.34f).Within(.005f));
            player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(.3f);
            Assert.That(player.GetComponent<Rigidbody>().position.z, Is.GreaterThan(.3f));
        }

        [UnityTest]
        public IEnumerator EmbeddedSupportRecoveryDoesNotPassThroughAnOverheadObstacle()
        {
            Solid("Low ceiling", new Vector3(0, .66f, 0), new Vector3(2, .1f, 2), false);
            player.Warp(new Vector3(0, .22f, 0), Quaternion.identity);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(player.GetComponent<Rigidbody>().position.y, Is.EqualTo(.22f).Within(.005f));
        }

        [UnityTest]
        public IEnumerator CivilianWrongTemperatureBuildsQuicklyAndCallsSecurityWithoutCapturing()
        {
            var citizen = ObjectAt("Civilian", Vector3.zero);
            var zone = Crowd(new Vector3(0, .34f, 0)); zone.transform.SetParent(citizen.transform);
            var suspicion = citizen.AddComponent<CivilianSuspicion>(); suspicion.Session = session;
            suspicion.Zone = zone; suspicion.ObstructionMask = 1 << WorldLayer; citizen.SetActive(true);
            int alerts = 0; Vector3 reported = Vector3.zero;
            session.CivilianAlert += p => { alerts++; reported = p; };
            player.Warp(new Vector3(1.5f, .34f, 0), Quaternion.identity); thermal.SetTemperature(22);
            // Another overlapping civilian accepting 22 must not excuse this civilian's own 36 target.
            Crowd(player.transform.position, 22);
            yield return new WaitForSeconds(.3f);
            Assert.That(thermal.Matched, Is.True);
            Assert.That(suspicion.Suspicion, Is.GreaterThan(.2f).And.LessThan(1));
            Assert.That(alerts, Is.Zero);
            yield return new WaitForSeconds(.6f);
            Assert.That(alerts, Is.EqualTo(1));
            Assert.That(reported.x, Is.EqualTo(1.5f).Within(.01f));
            Assert.That(session.Resetting, Is.False, "Civilians call wardens rather than directly capturing.");
            session.RestoreCheckpoint();
            Assert.That(suspicion.Suspicion, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CivilianSuspicionRespectsMatchingTransitionBandCoverAndSafeZones()
        {
            var citizen = ObjectAt("Civilian", Vector3.zero);
            var zone = Crowd(new Vector3(0, .34f, 0)); zone.transform.SetParent(citizen.transform);
            var suspicion = citizen.AddComponent<CivilianSuspicion>(); suspicion.Session = session;
            suspicion.Zone = zone; suspicion.ObstructionMask = 1 << WorldLayer; citizen.SetActive(true);
            session.Settings.PassiveCooling = 0;
            player.Warp(new Vector3(2.25f, .34f, 0), Quaternion.identity); thermal.SetTemperature(22);
            yield return new WaitForSeconds(.8f);
            Assert.That(suspicion.Suspicion, Is.Zero, "The transition band remains a safe thermal handoff.");
            player.Warp(new Vector3(1.5f, .34f, 0), Quaternion.identity); thermal.SetTemperature(36);
            yield return new WaitForSeconds(.3f);
            Assert.That(suspicion.Suspicion, Is.Zero);
            var wall = Solid("Civilian sight blocker", new Vector3(.75f, 1, 0), new Vector3(.2f, 2, 2));
            thermal.SetTemperature(22); Physics.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            Assert.That(suspicion.Suspicion, Is.Zero);
            wall.SetActive(false);
            yield return new WaitForSeconds(.25f);
            Assert.That(suspicion.Suspicion, Is.GreaterThan(.1f));
            var shelter = ObjectAt("Thermal shelter", player.transform.position);
            shelter.AddComponent<BoxCollider>().isTrigger = true; shelter.AddComponent<SafeZone>(); shelter.SetActive(true);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            Assert.That(suspicion.Suspicion, Is.Zero);
            shelter.SetActive(false);
            session.Settings.RespawnGrace = 10; session.RestoreCheckpoint();
            player.Warp(new Vector3(1.5f, .34f, 0), Quaternion.identity);
            yield return new WaitForSeconds(.8f);
            Assert.That(suspicion.Suspicion, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PressurePlateCapturesImmediatelyDespiteGraceShelterAndMatchingTemperature()
        {
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThermalTail/Prototype/Prefabs/PressurePlate.prefab");
            Assert.That(asset, Is.Not.Null);
            var plate = Object.Instantiate(asset, new Vector3(3, 0, 0), Quaternion.identity); owned.Add(plate);
            var safe = ObjectAt("Safe area overlapping trap", new Vector3(3, .5f, 0));
            var box = safe.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = Vector3.one * 2;
            safe.AddComponent<SafeZone>(); safe.SetActive(true);
            session.Settings.RespawnGrace = 10;
            session.RestoreCheckpoint();
            Assert.That(session.TryCapture("Ordinary camera alarm"), Is.False, "Normal captures retain respawn grace.");
            player.Warp(new Vector3(3, .34f, 0), Quaternion.identity);
            Physics.SyncTransforms(); thermal.SetTemperature(22);
            Assert.That(thermal.IsSafe && thermal.Matched, Is.True);
            session.OpenNotes();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.True);
            Assert.That(session.Screen, Is.EqualTo(ModalScreen.None));
            yield return new WaitForSeconds(1);
            Assert.That(session.Resetting, Is.False);
            Assert.That(player.transform.position.x, Is.EqualTo(0).Within(.01f));
            player.Warp(new Vector3(3, .34f, 0), Quaternion.identity); Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.True, "The same plate rearms after a checkpoint reset.");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PressurePlateIgnoresNonPlayersAndDisabledPlates()
        {
            var plate = ObjectAt("Plate", new Vector3(3, 0, 0));
            var trap = plate.AddComponent<PressurePlate>();
            var box = plate.GetComponent<BoxCollider>(); box.isTrigger = true; box.center = Vector3.up * .08f;
            box.size = new Vector3(2, .16f, 2); plate.SetActive(true);
            var npc = ObjectAt("Non-player body", new Vector3(3, .34f, 0));
            npc.AddComponent<SphereCollider>().radius = .32f; npc.AddComponent<Rigidbody>().isKinematic = true;
            npc.SetActive(true); Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.False);
            trap.enabled = false;
            player.Warp(new Vector3(3, .34f, 0), Quaternion.identity); Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.False);
            session.Complete(); trap.enabled = true;
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.False, "A plate cannot undo game completion.");
        }

        [UnityTest]
        public IEnumerator CameraCanLookUpWithoutPassingThroughFloor()
        {
            var go = ObjectAt("Camera", Vector3.zero);
            var camera = go.AddComponent<SurfaceCamera>();
            camera.Player = player;
            camera.ObstructionMask = 1 << WorldLayer;
            camera.AdjustPitch(-95f);
            go.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(Vector3.Dot(go.transform.forward, Vector3.up), Is.GreaterThan(0.5f));
            Assert.That(go.transform.position.y, Is.GreaterThanOrEqualTo(0.15f), "Camera body must stay above the floor.");
        }

        [UnityTest]
        public IEnumerator CrowdCoreRequiresCivilianTemperatureAndBandAcceptsEither()
        {
            Crowd(player.transform.position + Vector3.right);
            Physics.SyncTransforms();
            thermal.SetTemperature(22);
            Assert.That(thermal.InCrowd, Is.True);
            Assert.That(thermal.Matched, Is.False);
            thermal.SetTemperature(36);
            Assert.That(thermal.Matched, Is.True);
            player.Warp(new Vector3(-1.5f, 0.34f, 0), Quaternion.identity);
            Physics.SyncTransforms(); thermal.SetTemperature(22);
            Assert.That(thermal.InTransitionBand, Is.True);
            Assert.That(thermal.Matched, Is.True);
            thermal.SetTemperature(36); Assert.That(thermal.Matched, Is.True);
            player.Warp(new Vector3(-4, 0.34f, 0), Quaternion.identity);
            Physics.SyncTransforms(); thermal.SetTemperature(36);
            Assert.That(thermal.InTransitionBand, Is.False);
            Assert.That(thermal.Matched, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverlappingCoreTakesPrecedenceOverAnotherEdgeBand()
        {
            Crowd(player.transform.position);
            Crowd(player.transform.position + Vector3.right * 2.5f);
            Physics.SyncTransforms(); thermal.SetTemperature(22);
            Assert.That(thermal.InCrowd, Is.True);
            Assert.That(thermal.InTransitionBand, Is.False);
            Assert.That(thermal.TargetTemperature, Is.EqualTo(36));
            Assert.That(thermal.Matched, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CoolingStopsAtTargetAndDoesNotNeedTriggerEntryAfterWarp()
        {
            var go = ObjectAt("Cooling shelter", new Vector3(4, 0.5f, 0));
            var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = Vector3.one * 2;
            var zone = go.AddComponent<ThermalVolume>(); zone.TargetTemperature = 22; zone.DegreesPerSecond = 100;
            go.AddComponent<SafeZone>(); go.SetActive(true);
            player.Warp(new Vector3(4, 0.34f, 0), Quaternion.identity);
            Physics.SyncTransforms(); thermal.SetTemperature(36);
            Assert.That(thermal.IsSafe, Is.True);
            yield return new WaitForSeconds(0.3f);
            Assert.That(thermal.Temperature, Is.EqualTo(22).Within(0.05));
        }

        [UnityTest]
        public IEnumerator NarrowLedgeSkipReachesNextWallWithoutBodyPenetration()
        {
            Solid("Lower ledge", new Vector3(0, 1, 3), new Vector3(4, 2, 2));
            var wall = Solid("Next wall", new Vector3(0, 2.5f, 3.2f), new Vector3(4, 5, 2));
            foreach (float width in new[] { .08f, .2f, .28f })
            {
                wall.transform.position = new Vector3(0, 2.5f, 3 + width);
                player.Warp(new Vector3(0, 1.8f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
                player.MoveInput = Vector2.up; Physics.SyncTransforms();
                float deadline = Time.time + 1.4f;
                while (Time.time < deadline)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(Physics.CheckSphere(player.GetComponent<Rigidbody>().position, player.Radius - player.Skin,
                        1 << WorldLayer, QueryTriggerInteraction.Ignore), Is.False, "Clearance at ledge width " + width);
                }
                Assert.That(player.transform.position.y, Is.GreaterThan(2.7f), "Ledge width " + width + " at " + player.transform.position);
                Assert.That(Vector3.Dot(player.SurfaceNormal, Vector3.back), Is.GreaterThan(.99f));
                Assert.That(Vector3.Dot(player.ViewForward, Vector3.up), Is.GreaterThan(.99f));
                Assert.That(player.InTransition, Is.False);
                player.Warp(new Vector3(0, 2.6f, 2 + width - .34f), Quaternion.LookRotation(Vector3.down, Vector3.back));
                player.MoveInput = Vector2.up;
                deadline = Time.time + 1.4f;
                while (Time.time < deadline)
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(Physics.CheckSphere(player.GetComponent<Rigidbody>().position, player.Radius - player.Skin,
                        1 << WorldLayer, QueryTriggerInteraction.Ignore), Is.False, "Reverse clearance at ledge width " + width);
                }
                Assert.That(player.transform.position.y, Is.LessThan(1.8f), "Reverse ledge width " + width + " at " + player.transform.position);
                Assert.That(Vector3.Dot(player.SurfaceNormal, Vector3.back), Is.GreaterThan(.99f));
                Assert.That(Vector3.Dot(player.ViewForward, Vector3.down), Is.GreaterThan(.99f));
                Assert.That(player.InTransition, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator NarrowSkipCanBeDisabledAndHonorsMaximumDistance()
        {
            Solid("Lower ledge", new Vector3(0, 1, 3), new Vector3(4, 2, 2));
            Solid("Next wall", new Vector3(0, 2.5f, 3.2f), new Vector3(4, 5, 2));
            for (int i = 0; i < 3; i++)
            {
                player.SkipNarrowSurfaces = i != 0;
                player.MaximumSkipDistance = i == 1 ? .1f : .75f;
                player.AutomaticCorners = i != 2;
                player.Warp(new Vector3(0, 1.8f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
                player.MoveInput = Vector2.up; Physics.SyncTransforms();
                yield return new WaitForSeconds(.8f);
                Assert.That(player.transform.position.y, Is.LessThan(2.1f));
                Assert.That(player.InTransition, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator NarrowSkipRejectsUnmarkedLandingGapsAndBlockedRoutes()
        {
            var ledge = Solid("Lower ledge", new Vector3(0, 1, 3), new Vector3(4, 2, 2));
            var wall = Solid("Unmarked next wall", new Vector3(0, 2.5f, 3.2f), new Vector3(4, 5, 2), false);
            for (int i = 0; i < 3; i++)
            {
                if (i == 1)
                {
                    wall.AddComponent<ClimbableSurface>();
                    ledge.transform.position = new Vector3(0, 1, 2.02f);
                    ledge.GetComponent<BoxCollider>().size = new Vector3(4, 2, .04f);
                }
                if (i == 2)
                {
                    ledge.transform.position = new Vector3(0, 1, 3);
                    ledge.GetComponent<BoxCollider>().size = new Vector3(4, 2, 2);
                    Solid("Body clearance blocker", new Vector3(0, 2.25f, 1.65f), new Vector3(1, .1f, .15f), false);
                }
                player.Warp(new Vector3(0, 1.7f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
                player.MoveInput = Vector2.up; Physics.SyncTransforms();
                yield return new WaitForSeconds(.8f);
                Assert.That(player.transform.position.y, Is.LessThan(2.1f), "Rejected geometry case " + i);
                Assert.That(player.InTransition, Is.False);
                Assert.That(Physics.CheckSphere(player.GetComponent<Rigidbody>().position, player.Radius - player.Skin,
                    1 << WorldLayer, QueryTriggerInteraction.Ignore), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator NarrowCapSkipRoundsBothEdgesAndWarpCancelsIt()
        {
            Solid("Thin wall", new Vector3(0, 1, 2.015f), new Vector3(4, 2, .03f));
            player.Warp(new Vector3(0, 1.95f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            player.MoveInput = Vector2.up; Physics.SyncTransforms();
            float deadline = Time.time + 2;
            while (Time.time < deadline && (Vector3.Dot(player.SurfaceNormal, Vector3.forward) < .99f || player.InTransition))
            {
                yield return new WaitForFixedUpdate();
                Assert.That(Physics.CheckSphere(player.GetComponent<Rigidbody>().position, player.Radius - player.Skin,
                    1 << WorldLayer, QueryTriggerInteraction.Ignore), Is.False);
            }
            Assert.That(player.SurfaceNormal.z, Is.GreaterThan(.99f));
            Assert.That(player.InTransition, Is.False);
            Assert.That(Vector3.Dot(player.ViewForward, Vector3.down), Is.GreaterThan(.99f));
            player.Warp(new Vector3(0, 1.95f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(.15f);
            Assert.That(player.InTransition, Is.True);
            player.Warp(new Vector3(-4, .34f, 0), Quaternion.identity);
            yield return new WaitForFixedUpdate();
            Assert.That(player.InTransition, Is.False);
            Assert.That(player.SurfaceNormal, Is.EqualTo(Vector3.up));
        }

        [UnityTest]
        public IEnumerator AutomaticCornersConnectWallTopAndSideWithoutLinks()
        {
            Solid("Box", new Vector3(0, 1, 3), new Vector3(4, 2, 2));
            Physics.SyncTransforms();
            Vector3[] starts = { new Vector3(0, 1.8f, 1.66f), new Vector3(0, 2.34f, 2.2f), new Vector3(1.8f, 1, 1.66f) };
            Vector3[] normals = { Vector3.back, Vector3.up, Vector3.back };
            Vector3[] forwards = { Vector3.up, Vector3.back, Vector3.right };
            Vector3[] ends = { Vector3.up, Vector3.back, Vector3.right };
            for (int i = 0; i < starts.Length; i++)
            {
                player.Warp(starts[i], Quaternion.LookRotation(forwards[i], normals[i]));
                player.MoveInput = Vector2.up;
                Physics.SyncTransforms();
                float deadline = Time.time + 2f;
                while (Time.time < deadline && (Vector3.Dot(player.SurfaceNormal, ends[i]) < 0.99f || player.InTransition))
                {
                    yield return new WaitForFixedUpdate();
                    Assert.That(Physics.CheckSphere(player.GetComponent<Rigidbody>().position, player.Radius - player.Skin,
                        1 << WorldLayer, QueryTriggerInteraction.Ignore), Is.False, "Body clearance at corner " + i);
                }
                player.MoveInput = Vector2.zero;
                Assert.That(Vector3.Dot(player.SurfaceNormal, ends[i]), Is.GreaterThan(0.99f), "Corner " + i);
                Assert.That(player.InTransition, Is.False);
                Assert.That(Vector3.Dot(player.ViewForward, Quaternion.FromToRotation(normals[i], ends[i]) * forwards[i]), Is.GreaterThan(0.99f));
            }
        }

        [UnityTest]
        public IEnumerator AutomaticCornerRejectsUnmarkedFaceAndBlockedArc()
        {
            var box = Solid("Unmarked box", new Vector3(0, 1, 3), new Vector3(4, 2, 2), false);
            // A separate marked top meets the unmarked vertical face.
            var top = Solid("Marked top", new Vector3(0, 1.99f, 3), new Vector3(4, 0.02f, 2));
            player.Warp(new Vector3(0, 2.34f, 2.2f), Quaternion.LookRotation(Vector3.back, Vector3.up));
            player.MoveInput = Vector2.up;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.5f);
            Assert.That(player.SurfaceNormal, Is.EqualTo(Vector3.up));
            Assert.That(player.InTransition, Is.False);
            Assert.That(player.GetComponent<Rigidbody>().position.z, Is.GreaterThanOrEqualTo(2f));
            top.SetActive(false); box.AddComponent<ClimbableSurface>();
            Solid("Arc blocker", new Vector3(0, 2.2f, 1.8f), new Vector3(1, 0.15f, 0.15f), false);
            player.Warp(new Vector3(0, 1.6f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            player.MoveInput = Vector2.up; Physics.SyncTransforms();
            yield return new WaitForSeconds(0.7f);
            Assert.That(player.InTransition, Is.False);
            Assert.That(player.GetComponent<Rigidbody>().position.y, Is.LessThan(2f));
        }

        [UnityTest]
        public IEnumerator AutomaticCornerRoundsOctagonalWall()
        {
            var go = ObjectAt("Octagonal prism", Vector3.zero); go.layer = WorldLayer;
            var vertices = new Vector3[18];
            vertices[16] = Vector3.zero; vertices[17] = Vector3.up * 2f;
            var triangles = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                vertices[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2f;
                vertices[i + 8] = vertices[i] + Vector3.up * 2f;
                int j = (i + 1) % 8;
                triangles.AddRange(new[] { i, i + 8, j + 8, i, j + 8, j, i + 8, 17, j + 8, i, j, 16 });
            }
            var mesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            go.AddComponent<ClimbableSurface>(); go.SetActive(true);
            Vector3 normal = new Vector3(-0.3826834f, 0f, -0.9238795f);
            Vector3 direction = new Vector3(0.9238795f, 0f, -0.3826834f);
            Vector3 targetNormal = new Vector3(0.3826834f, 0f, -0.9238795f);
            player.Warp(new Vector3(0, 1, -2) - direction * 0.2f + normal * 0.34f,
                Quaternion.LookRotation(direction, normal));
            player.MoveInput = Vector2.up; Physics.SyncTransforms();
            float deadline = Time.time + 2f;
            while (Time.time < deadline && (Vector3.Dot(player.SurfaceNormal, targetNormal) < 0.99f || player.InTransition))
                yield return new WaitForFixedUpdate();
            player.MoveInput = Vector2.zero;
            Assert.That(Vector3.Dot(player.SurfaceNormal, targetNormal), Is.GreaterThan(0.99f));
            Assert.That(player.InTransition, Is.False);
        }

        [UnityTest]
        public IEnumerator UnsupportedSurfaceEdgeDoesNotJumpToNearbyPlatform()
        {
            Solid("Thin platform", new Vector3(0, 5, 0), new Vector3(4, 0.02f, 2));
            Solid("Separate platform", new Vector3(0, 5, 2.5f), new Vector3(4, 0.02f, 2));
            player.Warp(new Vector3(0, 5.35f, 0.8f), Quaternion.identity);
            player.MoveInput = Vector2.up; Physics.SyncTransforms();
            yield return new WaitForSeconds(0.7f);
            Assert.That(player.InTransition, Is.False);
            Assert.That(player.GetComponent<Rigidbody>().position.z, Is.LessThanOrEqualTo(1f));
        }

        [UnityTest]
        public IEnumerator AutomaticCornerWarpCancelsTraversal()
        {
            Solid("Box", new Vector3(0, 1, 3), new Vector3(4, 2, 2));
            player.Warp(new Vector3(0, 1.95f, 1.66f), Quaternion.LookRotation(Vector3.up, Vector3.back));
            player.MoveInput = Vector2.up; Physics.SyncTransforms();
            float deadline = Time.time + 1f;
            while (!player.InTransition && Time.time < deadline) yield return new WaitForFixedUpdate();
            Assert.That(player.InTransition, Is.True);
            player.Warp(new Vector3(0, 0.34f, 0), Quaternion.identity);
            yield return new WaitForFixedUpdate();
            Assert.That(player.InTransition, Is.False);
            Assert.That(player.GetComponent<Rigidbody>().position, Is.EqualTo(new Vector3(0, 0.34f, 0)));
        }

        [UnityTest]
        public IEnumerator MotorClimbsMarkedWallAndMovementRaisesTemperature()
        {
            Solid("Climbing wall", new Vector3(0, 2, 2), new Vector3(8, 4, 0.3f));
            Physics.SyncTransforms();
            player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(1.3f);
            Assert.That(Vector3.Dot(player.SurfaceNormal, Vector3.back), Is.GreaterThan(0.9));
            Assert.That(player.transform.position.y, Is.GreaterThan(0.6f));
            Assert.That(thermal.Temperature, Is.GreaterThan(22.1f));
        }

        [UnityTest]
        public IEnumerator UnmarkedWallBlocksPlayerInsteadOfAllowingClimb()
        {
            Solid("Obstacle", new Vector3(0, 2, 2), new Vector3(8, 4, 0.3f), false);
            Physics.SyncTransforms(); player.MoveInput = Vector2.up;
            yield return new WaitForSeconds(1.2f);
            Assert.That(player.transform.position.z, Is.LessThan(1.6f));
            Assert.That(player.transform.position.y, Is.EqualTo(0.34f).Within(0.06));
        }

        [UnityTest]
        public IEnumerator NoteCollectsOnceSurvivesResetAndHasNoMothReward()
        {
            var note = ScriptableObject.CreateInstance<NoteData>(); owned.Add(note);
            note.Id = "test-note"; note.Title = "Test"; note.Text = "First digit is 7";
            var go = ObjectAt("Note", new Vector3(5, 1, 0));
            go.AddComponent<SphereCollider>().isTrigger = true;
            var actor = go.AddComponent<NoteActor>(); actor.Note = note; actor.Session = session; go.SetActive(true);
            Assert.That(actor.Collect(), Is.True);
            Assert.That(actor.Collect(), Is.False);
            Assert.That(session.Screen, Is.EqualTo(ModalScreen.Notes));
            Assert.That(session.Notes.Count, Is.EqualTo(1));
            Assert.That(session.MothsCollected, Is.Zero);
            session.RestoreCheckpoint();
            Assert.That(session.Notes.Count, Is.EqualTo(1));
            Assert.That(session.HasCollected(note.Id), Is.True);
            Assert.That(session.Screen, Is.EqualTo(ModalScreen.None));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PhysicalNoteTriggerOpensJournal()
        {
            var data = ScriptableObject.CreateInstance<NoteData>(); owned.Add(data); data.Id = "touch-note";
            var go = ObjectAt("Touch note", player.transform.position + Vector3.forward * 0.1f);
            go.AddComponent<SphereCollider>().isTrigger = true;
            var actor = go.AddComponent<NoteActor>(); actor.Note = data; actor.Session = session; go.SetActive(true);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Notes, Does.Contain(data));
            Assert.That(session.Screen, Is.EqualTo(ModalScreen.Notes));
        }

        [UnityTest]
        public IEnumerator SuspiciousWardenTracksAcrossItsConeBeforeChasing()
        {
            var go = ObjectAt("Warden", new Vector3(0, 0, -3));
            var npc = go.AddComponent<NpcMotor>(); npc.Session = session;
            go.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var sensor = go.AddComponent<VisionSensor>(); sensor.Session = session;
            sensor.ObstructionMask = 1 << WorldLayer;
            var brain = go.AddComponent<WardenBrain>();
            brain.SuspiciousTurnSpeed = 90f;
            go.SetActive(true);
            Vector3 start = go.transform.position;
            for (int i = 0; i < 9; i++)
            {
                float angle = (15f + i * 5f) * Mathf.Deg2Rad;
                player.Warp(start + new Vector3(Mathf.Sin(angle) * 3f, 0.34f, Mathf.Cos(angle) * 3f), Quaternion.identity);
                thermal.SetTemperature(25f); Physics.SyncTransforms();
                Quaternion before = go.transform.rotation;
                float began = Time.time;
                yield return new WaitForSeconds(0.07f);
                Assert.That(Quaternion.Angle(before, go.transform.rotation),
                    Is.LessThanOrEqualTo(brain.SuspiciousTurnSpeed * (Time.time - began) + 1f), "Turning must be gradual.");
                Assert.That(brain.Mode, Is.EqualTo(WardenMode.Suspicious));
                Assert.That(sensor.Suspicion, Is.LessThan(session.Settings.ChaseThreshold));
                Assert.That(go.transform.position, Is.EqualTo(start));
            }
            Assert.That(Vector3.Angle(Vector3.forward, go.transform.forward), Is.GreaterThan(sensor.FieldOfView * 0.5f));
            Assert.That(sensor.CanSee(player), Is.True, "Turning should keep the crossing player in view.");
            float deadline = Time.time + 2.5f;
            while (brain.Mode != WardenMode.Chase && Time.time < deadline) yield return null;
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Chase));
            Assert.That(sensor.Suspicion, Is.GreaterThanOrEqualTo(session.Settings.ChaseThreshold));
        }

        [UnityTest]
        public IEnumerator SuspiciousWardenRemembersLastVisiblePositionAndCalmsDownBehindCover()
        {
            var go = ObjectAt("Warden", new Vector3(0, 0, -3));
            var npc = go.AddComponent<NpcMotor>(); npc.Session = session;
            go.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var sensor = go.AddComponent<VisionSensor>(); sensor.Session = session;
            sensor.ObstructionMask = 1 << WorldLayer;
            var brain = go.AddComponent<WardenBrain>(); go.SetActive(true);
            player.Warp(new Vector3(1, 0.34f, 0), Quaternion.identity);
            thermal.SetTemperature(40f); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Suspicious));
            Vector3 observed = brain.PursuitDestination;
            Solid("Cover", new Vector3(0, 1, -1.5f), new Vector3(5, 2, 0.2f), false);
            player.Warp(new Vector3(-2, 0.34f, 0), Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.1f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Suspicious));
            Assert.That(brain.PursuitDestination, Is.EqualTo(observed));
            yield return new WaitForSeconds(0.8f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Patrol));
            Assert.That(npc.HasOverride, Is.False);
            Assert.That(sensor.Suspicion, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WardenKeepsIdentifiedPlayerBehindItButCannotTrackThroughCover()
        {
            var go = ObjectAt("Warden", new Vector3(0, 0, -3));
            var npc = go.AddComponent<NpcMotor>(); npc.Session = session;
            go.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var sensor = go.AddComponent<VisionSensor>(); sensor.Session = session;
            sensor.ObstructionMask = 1 << WorldLayer;
            var brain = go.AddComponent<WardenBrain>();
            go.SetActive(true);
            // A matched player, even within the future awareness radius, cannot initiate a chase.
            yield return new WaitForSeconds(0.2f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Patrol));
            thermal.SetTemperature(40f);
            yield return new WaitForSeconds(0.9f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Chase));
            player.Warp(new Vector3(0, 0.34f, -5), Quaternion.identity);
            thermal.SetTemperature(22f); Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.False);
            yield return new WaitForSeconds(0.2f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Chase));
            Assert.That(Vector3.Distance(brain.PursuitDestination, player.transform.position), Is.LessThan(0.05f));
            Vector3 lastVisible = brain.PursuitDestination;
            Solid("Cover", new Vector3(0, 1, -4), new Vector3(5, 2, 0.2f), false);
            player.Warp(new Vector3(1, 0.34f, -5), Quaternion.identity);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player, brain.ChaseAwarenessRadius), Is.False);
            yield return new WaitForSeconds(brain.LostSightGrace * 0.5f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Chase));
            Assert.That(brain.PursuitDestination, Is.EqualTo(lastVisible), "Hidden player movement must not update memory.");
            yield return new WaitForSeconds(brain.LostSightGrace * 0.5f + 0.1f);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Search));
            session.RestoreCheckpoint();
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Patrol));
        }

        [UnityTest]
        public IEnumerator WallBlocksSightAndMatchedPlayerDoesNotBuildSuspicion()
        {
            var go = ObjectAt("Sensor", new Vector3(0, 0.5f, -4));
            var sensor = go.AddComponent<VisionSensor>(); sensor.Session = session; sensor.ObstructionMask = 1 << WorldLayer;
            go.SetActive(true);
            var wall = Solid("Cover", new Vector3(0, 1, -2), new Vector3(2, 2, 0.2f), false);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.False);
            wall.SetActive(false); Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.True);
            yield return new WaitForSeconds(0.2f);
            Assert.That(sensor.Suspicion, Is.Zero);
            thermal.SetTemperature(40);
            yield return new WaitForSeconds(0.2f);
            Assert.That(sensor.Suspicion, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TakedownRequiresReachableRearApproachAndDisablesCaptureUntilReset()
        {
            var go = ObjectAt("Warden", new Vector3(0, 0, 3));
            var npc = go.AddComponent<NpcMotor>(); npc.Session = session;
            go.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            go.AddComponent<WardenBrain>();
            go.GetComponent<VisionSensor>().Session = session;
            go.GetComponent<VisionSensor>().ObstructionMask = 1 << WorldLayer;
            var capture = ObjectAt("Capture", new Vector3(0, 0.32f, 3));
            capture.transform.SetParent(go.transform, true);
            capture.AddComponent<SphereCollider>().isTrigger = true;
            capture.AddComponent<CaptureContact>().ObstructionMask = 1 << WorldLayer;
            capture.SetActive(true);
            var takedown = go.AddComponent<WardenTakedown>();
            go.SetActive(true);
            foreach (var blocked in new[] { new Vector3(0, 0.34f, 4.2f), new Vector3(1.2f, 0.34f, 3), new Vector3(0, 0.34f, 0) })
            {
                player.Warp(blocked, Quaternion.identity); Physics.SyncTransforms();
                Assert.That(takedown.TryTakedown(player), Is.False, "Front, side, and distant attempts must fail.");
            }
            player.Warp(new Vector3(0, 0.34f, 1.8f), Quaternion.identity);
            var wall = Solid("Takedown cover", new Vector3(0, 1, 2.4f), new Vector3(2, 2, 0.15f), false);
            Physics.SyncTransforms();
            Assert.That(takedown.TryTakedown(player), Is.False, "Takedowns cannot pass through walls.");
            wall.SetActive(false); Physics.SyncTransforms();
            session.OpenNotes();
            Assert.That(takedown.TryTakedown(player), Is.False, "Modal screens block takedowns.");
            session.CloseScreen();
            Assert.That(takedown.TryTakedown(player), Is.True);
            Assert.That(takedown.TryTakedown(player), Is.False, "A downed target cannot be taken down twice.");
            Assert.That(npc.enabled, Is.False);
            Assert.That(go.GetComponent<WardenBrain>().enabled, Is.False);
            Assert.That(go.GetComponent<VisionSensor>().enabled, Is.False);
            Assert.That(capture.GetComponent<Collider>().enabled, Is.False);
            player.Warp(new Vector3(0, 0.34f, 3), Quaternion.identity); Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(session.Resetting, Is.False, "An incapacitated warden cannot capture on contact.");
            session.RestoreCheckpoint();
            Assert.That(takedown.IsTakenDown, Is.False);
            Assert.That(npc.enabled, Is.True);
            Assert.That(go.GetComponent<WardenBrain>().enabled, Is.True);
            Assert.That(go.GetComponent<WardenBrain>().Mode, Is.EqualTo(WardenMode.Patrol));
            Assert.That(go.GetComponent<VisionSensor>().enabled, Is.True);
            Assert.That(capture.GetComponent<Collider>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator AlertedTakedownsAndCheckpointPersistenceAreOptIn()
        {
            var go = ObjectAt("Warden", new Vector3(0, 0, 3));
            var npc = go.AddComponent<NpcMotor>(); npc.Session = session;
            go.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
            var brain = go.AddComponent<WardenBrain>();
            go.GetComponent<VisionSensor>().Session = session;
            go.GetComponent<VisionSensor>().ObstructionMask = 1 << WorldLayer;
            var takedown = go.AddComponent<WardenTakedown>(); go.SetActive(true);
            player.Warp(new Vector3(0, 0.34f, 1.8f), Quaternion.identity); Physics.SyncTransforms();
            session.ReportCivilianContact(player.transform.position);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Chase));
            Assert.That(takedown.TryTakedown(player), Is.False);
            takedown.AllowAlertedTakedowns = true;
            takedown.RestoreOnCheckpoint = false;
            Assert.That(takedown.TryTakedown(player), Is.True);
            session.RestoreCheckpoint();
            Assert.That(takedown.IsTakenDown, Is.True);
            Assert.That(brain.enabled, Is.False);
            takedown.RestoreOnCheckpoint = true;
            session.RestoreCheckpoint();
            Assert.That(takedown.IsTakenDown, Is.False);
            Assert.That(brain.Mode, Is.EqualTo(WardenMode.Patrol));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseRangeSeesLowPlayersAcrossFrontHemisphereButNotBehindOrBeyondIt()
        {
            var sensor = SensorAt(Vector3.zero, 2f);
            var eye = ObjectAt("Elevated eye", Vector3.up * 1.5f);
            eye.transform.SetParent(sensor.transform, true); eye.SetActive(true);
            sensor.Eye = eye.transform;
            foreach (float side in new[] { -1f, 1f })
            {
                player.Warp(new Vector3(side, 0.34f, 0.4f), Quaternion.identity);
                Physics.SyncTransforms();
                sensor.CloseRange = 0f;
                Assert.That(sensor.CanSee(player), Is.False, "This low, sideways position must be outside the normal cone.");
                sensor.CloseRange = 2f;
                Assert.That(sensor.CanSee(player), Is.True, "The near hemisphere includes the ground below the elevated eye.");
            }
            sensor.CloseFieldOfView = 90f;
            Assert.That(sensor.CanSee(player), Is.False, "Narrowing the close field of view excludes the sideways player.");
            sensor.CloseFieldOfView = 180f;
            Assert.That(sensor.CanSee(player), Is.True);
            player.Warp(new Vector3(1f, 0.34f, -0.4f), Quaternion.identity);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.False, "Close detection must not extend behind the eye.");
            sensor.CloseFieldOfView = 270f;
            Assert.That(sensor.CanSee(player), Is.True, "An explicitly wider setting permits rear-side coverage.");
            sensor.CloseFieldOfView = 180f;
            eye.transform.rotation = Quaternion.LookRotation(Vector3.back);
            Assert.That(sensor.CanSee(player), Is.True, "The hemisphere follows the eye's forward direction.");
            eye.transform.rotation = Quaternion.identity;
            player.Warp(new Vector3(2.3f, 0.34f, 0.4f), Quaternion.identity);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.False, "Beyond two units, the narrow cone still applies.");
            player.Warp(new Vector3(0f, 0.34f, 4f), Quaternion.identity);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.True, "Adding a near hemisphere must preserve distant cone detection.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseRangeDetectionStillRequiresAnUnobstructedBodySample()
        {
            var sensor = SensorAt(Vector3.up * 1.5f, 2f);
            player.Warp(new Vector3(1f, 0.34f, 0.4f), Quaternion.identity);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.True);
            var wall = Solid("Close cover", new Vector3(0.5f, 1f, 0.2f), new Vector3(0.1f, 3f, 2f), false);
            Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.False, "The near hemisphere must respect walls for every body sample.");
            wall.SetActive(false); Physics.SyncTransforms();
            Assert.That(sensor.CanSee(player), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CloseRangeMismatchBuildsNormalSuspicionAndMatchedPlayerRemainsConcealed()
        {
            player.Warp(new Vector3(1f, 0.34f, 0.4f), Quaternion.identity);
            var closeSensor = SensorAt(Vector3.up * 1.5f, 2f);
            var normalSensor = SensorAt(new Vector3(1f, 0.5f, -3f));
            session.Settings.PassiveCooling = 0f;
            thermal.SetTemperature(session.Settings.Ambient);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            Assert.That(closeSensor.SeesPlayer, Is.True);
            Assert.That(closeSensor.TrackingMismatch, Is.False);
            Assert.That(closeSensor.Suspicion, Is.Zero);
            thermal.SetTemperature(session.Settings.Ambient + session.Settings.MatchTolerance + 1f);
            yield return new WaitForSeconds(0.2f);
            Assert.That(closeSensor.TrackingMismatch, Is.True);
            Assert.That(closeSensor.Suspicion, Is.GreaterThan(0f));
            Assert.That(closeSensor.Suspicion, Is.EqualTo(normalSensor.Suspicion).Within(0.0001f),
                "Close detection uses the same buildup rate as the normal cone.");
            Assert.That(closeSensor.Suspicion, Is.LessThan(session.Settings.ChaseThreshold),
                "The near hemisphere must not trigger an immediate chase.");
        }

        [UnityTest]
        public IEnumerator CameraTimingIsIndependentAndTemperatureSensitivityCanBeDisabled()
        {
            session.Settings.PassiveCooling = 0;
            session.Settings.SuspicionGain = session.Settings.GainPerDegree = session.Settings.SuspicionDecay = 0;
            thermal.SetTemperature(26); // Four degree gap, two beyond the matching tolerance.
            var ordinarySensor = SensorAt(new Vector3(0, .5f, -3));
            var sensors = new VisionSensor[3];
            for (int i = 0; i < sensors.Length; i++)
            {
                var go = ObjectAt("Independent camera " + i, new Vector3(0, .5f, -3));
                sensors[i] = go.AddComponent<VisionSensor>(); sensors[i].Session = session;
                sensors[i].ObstructionMask = 1 << WorldLayer;
                var camera = go.AddComponent<SecurityCamera>(); camera.SweepDegrees = 0; camera.ActiveFraction = 1;
                camera.BaseDetectionTime = i == 1 ? 2 : 4;
                camera.TemperatureSensitivity = i == 2 ? .25f : 0;
                camera.SuspicionRecoveryTime = i == 0 ? 4 : .5f;
                go.SetActive(true);
            }
            yield return new WaitForSeconds(.4f);
            Assert.That(ordinarySensor.Suspicion, Is.Zero, "Ordinary sensors still use the shared warden timing.");
            Assert.That(sensors[0].Suspicion, Is.GreaterThan(.08f));
            Assert.That(sensors[1].Suspicion, Is.EqualTo(sensors[0].Suspicion * 2).Within(.01f));
            Assert.That(sensors[2].Suspicion, Is.EqualTo(sensors[0].Suspicion * 3).Within(.01f));
            float previous = sensors[0].Suspicion;
            ordinarySensor.RaiseAlarm(player.transform.position);
            thermal.SetTemperature(22);
            yield return new WaitForSeconds(.2f);
            Assert.That(sensors[0].Suspicion, Is.GreaterThan(0).And.LessThan(previous));
            Assert.That(sensors[1].Suspicion, Is.Zero);
            Assert.That(sensors[2].Suspicion, Is.Zero);
            Assert.That(ordinarySensor.Suspicion, Is.EqualTo(.8f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator CameraResetsCheckpointWhileJournalIsOpen()
        {
            var go = ObjectAt("Camera", new Vector3(0, 0.5f, -3));
            var sensor = go.AddComponent<VisionSensor>(); sensor.Session = session; sensor.ObstructionMask = 1 << WorldLayer;
            var camera = go.AddComponent<SecurityCamera>(); camera.SweepDegrees = 0; camera.ActiveFraction = 1;
            go.SetActive(true);
            thermal.SetTemperature(70); session.OpenNotes();
            player.MoveInput = Vector2.up;
            Vector3 start = player.transform.position;
            yield return new WaitForSeconds(0.25f);
            Assert.That(player.transform.position, Is.EqualTo(start));
            Assert.That(sensor.Suspicion, Is.GreaterThan(0));
            yield return new WaitForSeconds(1.4f);
            Assert.That(thermal.Temperature, Is.EqualTo(22).Within(0.1));
            Assert.That(session.Screen, Is.EqualTo(ModalScreen.None));
            Assert.That(sensor.Suspicion, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CivilianContactAlertsButDoesNotDirectlyReset()
        {
            int alerts = 0; session.CivilianAlert += _ => alerts++;
            var go = ObjectAt("Civilian contact", player.transform.position);
            var body = go.AddComponent<Rigidbody>(); body.isKinematic = true;
            go.AddComponent<CivilianContact>(); go.SetActive(true);
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Assert.That(alerts, Is.EqualTo(1));
            Assert.That(session.Resetting, Is.False);
        }

        [UnityTest]
        public IEnumerator FourDigitLockRejectsWrongInputAndCompletesImmediately()
        {
            var go = ObjectAt("Lock", player.transform.position + Vector3.forward);
            var actor = go.AddComponent<LockActor>(); actor.Session = session; actor.Passcode = "7139"; actor.ObstructionMask = 1 << WorldLayer;
            go.SetActive(true); session.OpenLock(actor);
            Assert.That(actor.Submit("713"), Is.False);
            Assert.That(actor.Submit("7319"), Is.False);
            Assert.That(session.IsComplete, Is.False);
            Assert.That(actor.Submit("7139"), Is.True);
            Assert.That(session.IsComplete, Is.True);
            Assert.That(actor.Locked, Is.False);
            Assert.That(session.Screen, Is.EqualTo(ModalScreen.None));
            Assert.That(actor.HasFingerprint(7), Is.True);
            Assert.That(actor.HasFingerprint(2), Is.False);
            yield return null;
        }

        [TestCase("7139", true)]
        [TestCase("7119", false)]
        [TestCase("713", false)]
        [TestCase("abcd", false)]
        public void PasscodeRequiresFourDistinctDigits(string code, bool valid)
        { Assert.That(StealthRules.ValidCode(code), Is.EqualTo(valid)); }
    }
}
