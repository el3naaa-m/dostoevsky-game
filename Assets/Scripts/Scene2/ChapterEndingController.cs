using System.Collections;
using TMPro;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class ChapterEndingController : MonoBehaviour
    {
        internal const string ExitObjective = "ВЫБЕРИТЕСЬ ИЗ УЧАСТКА";
        const string ExitDirections = "Направляйтесь к запасному выходу.";
        const int CountdownSeconds = 66 * 60 * 60;
        static readonly DialogueData.Line[] Monologue =
        {
            new("Карамазов","Два дня и восемнадцать часов. Вот и всё, что осталось от нашей семьи."),
            new("Карамазов","Всю жизнь я думал, что ты герой."),
            new("Карамазов","А ты... ты просто сломанный человек, который хочет забрать с собой как можно больше людей."),
            new("Карамазов","Я не сдам тебя федералам. Я сам найду тебя и остановлю.")
        };

        Scene2FlowManager flow;
        Transform eyes;
        TextMeshProUGUI timer, directions;
        double countdownStart;
        int displayedSeconds = -1;
        bool started, exiting;

        internal bool CanExit => started && !exiting && flow.State == Scene2FlowManager.Stage.Exit;

        internal void Bind(Scene2FlowManager manager, Camera view)
        {
            flow = manager;
            eyes = view ? view.transform : null;
        }

        internal void Begin(InvestigationNotes notes)
        {
            if (started || flow.State != Scene2FlowManager.Stage.SearchPuzzle || !flow.InvestigationUnlocked ||
                notes.FatherCaseCount != 3 || !notes.FinalRecorded) return;

            started = true;
            flow.Audio.BeginEndingMusic();
            flow.Lock(true);
            Cursor.visible = false;
            flow.SetStage(Scene2FlowManager.Stage.ChapterEnding);
            flow.UI.Objective.gameObject.SetActive(false);
            flow.UI.EvidenceProgress.gameObject.SetActive(false);
            flow.UI.Prompt.gameObject.SetActive(false);
            StartCoroutine(Introduction());
        }

        IEnumerator Introduction()
        {
            yield return new WaitForSecondsRealtime(5.5f);
            flow.ClosePanel(false);
            flow.Dialogue.PlayMonologue(DialogueData.From(Monologue), () => StartCoroutine(RevealObjective()));
        }

        IEnumerator RevealObjective()
        {
            yield return new WaitForSecondsRealtime(.55f);
            timer = flow.UI.ChapterLabel("Chapter countdown","",new(.08f,.727f),new(.92f,.81f),33);
            timer.fontStyle = FontStyles.Bold;
            timer.characterSpacing = 3;
            timer.color = new Color(.98f,.88f,.74f);
            timer.alpha = 0;
            countdownStart = Time.unscaledTimeAsDouble;
            flow.Audio.StartCountdownAudio();
            UpdateTimer();
            yield return FadeText(timer, .65f);
            flow.SetStage(Scene2FlowManager.Stage.Exit);
            var objective = flow.UI.Objective;
            objective.alpha = 0;
            objective.gameObject.SetActive(true);
            directions = flow.UI.ChapterLabel("Emergency exit directions",ExitDirections,new(.1f,.825f),new(.9f,.895f),20);
            directions.alpha = 0;
            StartCoroutine(FadeText(objective, .52f));
            yield return FadeText(directions, .52f);
            flow.UI.Prompt.gameObject.SetActive(true);
            flow.Lock(false);
        }

        void Update()
        {
            if (timer && timer.gameObject.activeSelf) UpdateTimer();
        }

        void UpdateTimer()
        {
            int seconds = Mathf.Clamp(CountdownSeconds - (int)System.Math.Floor(Time.unscaledTimeAsDouble - countdownStart),0,CountdownSeconds);
            if (seconds == displayedSeconds) return;
            displayedSeconds = seconds;
            if (seconds == 0) flow.Audio.EndCountdownAudio();
            timer.text = FormatCountdown(seconds);
        }

        internal static string FormatCountdown(int seconds)
        {
            seconds = Mathf.Clamp(seconds,0,CountdownSeconds);
            return $"{seconds / 86400:00} ДНЯ : {seconds / 3600 % 24:00} ЧАСОВ : {seconds / 60 % 60:00} МИНУТ : {seconds % 60:00} СЕКУНД";
        }

        internal void ExitThrough(ExitTrigger door)
        {
            if (!CanExit) return;
            exiting = true;
            StartCoroutine(Leave(door));
        }

        IEnumerator Leave(ExitTrigger door)
        {
            flow.ClosePanel(false);
            flow.Lock(true);
            Cursor.visible = false;
            flow.UI.Prompt.gameObject.SetActive(false);
            flow.UI.Objective.gameObject.SetActive(false);
            directions.gameObject.SetActive(false);
            var threshold = door.transform.position;
            var inward = door.transform.forward;
            var motor = flow.Player.GetComponent<CharacterController>();
            var previousFacing = motor.transform.rotation;
            var previousLook = eyes ? eyes.localRotation : Quaternion.identity;
            var exitFacing = Quaternion.LookRotation(-inward);
            flow.Audio.PlayOneShot(flow.Audio.exitDoor);
            yield return door.Open();
            yield return WalkTo(motor, threshold + inward * .67f, exitFacing);
            yield return WalkTo(motor, threshold - inward * 1.05f, exitFacing);
            if (Vector3.Dot(motor.transform.position - threshold,inward) > -.3f)
            {
                motor.transform.rotation = previousFacing;
                if (eyes) eyes.localRotation = previousLook;
                flow.UI.Objective.gameObject.SetActive(true);
                flow.UI.Prompt.gameObject.SetActive(true);
                directions.gameObject.SetActive(true);
                flow.Notify("Подойдите ближе к центру двери и нажмите E.", 2.5f);
                exiting = false;
                flow.Lock(false);
                yield break;
            }

            yield return flow.Fade(1, 1.3f);
            timer.gameObject.SetActive(false);
            flow.Audio.EndCountdownAudio();
            var title = flow.UI.ChapterLabel("Chapter end title","ПРОДОЛЖЕНИЕ СЛЕДУЕТ...",new(.06f,.40f),new(.94f,.60f),43,true);
            title.color = new Color(.93f,.94f,.95f);
            title.characterSpacing = 7;
            yield return FadeText(title, 1.1f);
        }

        IEnumerator WalkTo(CharacterController motor, Vector3 target, Quaternion facing)
        {
            for (float time = 0; time < 3.1f; time += Time.deltaTime)
            {
                var remaining = Vector3.ProjectOnPlane(target - motor.transform.position, Vector3.up);
                if (remaining.sqrMagnitude < .004f) yield break;
                float dt = Time.deltaTime;
                motor.transform.rotation = Quaternion.RotateTowards(motor.transform.rotation,facing,180 * dt);
                if (eyes) eyes.localRotation = Quaternion.RotateTowards(eyes.localRotation,Quaternion.identity,120 * dt);
                motor.Move(Vector3.ClampMagnitude(remaining,1.45f * dt));
                yield return null;
            }
        }

        static IEnumerator FadeText(TMP_Text text, float duration)
        {
            text.alpha = 0;
            for (float time = 0; time < duration; time += Time.unscaledDeltaTime)
            {
                text.alpha = Mathf.SmoothStep(0,1,time / duration);
                yield return null;
            }
            text.alpha = 1;
        }
    }
}
