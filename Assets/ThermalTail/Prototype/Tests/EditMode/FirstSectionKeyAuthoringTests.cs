using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.Prototype.Tests
{
    public class FirstSectionKeyAuthoringTests
    {
        [Test]
        public void KeysDoorAndNewWardenAreConnectedPrefabsWithSceneReferences()
        {
            EditorSceneManager.OpenScene("Assets/ThermalTail/Prototype/Scenes/ThermalTailLevel.unity");
            try
            {
                var keys = Object.FindObjectsByType<KeyCollectible>(FindObjectsSortMode.None);
                var door = Object.FindFirstObjectByType<KeyDoor>();
                var warden = GameObject.Find("Warden · opposite square civilian").GetComponent<NpcMotor>();
                Assert.That(keys.Length, Is.EqualTo(8));
                foreach (var component in keys.Cast<Component>().Concat(new Component[] { door, warden }))
                {
                    Assert.That(PrefabUtility.GetPrefabInstanceStatus(component), Is.EqualTo(PrefabInstanceStatus.Connected));
                    Assert.That(component.transform.root.name, Does.StartWith("01"));
                    Assert.That(component.transform.localScale, Is.EqualTo(Vector3.one));
                }
                Assert.That(door.Barrier.GetComponent<BoxCollider>().isTrigger, Is.False);
                Assert.That(door.Barrier.GetComponent<UnityEngine.AI.NavMeshObstacle>().carving, Is.True);
                Assert.That(door.StatusText.GetComponent<WorldTextMaterial>().DepthTestedMaterial, Is.Not.Null);
                Assert.That(door.Session, Is.Not.Null);
                Assert.That(warden.Session, Is.EqualTo(door.Session));
                Assert.That(keys.All(k => k.Session == door.Session), Is.True);
            }
            finally { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }
        }
    }
}
