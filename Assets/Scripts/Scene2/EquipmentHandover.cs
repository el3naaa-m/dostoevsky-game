using UnityEngine;
using UnityEngine.Events;

namespace NightChase.Station
{
    public sealed class EquipmentHandover : StationInteractable
    {
        static readonly string[] Ids = { "Pistol", "IdCard", "Handcuffs" };
        static readonly string[] Names = { "Пистолет", "Удостоверение", "Наручники" };
        readonly bool[] surrendered = new bool[3];
        [SerializeField] UnityEvent onComplete = new();
        int count;

        public override string Prompt(Scene2FlowManager flow) =>
            flow.State == Scene2FlowManager.Stage.HandoverEquipment ? "E — сдать снаряжение сотруднику архива" : "";

        public override void Use(Scene2FlowManager flow)
        {
            if (string.IsNullOrEmpty(Prompt(flow))) return;
            flow.OpenPanel("Сотрудник архива", $"Сдано {count}/3");
            for (int i = 0; i < Ids.Length; i++)
            {
                int index = i;
                flow.UI.Button(surrendered[i] ? Names[i] + " — сдано" : "Сдать: " + Names[i], () =>
                {
                    if (!flow.Inventory.Remove(Ids[index])) return;
                    flow.Audio.PlayOneShot(flow.Audio.metal);
                    surrendered[index] = true;
                    if (++count < 3) { Use(flow); return; }
                    flow.ClosePanel();
                    flow.SetStage(Scene2FlowManager.Stage.NoticeBoard);
                    onComplete.Invoke();
                }, !surrendered[i] && flow.Inventory.Has(Ids[i]));
            }
            flow.UI.Button("Закрыть", flow.ClosePanel);
        }
    }
}
// На сотрудника архива с коллайдером. On Complete — дополнительное событие.
