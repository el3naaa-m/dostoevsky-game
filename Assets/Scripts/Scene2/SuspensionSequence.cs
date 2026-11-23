using System.Collections;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class SuspensionSequence : MonoBehaviour
    {
        internal IEnumerator Run(Scene2FlowManager flow, DialogueData lines, StationLayout layout, InterrogationSequence seating, Transform eyes)
        {
            bool delivered = false;
            flow.Dialogue.Play(lines, () =>
            {
                flow.Lock(true);
                delivered = true;
            });
            yield return new WaitUntil(() => delivered);
            yield return seating.StandPlayer(flow.Player.transform, eyes);
            if (layout.Chief)
            {
                seating.ReleaseChief(layout);
                var departure = gameObject.AddComponent<EscortSystem>();
                departure.Begin(layout.Chief, null, null, layout.ChiefOfficeRoute);
                layout.OfficeArrival = departure;
            }
            flow.SetStage(Scene2FlowManager.Stage.HandoverEquipment);
        }

    }
}
// На Scene2 Mechanics, запускается менеджером после допроса.
