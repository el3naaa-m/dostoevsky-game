using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NightChase.Station
{
    public sealed class InterrogationSequence : MonoBehaviour
    {
        [SerializeField] ChairInteraction chiefChair, playerChair;
        [SerializeField] Transform chiefSeat, playerSeat;
        // Опционально: назначьте Humanoid-клип Stand To Sit без Loop Time.
        // Можно указать Animator шефа вручную. Playables сохраняет последний кадр.
        [SerializeField] Animator chiefAnimator, playerAnimator;
        [SerializeField] AnimationClip standToSit;
        PlayableGraph graph;
        Vector3 chiefGoal, playerGoal, chiefRest, playerRest, eyeRest;
        Quaternion playerRotation, eyeRotation, playerFacing, chiefFacing;
        bool chiefAgentEnabled, controllerEnabled;
        CharacterController controller;
        public bool ChiefSeated { get; private set; }
        internal Vector3 PlayerSeatPosition => playerGoal;

        internal IEnumerator SeatChief(StationLayout layout)
        {
            var chief = layout.Chief;
            chiefChair = Resolve(chiefChair, "ChairA (31)");
            playerChair = Resolve(playerChair, "ChairA (32)");
            chiefRest = Position(chiefSeat, chiefChair, new Vector3(-5.33f, 0, 15.56f));
            playerRest = Position(playerSeat, playerChair, new Vector3(-6.41f, 0, 15.53f));
            var backward = (chiefRest - playerRest).normalized;
            chiefFacing = chiefSeat ? chiefSeat.rotation : Quaternion.LookRotation(-backward);
            playerFacing = playerSeat ? playerSeat.rotation : Quaternion.LookRotation(backward);
            if (chiefChair) chiefChair.Bind(backward);
            if (playerChair) playerChair.Bind(-backward);
            chiefGoal = chiefChair ? chiefChair.PulledPosition(chiefRest) : chiefRest + backward * .4f;
            playerGoal = playerChair ? playerChair.PulledPosition(playerRest) : playerRest - backward * .4f;
            chiefAgentEnabled = chief.enabled;
            if (chief.isOnNavMesh && NavMesh.SamplePosition(chiefGoal, out var hit, .65f, chief.areaMask))
            {
                chief.isStopped = false;
                if (chief.SetDestination(hit.position))
                {
                    float deadline = Time.time + 3f;
                    while (Time.time < deadline && Vector3.Distance(chief.transform.position, hit.position) > .45f)
                        yield return null;
                }
            }
            chief.enabled = false;
            layout.SeatingChief = true;
            var from = chief.transform.position;
            // Короткая доводка до сиденья после прохода по NavMesh.
            yield return Move(chief.transform, chiefGoal, chiefFacing, Mathf.Max(.25f, Vector3.Distance(from, chiefGoal) / 1.8f));
            if (chiefChair) yield return chiefChair.SitDown();
            if (!chiefAnimator) chiefAnimator = chief.GetComponentInChildren<Animator>();
            yield return Animate(chiefAnimator, "Chief", chief.transform, chiefGoal, .36f);
            ChiefSeated = true;
        }

        internal IEnumerator SeatPlayer(Scene2FlowManager flow, Transform eyes)
        {
            flow.Lock(true);
            var player = flow.Player.transform;
            playerRest = player.position;
            playerRotation = player.rotation;
            eyeRest = eyes.localPosition;
            eyeRotation = eyes.localRotation;
            controller = player.GetComponent<CharacterController>();
            controllerEnabled = controller && controller.enabled;
            if (controller) controller.enabled = false;
            if (playerChair) yield return playerChair.SitDown();
            yield return Move(player, playerGoal, playerFacing, .45f);
            yield return Animate(playerAnimator, "Player", eyes, eyeRest, .48f, true);
        }

        internal IEnumerator Stand(StationLayout layout, Transform player, Transform eyes)
        {
            if (graph.IsValid()) graph.Destroy();
            var chief = layout.Chief;
            float t = 0;
            var eyeFrom = eyes.localPosition;
            var chiefFrom = chief.transform.position;
            while (t < .3f)
            {
                t += Time.deltaTime;
                float blend = Mathf.SmoothStep(0,1,t/.3f);
                eyes.localPosition = Vector3.Lerp(eyeFrom, eyeRest, blend);
                chief.transform.position = Vector3.Lerp(chiefFrom, chiefGoal, blend);
                yield return null;
            }
            player.SetPositionAndRotation(playerRest, playerRotation);
            eyes.localRotation = eyeRotation;
            if (controller) controller.enabled = controllerEnabled;
            if (playerChair) yield return playerChair.StandUp();
            if (chiefChair) yield return chiefChair.StandUp();
            chief.enabled = chiefAgentEnabled;
            if (chief.enabled && NavMesh.SamplePosition(chiefGoal, out var hit, 1f, chief.areaMask)) chief.Warp(hit.position);
            layout.SeatingChief = false;
        }

        IEnumerator Animate(Animator animator, string track, Transform target, Vector3 origin, float drop, bool local = false)
        {
            bool useClip = standToSit && animator && animator.isHuman;
            var motion = default(AnimationClipPlayable);
            if (useClip)
            {
                if (!graph.IsValid())
                {
                    graph = PlayableGraph.Create("Interrogation seating");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    graph.Play();
                }
                animator.applyRootMotion = false;
                motion = AnimationClipPlayable.Create(graph, standToSit);
                AnimationPlayableOutput.Create(graph, track, animator).SetSourcePlayable(motion);
            }
            float length = useClip ? Mathf.Max(.1f,standToSit.length) : .65f;
            for (float t = 0; t < length; t += Time.deltaTime)
            {
                Vector3 position = origin + Vector3.down * (drop * Mathf.SmoothStep(0,1,t/length));
                if (useClip) { motion.SetTime(t); graph.Evaluate(0); }
                if (local) target.localPosition = position;
                else if (!useClip) target.position = position;
                yield return null;
            }
            if (useClip) { motion.SetTime(length - .01f); graph.Evaluate(0); }
            if (local) target.localPosition = origin + Vector3.down * drop;
            else if (!useClip) target.position = origin + Vector3.down * drop;
        }

        static IEnumerator Move(Transform actor, Vector3 goal, Quaternion facing, float duration)
        {
            var position = actor.position;
            var rotation = actor.rotation;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0,1,t/duration);
                actor.SetPositionAndRotation(Vector3.Lerp(position,goal,blend),Quaternion.Slerp(rotation,facing,blend));
                yield return null;
            }
            actor.SetPositionAndRotation(goal,facing);
        }

        ChairInteraction Resolve(ChairInteraction assigned, string sceneName)
        {
            if (assigned) return assigned;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var node in root.GetComponentsInChildren<Transform>())
                    if (node.name == sceneName)
                        return node.TryGetComponent<ChairInteraction>(out var chair) ? chair : node.gameObject.AddComponent<ChairInteraction>();
            return null;
        }

        static Vector3 Position(Transform anchor, ChairInteraction chair, Vector3 fallback) =>
            anchor ? anchor.position : chair ? chair.transform.position : fallback;

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
