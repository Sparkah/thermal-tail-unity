using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Kinematic spherical body, swept movement, surface adhesion, no jumping.
    /// Adjoining corners attach automatically; authored links remain available for special routes.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    [DefaultExecutionOrder(-50)]
    public sealed class SurfaceMotor : MonoBehaviour
    {
        public PrototypeSession Session;
        public LayerMask SolidMask = ~0;
        [Min(0.01f)] public float Skin = 0.02f;
        public bool AutomaticCorners = true;
        [Range(10f, 135f)] public float MaximumCornerAngle = 110f;
        [Tooltip("If the ordinary corner cannot fit, try crossing one short adjoining surface to the next clear face.")]
        public bool SkipNarrowSurfaces = true;
        [Min(0f), Tooltip("Maximum distance along the one skipped face. Requires connected climbable surfaces and body clearance throughout. Zero disables skipping.")]
        public float MaximumSkipDistance = .75f;
        public Vector2 MoveInput { get; set; }
        public float Heading { get; set; }
        public Vector3 Velocity { get; private set; }
        public Vector3 SurfaceNormal { get; private set; } = Vector3.up;
        public float Radius => bodyCollider != null ? bodyCollider.radius : 0.32f;
        public bool InTransition => transition != null || corner != null;
        Rigidbody body;
        SphereCollider bodyCollider;
        Vector3 tangentForward = Vector3.forward;
        SurfaceTransition transition;
        float transitionProgress;
        SurfaceCornerPath corner;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<SphereCollider>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            if (Session == null) Session = FindFirstObjectByType<PrototypeSession>();
            SurfaceNormal = transform.up;
            tangentForward = transform.forward;
        }

        public Vector3 ViewForward => Quaternion.AngleAxis(Heading, SurfaceNormal) * tangentForward;

        void FixedUpdate()
        {
            Velocity = Vector3.zero;
            if (Session == null || Session.PlayerInputBlocked) return;
            if (transition != null) { AdvanceTransition(); return; }
            if (corner != null) { AdvanceCorner(); return; }
            Vector3 start = body.position;
            Vector3 forward = ViewForward;
            Vector3 right = Vector3.Cross(SurfaceNormal, forward).normalized;
            Vector3 direction = Vector3.ClampMagnitude(forward * MoveInput.y + right * MoveInput.x, 1f);
            float speed = Vector3.Dot(SurfaceNormal, Vector3.up) > 0.7f
                ? Session.Settings.MoveSpeed : Session.Settings.ClimbSpeed;
            Vector3 delta = direction * speed * Time.fixedDeltaTime;
            Vector3 position = start;
            Vector3 normal = SurfaceNormal;

            for (int i = 0; i < 3 && delta.sqrMagnitude > 0.000001f; i++)
            {
                if (!Sweep(position, delta, out RaycastHit hit)) { position += delta; break; }
                float travelled = Mathf.Max(0f, hit.distance - Skin);
                position += delta.normalized * travelled;
                Vector3 remainder = delta.normalized * Mathf.Max(0f, delta.magnitude - travelled);
                if (hit.collider.GetComponentInParent<ClimbableSurface>() != null &&
                    Vector3.Dot(hit.normal, normal) < 0.8f && Vector3.Dot(delta.normalized, -hit.normal) > 0.5f)
                {
                    Quaternion turn = Quaternion.FromToRotation(normal, hit.normal);
                    normal = hit.normal;
                    delta = turn * remainder;
                }
                else delta = Vector3.ProjectOnPlane(remainder, hit.normal);
            }

            // Flat support is ordinary movement; a departing edge can start a convex turn.
            bool supported = false;
            if (Physics.Raycast(position + normal * 0.12f, -normal, out RaycastHit support,
                Radius + 0.32f, SolidMask, QueryTriggerInteraction.Ignore) &&
                support.collider.GetComponentInParent<ClimbableSurface>() != null &&
                Vector3.Dot(support.normal, normal) > 0.99f)
            {
                Vector3 snapped = support.point + normal * (Radius + Skin);
                Vector3 correction = snapped - position;
                // A support ray can see past a thin lip; don't snap the body through it.
                if (ClearBody(snapped) && (correction.sqrMagnitude < .000001f ||
                    !Sweep(position, correction, out _, support.collider)))
                { position = snapped; SetNormal(normal); supported = true; }
            }
            if (!supported)
            {
                if (AutomaticCorners && TryCorner(start, position)) { AdvanceCorner(); return; }
                position = start;
            }
            Velocity = (position - start) / Time.fixedDeltaTime;
            body.MovePosition(position);
            Vector3 look = Vector3.ProjectOnPlane(Velocity, SurfaceNormal);
            if (look.sqrMagnitude < 0.001f) look = ViewForward;
            body.MoveRotation(Quaternion.LookRotation(look.normalized, SurfaceNormal));
        }

        bool Sweep(Vector3 position, Vector3 delta, out RaycastHit nearest, Collider supportToEscape = null)
        {
            nearest = default;
            float distance = float.PositiveInfinity;
            // Ignore our own body explicitly; query triggers never block movement.
            foreach (var hit in Physics.SphereCastAll(position, Radius - Skin, delta.normalized,
                delta.magnitude + Skin, SolidMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == bodyCollider || hit.distance >= distance) continue;
                // An editor placement can start slightly inside its supporting floor.
                // Unity reports initial overlap as a zero-distance hit opposing the cast.
                // Permit only a complete outward correction from that support, never a
                // sweep through a different obstacle or a normal movement/corner sweep.
                if (hit.distance == 0f && hit.collider == supportToEscape &&
                    Physics.ComputePenetration(bodyCollider, position, body.rotation,
                        hit.collider, hit.collider.transform.position, hit.collider.transform.rotation,
                        out Vector3 escape, out float penetration) &&
                    Vector3.Dot(delta.normalized, escape) > .999f && delta.magnitude >= penetration)
                    continue;
                // Contacts tangent to movement must not stall surface sliding.
                if (Vector3.Dot(delta.normalized, hit.normal) >= -0.001f) continue;
                nearest = hit; distance = hit.distance;
            }
            return distance < float.PositiveInfinity;
        }

        bool ClearBody(Vector3 position)
        {
            foreach (var hit in Physics.OverlapSphere(position, Radius - Skin, SolidMask, QueryTriggerInteraction.Ignore))
                if (hit != bodyCollider) return false;
            return true;
        }

        bool TryCorner(Vector3 start, Vector3 attempted)
        {
            var path = SurfaceCornerPath.Find(start, attempted, SurfaceNormal, Radius, Skin,
                MaximumCornerAngle, SolidMask, bodyCollider);
            if (CanTraverse(path)) { corner = path; return true; }
            if (!SkipNarrowSurfaces) return false;
            path = SurfaceCornerPath.FindSkip(start, attempted, SurfaceNormal, Radius, Skin,
                MaximumCornerAngle, MaximumSkipDistance, SolidMask, bodyCollider);
            if (!CanTraverse(path)) return false;
            corner = path;
            return true;
        }

        bool CanTraverse(SurfaceCornerPath path)
        {
            if (path == null) return false;
            // Validate every chord before committing, including clearance around the new face.
            for (int i = 1; i < path.Points.Length; i++)
            {
                Vector3 delta = path.Points[i] - path.Points[i - 1];
                if (!ClearBody(path.Points[i]) || (delta.sqrMagnitude > 0.000001f &&
                    Sweep(path.Points[i - 1], delta, out _))) return false;
            }
            return true;
        }

        void AdvanceCorner()
        {
            Vector3 start = body.position, position = start;
            float remaining = Session.Settings.ClimbSpeed * Time.fixedDeltaTime;
            while (corner != null && remaining > 0f)
            {
                Vector3 target = corner.Points[corner.Next];
                float distance = Vector3.Distance(position, target);
                float step = Mathf.Min(distance, remaining);
                Vector3 next = Vector3.MoveTowards(position, target, step);
                Vector3 delta = next - position;
                // Recheck during travel for moving actors/obstacles; wait at the last clear position.
                if (!ClearBody(next) || (delta.sqrMagnitude > 0.000001f && Sweep(position, delta, out _))) break;
                SetNormal(Vector3.Slerp(SurfaceNormal, corner.Normals[corner.Next], distance > 0.00001f ? step / distance : 1f));
                position = next;
                remaining -= step;
                if (distance <= step + 0.00001f && ++corner.Next >= corner.Points.Length) corner = null;
            }
            Velocity = (position - start) / Time.fixedDeltaTime;
            body.MovePosition(position);
            body.MoveRotation(Quaternion.LookRotation(ViewForward, SurfaceNormal));
        }

        void SetNormal(Vector3 normal)
        {
            tangentForward = Quaternion.FromToRotation(SurfaceNormal, normal) * tangentForward;
            SurfaceNormal = normal.normalized;
            tangentForward = Vector3.ProjectOnPlane(tangentForward, SurfaceNormal).normalized;
        }

        public bool BeginTransition(SurfaceTransition link)
        {
            if (InTransition || Session.PlayerInputBlocked || link.Exit == null || link.Entry == null) return false;
            transition = link;
            transitionProgress = 0f;
            transition.BeginPosition = body.position;
            return true;
        }

        void AdvanceTransition()
        {
            float t = Mathf.Min(1f, transitionProgress + Time.fixedDeltaTime / Mathf.Max(0.1f, transition.Duration));
            Vector3 control = transition.Control != null ? transition.Control.position
                : (transition.BeginPosition + transition.Exit.position) * 0.5f;
            Vector3 end = transition.Exit.position;
            Vector3 next = (1 - t) * (1 - t) * transition.BeginPosition + 2 * (1 - t) * t * control + t * t * end;
            Vector3 delta = next - body.position;
            if (delta.sqrMagnitude > 0.000001f && Sweep(body.position, delta, out _))
            {
                Session.Notify("Climb route obstructed"); transition = null; return;
            }
            Velocity = delta / Time.fixedDeltaTime;
            body.MovePosition(next);
            body.MoveRotation(Quaternion.Slerp(transition.Entry.rotation, transition.Exit.rotation, t));
            transitionProgress = t;
            if (t >= 1f)
            {
                SurfaceNormal = transition.Exit.up;
                tangentForward = transition.Exit.forward;
                Heading = 0f;
                transition = null;
            }
        }

        public void Warp(Vector3 position, Quaternion rotation)
        {
            transition = null;
            corner = null;
            SurfaceNormal = rotation * Vector3.up;
            tangentForward = rotation * Vector3.forward;
            Heading = 0f;
            MoveInput = Vector2.zero;
            Velocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
