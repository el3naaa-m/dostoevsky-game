using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace NightChase.Station
{
    public sealed class SuspensionSequence : MonoBehaviour
    {
        internal IEnumerator Run(Scene2FlowManager flow, DialogueData lines, NavMeshAgent chief, Transform[] officeRoute)
        {
            bool delivered = false;
            flow.Dialogue.Play(lines, () => delivered = true);
            yield return new WaitUntil(() => delivered);
            if (chief)
            {
                var departure = gameObject.AddComponent<EscortSystem>();
                departure.Begin(chief, null, null, officeRoute);
            }
            flow.SetStage(Scene2FlowManager.Stage.HandoverEquipment);
        }
    }
}
// На Scene2 Mechanics, запускается менеджером после допроса.
