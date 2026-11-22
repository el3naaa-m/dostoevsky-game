using System;
using UnityEngine;

namespace NightChase
{
    sealed class ChaseTrigger : MonoBehaviour
    {
        BoxCollider volume;
        Transform subject;
        Action entered;
        bool fired;

        public static ChaseTrigger Create(Transform parent, string name, Vector3 center, Vector3 size,
            Transform subject = null, Action entered = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = center;
            var trigger = root.AddComponent<ChaseTrigger>();
            trigger.subject = subject;
            trigger.entered = entered;
            trigger.volume = root.AddComponent<BoxCollider>();
            trigger.volume.isTrigger = true;
            trigger.volume.size = size;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return trigger;
        }

        public bool Fired => fired;
        public bool Contains(Vector3 position) => volume.bounds.Contains(position);

        void OnTriggerEnter(Collider other)
        {
            if (subject && other.transform.IsChildOf(subject)) Check(other.bounds.center);
        }

        public void Check(Vector3 position)
        {
            if (fired || entered == null || !Contains(position)) return;
            fired = true;
            entered();
        }
    }
}
