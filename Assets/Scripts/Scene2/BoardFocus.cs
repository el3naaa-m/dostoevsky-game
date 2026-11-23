using UnityEngine;
namespace NightChase.Station
{
    public sealed class BoardFocus : MonoBehaviour
    {
        Camera eyes;
        float original, desired;
        bool tracking;
        public void Inspect(Camera camera, Vector3 point)
        {
            if (!camera) return;
            if (!tracking) { original = camera.fieldOfView; origin = camera.transform.localRotation; }
            eyes = camera;
            desired = original * .73f;
            tracking = true;

            target = Quaternion.LookRotation(point - eyes.transform.position, Vector3.up);
            returning = false;
        }
        Quaternion origin, target;
        bool returning;
        public void Release() { returning = true; desired = original; }
        void LateUpdate()
        {
            if (!tracking || !eyes) return;
            float t = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            eyes.fieldOfView = Mathf.Lerp(eyes.fieldOfView,desired,t);
            if (!returning) eyes.transform.rotation = Quaternion.Slerp(eyes.transform.rotation,target,t);
            else
            {
                eyes.transform.localRotation = Quaternion.Slerp(eyes.transform.localRotation,origin,t);
                if (Mathf.Abs(eyes.fieldOfView-original) < .1f)
                { eyes.fieldOfView = original; eyes.transform.localRotation = origin; tracking = false; }
            }
        }
    }
}
