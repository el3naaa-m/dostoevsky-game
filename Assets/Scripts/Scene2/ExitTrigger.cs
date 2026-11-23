using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightChase.Station
{
    public sealed class ExitTrigger : MonoBehaviour
    {
        [SerializeField] string nextScene;
        Scene2FlowManager flow;
        bool running;
        internal void Bind(Scene2FlowManager manager) => flow = manager;

        public void Enter()
        {
            if (!flow || running || flow.State != Scene2FlowManager.Stage.Exit) return;
            running = true;
            var question = new DialogueData.Line("Джо", "Ты уверен, что хочешь копать это дальше?");
            var conclusion = new DialogueData.Line("Джо", "Похоже, твой отец не просто пропал. Он сбежал. И, судя по всему, не от хорошей жизни.");
            flow.Dialogue.Play(flow.Inventory.Has("father_note") ? DialogueData.From(question, conclusion) : DialogueData.From(question), () => StartCoroutine(Finish()));
        }

        IEnumerator Finish()
        {
            flow.ClosePanel();
            flow.Lock(true);
            flow.Audio.PlayOneShot(flow.Audio.exitDoor);
            yield return flow.Fade(1, .7f);
            if (!string.IsNullOrWhiteSpace(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            {
                yield return SceneManager.LoadSceneAsync(nextScene);
                yield break;
            }

            Debug.Log("Конец сцены 2");
            yield return flow.Fade(0, .45f);
            flow.UI.Objective.text = "Конец сцены 2";
        }
    }
}
// У выхода рядом с TriggerZone. Его On Enter вызывает Enter; Next Scene необязательна.
