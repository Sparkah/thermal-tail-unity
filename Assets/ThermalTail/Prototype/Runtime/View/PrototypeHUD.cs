using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class PrototypeHUD : MonoBehaviour
    {
        public PrototypeSession Session;
        public PrototypeControls Controls;
        VisionSensor[] sensors;
        CivilianSuspicion[] civilians;
        void Start()
        {
            sensors = FindObjectsByType<VisionSensor>(FindObjectsSortMode.None);
            civilians = FindObjectsByType<CivilianSuspicion>(FindObjectsSortMode.None);
        }
        void OnGUI()
        {
            if (Session == null || Session.Thermal == null) return;
            var thermal = Session.Thermal;
            GUI.Box(new Rect(12, 12, 340, 190), "THERMAL TAIL — " + Session.Section);
            GUI.Label(new Rect(26, 42, 310, 25), $"BODY {thermal.Temperature:0.0}     TARGET {thermal.TargetTemperature:0.0}");
            GUI.Label(new Rect(26, 68, 320, 25), thermal.Context);
            GUI.Label(new Rect(26, 94, 310, 25), thermal.Matched ? "THERMALLY MATCHED" : $"MISMATCH {thermal.Mismatch:0.0} — find a vent");
            GUI.Label(new Rect(26, 120, 310, 25), $"CLUES {Session.Notes.Count}     MOTHS {Session.MothsCollected}");
            GUI.Label(new Rect(26, 146, 310, 25), Session.GraceRemaining > 0 ? $"RECOVERY GRACE {Session.GraceRemaining:0.0}s" : "Civilian contact alerts security");
            GUI.Label(new Rect(26, 172, 310, 25), "WASD move · Mouse look · J notes");
            if (Session.MessageUntil > Time.time)
                GUI.Label(new Rect(20, Screen.height - 90, Screen.width - 40, 32), Session.Message,
                    new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
            if (Session.Screen == ModalScreen.None && Controls != null && Controls.NearbyLock != null)
                GUI.Label(new Rect(Screen.width / 2f - 100, Screen.height - 140, 260, 30), "E — use vault keypad");
            else if (Session.Screen == ModalScreen.None && Controls != null && Controls.NearbyTakedown != null)
                GUI.Label(new Rect(Screen.width / 2f - 100, Screen.height - 140, 260, 30), "E — take down warden");
            DrawSensorIndicators();
            DrawCivilianIndicators();
            if (Session.IsComplete)
                GUI.Box(new Rect(Screen.width / 2f - 230, Screen.height / 2f - 40, 460, 80), "MISSION COMPLETE\nThe Inner Vault is open.");
        }
        void DrawSensorIndicators()
        {
            var viewCamera = Camera.main;
            if (sensors == null || viewCamera == null) return;
            foreach (var sensor in sensors)
            {
                if (sensor == null || !sensor.isActiveAndEnabled) continue;
                var warden = sensor.GetComponent<WardenBrain>();
                if (warden == null && sensor.Suspicion <= 0f) continue;
                Vector3 point = viewCamera.WorldToScreenPoint(sensor.transform.position + Vector3.up * 1.7f);
                if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                // IMGUI has no scene depth test: hide indicators when their actor is behind geometry.
                if (!VisionSensor.Unobstructed(viewCamera.transform.position, sensor.Origin.position,
                    sensor.ObstructionMask, viewCamera.transform, sensor.transform)) continue;
                float x = point.x - 55, y = Screen.height - point.y;
                if (warden != null)
                    GUI.Label(new Rect(x - 10, y - 23, 150, 24), warden.Mode.ToString().ToUpperInvariant());
                Color old = GUI.color;
                GUI.color = Color.black; GUI.DrawTexture(new Rect(x, y, 110, 9), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.yellow, Color.red, sensor.Suspicion);
                GUI.DrawTexture(new Rect(x, y, 110 * sensor.Suspicion, 9), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }
        void DrawCivilianIndicators()
        {
            var viewCamera = Camera.main;
            if (civilians == null || viewCamera == null) return;
            foreach (var civilian in civilians)
            {
                if (civilian == null || !civilian.isActiveAndEnabled || civilian.Suspicion <= 0) continue;
                var point = viewCamera.WorldToScreenPoint(civilian.transform.position + Vector3.up * 1.7f);
                if (point.z <= 0 || point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                if (!VisionSensor.Unobstructed(viewCamera.transform.position, civilian.EyePosition,
                    civilian.ObstructionMask, viewCamera.transform, civilian.transform)) continue;
                float x = point.x - 55, y = Screen.height - point.y;
                GUI.Label(new Rect(x - 20, y - 23, 160, 24), civilian.Suspicion >= 1 ? "SECURITY ALERTED" : "SUSPICIOUS");
                Color previous = GUI.color;
                GUI.color = Color.black; GUI.DrawTexture(new Rect(x, y, 110, 9), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(Color.yellow, Color.red, civilian.Suspicion);
                GUI.DrawTexture(new Rect(x, y, 110 * civilian.Suspicion, 9), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }
    }
}
