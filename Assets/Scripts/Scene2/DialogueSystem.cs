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
        DialogueData dialogue;
        int index, shownFrame;
        Action finished;
        public bool Active => dialogue;

        internal void Bind(Scene2FlowManager manager) => flow = manager;

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
            if (Active && Keyboard.current?.spaceKey.wasPressedThisFrame == true) Next();
        }

        void Show()
        {
            var line = dialogue.lines[index];
            shownFrame = Time.frameCount;
            flow.Lock(true);
            flow.UI.ShowDialogue(line.speaker, line.text, line.portrait ? line.portrait : defaultPortrait ? defaultPortrait.sprite : null,
                line.choices, SelectChoice);
        }

        public void Next()
        {
            if (!Active || shownFrame == Time.frameCount) return;
            if (dialogue.lines[index].choices is { Length: > 0 }) return;
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
            Advance(target);
        }

        void Advance(int target)
        {
            string cue = dialogue.lines[index].eventId;
            if (!string.IsNullOrEmpty(cue)) onLineEvent.Invoke(cue);
            if (target >= 0 && target < dialogue.lines.Length) { index = target; Show(); return; }
            dialogue = null;
            flow.ClosePanel();
            var callback = finished;
            finished = null;
            callback?.Invoke();
            OnDialogueEnd?.Invoke();
        }
    }
}
// На Scene2 Mechanics. В On Line Event подключайте нужные события по ID реплики.
