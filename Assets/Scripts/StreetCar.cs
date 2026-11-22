using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase
{
    [RequireComponent(typeof(BoxCollider), typeof(NavMeshObstacle))]
    sealed class StreetCar : MonoBehaviour
    {
        static readonly HashSet<StreetCar> Active = new();
        BoxCollider body;
        Vector3 velocity;

        public bool IsMoving => velocity.sqrMagnitude > .25f;

        void Awake()
        {
            body = GetComponent<BoxCollider>();
            var obstacle = GetComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = body.center;
            obstacle.size = body.size + new Vector3(.25f, 0, .35f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;
            obstacle.carvingMoveThreshold = .25f;

            var bumper = gameObject.AddComponent<BoxCollider>();
            bumper.center = body.center;
            bumper.size = body.size + new Vector3(.12f, .03f, .12f);
            bumper.isTrigger = true;
        }

        void OnEnable() => Active.Add(this);

        void OnDisable()
        {
            Active.Remove(this);
            velocity = Vector3.zero;
        }

        public void SetVelocity(Vector3 value) => velocity = value;

        void OnTriggerEnter(Collider other) => CheckImpact(other);
        void OnTriggerStay(Collider other) => CheckImpact(other);

        void CheckImpact(Collider other)
        {
            if (IsMoving && other.TryGetComponent<ChaseTrafficImpact>(out var player))
                player.Hit();
        }

        public static bool Blocks(Vector3 position, Vector3 direction, float speed, float radius)
        {
            if (direction.sqrMagnitude < .001f) return false;

            Vector3 pedestrianVelocity = direction.normalized * speed;
            foreach (var car in Active)
            {
                if ((car.transform.position - position).sqrMagnitude > 64f) continue;
                float verticalGap = position.y - car.transform.position.y;
                if (verticalGap > car.body.size.y || verticalGap < -1.65f) continue;

                Vector3 from = car.transform.InverseTransformPoint(position) - car.body.center;
                Vector3 projected = car.transform.InverseTransformDirection(pedestrianVelocity - car.velocity);
                Vector3 size = car.body.size * .5f + new Vector3(radius + .12f, 0, radius + .16f);
                float enter = 0f;
                float exit = .27f;
                if (IntersectsAxis(from.x, projected.x, size.x, ref enter, ref exit)
                    && IntersectsAxis(from.z, projected.z, size.z, ref enter, ref exit))
                    return true;
            }
            return false;
        }

        static bool IntersectsAxis(float start, float speed, float extent, ref float enter, ref float exit)
        {
            if (Mathf.Abs(speed) < .001f) return Mathf.Abs(start) <= extent;
            float first = (-extent - start) / speed;
            float last = (extent - start) / speed;
            enter = Mathf.Max(enter, Mathf.Min(first, last));
            exit = Mathf.Min(exit, Mathf.Max(first, last));
            return enter <= exit;
        }
    }
}
