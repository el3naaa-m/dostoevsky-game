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
                var departure = gameObject.AddComponent<EscortSystem>();
                var route = layout.BuildChiefOfficeRoute();
                if (route.Length > 0)
                {
                    departure.Begin(layout.Chief, null, null, route, navMeshOnly: true, movementSpeed: layout.ChiefWalkSpeed);
                    StartCoroutine(FaceBoardOnArrival(departure, layout));
                }
            }
            flow.SetStage(Scene2FlowManager.Stage.HandoverEquipment);
        }
        IEnumerator FaceBoardOnArrival(EscortSystem departure, StationLayout layout)
        {
            yield return new WaitUntil(() => !layout.Chief || departure.Complete || departure.Failed);
            if (!layout.Chief || !departure.Complete || !layout.OfficeBoard) yield break;
            Vector3 direction = layout.OfficeBoard.position - layout.Chief.transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f) layout.Chief.transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
// На Scene2 Mechanics, запускается менеджером после допроса.
