using System.Collections;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class SuspensionSequence : MonoBehaviour
    {
        internal IEnumerator Run(Scene2FlowManager flow, DialogueData lines, StationLayout layout, InterrogationSequence seating)
        {
            bool delivered = false;
            flow.Dialogue.Play(lines, () => delivered = true);
            yield return new WaitUntil(() => delivered);
            if (layout.Chief)
            {
                var departure = gameObject.AddComponent<EscortSystem>();
                departure.Begin(layout.Chief, null, null, layout.ChiefOfficeRoute);
                StartCoroutine(SeatOnArrival(departure, layout, seating));
            }
            flow.SetStage(Scene2FlowManager.Stage.HandoverEquipment);
        }
        IEnumerator SeatOnArrival(EscortSystem departure, StationLayout layout, InterrogationSequence seating)
        {
            yield return new WaitUntil(() => !layout.Chief || departure.Complete);
            if (layout.Chief && seating)
                yield return seating.SeatChiefInOffice(layout);
        }
    }
}
// На Scene2 Mechanics, запускается менеджером после допроса.
