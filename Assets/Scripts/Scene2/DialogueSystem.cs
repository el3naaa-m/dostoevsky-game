using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace NightChase.Station
{
    public sealed class DialogueSystem : MonoBehaviour
    {
        [SerializeField] UnityEvent<string> onLineEvent = new();
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
            flow.OpenPanel(line.speaker, line.text);
            flow.UI.Button("Далее   [Пробел]", Next);
        }

        public void Next()
        {
            if (!Active || shownFrame == Time.frameCount) return;
            string cue = dialogue.lines[index].eventId;
            if (!string.IsNullOrEmpty(cue)) onLineEvent.Invoke(cue);
            if (++index < dialogue.lines.Length) { Show(); return; }
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
