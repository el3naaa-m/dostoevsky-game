using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightChase
{
    sealed class ChasePlayerClimb : MonoBehaviour
    {
        FirstPersonController movement;
        CharacterController body;
        Transform eyes;
        ChaseTrigger gate;
        float gateZ, floorHeight;
        Vector3 eyePosition;
        Quaternion eyeRotation;
        bool climbing, restoreCapture;
        float jumpAt = float.NegativeInfinity;

        public void Initialize(FirstPersonController controller, Transform cameraEyes, ChaseTrigger gateZone, float fenceZ)
        {
            movement = controller;
            body = controller.GetComponent<CharacterController>();
            eyes = cameraEyes;
            gate = gateZone;
            gateZ = fenceZ;
        }

        void Update()
        {
            if (climbing || !movement || !movement.enabled) return;
            var keyboard = Keyboard.current;
            if (keyboard == null || !gate.Contains(body.bounds.center)) return;
            if (keyboard.spaceKey.wasPressedThisFrame) jumpAt = Time.time;
            if (body.isGrounded) floorHeight = movement.transform.position.y;
            bool recentJump = Time.time - jumpAt <= .30f;
            bool airborne = !body.isGrounded && movement.transform.position.y > floorHeight + .09f;
            if (keyboard.eKey.wasPressedThisFrame && (airborne || recentJump) &&
                Cursor.lockState == CursorLockMode.Locked)
                StartCoroutine(Climb());
        }

        IEnumerator Climb()
        {
            Transform root = movement.transform;
            Vector3 from = root.position;
            float side = from.z >= gateZ ? 1f : -1f;
            var to = new Vector3(Mathf.Clamp(from.x, 36.58f, 38.39f), from.y, gateZ - side * 1.10f);
            if (Physics.Raycast(to + Vector3.up * 2.5f, Vector3.down, out var ground, 4f,
                    ~0, QueryTriggerInteraction.Ignore))
                to.y = ground.point.y + body.skinWidth;

            float capsuleRadius = body.radius * .88f;
            Vector3 lower = to + Vector3.up * (body.radius + .02f);
            Vector3 upper = to + Vector3.up * (body.height - body.radius);
            if (Physics.CheckCapsule(lower, upper, capsuleRadius, ~0, QueryTriggerInteraction.Ignore))
                yield break;

            climbing = true;
            eyePosition = eyes.localPosition;
            eyeRotation = eyes.localRotation;
            restoreCapture = Cursor.lockState == CursorLockMode.Locked;
            movement.enabled = false;
            body.enabled = false;
            for (float elapsed = 0; elapsed < .88f; elapsed += Time.deltaTime)
            {
                float t = elapsed / .88f;
                root.position = Arc(from, to, t, 1.80f);
                eyes.SetLocalPositionAndRotation(eyePosition - Vector3.up * (.16f * Mathf.Sin(Mathf.PI * t)),
                    eyeRotation * Quaternion.Euler(9f * Mathf.Sin(2f * Mathf.PI * t), 0, 0));
                yield return null;
            }

            root.position = to;
            Restore();
        }

        public static Vector3 Arc(Vector3 from, Vector3 to, float t, float height)
        {
            return Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t)) + Vector3.up * (Mathf.Sin(Mathf.PI * t) * height);
        }

        void Restore()
        {
            if (!climbing) return;
            climbing = false;
            if (eyes) eyes.SetLocalPositionAndRotation(eyePosition, eyeRotation);
            if (body) body.enabled = true;
            if (!movement) return;
            movement.enabled = true;
            movement.SendMessage("SetCapture", restoreCapture && Application.isFocused, SendMessageOptions.DontRequireReceiver);
        }

        void OnDisable() => Restore();
    }
}
