using System.Collections.Generic;
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
            float radius, float skin, float maxAngle, LayerMask mask, Collider self,
            float faceInset = -1f, float probeDepth = -1f)
        {
            Vector3 movement = Vector3.ProjectOnPlane(attempted - start, normal);
            if (movement.sqrMagnitude < 0.000001f) return null;
            Vector3 direction = movement.normalized;
            float clearance = radius + skin;
            if (!Ray(start + normal * skin, -normal, clearance + skin * 2f, mask, self, out var oldFace) ||
                !Climbable(oldFace) || Vector3.Dot(oldFace.normal, normal) < 0.99f) return null;

            // A bevel can still be under the support ray; a right-angle edge needs a side probe.
            float depth = probeDepth > 0 ? probeDepth : Mathf.Max(radius * 0.5f, skin * 3f);
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
            float inset = faceInset > 0 ? faceInset : Mathf.Max(skin * 3f, 0.06f);

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

        /// <summary>Cross exactly one short adjoining face. Never searches across missing support.</summary>
        public static SurfaceCornerPath FindSkip(Vector3 start, Vector3 attempted, Vector3 normal,
            float radius, float skin, float maxAngle, float maximumDistance, LayerMask mask, Collider self)
        {
            if (maximumDistance <= 0) return null;
            return FindForwardSkip(start, attempted, normal, radius, skin, maxAngle, maximumDistance, mask, self) ??
                FindReverseSkip(start, attempted, normal, radius, skin, maxAngle, maximumDistance, mask, self);
        }

        static SurfaceCornerPath FindForwardSkip(Vector3 start, Vector3 attempted, Vector3 normal,
            float radius, float skin, float maxAngle, float maximumDistance, LayerMask mask, Collider self)
        {
            if (maximumDistance <= 0) return null;
            float inset = Mathf.Min(skin * .25f, .005f);
            float clearance = radius + skin;
            // A shallow lip may be thinner than the ordinary side probe's depth.
            SurfaceCornerPath first = null;
            float maxDepth = Mathf.Max(radius * .5f, skin * 3);
            for (float depth = inset; depth <= maxDepth + inset && first == null; depth += inset)
                first = Find(start, attempted, normal, radius, skin, maxAngle, mask, self, inset, depth);
            if (first == null) return null;
            Vector3 faceNormal = first.Normals[first.Normals.Length - 1];
            Vector3 edge = first.Points[1] - normal * clearance;
            Vector3 along = -Vector3.ProjectOnPlane(normal, faceNormal).normalized;

            // A narrow step ends at a concave corner. Stop the first arc before it
            // pushes the body into the next wall, then attach above that corner.
            if (Ray(edge + faceNormal * (skin * 2), along, maximumDistance + skin, mask, self, out var wall) &&
                Vector3.Dot(wall.normal, along) < -.2f)
            {
                if (!Climbable(wall)) return null;
                float angle = Vector3.Angle(faceNormal, wall.normal);
                if (angle < 5 || angle > maxAngle) return null;
                float distance = Vector3.Dot(wall.normal, wall.point - edge) / Vector3.Dot(wall.normal, along);
                if (distance <= inset || distance > maximumDistance ||
                    !SupportedStrip(edge, along, faceNormal, distance, inset, mask, self, skin)) return null;
                Vector3 nextEdge = edge + along * distance;
                Vector3 exit = Vector3.ProjectOnPlane(faceNormal, wall.normal).normalized;
                float lift = clearance * (1 - Vector3.Dot(faceNormal, wall.normal)) / Vector3.Dot(faceNormal, exit);
                Vector3 landing = nextEdge + wall.normal * clearance + exit * (lift + skin * 3);
                if (!Touches(nextEdge + exit * inset, wall.normal, mask, self, skin) ||
                    !Touches(landing - wall.normal * clearance, wall.normal, mask, self, skin)) return null;
                var points = new List<Vector3> { start };
                var normals = new List<Vector3> { normal };
                for (int i = 1; i < first.Points.Length - 1; i++)
                {
                    if (Vector3.Dot(first.Points[i] - wall.point, wall.normal) < clearance) break;
                    points.Add(first.Points[i]); normals.Add(first.Normals[i]);
                }
                points.Add(landing); normals.Add(wall.normal);
                return new SurfaceCornerPath(points.ToArray(), normals.ToArray());
            }

            // Otherwise find the far convex edge of a thin cap. Sampling the whole
            // strip proves the faces are connected; a separate platform is not a landing.
            float previous = inset;
            float step = Mathf.Min(skin, .02f);
            for (float distance = inset + step; distance <= maximumDistance + step; distance += step)
            {
                if (Touches(edge + along * distance, faceNormal, mask, self, skin))
                { previous = distance; continue; }
                Vector3 from = edge + along * previous + faceNormal * clearance;
                Vector3 to = edge + along * distance + faceNormal * clearance;
                var second = Find(from, to, faceNormal, radius, skin, maxAngle, mask, self, inset);
                if (second == null) return null;
                Vector3 nextEdge = second.Points[1] - faceNormal * clearance;
                if (Vector3.Distance(edge, nextEdge) > maximumDistance ||
                    !Touches(second.Points[second.Points.Length - 1] - second.Normals[second.Normals.Length - 1] * clearance,
                        second.Normals[second.Normals.Length - 1], mask, self, skin)) return null;
                var points = new List<Vector3>(first.Points);
                var normals = new List<Vector3>(first.Normals);
                for (int i = 1; i < second.Points.Length; i++)
                { points.Add(second.Points[i]); normals.Add(second.Normals[i]); }
                return new SurfaceCornerPath(points.ToArray(), normals.ToArray());
            }
            return null;
        }

        // Concave entry followed by a convex exit is the same ledge in reverse.
        // Find its far face geometrically, plan back to the current face, then reverse
        // the validated candidate. No recursive search or second skipped face is allowed.
        static SurfaceCornerPath FindReverseSkip(Vector3 start, Vector3 attempted, Vector3 normal,
            float radius, float skin, float maxAngle, float maximumDistance, LayerMask mask, Collider self)
        {
            Vector3 movement = Vector3.ProjectOnPlane(attempted - start, normal);
            if (movement.sqrMagnitude < .000001f) return null;
            float clearance = radius + skin, inset = Mathf.Min(skin * .25f, .005f);
            Vector3 direction = movement.normalized;
            if (!Ray(start + normal * skin, -normal, clearance + skin * 2, mask, self, out var oldFace) ||
                !Climbable(oldFace) || Vector3.Dot(oldFace.normal, normal) < .99f ||
                !Ray(oldFace.point + normal * inset, direction, clearance + movement.magnitude + skin,
                    mask, self, out var face) || !Climbable(face) || Vector3.Dot(face.normal, direction) > -.2f)
                return null;
            float angle = Vector3.Angle(normal, face.normal);
            if (angle < 5 || angle > maxAngle) return null;
            Vector3 across = Vector3.ProjectOnPlane(face.normal, normal).normalized;
            Vector3 edge = oldFace.point + across * (Vector3.Dot(face.normal, face.point - oldFace.point) /
                Vector3.Dot(face.normal, across));
            Vector3 along = Vector3.ProjectOnPlane(normal, face.normal).normalized;
            if (!Touches(edge - direction * inset, normal, mask, self, skin) ||
                !Touches(edge + along * inset, face.normal, mask, self, skin)) return null;
            float previous = inset, step = Mathf.Min(skin, .02f);
            for (float distance = inset + step; distance <= maximumDistance + step; distance += step)
            {
                if (Touches(edge + along * distance, face.normal, mask, self, skin))
                { previous = distance; continue; }
                var turn = Find(edge + along * previous + face.normal * clearance,
                    edge + along * distance + face.normal * clearance, face.normal,
                    radius, skin, maxAngle, mask, self, inset);
                if (turn == null || Vector3.Distance(turn.Points[1] - face.normal * clearance, edge) > maximumDistance) return null;
                int last = turn.Points.Length - 1;
                Vector3 destination = turn.Points[last], destinationNormal = turn.Normals[last];
                Vector3 returnDirection = Vector3.ProjectOnPlane(face.normal, destinationNormal).normalized;
                var reverse = FindForwardSkip(destination, destination + returnDirection * step, destinationNormal,
                    radius, skin, maxAngle, maximumDistance, mask, self);
                if (reverse == null) return null;
                int end = reverse.Points.Length - 1;
                if (Vector3.Dot(reverse.Normals[end], normal) < .99f ||
                    Mathf.Abs(Vector3.Dot(reverse.Points[end] - normal * clearance - oldFace.point, normal)) > skin) return null;
                var points = new List<Vector3> { start };
                var normals = new List<Vector3> { normal };
                for (int i = end; i >= 0; i--)
                { points.Add(reverse.Points[i]); normals.Add(reverse.Normals[i]); }
                return new SurfaceCornerPath(points.ToArray(), normals.ToArray());
            }
            return null;
        }

        static bool SupportedStrip(Vector3 edge, Vector3 along, Vector3 normal, float distance,
            float inset, LayerMask mask, Collider self, float skin)
        {
            int samples = Mathf.Max(1, Mathf.CeilToInt(distance / Mathf.Min(skin, .02f)));
            for (int i = 0; i <= samples; i++)
                if (!Touches(edge + along * Mathf.Lerp(inset, distance - inset, i / (float)samples), normal, mask, self, skin)) return false;
            return true;
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
