using UnityEngine;

namespace ThermalTail.Prototype
{
    /// <summary>Builds a short path around the intersection of two adjoining, planar climbable faces.</summary>
    internal sealed class SurfaceCornerPath
    {
        public readonly Vector3[] Points, Normals;
        public int Next = 1;

        SurfaceCornerPath(Vector3[] points, Vector3[] normals) { Points = points; Normals = normals; }

        internal static bool Ray(Vector3 from, Vector3 direction, float distance, LayerMask mask,
            Collider self, out RaycastHit nearest)
        {
            nearest = default;
            float closest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(from, direction, distance, mask, QueryTriggerInteraction.Ignore))
                if (hit.collider != self && hit.distance < closest) { nearest = hit; closest = hit.distance; }
            return closest < float.PositiveInfinity;
        }

        static bool Climbable(RaycastHit hit) => hit.collider.GetComponentInParent<ClimbableSurface>() != null;

        public static SurfaceCornerPath Find(Vector3 start, Vector3 attempted, Vector3 normal,
            float radius, float skin, float maxAngle, LayerMask mask, Collider self)
        {
            Vector3 movement = Vector3.ProjectOnPlane(attempted - start, normal);
            if (movement.sqrMagnitude < 0.000001f) return null;
            Vector3 direction = movement.normalized;
            float clearance = radius + skin;
            if (!Ray(start + normal * skin, -normal, clearance + skin * 2f, mask, self, out var oldFace) ||
                !Climbable(oldFace) || Vector3.Dot(oldFace.normal, normal) < 0.99f) return null;

            // A bevel can still be under the support ray; a right-angle edge needs a side probe.
            float depth = Mathf.Max(radius * 0.5f, skin * 3f);
            bool found = Ray(attempted + normal * skin, -normal, clearance + depth, mask, self, out var face) &&
                Vector3.Dot(face.normal, normal) < 0.99f;
            if (!found)
            {
                Vector3 probe = attempted - normal * (clearance + depth) + direction * clearance * 2f;
                if (!Ray(probe, -direction, clearance * 3f, mask, self, out face)) return null;
            }
            if (!Climbable(face)) return null;
            float angle = Vector3.Angle(normal, face.normal);
            if (angle < 5f || angle > maxAngle || Vector3.Dot(face.normal, direction) < 0.2f) return null;

            // Intersect the two face planes, choosing the closest point on their shared edge.
            Vector3 across = Vector3.ProjectOnPlane(face.normal, normal).normalized;
            Vector3 edge = oldFace.point + across * (Vector3.Dot(face.normal, face.point - oldFace.point) /
                Vector3.Dot(face.normal, across));
            if (Vector3.Distance(edge, oldFace.point) > movement.magnitude + skin * 4f) return null;
            Vector3 exitDirection = -Vector3.ProjectOnPlane(normal, face.normal).normalized;
            float inset = Mathf.Max(skin * 3f, 0.06f);

            // Both faces must actually reach the edge. Nearby separate platforms are not links.
            if (!Touches(edge - across * inset, normal, mask, self, skin) ||
                !Touches(edge + exitDirection * inset, face.normal, mask, self, skin)) return null;

            int segments = Mathf.CeilToInt(angle / 5f);
            var points = new Vector3[segments + 3];
            var normals = new Vector3[points.Length];
            points[0] = start; normals[0] = normal;
            for (int i = 0; i <= segments; i++)
            {
                Vector3 n = Vector3.Slerp(normal, face.normal, i / (float)segments).normalized;
                points[i + 1] = edge + n * clearance;
                normals[i + 1] = n;
            }
            points[points.Length - 1] = edge + face.normal * clearance + exitDirection * inset;
            normals[normals.Length - 1] = face.normal;
            return new SurfaceCornerPath(points, normals);
        }

        static bool Touches(Vector3 point, Vector3 normal, LayerMask mask, Collider self, float skin)
        {
            float reach = Mathf.Max(skin * 2f, 0.04f);
            return Ray(point + normal * reach, -normal, reach * 2f, mask, self, out var hit) &&
                Climbable(hit) && Vector3.Dot(hit.normal, normal) > 0.99f &&
                Vector3.Distance(hit.point, point) < skin;
        }
    }
}
