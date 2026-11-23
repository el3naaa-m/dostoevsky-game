using System.Collections;
using UnityEngine;

using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NightChase.Station
{
    public sealed class InterrogationSequence : MonoBehaviour
    {
        [SerializeField] ChairInteraction playerChair;
        [SerializeField] Transform playerSeat;
        // Опционально: назначьте Humanoid-клип Stand To Sit без Loop Time.
        // Можно указать Animator героя вручную. Playables сохраняет последний кадр.
        [SerializeField] Animator playerAnimator;
        [SerializeField] AnimationClip standToSit;
        PlayableGraph graph;
        Vector3 playerGoal, playerRest, eyeRest;
        Quaternion playerRotation, eyeRotation, playerFacing;
        bool controllerEnabled;
        CharacterController controller;
        public bool PlayerSeatReady { get; private set; }
        internal Vector3 PlayerSeatPosition => playerGoal;

        internal void PrepareInterview(StationLayout layout)
        {
            var chief = layout.Chief;
            playerChair = Resolve(playerChair, "ChairA (32)");
            Vector3 playerSeatPoint = Position(playerSeat, playerChair, new Vector3(-6.41f,0,15.53f));
            Vector3 toChief = chief.transform.position - playerSeatPoint;
            toChief.y = 0;
            if (toChief.sqrMagnitude < .01f) toChief = Vector3.right;
            var facing = playerSeat ? playerSeat.forward : toChief.normalized;
            playerFacing = playerSeat ? playerSeat.rotation : Quaternion.LookRotation(facing);
            if (playerChair) playerChair.Bind(-facing);
            playerGoal = playerChair ? playerChair.PulledPosition(playerSeatPoint) : playerSeatPoint - facing*.4f;
            // Шеф ждёт у стола стоя и остаётся на NavMesh для следующего маршрута.
            if (chief.isOnNavMesh) chief.isStopped = true;
            chief.transform.rotation = Quaternion.LookRotation(-toChief);
            PlaceCup(new Vector3(-5.92f,.78f,15.55f));
            PlayerSeatReady = true;
        }
        void PlaceCup(Vector3 point)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>())
                    if (item.name == "CupB (1)")
                    {
                        var cup = Instantiate(item.gameObject, point, item.rotation, transform);
                        cup.name = "Чашка шефа";
                        foreach (var collider in cup.GetComponentsInChildren<Collider>()) collider.enabled = false;
                        return;
                    }
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

        internal IEnumerator StandPlayer(Transform player, Transform eyes)
        {
            var eyeFrom = eyes.localPosition;
            var lookFrom = eyes.localRotation;
            for (float t = 0; t < .3f; t += Time.deltaTime)
            {
                float blend = Mathf.SmoothStep(0, 1, t / .3f);
                eyes.localPosition = Vector3.Lerp(eyeFrom, eyeRest, blend);
                eyes.localRotation = Quaternion.Slerp(lookFrom, eyeRotation, blend);
                yield return null;
            }
            player.SetPositionAndRotation(playerRest, playerRotation);
            eyes.localPosition = eyeRest;
            eyes.localRotation = eyeRotation;
            if (controller) controller.enabled = controllerEnabled;
            if (playerChair) yield return playerChair.StandUp();
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
