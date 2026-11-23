using System.Linq;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class EvidenceLog : MonoBehaviour
    {
        internal static EvidenceDocument[] Documents => Resources.LoadAll<EvidenceDocument>("StationEvidence");
        internal static EvidenceDocument Find(string id) => Documents.FirstOrDefault(d => d.documentId == id);

        internal void Show(Scene2FlowManager flow)
        {
            var found = Documents.Where(d => flow.Inventory.Has(d.documentId)).OrderBy(d => d.titleRU).ToArray();
            flow.OpenPanel("Журнал улик", $"Найдено {found.Length} из {Documents.Length}");
            foreach (var doc in found)
                flow.UI.Button(doc.titleRU, () => Read(flow, doc), true, Resources.Load<Texture2D>("StationEvidence/" + doc.documentId + "Icon"));
            flow.UI.Button("Закрыть", flow.ClosePanel);
        }

        void Read(Scene2FlowManager flow, EvidenceDocument doc)
        {
            string annotation = doc.documentId == "Immigration" && flow.Inventory.Has("SovietRecord")
                ? "\n\nНа полях блокнота: почему снимок из Бруклина старше карточки прибытия?" : "";
            // [ИМПРОВИЗАЦИЯ] Следователь сопоставляет пальто в записке со словами официантки.
            if (doc.documentId == "father_note" && flow.Inventory.Has("BrightonNote"))
                annotation = "\n\nНа полях блокнота: официантка запомнила пальто. Надо разыскать её.";
            flow.Audio.PlayOneShot(flow.Audio.paper);
            flow.OpenPanel(doc.titleRU, doc.bodyRU + annotation);
            flow.UI.Button("Назад к уликам", () => Show(flow));
        }
    }
}
