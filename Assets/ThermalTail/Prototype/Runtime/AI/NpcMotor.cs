using UnityEngine;
using UnityEngine.AI;

namespace ThermalTail.Prototype
{
    [RequireComponent(typeof(NavMeshAgent), typeof(Rigidbody))]
    public sealed class NpcMotor : MonoBehaviour
    {
        public PrototypeSession Session;
        public PatrolRoute Route;
        [Min(0)] public float PatrolSpeed = 1.1f;
        [Min(0)] public float ChaseSpeed = 2.3f;
        public NavMeshAgent Agent { get; private set; }
        public bool HasOverride { get; private set; }
        Vector3 home;
        Quaternion homeRotation;
        int point, step = 1;
        float waitUntil, nextDestination;
        float baseAcceleration, baseAngularSpeed;
        bool holding, baseUpdateRotation;

        void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            baseAcceleration = Agent.acceleration; baseAngularSpeed = Agent.angularSpeed;
            baseUpdateRotation = Agent.updateRotation;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true; body.useGravity = false;
            if (Session == null) Session = FindFirstObjectByType<PrototypeSession>();
            home = transform.position; homeRotation = transform.rotation;
        }
        void OnEnable() { if (Session != null) Session.ResetOccurred += ResetActor; }
        void OnDisable() { if (Session != null) Session.ResetOccurred -= ResetActor; }
        void Update()
        {
            if (Session == null || !Agent.isOnNavMesh) return;
            Agent.isStopped = Session.Resetting || Session.IsComplete || holding;
            if (Agent.isStopped || HasOverride || Route == null || Route.Points.Length == 0) return;
            Agent.speed = PatrolSpeed;
            if (Time.time < waitUntil) return;
            // Setting a destination at the spawn can produce no path at all. Arrival is
            // geometric so an actor authored on its first waypoint still advances.
            if (!Agent.pathPending && Route.Points[point] != null &&
                Vector3.Distance(transform.position, Route.Points[point].position) <= Agent.stoppingDistance + 0.15f)
            {
                if (Route.Loop) point = (point + 1) % Route.Points.Length;
                else if (Route.Points.Length > 1)
                {
                    if (point == Route.Points.Length - 1) step = -1;
                    if (point == 0) step = 1;
                    point += step;
                }
                waitUntil = Time.time + Route.WaitSeconds;
                Agent.ResetPath();
                return;
            }
            if (Route.Points[point] != null) SetDestination(Route.Points[point].position);
        }

        void SetDestination(Vector3 target)
        {
            if (Time.time < nextDestination) return;
            nextDestination = Time.time + 0.15f;
            if (NavMesh.SamplePosition(target, out var hit, 2f, Agent.areaMask)) Agent.SetDestination(hit.position);
        }
        public void Pursue(Vector3 position, float speedMultiplier = 1f, float acceleration = -1f, float angularSpeed = -1f)
        {
            HasOverride = true;
            ReleaseHold();
            if (!Agent.isOnNavMesh) return;
            Agent.speed = ChaseSpeed * Mathf.Max(0f, speedMultiplier);
            Agent.acceleration = acceleration >= 0f ? acceleration : baseAcceleration;
            Agent.angularSpeed = angularSpeed >= 0f ? angularSpeed : baseAngularSpeed;
            SetDestination(position);
        }
        public void HoldAndFace(Vector3 position, float turnSpeed)
        {
            HasOverride = holding = true;
            // Navigation must not steer toward its old waypoint while attention owns facing.
            Agent.updateRotation = false;
            if (Agent.isOnNavMesh) Agent.isStopped = true;
            Vector3 direction = Vector3.ProjectOnPlane(position - transform.position, Vector3.up);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), Mathf.Max(0f, turnSpeed) * Time.deltaTime);
        }
        void ReleaseHold()
        {
            if (!holding) return;
            holding = false;
            nextDestination = 0f;
            Agent.updateRotation = baseUpdateRotation;
            if (Agent.isOnNavMesh) Agent.isStopped = Session != null && (Session.Resetting || Session.IsComplete);
        }
        public void ResumePatrol()
        {
            if (!HasOverride) return;
            HasOverride = false;
            ReleaseHold();
            Agent.speed = PatrolSpeed;
            Agent.acceleration = baseAcceleration; Agent.angularSpeed = baseAngularSpeed;
            if (Agent.isOnNavMesh) Agent.ResetPath();
        }
        public void ResetActor()
        {
            ReleaseHold();
            point = 0; step = 1; waitUntil = nextDestination = 0f; HasOverride = false;
            Agent.speed = PatrolSpeed;
            Agent.acceleration = baseAcceleration; Agent.angularSpeed = baseAngularSpeed;
            if (Agent.isOnNavMesh) Agent.ResetPath();
            if (Agent.enabled) Agent.Warp(home);
            transform.SetPositionAndRotation(home, homeRotation);
        }
    }
}
