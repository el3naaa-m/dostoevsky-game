using UnityEngine;
namespace NightChase.Station
{
    // [ИМПРОВИЗАЦИЯ] Стул, дверь допросной и кашель слышны из разных точек.
    public sealed class RandomAudioPlayer : MonoBehaviour
    {
        AudioClip[] clips;
        float next;
        readonly Vector3[] positions = { new(-14,1,10), new(-12,1,10), new(-10,1,15) };
        internal void Bind(AudioClip[] values) { clips = values; next = Time.time + Random.Range(15,35); }
        void Update()
        {
            if (clips == null || Time.time < next) return;
            next = Time.time + Random.Range(20,55);
            int i = Random.Range(0,clips.Length);
            if (clips[i]) AudioSource.PlayClipAtPoint(clips[i],positions[i],.35f);
        }
    }
}
