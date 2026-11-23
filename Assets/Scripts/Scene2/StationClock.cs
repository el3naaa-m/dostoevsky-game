using System;
using UnityEngine;

namespace NightChase.Station
{
    internal sealed class StationClock : StationInteractable
    {
        public override string Prompt(Scene2FlowManager flow) => "E — Посмотреть на часы";

        public override void Use(Scene2FlowManager flow)
        {
            // Время берётся с устройства игрока, дата принадлежит сюжету.
            flow.Notify($"{DateTime.Now:HH:mm}  ·  12 сентября 1999 г.",5f);
        }
    }
}
