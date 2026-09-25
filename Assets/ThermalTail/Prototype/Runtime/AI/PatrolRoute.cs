using UnityEngine;

namespace ThermalTail.Prototype
{
    public sealed class PatrolRoute : MonoBehaviour
    {
        public Transform[] Points = new Transform[0];
        public bool Loop = true;
        [Min(0)] public float WaitSeconds = 1f;
        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < Points.Length; i++)
            {
                if (Points[i] == null) continue;
                Gizmos.DrawWireSphere(Points[i].position, 0.15f);
                int next = (i + 1) % Points.Length;
                if ((Loop || next != 0) && Points[next] != null) Gizmos.DrawLine(Points[i].position, Points[next].position);
            }
        }
    }
}
