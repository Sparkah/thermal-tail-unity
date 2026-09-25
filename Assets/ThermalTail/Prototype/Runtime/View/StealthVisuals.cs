using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Runtime rings and a ray-clipped sight cone. These are presentation, never physics geometry.</summary>
    public sealed class StealthVisuals : MonoBehaviour
    {
        public CrowdZone Crowd;
        public CivilianContact PersonalSpace;
        public CaptureContact Capture;
        public VisionSensor Sensor;
        LineRenderer outer, band, inner, cone, awareness, closeVision;
        WardenBrain warden;
        CivilianSuspicion civilian;
        Material material;
        MaterialPropertyBlock colors;
        void Awake()
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            colors = new MaterialPropertyBlock();
            outer = Make("Crowd thermal boundary", Color.cyan);
            band = Make("Transition boundary", new Color(0.45f, 0.8f, 0.65f));
            inner = Make("Contact boundary", new Color(1f, 0.25f, 0.12f));
            cone = Make("Sight cone", Color.yellow);
            awareness = Make("Chase awareness", Color.red);
            closeVision = Make("Close-range vision", Color.yellow);
            warden = GetComponent<WardenBrain>();
            civilian = GetComponent<CivilianSuspicion>();
        }
        LineRenderer Make(string label, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.startColor = line.endColor = color;
            Tint(line, color);
            line.widthMultiplier = 0.025f;
            line.useWorldSpace = true;
            return line;
        }
        void LateUpdate()
        {
            outer.enabled = band.enabled = Crowd != null;
            if (Crowd != null)
            {
                Tint(outer, civilian != null && civilian.isActiveAndEnabled && civilian.Suspicion > 0
                    ? Color.Lerp(Color.yellow, Color.red, civilian.Suspicion) : Color.cyan);
                Ring(outer, Crowd.transform.position, Crowd.WorldRadius);
                Ring(band, Crowd.transform.position, Crowd.WorldRadius + Crowd.WorldBand);
            }
            inner.enabled = PersonalSpace != null || Capture != null;
            if (PersonalSpace != null) Ring(inner, PersonalSpace.transform.position, PersonalSpace.PersonalRadius);
            else if (Capture != null) Ring(inner, Capture.transform.position, Capture.GetComponent<SphereCollider>().radius);
            cone.enabled = Sensor != null;
            if (Sensor != null) DrawCone();
            closeVision.enabled = Sensor != null && Sensor.CloseRange > 0f;
            if (closeVision.enabled) DrawCloseVision();
            awareness.enabled = Sensor != null && warden != null && warden.Mode == WardenMode.Chase;
            if (awareness.enabled) Ring(awareness, transform.position, Mathf.Min(Sensor.Range, warden.ChaseAwarenessRadius));
        }
        void Ring(LineRenderer line, Vector3 center, float radius)
        {
            center.y = transform.position.y + 0.045f;
            line.loop = true; line.positionCount = 64;
            for (int i = 0; i < 64; i++)
            { float angle = i * Mathf.PI * 2 / 64; line.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius); }
        }
        void DrawCone()
        {
            Transform eye = Sensor.Origin;
            Tint(cone, SensorColor());
            const int rays = 40;
            cone.loop = false; cone.positionCount = rays * 3;
            for (int i = 0; i < rays; i++)
            {
                float angle = i * Mathf.PI * 2f / rays;
                float spread = Mathf.Tan(Sensor.FieldOfView * 0.5f * Mathf.Deg2Rad);
                Vector3 direction = (eye.forward + spread * (eye.right * Mathf.Cos(angle) + eye.up * Mathf.Sin(angle))).normalized;
                float length = Sensor.Range;
                if (Physics.Raycast(eye.position, direction, out var hit, length, Sensor.ObstructionMask, QueryTriggerInteraction.Ignore)) length = hit.distance;
                cone.SetPosition(i * 3, eye.position);
                cone.SetPosition(i * 3 + 1, eye.position + direction * length);
                cone.SetPosition(i * 3 + 2, eye.position);
            }
        }
        Color SensorColor() => !Sensor.Active ? Color.gray : warden != null && warden.Mode == WardenMode.Chase
            ? Color.red : warden != null && warden.Mode == WardenMode.Suspicious
            ? new Color(1f, 0.5f, 0f) : Color.Lerp(Color.yellow, Color.red, Sensor.Suspicion);

        void DrawCloseVision()
        {
            Transform eye = Sensor.Origin;
            Tint(closeVision, SensorColor());
            // Two arcs show horizontal and vertical coverage, including 180-degree and wider angles.
            const int segments = 32;
            closeVision.loop = false;
            closeVision.positionCount = 2 * (segments + 3);
            for (int plane = 0; plane < 2; plane++)
            {
                int first = plane * (segments + 3);
                closeVision.SetPosition(first, eye.position);
                Vector3 across = plane == 0 ? eye.right : eye.up;
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (-Sensor.CloseFieldOfView * 0.5f + Sensor.CloseFieldOfView * i / segments) * Mathf.Deg2Rad;
                    Vector3 direction = eye.forward * Mathf.Cos(angle) + across * Mathf.Sin(angle);
                    float length = Sensor.CloseRange;
                    if (Physics.Raycast(eye.position, direction, out var hit, length, Sensor.ObstructionMask,
                        QueryTriggerInteraction.Ignore)) length = hit.distance;
                    closeVision.SetPosition(first + i + 1, eye.position + direction * length);
                }
                closeVision.SetPosition(first + segments + 2, eye.position);
            }
        }
        void Tint(LineRenderer line, Color color)
        {
            colors.SetColor("_BaseColor", color);
            colors.SetColor("_Color", color);
            line.SetPropertyBlock(colors);
        }
        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
