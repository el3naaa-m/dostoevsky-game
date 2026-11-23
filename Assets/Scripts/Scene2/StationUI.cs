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
        public TextMeshProUGUI Prompt { get; private set; }
        public CanvasGroup Fade { get; private set; }
        RectTransform canvas, panel, buttons;
        TextMeshProUGUI header, body;
        RawImage photo;
        RectTransform details;
        TMP_FontAsset font;
        public bool Modal => panel.gameObject.activeSelf;

        public void Build()
        {
            font = TMP_FontAsset.CreateFontAsset(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            canvas = Rect(transform, "Station Canvas", Vector2.zero, Vector2.zero);
            var view = canvas.gameObject.AddComponent<Canvas>();
            view.renderMode = RenderMode.ScreenSpaceOverlay;
            view.sortingOrder = 120;
            var scale = canvas.gameObject.AddComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (!FindAnyObjectByType<EventSystem>())
            {
                var events = new GameObject("Station Events", typeof(EventSystem));
                events.transform.SetParent(transform, false);
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            Objective = Text(canvas, "Задача", new Vector2(0, 299), new Vector2(1150, 50), 24);
            Prompt = Text(canvas, "", new Vector2(0, -220), new Vector2(930, 56), 26);
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

        public void Close() => panel.gameObject.SetActive(false);

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

        void OnDestroy()
        {
            if (font) Destroy(font);
        }
    }
}
// Служебный Canvas создаёт Scene2FlowManager, ссылки на старый UI не нужны.
