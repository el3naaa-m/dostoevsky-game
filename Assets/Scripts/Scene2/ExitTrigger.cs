using System.Collections;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class ExitTrigger : StationInteractable
    {
        ChapterEndingController ending;
        Transform leaf;
        Animation animationPlayer;
        string clipName;
        Quaternion closed;
        bool opened;

        internal void Bind(ChapterEndingController chapter)
        {
            ending = chapter;
            leaf = transform.Find("Door");
            if (leaf) closed = leaf.localRotation;
            animationPlayer = GetComponent<Animation>();
            if (TryGetComponent<QA_Door>(out var original))
            {
                clipName = original.OpenForwardAnimName;
                original.enabled = false;
            }
            if (animationPlayer) animationPlayer.Stop();
        }

        public override string Prompt(Scene2FlowManager flow) =>
            ending && ending.CanExit && Vector3.Dot(flow.Player.transform.position - transform.position,transform.forward) > 0
                ? "E — Выйти"
                : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (Prompt(flow).Length != 0) ending.ExitThrough(this);
        }

        internal IEnumerator Open()
        {
            if (opened) yield break;
            opened = true;
            var clip = animationPlayer && !string.IsNullOrEmpty(clipName) ? animationPlayer[clipName] : null;
            if (clip != null)
            {
                animationPlayer.Play(clipName);
                clip.wrapMode = WrapMode.ClampForever;
                clip.speed = 1;
                clip.normalizedTime = 0;
                yield return new WaitForSeconds(clip.length);
                clip.normalizedTime = 1;
                animationPlayer.Sample();
                yield break;
            }
            if (!leaf) yield break;
            const float duration = .65f;
            var open = closed * Quaternion.Euler(0,-95,0);
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                leaf.localRotation = Quaternion.Slerp(closed,open,Mathf.SmoothStep(0,1,elapsed / duration));
                yield return null;
            }
            leaf.localRotation = open;
        }
    }
}
