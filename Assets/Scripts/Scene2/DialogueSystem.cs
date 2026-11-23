using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace NightChase.Station
{
    public sealed class DialogueSystem : MonoBehaviour
    {
        [SerializeField] UnityEvent<string> onLineEvent = new();
        [SerializeField] UnityEngine.UI.Image defaultPortrait;
        public event Action OnDialogueEnd;
        Scene2FlowManager flow;
        StationUI view;
        bool linear;
        float elapsed;
        bool pendingNext, monologue, turningPage, spaceArmed;
        AudioSource fallbackAudio;
        internal int CurrentIndex => index;

        // Оба режима используют один Canvas. Улица сохраняет собственные блокировки камеры и игрока.
        internal static StationUI CreateView(Transform parent, bool linear = false, bool interactive = false)
        {
            var template = Resources.Load<GameObject>("SharedDialogueCanvas");
            var view = template ? Instantiate(template, parent, false).GetComponent<StationUI>()
                : new GameObject("Dialogue Canvas").AddComponent<StationUI>();
            if (!view.transform.parent) view.transform.SetParent(parent, false);
            view.Build(linear, interactive);
            return view;
        }

        internal static DialogueSystem CreateLinear(GameObject owner)
        {
            var system = owner.AddComponent<DialogueSystem>();
            system.linear = true;
            system.view = CreateView(owner.transform, true);
            return system;
        }
        DialogueData dialogue;
        int index, shownFrame;
        Action finished;
        public bool Active => dialogue;

        internal void Bind(Scene2FlowManager manager) { flow = manager; view = manager.UI; }

        public void Play(DialogueData data, Action onEnd = null) => Begin(data, onEnd, false);

        internal void PlayMonologue(DialogueData data, Action onEnd) => Begin(data, onEnd, true);

        void Begin(DialogueData data, Action onEnd, bool useMonologue)
        {
            if (Active) return;
            if (!data || data.lines == null || data.lines.Length == 0)
            {
                onEnd?.Invoke();
                return;
            }
            pendingNext = false;
            monologue = useMonologue;
            spaceArmed = false;
            dialogue = data;
            index = 0;
            finished = onEnd;
            Show();
        }

        void Update()
        {
            if (!Active) return;
            var keyboard = Keyboard.current;
            if (monologue)
            {
                if (keyboard == null) return;
                bool pressed = keyboard.spaceKey.isPressed;
                bool nextPage = spaceArmed && pressed;
                spaceArmed = !pressed;
                if (nextPage && !turningPage && shownFrame != Time.frameCount)
                    StartCoroutine(TurnPage());
                return;
            }
            if (keyboard?.spaceKey.wasPressedThisFrame == true) pendingNext = true;
            if (pendingNext && shownFrame != Time.frameCount)
            {
                pendingNext = false;
                Next();
                return;
            }
            float duration = dialogue.lines[index].autoAdvanceSeconds;
            if (!linear || duration <= 0 || shownFrame == Time.frameCount) return;
            elapsed += Time.unscaledDeltaTime * (keyboard?.shiftKey.isPressed == true ? 3f : 1f);
            if (elapsed >= duration) NextLine(false);
        }

        void Show()
        {
            var line = dialogue.lines[index];
            shownFrame = Time.frameCount;
            elapsed = 0;
            if (flow)
            {
                flow.Lock(true);
                if (!monologue) flow.FocusDialogueSpeaker(line.speaker);
            }
            view.ShowDialogue(line.speaker, line.text, line.portrait ? line.portrait : defaultPortrait ? defaultPortrait.sprite : null,
                linear || monologue ? null : line.choices, SelectChoice, monologue);
        }

        public void Next() { if (!monologue) NextLine(true); }

        IEnumerator TurnPage()
        {
            turningPage = true;
            yield return view.FadeDialogue(0, .18f);
            Advance(index + 1);
            if (Active) yield return view.FadeDialogue(1, .2f);
            turningPage = false;
        }

        void NextLine(bool userInput)
        {
            if (!Active || shownFrame == Time.frameCount) return;
            if (!linear && dialogue.lines[index].choices is { Length: > 0 }) return;
            if (userInput || linear) PlayPageClick();
            Advance(index + 1);
        }

        void PlayPageClick()
        {
            if (AudioManager.Instance && AudioManager.Instance.PlayDialogueClick()) return;
            var clip = Resources.Load<AudioClip>("StreetDialogueClick");
            if (!clip) return;
            if (!fallbackAudio)
            {
                fallbackAudio = gameObject.AddComponent<AudioSource>();
                fallbackAudio.playOnAwake = false;
                fallbackAudio.spatialBlend = 0;
            }
            fallbackAudio.PlayOneShot(clip,.13f);
        }

        void SelectChoice(int selected)
        {
            if (!Active || shownFrame == Time.frameCount) return;
            var choices = dialogue.lines[index].choices;
            if (choices == null || selected < 0 || selected >= choices.Length) return;
            int target = choices[selected].nextLineIndex;
            if (target < -1 || target >= dialogue.lines.Length)
            {
                Debug.LogWarning($"Некорректная цель варианта ответа: {target}.", this);
                return;
            }
            AudioManager.Instance?.PlayDialogueClick();
            Advance(target);
        }

        void Advance(int target)
        {
            string cue = dialogue.lines[index].eventId;
            if (!string.IsNullOrEmpty(cue)) onLineEvent.Invoke(cue);
            if (target >= 0 && target < dialogue.lines.Length) { index = target; Show(); return; }
            dialogue = null;
            if (flow) flow.ClosePanel(!monologue);
            else view.Close();
            monologue = false;
            var callback = finished;
            finished = null;
            callback?.Invoke();
            OnDialogueEnd?.Invoke();
        }
    }
}
// На Scene2 Mechanics. В On Line Event подключайте нужные события по ID реплики.
