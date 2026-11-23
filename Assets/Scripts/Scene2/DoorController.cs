using UnityEngine;

namespace NightChase.Station
{
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField] Transform leaf;
        [SerializeField] float openAngle = 95f;
        [SerializeField, Min(.1f)] float openingSpeed = 260f, closingSpeed = 145f;
        [SerializeField, Min(.1f)] float approachDistance = 1.65f, closeDelay = 1.4f;
        Transform player, chief;
        Quaternion closed;
        float holdUntil;

        internal void Bind(Transform playerRoot, Transform chiefRoot)
        {
            player = playerRoot;
            chief = chiefRoot;
            if (!leaf) leaf = transform.Find("Door");
            if (!leaf) { Debug.LogWarning("Не найдено полотно двери допросной.", this); return; }
            closed = leaf.localRotation;
            // Используем один источник движения для полотна.
            if (TryGetComponent<QA_Door>(out var legacy)) { legacy.isAutomatic = false; legacy.enabled = false; }
            if (TryGetComponent<Animation>(out var legacyAnimation)) legacyAnimation.Stop();
        }

        public void Open() => holdUntil = Time.time + closeDelay;
        public void Close() => holdUntil = Time.time;

        void Update()
        {
            if (!leaf) return;
            if (Near(player) || Near(chief)) Open();
            bool open = Time.time < holdUntil;
            var target = open ? closed * Quaternion.Euler(0, openAngle, 0) : closed;
            leaf.localRotation = Quaternion.RotateTowards(leaf.localRotation, target,
                (open ? openingSpeed : closingSpeed) * Time.deltaTime);
        }

        bool Near(Transform actor) => actor && (actor.position - transform.position).sqrMagnitude < approachDistance * approachDistance;
    }
}
