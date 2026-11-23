using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace NightChase.Station
{
    public sealed class SecurityAttention : MonoBehaviour
    {
        [SerializeField, Min(.5f)] float detectionSeconds = 2.6f;
        [SerializeField, Min(1)] float sightRange = 13;
        Scene2FlowManager flow;
        StationLayout station;
        CharacterController body;
        Transform eyes;
        EscortSystem patrol;
        Transform[] tour, returnRoute;
        const float OfficeWaitSeconds = 40f;
        enum PatrolState { WaitingForArrival, Office, Outbound, Looking, Returning }
        PatrolState patrolState;
        float attention, nextTour, graceUntil;
        bool crouched, caught;
        float standingHeight;
        Vector3 standingCenter, standingEyes;

        internal void Bind(Scene2FlowManager manager, StationLayout layout)
        {
            flow = manager;
            station = layout;
            body = flow.Player.GetComponent<CharacterController>();
            eyes = flow.Player.GetComponentInChildren<Camera>().transform;
            patrol = gameObject.AddComponent<EscortSystem>();
            flow.StageChanged += OnStage;
        }

        void OnStage(Scene2FlowManager.Stage stage)
        {
            if (stage == Scene2FlowManager.Stage.SearchPuzzle)
            {
                if (patrolState == PatrolState.Office) nextTour = Time.time + OfficeWaitSeconds;
                return;
            }
            if (crouched) SetCrouched(false);
            flow.UI.HideAttention();
        }

        void Update()
        {
            if (!flow || caught || flow.State != Scene2FlowManager.Stage.SearchPuzzle) return;
            UpdatePatrol();

            if (!flow.UI.Modal && flow.Player.enabled && Keyboard.current?.fKey.wasPressedThisFrame == true)
            {
                if (!crouched || CanStand()) SetCrouched(!crouched);
                else flow.Notify("Чтобы встать, выйдите из-под укрытия.", 1.4f);
            }

            bool seen = Time.time >= graceUntil && Visible();
            attention = Mathf.MoveTowards(attention, seen ? 1 : 0, Time.deltaTime / (seen ? detectionSeconds : 1.8f));
            flow.UI.ShowAttention(attention, seen, crouched);
            if (attention >= 1) StartCoroutine(Caught());
        }

        void UpdatePatrol()
        {
            if (station.OfficeArrival == null || !station.OfficeArrival.Complete) return;
            switch (patrolState)
            {
                case PatrolState.WaitingForArrival:
                    nextTour = Time.time + OfficeWaitSeconds;
                    patrolState = PatrolState.Office;
                    break;
                case PatrolState.Office:
                    if (Time.time < nextTour) return;
                    tour ??= MakeTour();
                    patrol.Begin(station.Chief, null, null, tour);
                    patrolState = PatrolState.Outbound;
                    break;
                case PatrolState.Outbound:
                    if (!patrol.Complete) return;
                    nextTour = Time.time + 3.5f;
                    patrolState = PatrolState.Looking;
                    break;
                case PatrolState.Looking:
                    var chief = station.Chief.transform;
                    var toward = Vector3.ProjectOnPlane(station.Board.transform.position - chief.position, Vector3.up);
                    if (toward.sqrMagnitude > .01f)
                    {
                        var sweep = Quaternion.AngleAxis(30f * Mathf.Sin((nextTour - Time.time) * 2f), Vector3.up);
                        chief.rotation = Quaternion.RotateTowards(chief.rotation, Quaternion.LookRotation(sweep * toward), 125f * Time.deltaTime);
                    }
                    if (Time.time < nextTour) return;
                    patrol.Begin(station.Chief, null, null, returnRoute);
                    patrolState = PatrolState.Returning;
                    break;
                case PatrolState.Returning:
                    if (!patrol.Complete) return;
                    nextTour = Time.time + OfficeWaitSeconds;
                    patrolState = PatrolState.Office;
                    break;
            }
        }

        Transform[] MakeTour()
        {
            var home = Waypoint("Chief office", station.Chief.transform.position);
            Vector3 board = station.Board.transform.position;
            Vector3 approach = Vector3.MoveTowards(board, home.position, 1.7f);
            approach.y = home.position.y;
            var nearBoard = Waypoint("Board aisle", approach);
            returnRoute = new[] { home };
            return new[] { nearBoard };
        }

        Transform Waypoint(string label, Vector3 point)
        {
            var marker = new GameObject(label).transform;
            marker.SetParent(transform);
            marker.position = NavMesh.SamplePosition(point, out var hit, 2f, NavMesh.AllAreas)
                && Mathf.Abs(hit.position.y - point.y) < .5f ? hit.position : point;
            return marker;
        }

        bool Visible()
        {
            var chief = station.Chief.transform;
            var origin = chief.position + Vector3.up * 1.58f;
            var ray = eyes.position - origin;
            if (ray.sqrMagnitude > sightRange * sightRange ||
                Vector3.Angle(chief.forward, Vector3.ProjectOnPlane(ray, Vector3.up)) > 53) return false;
            // An unobstructed ray can end inside the player's capsule while crouching.
            foreach (var hit in Physics.RaycastAll(origin, ray.normalized, ray.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(chief) && !hit.transform.IsChildOf(flow.Player.transform)) return false;
            return true;
        }

        void SetCrouched(bool value)
        {
            if (value)
            {
                standingHeight = body.height;
                standingCenter = body.center;
                standingEyes = eyes.localPosition;
                float height = Mathf.Max(body.radius * 2, .72f);
                float drop = Mathf.Max(0, standingHeight - height);
                body.height = standingHeight - drop;
                body.center = standingCenter - Vector3.up * (drop * .5f);
                eyes.localPosition = standingEyes - Vector3.up * drop;
            }
            else
            {
                body.center = standingCenter;
                body.height = standingHeight;
                eyes.localPosition = standingEyes;
            }
            crouched = value;
        }

        bool CanStand()
        {
            var t = body.transform;
            float radius = body.radius * .96f;
            Vector3 top = t.TransformPoint(body.center + Vector3.up * (body.height * .5f - body.radius));
            foreach (var hit in Physics.SphereCastAll(top, radius, t.up, standingHeight - body.height, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(t)) return false;
            return true;
        }

        IEnumerator Caught()
        {
            caught = true;
            flow.ClosePanel();
            flow.Lock(true);
            flow.UI.ShowCaught();
            flow.Audio.PlayOneShot(flow.Audio.caught);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return flow.Fade(1, .35f);
            flow.UI.HideAttention();
            flow.RestartSearch();
            if (crouched) SetCrouched(false);
            attention = 0;
            graceUntil = Time.time + 4;
            yield return flow.Fade(0, .35f);
            flow.Lock(false);
            caught = false;
        }
    }
}
