using System;
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
        internal int CurrentIndex => index;

        // Оба режима используют один Canvas. Улица сохраняет собственные блокировки камеры и игрока.
        internal static StationUI CreateView(Transform parent, bool linear = false)
        {
            var template = Resources.Load<GameObject>("SharedDialogueCanvas");
            var view = template ? Instantiate(template, parent, false).GetComponent<StationUI>()
                : new GameObject("Dialogue Canvas").AddComponent<StationUI>();
            if (!view.transform.parent) view.transform.SetParent(parent, false);
            view.Build(linear);
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

        public void Play(DialogueData data, Action onEnd = null)
        {
            if (Active) return;
            if (!data || data.lines == null || data.lines.Length == 0)
            {
                onEnd?.Invoke();
                return;
            }
            dialogue = data;
            index = 0;
            finished = onEnd;
            Show();
        }

        void Update()
        {
            if (!Active) return;
            var keyboard = Keyboard.current;
            if (keyboard?.spaceKey.wasPressedThisFrame == true) { Next(); return; }
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
            if (flow) flow.Lock(true);
            view.ShowDialogue(line.speaker, line.text, line.portrait ? line.portrait : defaultPortrait ? defaultPortrait.sprite : null,
                linear ? null : line.choices, SelectChoice);
        }

        public void Next() => NextLine(true);

        void NextLine(bool userInput)
        {
            if (!Active || shownFrame == Time.frameCount) return;
            if (!linear && dialogue.lines[index].choices is { Length: > 0 }) return;
            if (userInput) AudioManager.Instance?.PlayDialogueClick();
            Advance(index + 1);
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
            if (flow) flow.ClosePanel();
            else view.Close();
            var callback = finished;
            finished = null;
            callback?.Invoke();
            OnDialogueEnd?.Invoke();
        }
    }
}
// На Scene2 Mechanics. В On Line Event подключайте нужные события по ID реплики.
