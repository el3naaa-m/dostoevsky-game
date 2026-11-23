using System;
using UnityEngine;
using UnityEngine.Events;

namespace NightChase.Station
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TriggerZone : MonoBehaviour
    {
        [SerializeField] bool oneShot = true;
        [SerializeField] UnityEvent onEnter = new();
        bool fired;
        Action callback;

        void Awake() => GetComponent<BoxCollider>().isTrigger = true;

        internal void Bind(Action action) => callback = action;

        void OnTriggerEnter(Collider hit)
        {
            if (fired && oneShot || !hit.GetComponentInParent<FirstPersonController>()) return;
            fired = true;
            onEnter.Invoke();
            callback?.Invoke();
        }
    }
}
// Повесьте на пустой объект, задайте Size и On Enter. Тег Player не нужен.
