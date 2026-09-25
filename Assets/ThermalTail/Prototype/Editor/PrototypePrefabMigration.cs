using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThermalTail.Prototype.Editor
{
    /// <summary>Connects the original example's loose actors without regenerating its layout.</summary>
    public static class PrototypePrefabMigration
    {
        [MenuItem("Tools/Thermal Tail/3D Prototype/Connect Example Prefabs")]
        static void FromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Connect();
        }

        public static void Connect()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeSceneBuilder.ScenePath);
            var actors = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject).Where(go => PrefabName(go) != null && !PrefabUtility.IsPartOfPrefabInstance(go)).ToArray();
            if (actors.Length == 0) { Debug.Log("[Thermal Tail 3D] Example prefabs are already connected."); return; }

            // The old generator embedded example identities in the reusable pickup/camera assets.
            if (actors.Any(go => go.GetComponent<NoteActor>() != null))
                EditPrefab("NoteActor", go => go.GetComponent<NoteActor>().Note = null);
            if (actors.Any(go => go.GetComponent<MothCollectible>() != null))
                EditPrefab("MothCollectible", go => go.GetComponent<MothCollectible>().CollectionId = "");
            if (actors.Any(go => go.GetComponent<SecurityCamera>() != null))
                EditPrefab("SecurityCamera", go => go.GetComponent<SecurityCamera>().PhaseOffset = 0f);
            var player = actors.FirstOrDefault(go => go.GetComponent<PrototypeControls>() != null);
            if (player != null)
            {
                // Preserve mouse tuning that previously existed only on the loose scene player.
                var controls = player.GetComponent<PrototypeControls>();
                EditPrefab("Player3D", go =>
                {
                    go.GetComponent<PrototypeControls>().HorizontalSensitivity = controls.HorizontalSensitivity;
                    go.GetComponent<PrototypeControls>().VerticalSensitivity = controls.VerticalSensitivity;
                });
            }

            foreach (var go in actors)
            {
                string path = PrefabPath(PrefabName(go));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing prototype prefab: " + path);
                PrefabUtility.ConvertToPrefabInstance(go, prefab, new ConvertToPrefabInstanceSettings
                {
                    objectMatchMode = ObjectMatchMode.ByHierarchy,
                    recordPropertyOverridesOfMatches = true,
                    componentsNotMatchedBecomesOverride = true,
                    gameObjectsNotMatchedBecomesOverride = true,
                    changeRootNameToAssetName = false
                }, InteractionMode.AutomatedAction);
                // The warden asset is the tuned source; old loose objects still held script defaults.
                if (go.TryGetComponent<WardenBrain>(out var warden))
                    PrefabUtility.RevertObjectOverride(warden, InteractionMode.AutomatedAction);
            }
            PrototypeSceneBuilder.RecordSceneOverrides(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Thermal Tail 3D] Connected " + actors.Length + " existing actors to their prefabs.");
        }

        public static string PrefabName(GameObject go)
        {
            if (go.GetComponent<SurfaceMotor>() != null) return "Player3D";
            if (go.GetComponent<NpcMotor>() != null) return go.GetComponent<WardenBrain>() != null ? "Warden" : "Civilian";
            if (go.GetComponent<NoteActor>() != null) return "NoteActor";
            if (go.GetComponent<MothCollectible>() != null) return "MothCollectible";
            if (go.GetComponent<SecurityCamera>() != null) return "SecurityCamera";
            if (go.GetComponent<LockActor>() != null) return "LockActor";
            if (go.GetComponent<SafeZone>() != null && go.transform.Find("Safe area floor") != null &&
                go.TryGetComponent<ThermalVolume>(out var thermal))
                return thermal.TargetTemperature > 25f ? "HeatingShelter" : "CoolingShelter";
            return null;
        }

        public static string PrefabPath(string name) => PrototypeSceneBuilder.Root + "/Prefabs/" + name + ".prefab";

        static void EditPrefab(string name, Action<GameObject> edit)
        {
            string path = PrefabPath(name);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try { edit(contents); PrefabUtility.SaveAsPrefabAsset(contents, path); }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
