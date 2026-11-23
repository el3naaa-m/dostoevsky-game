using System.Collections;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class ChairInteraction : MonoBehaviour
    {
        [SerializeField, Range(.3f,.5f)] float distance = .4f;
        [SerializeField, Min(.05f)] float duration = .3f;
        Vector3 rest, back;

        internal void Bind(Vector3 direction)
        {
            rest = transform.position;
            back = direction.normalized * distance;
        }

        internal Vector3 PulledPosition(Vector3 seatPosition) => seatPosition + back;

        public IEnumerator SitDown()
        {
            if ((transform.position - (rest + back)).sqrMagnitude > .0001f)
                AudioManager.Instance?.ChairSlideAt(transform.position);
            yield return Slide(rest + back);
        }
        public IEnumerator StandUp() => Slide(rest);

        IEnumerator Slide(Vector3 goal)
        {
            Vector3 start = transform.position;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                transform.position = Vector3.Lerp(start, goal, Mathf.SmoothStep(0,1,elapsed/duration));
                yield return null;
            }
            transform.position = goal;
        }
    }
}
