using UnityEngine;

namespace NightChase
{
    sealed class ChasePace
    {
        readonly Transform player;
        readonly Vector3[] points;
        readonly float[] progress;
        readonly float baseSpeed, maximumSpeed, nearSpeed, minDistance, farSlowdown, warningDistance;
        float playerProgress;

        public float Gap { get; private set; }

        public ChasePace(Transform playerRoot, Vector3 spawn, Vector3[] route, float baseSpeed,
            float minDistance, float maxSpeed, float catchUpSpeed, float slowdownWhenFar, float warningDistance)
        {
            player = playerRoot;
            this.baseSpeed = baseSpeed;
            this.minDistance = minDistance;
            maximumSpeed = Mathf.Max(baseSpeed, maxSpeed);
            nearSpeed = Mathf.Clamp(catchUpSpeed, baseSpeed, maximumSpeed);
            farSlowdown = slowdownWhenFar;
            this.warningDistance = Mathf.Max(minDistance + .1f, warningDistance);
            points = new Vector3[route.Length + 1];
            progress = new float[points.Length];
            points[0] = spawn;
            for (int i = 1; i < points.Length; i++)
            {
                points[i] = route[i - 1];
                progress[i] = progress[i - 1] + Flat(points[i] - points[i - 1]).magnitude;
            }
        }

        public float Speed(Vector3 suspect, int segmentEnd, out float correction)
        {
            float nearest = float.PositiveInfinity;
            for (int i = 1; i < points.Length; i++)
            {
                float t = Project(player.position, i, i == 1 ? -10f : 0);
                Vector3 offset = Flat(player.position - Vector3.LerpUnclamped(points[i - 1], points[i], t));
                if (offset.sqrMagnitude >= nearest) continue;
                nearest = offset.sqrMagnitude;
                playerProgress = Mathf.LerpUnclamped(progress[i - 1], progress[i], t);
            }

            float suspectProgress = Mathf.LerpUnclamped(progress[segmentEnd - 1], progress[segmentEnd],
                Project(suspect, segmentEnd, 0));
            Gap = suspectProgress - playerProgress;
            correction = nearest <= 4f && Gap < minDistance - .12f
                ? Mathf.Max(suspectProgress, playerProgress + minDistance + .12f)
                : -1f;

            if (Gap < minDistance)
                return Mathf.Lerp(nearSpeed, maximumSpeed, Mathf.Clamp01((minDistance - Gap) / minDistance));
            if (Gap < minDistance + 4f)
                return Mathf.Lerp(nearSpeed, baseSpeed, (Gap - minDistance) / 4f);
            return Mathf.Max(1f, baseSpeed - farSlowdown * Mathf.InverseLerp(warningDistance * .65f, warningDistance, Gap));
        }

        public bool TryGetCorrection(float distance, int currentSegment, ChaseObstacleCourse obstacles,
            Vector3 currentPosition, out Vector3 point, out int segment)
        {
            point = currentPosition;
            segment = currentSegment;
            if (distance < 0) return false;
            distance = Mathf.Min(distance, progress[progress.Length - 1] - .12f);

            Vector3 from = currentPosition;
            for (int i = currentSegment; i < points.Length; i++)
            {
                float length = progress[i] - progress[i - 1];
                if (length <= .001f) continue;
                float t = (distance - progress[i - 1]) / length;
                Vector3 to = Vector3.Lerp(points[i - 1], points[i], t);
                if (obstacles.CrossesTraversal(from, to)) return false;
                if (t <= 1f)
                {
                    if (Flat(to - currentPosition).sqrMagnitude < .35f * .35f) return false;
                    point = to;
                    segment = i;
                    return true;
                }
                from = points[i];
            }
            return false;
        }

        float Project(Vector3 position, int segment, float minimum)
        {
            Vector3 direction = Flat(points[segment] - points[segment - 1]);
            return direction.sqrMagnitude < .001f ? 0
                : Mathf.Clamp(Vector3.Dot(Flat(position - points[segment - 1]), direction) / direction.sqrMagnitude,
                    minimum, 1f);
        }

        static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0, vector.z);
    }
}
