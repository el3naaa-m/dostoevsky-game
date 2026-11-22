using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightChase
{
    [RequireComponent(typeof(CharacterController))]
    sealed class ChaseTrafficImpact : MonoBehaviour
    {
        float impactAt;
        bool hit;
        const float FadeDuration = .72f;

        void OnControllerColliderHit(ControllerColliderHit collision)
        {
            var car = collision.collider.GetComponentInParent<StreetCar>();
            if (car && car.IsMoving) Hit();
        }

        public void Hit()
        {
            if (hit) return;
            hit = true;
            impactAt = Time.unscaledTime;
            if (TryGetComponent<FirstPersonController>(out var movement)) movement.enabled = false;
            StartCoroutine(Restart());
        }

        IEnumerator Restart()
        {
            yield return new WaitForSecondsRealtime(FadeDuration);
            string path = gameObject.scene.path;
#if UNITY_EDITOR
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield break;
            }
#endif
            SceneManager.LoadScene(path);
        }

        void OnGUI()
        {
            if (!hit) return;
            int depth = GUI.depth;
            Color color = GUI.color;
            GUI.depth = -100;
            GUI.color = new Color(0, 0, 0, Mathf.Clamp01((Time.unscaledTime - impactAt) / FadeDuration));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.depth = depth;
        }
    }
}
