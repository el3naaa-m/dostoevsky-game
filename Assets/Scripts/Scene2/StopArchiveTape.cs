using UnityEngine;
namespace NightChase.Station
{
    internal sealed class StopArchiveTape : MonoBehaviour
    {
        internal AudioSource Tape { get; set; }
        void OnDisable() { if (Tape) Tape.Stop(); }
    }
}
