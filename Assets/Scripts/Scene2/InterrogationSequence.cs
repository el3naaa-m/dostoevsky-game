using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NightChase.Station
{
    public sealed class InterrogationSequence : MonoBehaviour
    {
        [SerializeField] Transform chiefSeat, playerSeat;
        // Импортируйте Stand To Sit как Humanoid, выключите Loop Time.
        // Animator Controller и Timeline не нужны: клип играет через Playables,
        // последний кадр удерживается до конца допроса. Для ГГ без рига работает камера.
        [SerializeField] AnimationClip standToSit;
        PlayableGraph graph;

        internal IEnumerator Run(Scene2FlowManager flow, StationLayout layout, Transform eyes, Action ready)
        {
            flow.Lock(true);
            var chief = layout.Chief;
            var player = flow.Player.transform;
            var controller = player.GetComponent<CharacterController>();
            bool controllerEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            Vector3 playerStart = player.position;
            Vector3 chiefStart = chief.transform.position;
            Quaternion playerRotation = player.rotation;
            Quaternion chiefRotation = chief.transform.rotation;
            Vector3 eyesStart = eyes.localPosition;
            Quaternion eyesRotation = eyes.localRotation;
            bool agentEnabled = chief.enabled;
            chief.enabled = false;
            layout.SeatingChief = true;

            Vector3 chiefGoal = chiefSeat ? chiefSeat.position : new Vector3(-5.33f, 0, 15.56f);
            Vector3 playerGoal = playerSeat ? playerSeat.position : new Vector3(-6.41f, 0, 15.53f);
            Quaternion chiefFacing = chiefSeat ? chiefSeat.rotation : Face(playerGoal - chiefGoal);
            Quaternion playerFacing = playerSeat ? playerSeat.rotation : Face(chiefGoal - playerGoal);
            float approach = Mathf.Max(.65f, Mathf.Max(Vector3.Distance(chiefStart, chiefGoal), Vector3.Distance(playerStart, playerGoal)) / 2.2f);
            for (float t = 0; t < approach; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0, 1, t / approach);
                chief.transform.SetPositionAndRotation(Vector3.Lerp(chiefStart, chiefGoal, blend), Quaternion.Slerp(chiefRotation, chiefFacing, blend));
                player.SetPositionAndRotation(Vector3.Lerp(playerStart, playerGoal, blend), Quaternion.Slerp(playerRotation, playerFacing, blend));
                eyes.localRotation = Quaternion.Slerp(eyesRotation, Quaternion.identity, blend);
                yield return null;
            }
            player.SetPositionAndRotation(playerGoal, playerFacing);
            chief.transform.SetPositionAndRotation(chiefGoal, chiefFacing);
            eyes.localRotation = Quaternion.identity;

            var animator = chief.GetComponentInChildren<Animator>();
            var motion = default(AnimationClipPlayable);
            bool animated = standToSit && animator && animator.isHuman;
            if (animated)
            {
                graph = PlayableGraph.Create("Interrogation seating");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                motion = AnimationClipPlayable.Create(graph, standToSit);
                AnimationPlayableOutput.Create(graph, "Chief", animator).SetSourcePlayable(motion);
                graph.Play();
            }
            float duration = animated ? Mathf.Max(.1f, standToSit.length) : .75f;
            Vector3 seatedEyes = eyesStart + Vector3.down * .48f;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0, 1, t / duration);
                eyes.localPosition = Vector3.Lerp(eyesStart, seatedEyes, blend);
                if (animated) { motion.SetTime(t); graph.Evaluate(0); }
                else chief.transform.position = chiefGoal + Vector3.down * (.38f * blend);
                yield return null;
            }
            eyes.localPosition = seatedEyes;
            if (animated) { motion.SetTime(duration - .01f); graph.Evaluate(0); }
            else chief.transform.position = chiefGoal + Vector3.down * .38f;
            Restore = () =>
            {
                if (graph.IsValid()) graph.Destroy();
                player.SetPositionAndRotation(playerStart, playerRotation);
                eyes.localPosition = eyesStart;
                eyes.localRotation = eyesRotation;
                chief.transform.position = chiefGoal;
                chief.enabled = agentEnabled;
                if (chief.enabled && NavMesh.SamplePosition(chiefGoal, out var hit, 1f, chief.areaMask)) chief.Warp(hit.position);
                layout.SeatingChief = false;
                if (controller) controller.enabled = controllerEnabled;
            };
            ready();
        }

        internal Action Restore { get; private set; }
        static Quaternion Face(Vector3 vector) => Quaternion.LookRotation(Vector3.ProjectOnPlane(vector, Vector3.up));
        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
