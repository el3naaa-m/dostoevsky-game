using UnityEngine;

namespace NightChase.Station
{
    [CreateAssetMenu(menuName="Night Chase/Документ")]
    public sealed class EvidenceDocument : ScriptableObject
    {
        public string documentId, titleRU, titleEN;
        [TextArea] public string bodyRU, bodyEN;
        public bool isKeyEvidence = true;
        public Sprite icon;
    }
}
