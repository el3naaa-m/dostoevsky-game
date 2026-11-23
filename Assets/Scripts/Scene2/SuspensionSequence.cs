using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    public sealed class SuspensionSequence : MonoBehaviour
    {
        internal IEnumerator Run(Scene2FlowManager flow, DialogueData lines, NavMeshAgent chief, Transform exit)
        {
            bool delivered = false;
            flow.Dialogue.Play(lines, () => delivered = true);
            yield return new WaitUntil(() => delivered);
            if (chief && chief.isOnNavMesh && exit &&
                NavMesh.SamplePosition(exit.position, out var target, 1, chief.areaMask))
                {
                chief.isStopped = false;
                chief.SetDestination(target.position);
            }
            flow.SetStage(Scene2FlowManager.Stage.HandoverEquipment);
        }
    }
}
// На Scene2 Mechanics, запускается менеджером после допроса.
