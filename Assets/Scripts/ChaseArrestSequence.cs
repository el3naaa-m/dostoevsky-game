using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase
{
    [DefaultExecutionOrder(100)]
    sealed class ChaseArrestSequence : MonoBehaviour
    {
        Transform leftArm, leftForearm, leftHand, rightArm, rightForearm, rightHand;
        Transform suspectRoot;
        float restraint;
        bool holding;

        public IEnumerator Run(FirstPersonController player, Transform eyes, ChaseRunner suspect, float delay, Action<string> say)
        {
            suspectRoot = suspect.transform;
            var actor = suspect.GetComponentInChildren<MixamoNpcVisual>();
            if (actor)
            {
                leftArm = actor.Bone(HumanBodyBones.LeftUpperArm, "LeftArm");
                leftForearm = actor.Bone(HumanBodyBones.LeftLowerArm, "LeftForeArm");
                leftHand = actor.Bone(HumanBodyBones.LeftHand, "LeftHand");
                rightArm = actor.Bone(HumanBodyBones.RightUpperArm, "RightArm");
                rightForearm = actor.Bone(HumanBodyBones.RightLowerArm, "RightForeArm");
                rightHand = actor.Bone(HumanBodyBones.RightHand, "RightHand");
                actor.StandForArrest();
            }

            Vector3 away = suspectRoot.position - player.transform.position;
            away.y = 0;
            if (away.sqrMagnitude > .01f) suspect.Face(suspectRoot.position + away);
            var cuffs = ItemProps.Create(InventoryItem.Handcuffs);
            cuffs.transform.SetParent(transform, false);
            Vector3 start = eyes.TransformPoint(new Vector3(.19f, -.26f, .48f));
            cuffs.transform.SetPositionAndRotation(start, eyes.rotation);
            holding = true;
            for (float elapsed = 0; elapsed < 1f; elapsed += Time.deltaTime)
            {
                restraint = Mathf.SmoothStep(0, 1, elapsed);
                cuffs.transform.position = Vector3.Lerp(start, WristCenter(), restraint);
                yield return null;
            }
            restraint = 1;
            cuffs.transform.SetParent(suspectRoot, true);
            cuffs.transform.SetPositionAndRotation(WristCenter(), suspectRoot.rotation);

            var partner = FindPartner(suspect);
            Transform searchTarget = new GameObject("Search target").transform;
            searchTarget.SetParent(suspectRoot, false);
            searchTarget.localPosition = new Vector3(.23f, .94f, -.20f);
            if (partner)
            {
                partner.Follow(new[] { suspectRoot.position + new Vector3(-1.08f, 0, .36f) }, 4.8f);
                float deadline = Time.time + 6;
                while (partner.Moving && Time.time < deadline) yield return null;
                partner.Follow(Array.Empty<Vector3>(), 0);
                partner.Face(suspectRoot.position);
                partner.Reach(searchTarget);
                for (float elapsed = 0; elapsed < 1.45f; elapsed += Time.deltaTime)
                {
                    searchTarget.localPosition = new Vector3(.22f * Mathf.Cos(elapsed * 7f),
                        Mathf.Lerp(1.06f, .73f, Mathf.PingPong(elapsed * 1.7f, 1f)), -.20f);
                    yield return null;
                }
                partner.Reach(null);
                partner.Face(player.transform.position);
            }

            yield return new WaitForSeconds(delay);
            say("О, смотри-ка, он после того как тебя приложил, ещё и прикарманить кое-что успел. Твоё же?");
            Transform hand = partner ? partner.GetComponentInChildren<MixamoNpcVisual>()?.Bone(
                HumanBodyBones.RightHand, "RightHand") : null;
            var bundle = new GameObject("Returned wallet and notebook").transform;
            bundle.SetParent(transform, false);
            ItemProps.Create(InventoryItem.Notebook, bundle);
            ItemProps.Create(InventoryItem.Wallet, bundle).transform.localPosition = Vector3.up * .029f;
            if (hand)
            {
                bundle.SetParent(hand, false);
                bundle.localPosition = Vector3.forward * .08f;
                partner.Reach(eyes);
            }
            else bundle.position = suspectRoot.position + Vector3.up;
            yield return new WaitForSeconds(delay);

            Vector3 from = bundle.position;
            bundle.SetParent(transform, true);
            for (float elapsed = 0; elapsed < .72f; elapsed += Time.deltaTime)
            {
                float progress = Mathf.SmoothStep(0, 1, elapsed / .72f);
                bundle.position = Vector3.Lerp(from, eyes.TransformPoint(new Vector3(.16f, -.27f, .53f)), progress);
                bundle.rotation = Quaternion.Slerp(bundle.rotation, eyes.rotation, progress);
                yield return null;
            }

            if (partner) partner.Reach(null);
            Destroy(bundle.gameObject);
            say(null);
        }

        ChaseRunner FindPartner(ChaseRunner suspect)
        {
            ChaseRunner hidden = null;
            foreach (var actor in GetComponentsInChildren<ChaseRunner>(true))
                if (actor.name == "Partner")
                {
                    if (actor.gameObject.activeSelf &&
                        actor.transform.position.x < suspect.transform.position.x - 1f &&
                        actor.transform.position.z < -20f &&
                        (actor.transform.position - suspect.transform.position).sqrMagnitude < 64f)
                        return actor;
                    if (!actor.gameObject.activeSelf) hidden = actor;
                }

            var entrance = new Vector3(26.1f, 0, -22.8f);
            if (!NavMesh.SamplePosition(entrance, out var safe, 1f, NavMesh.AllAreas)) return null;
            if (hidden)
            {
                hidden.transform.position = safe.position;
                hidden.gameObject.SetActive(true);
                return hidden;
            }

            return ChaseRunner.Spawn(transform, "Partner", safe.position, new Color(.20f, .22f, .27f), "MixamoNpc/Ch33");
        }

        Vector3 WristCenter() => suspectRoot.TransformPoint(new Vector3(0, .95f, -.205f));

        void LateUpdate()
        {
            if (!holding || !suspectRoot) return;
            Vector3 center = WristCenter();
            AimArm(leftArm, leftForearm, leftHand, center - suspectRoot.right * .065f);
            AimArm(rightArm, rightForearm, rightHand, center + suspectRoot.right * .065f);
        }

        void AimArm(Transform upper, Transform lower, Transform hand, Vector3 wrist)
        {
            if (!upper || !lower || !hand) return;
            var elbow = Vector3.Lerp(lower.position,
                wrist - suspectRoot.up * .14f + (lower.position - suspectRoot.position).normalized * .12f, restraint);
            RotateToward(upper, lower.position - upper.position, elbow - upper.position);
            RotateToward(lower, hand.position - lower.position, wrist - lower.position);
        }

        void RotateToward(Transform bone, Vector3 from, Vector3 to)
        {
            if (from.sqrMagnitude < .001f || to.sqrMagnitude < .001f) return;
            Quaternion target = Quaternion.FromToRotation(from, to) * bone.rotation;
            bone.rotation = Quaternion.Slerp(bone.rotation, target, restraint);
        }
    }
}
