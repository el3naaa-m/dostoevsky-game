using System.Collections;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class SecurityAttention : MonoBehaviour
    {
        [SerializeField, Min(1)] float secondsAtBoard = 18;
        [SerializeField, Min(.1f)] float fadeTime = .45f;
        Scene2FlowManager flow;
        NoticeBoard board;
        float time;
        bool warned, resetting;

        internal void Bind(Scene2FlowManager manager, NoticeBoard noticeBoard)
        { flow = manager; board = noticeBoard; }

        void Update()
        {
            if (!flow || resetting || flow.Dialogue.Active || flow.State != Scene2FlowManager.Stage.SearchPuzzle) return;
            bool nearby = board.Inspecting || Vector3.Distance(flow.Player.transform.position, board.transform.position) < 2.25f;
            if (!nearby) { time = 0; return; }
            time += Time.unscaledDeltaTime;
            if (time < secondsAtBoard) return;
            time = 0;
            if (!warned)
            {
                warned = true;
                flow.Audio.PlayOneShot(flow.Audio.warning);
                flow.Notify("Охрана обратила на вас внимание. Отойдите от доски.", 4);
            }
            else StartCoroutine(Caught());
        }

        IEnumerator Caught()
        {
            resetting = true;
            flow.Audio.PlayOneShot(flow.Audio.caught);
            flow.ClosePanel();
            flow.Lock(true);
            flow.Notify("Вас заметили. Обыск начнётся заново.", 3);
            yield return flow.Fade(1, fadeTime);
            flow.RestartSearch();
            yield return flow.Fade(0, fadeTime);
            flow.Lock(false);
            warned = resetting = false;
        }
    }
}
// На Scene2 Mechanics. Seconds At Board задаёт время до предупреждения и повторной проверки.
