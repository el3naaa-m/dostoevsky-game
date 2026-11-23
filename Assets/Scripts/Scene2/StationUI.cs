using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NightChase.Station
{
    sealed class StationUI : MonoBehaviour
    {
        public TextMeshProUGUI Objective { get; private set; }
        internal TextMeshProUGUI EvidenceProgress { get; private set; }
        public TextMeshProUGUI Prompt { get; private set; }
        public CanvasGroup Fade { get; private set; }
        RectTransform canvas, panel, buttons;
        TextMeshProUGUI header, body;
        RawImage photo;
        RectTransform details;
        TMP_FontAsset font;
        RectTransform dialogueStrip;
        TextMeshProUGUI dialogueSpeaker, dialogueLine, dialogueNext;
        Image dialogueAvatar;
        RectTransform dialogueOptions;
        internal EvidenceWorkspace Workspace { get; private set; }
        public bool Modal => (Workspace && Workspace.Active) || panel.gameObject.activeSelf || dialogueStrip.gameObject.activeSelf;

        public void Build(bool linear = false)
        {
            font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            var view = GetComponent<Canvas>();
            if (view) canvas = (RectTransform)transform;
            else
            {
                canvas = Rect(transform, "Station Canvas", Vector2.zero, Vector2.zero);
                view = canvas.gameObject.AddComponent<Canvas>();
            }
            view.renderMode = RenderMode.ScreenSpaceOverlay;
            view.sortingOrder = 120;
            var scale = canvas.gameObject.AddComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720);
            if (!linear) canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (!linear && !FindAnyObjectByType<EventSystem>())
            {
                var events = new GameObject("Station Events", typeof(EventSystem));
                events.transform.SetParent(transform, false);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Objective = Text(canvas, "Задача", new Vector2(0, 299), new Vector2(1150, 50), 24);
            Prompt = Text(canvas, "", new Vector2(0, -220), new Vector2(930, 56), 26);
            if (linear) { Objective.gameObject.SetActive(false); Prompt.gameObject.SetActive(false); }
            else
            {
                EvidenceProgress = Text(canvas, "Улики 0/3   ·   J — журнал улик", new Vector2(0,259), new Vector2(725,33), 20);
                EvidenceProgress.gameObject.SetActive(false);
            }
            panel = Rect(canvas, "Station panel", Vector2.zero, new Vector2(880, 500));
            panel.gameObject.AddComponent<Image>().color = new Color(.065f, .075f, .09f, .97f);
            header = Text(panel, "", new Vector2(0, 205), new Vector2(810, 45), 30);
            body = Text(panel, "", new Vector2(0, 80), new Vector2(790, 185), 25);
            photo = Rect(panel, "Photo", new Vector2(0, 124), new Vector2(180, 126)).gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false;
            details = Rect(panel, "Inventory items", new Vector2(0, 42), new Vector2(760, 260));
            buttons = Rect(panel, "Actions", new Vector2(0, -123), new Vector2(760, 205));
            var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;
            dialogueStrip = Rect(canvas, "Dialogue strip", Vector2.zero, Vector2.zero);
            dialogueStrip.anchorMin = new Vector2(.035f,.025f);
            dialogueStrip.anchorMax = new Vector2(.965f,.245f);
            dialogueStrip.offsetMin = dialogueStrip.offsetMax = Vector2.zero;
            var backdrop = dialogueStrip.gameObject.AddComponent<Image>();
            backdrop.color = new Color(.028f,.044f,.048f,.92f);
            var border = dialogueStrip.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(.59f,.51f,.32f,.8f);
            border.effectDistance = new Vector2(1,-1);
            var portrait = Rect(dialogueStrip, "Portrait", Vector2.zero, new Vector2(105,105));
            portrait.anchorMin = portrait.anchorMax = new Vector2(0,.5f);
            portrait.anchoredPosition = new Vector2(68,0);
            dialogueAvatar = portrait.gameObject.AddComponent<Image>();
            dialogueAvatar.preserveAspect = true;
            dialogueAvatar.raycastTarget = false;
            dialogueOptions = Rect(dialogueStrip, "Choices", Vector2.zero, Vector2.zero);
            dialogueOptions.anchorMin = new Vector2(.13f,.04f);
            dialogueOptions.anchorMax = new Vector2(.965f,.45f);
            dialogueOptions.offsetMin = dialogueOptions.offsetMax = Vector2.zero;
            backdrop.raycastTarget = false;
            dialogueSpeaker = DialogueLabel("", new Vector2(.025f,.68f), new Vector2(.94f,.95f), 24, TextAlignmentOptions.Left);
            dialogueLine = DialogueLabel("", new Vector2(.025f,.25f), new Vector2(.965f,.69f), 25, TextAlignmentOptions.MidlineLeft);
            dialogueSpeaker.color = new Color(.91f,.78f,.55f);
            dialogueNext = DialogueLabel("[Пробел] Далее", new Vector2(.55f,.025f), new Vector2(.965f,.24f), 20, TextAlignmentOptions.MidlineRight);
            Workspace = gameObject.AddComponent<EvidenceWorkspace>();
            Workspace.Build(canvas,font);
            var cover = Rect(canvas, "Fade", Vector2.zero, Vector2.zero);
            cover.anchorMin = Vector2.zero;
            cover.anchorMax = Vector2.one;
            cover.sizeDelta = Vector2.zero;
            cover.gameObject.AddComponent<Image>().color = Color.black;
            Fade = cover.gameObject.AddComponent<CanvasGroup>();
            Fade.blocksRaycasts = false;
            Fade.alpha = 0;
            Close();
        }

        internal void MagnifyText() => StartCoroutine(EnlargeText());
        System.Collections.IEnumerator EnlargeText()
        {
            for (float t=0; t < .18f; t+=Time.unscaledDeltaTime)
            {
                body.rectTransform.localScale = Vector3.one * Mathf.Lerp(.92f,1,t/.18f);
                yield return null;
            }
            body.rectTransform.localScale = Vector3.one;
        }

        public void BoardStyle() => panel.GetComponent<Image>().color = new Color(.10f,.095f,.08f,.84f);

        public void Show(string heading, string content)
        {
            panel.GetComponent<Image>().color = new Color(.065f,.075f,.09f,.97f);
            header.text = heading;
            body.text = content;
            body.enableAutoSizing = true;
            body.fontSizeMin = 17;
            body.fontSizeMax = 25;
            body.rectTransform.anchoredPosition = new Vector2(0, 80);
            photo.gameObject.SetActive(false);
            details.gameObject.SetActive(false);
            foreach (Transform row in details)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            foreach (Transform child in buttons)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            panel.gameObject.SetActive(true);
        }

        public void Button(string title, Action action, bool enabled = true, Texture texture = null)
        {
            var rect = Rect(buttons, title, Vector2.zero, new Vector2(740, 36));
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 36;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = title.StartsWith("Фото") ? new Color(.32f,.34f,.38f) :
                title.StartsWith("Газетная") ? new Color(.46f,.35f,.16f) :
                title.StartsWith("Снять советский") ? new Color(.37f,.40f,.32f) : new Color(.21f,.25f,.30f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            button.onClick.AddListener(() => action());
            var label = Text(rect, title, Vector2.zero, new Vector2(740, 34), 22);
            if (texture)
            {
                var art = Rect(rect, "Иллюстрация", new Vector2(-315,0), new Vector2(32,32)).gameObject.AddComponent<RawImage>();
                art.texture = texture;
                art.raycastTarget = false;
            }
            label.color = enabled ? Color.white : Color.gray;
        }

        public void ShowPhoto(Texture2D texture)
        {
            if (!texture) return;
            photo.texture = texture;
            StartCoroutine(Magnify());
            photo.gameObject.SetActive(true);
            body.rectTransform.anchoredPosition = new Vector2(0, 21);
        }

        System.Collections.IEnumerator Magnify()
        {
            for (float t = 0; t < .22f; t += Time.unscaledDeltaTime)
            {
                photo.rectTransform.localScale = Vector3.one * Mathf.Lerp(.8f, 1,t/.22f);
                yield return null;
            }
            photo.rectTransform.localScale = Vector3.one;
        }

        public void ShowItems(IEnumerable<Item> items)
        {
            details.gameObject.SetActive(true);
            int index = 0;
            foreach (var item in items)
            {
                Vector2 pos = new Vector2(-190 + (index / 5) * 380, 111 - (index % 5) * 39);
                var icon = Rect(details, "Item icon", pos + Vector2.left * 155, new Vector2(32, 32))
                    .gameObject.AddComponent<Image>();
                icon.sprite = item.icon;
                icon.preserveAspect = true;
                icon.enabled = item.icon;
                icon.raycastTarget = false;
                var label = Text(details, item.displayName, pos, new Vector2(255, 36), 22);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                index++;
            }
        }

        TextMeshProUGUI DialogueLabel(string value, Vector2 min, Vector2 max, int size, TextAlignmentOptions align)
        {
            var label = Text(dialogueStrip, value, Vector2.zero, Vector2.zero, size);
            label.rectTransform.anchorMin = min;
            label.rectTransform.anchorMax = max;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = align;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = size;
            return label;
        }

        void Update()
        {
            if (dialogueNext && dialogueNext.gameObject.activeInHierarchy)
                dialogueNext.alpha = .85f + .15f * Mathf.Sin(Time.unscaledTime * 3);
        }

        public void ShowDialogue(string speaker, string line, Sprite portrait, DialogueData.Choice[] choices, Action<int> select)
        {
            panel.gameObject.SetActive(false);
            dialogueSpeaker.text = speaker;
            dialogueLine.text = line;
            bool branching = choices != null && choices.Length > 0;
            dialogueAvatar.sprite = portrait;
            dialogueAvatar.enabled = portrait;
            float left = portrait ? .13f : .025f;
            dialogueSpeaker.rectTransform.anchorMin = new Vector2(left, branching ? .79f : .68f);
            dialogueSpeaker.rectTransform.anchorMax = new Vector2(.965f,.95f);
            dialogueLine.rectTransform.anchorMin = new Vector2(left, branching ? .48f : .25f);
            dialogueLine.rectTransform.anchorMax = new Vector2(.965f,branching ? .79f : .69f);
            dialogueStrip.anchorMax = new Vector2(.965f,branching ? Mathf.Min(.65f,.27f + choices.Length * .032f) : .245f);
            dialogueNext.gameObject.SetActive(!branching);
            foreach (Transform old in dialogueOptions)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }
            if (branching)
                for (int i = 0; i < choices.Length; i++)
                {
                    int selected = i;
                    var row = Rect(dialogueOptions, "Choice", Vector2.zero, Vector2.zero);
                    float top = 1f - i / (float)choices.Length;
                    row.anchorMin = new Vector2(0,top - .88f / choices.Length);
                    row.anchorMax = new Vector2(1,top);
                    row.offsetMin = row.offsetMax = Vector2.zero;
                    var fill = row.gameObject.AddComponent<Image>();
                    fill.color = new Color(.19f,.23f,.235f);
                    var button = row.gameObject.AddComponent<Button>();
                    button.targetGraphic = fill;
                    button.onClick.AddListener(() => select(selected));
                    var label = Text(row, choices[i].label, Vector2.zero, Vector2.zero, 20);
                    label.rectTransform.anchorMin = new Vector2(.025f,0);
                    label.rectTransform.anchorMax = new Vector2(.98f,1);
                    label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 12;
                    label.fontSizeMax = 20;
                    label.alignment = TextAlignmentOptions.MidlineLeft;
                }
            dialogueStrip.gameObject.SetActive(true);
        }

        public void Close()
        {
            panel.gameObject.SetActive(false);
            dialogueStrip.gameObject.SetActive(false);
            Workspace.Close();
        }

        TextMeshProUGUI Text(Transform parent, string value, Vector2 pos, Vector2 size, int points)
        {
            var text = Rect(parent, "Label", pos, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.text = value;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = points;
            text.raycastTarget = false;
            return text;
        }

        static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            result.anchorMin = result.anchorMax = Vector2.one * .5f;
            result.anchoredPosition = pos;
            result.sizeDelta = size;
            return result;
        }

        RectTransform alertBar, caughtPanel;
        Image alertFill;
        TextMeshProUGUI alertLabel;

        internal void ShowAttention(float amount, bool seen, bool crouched)
        {
            if (!alertBar)
            {
                alertBar = Rect(canvas, "Внимание шефа", Vector2.zero, new Vector2(350, 54));
                alertBar.anchorMin = alertBar.anchorMax = new Vector2(.5f, 0);
                alertBar.anchoredPosition = new Vector2(0, 40);
                var background = alertBar.gameObject.AddComponent<Image>();
                background.color = new Color(.035f,.045f,.05f,.9f);
                background.raycastTarget = false;
                alertLabel = Text(alertBar, "", new Vector2(0,8), new Vector2(346,30), 17);
                var fill = Rect(alertBar, "Уровень", Vector2.zero, new Vector2(20,7));
                fill.pivot = new Vector2(0,.5f);
                fill.anchoredPosition = new Vector2(-160,-13);
                alertFill = fill.gameObject.AddComponent<Image>();
                alertFill.raycastTarget = false;
            }
            alertBar.gameObject.SetActive(true);
            alertBar.SetAsLastSibling();
            alertFill.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(18,320,amount),7);
            alertFill.color = seen ? new Color(1,.18f,.13f) : Color.Lerp(new Color(.23f,.85f,.43f), Color.red, amount);
            alertLabel.text = seen ? "Шеф вас видит!" : crouched ? "Вы пригнулись   ·   F — встать" : "Внимание шефа   ·   F — пригнуться";
        }

        internal void ShowCaught()
        {
            HideAttention();
            if (!caughtPanel)
            {
                caughtPanel = Rect(canvas, "Пойманы", Vector2.zero, new Vector2(500,100));
                caughtPanel.gameObject.AddComponent<Image>().color = new Color(.53f,.04f,.04f,.98f);
                Text(caughtPanel, "ВАС ПОЙМАЛИ", Vector2.zero, new Vector2(480,90), 46);
            }
            caughtPanel.gameObject.SetActive(true);
            caughtPanel.SetAsLastSibling();
        }

        internal void HideAttention()
        {
            if (alertBar) alertBar.gameObject.SetActive(false);
            if (caughtPanel) caughtPanel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (font) Destroy(font);
        }
    }
}
// Служебный Canvas создаёт Scene2FlowManager, ссылки на старый UI не нужны.
