using System;
using UnityEngine;

namespace NightChase.Station
{
    [CreateAssetMenu(menuName = "Night Chase/Station Dialogue")]
    public sealed class DialogueData : ScriptableObject
    {
        [Serializable] public struct Choice
        {
            public string label;
            [Tooltip("Индекс реплики с нуля. -1 завершает диалог.")]
            public int nextLineIndex;
        }

        [Serializable] public struct Line
        {
            public string speaker;
            [TextArea] public string text;
            public string eventId;
            public Sprite portrait;
            public Choice[] choices;
            public Line(string speaker, string text, string eventId = null)
            { this.speaker = speaker; this.text = text; this.eventId = eventId; portrait = null; choices = null; }
        }

        public Line[] lines;

        public static DialogueData From(params Line[] lines)
        {
            var asset = CreateInstance<DialogueData>();
            asset.lines = lines;
            return asset;
        }
    }
}
// Назначьте DialogueData в Inspector Scene2FlowManager. Event Id необязателен.
