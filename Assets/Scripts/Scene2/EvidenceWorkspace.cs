using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    sealed class EvidenceWorkspace : MonoBehaviour
    {
        RectTransform screen;
        TMP_FontAsset font;
        Action backAction;
        internal Action Left, Right;
        internal void Back() => backAction?.Invoke();
        internal bool Active => screen && screen.gameObject.activeSelf;

        internal void Build(RectTransform canvas, TMP_FontAsset typeface)
        {
            font = typeface;
            screen = Rect(canvas, "Evidence workspace", Vector2.zero, Vector2.one);
            screen.gameObject.AddComponent<Image>().color = new Color(.07f,.085f,.09f);
            Close();
        }

        internal RectTransform Page(string title, Action back, string backLabel = "Закрыть  [Esc]")
        {
            backAction = back;
            Left = Right = null;
            foreach (Transform child in screen)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            screen.GetComponent<Image>().color = new Color(.07f,.085f,.09f);
            screen.gameObject.SetActive(true);
            Label(screen,title,new(.05f,.89f),new(.69f,.98f),34);
            Button(screen,backLabel,new(.74f,.9f),new(.96f,.97f),back);
            return screen;
        }

        internal void Close() { if (screen) screen.gameObject.SetActive(false); }

        internal void Reveal(RectTransform page)
        {
            StartCoroutine(Zoom(page));
        }

        IEnumerator Zoom(RectTransform page)
        {
            for (float t = 0; t < .22f && page && page.gameObject.activeInHierarchy; t += Time.unscaledDeltaTime)
            {
                page.localScale = Vector3.one * Mathf.Lerp(.88f,1,Mathf.SmoothStep(0,1,t/.22f));
                yield return null;
            }
            if (page) page.localScale = Vector3.one;
        }

        internal static RectTransform Rect(Transform parent, string title, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(title,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        internal TextMeshProUGUI Label(Transform parent,string value,Vector2 min,Vector2 max,int size)
        {
            var label = Rect(parent,"Text",min,max).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = value;
            label.richText = false;
            label.fontSize = label.fontSizeMax = size;
            label.fontSizeMin = 13;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            return label;
        }

        internal Button Button(Transform parent,string value,Vector2 min,Vector2 max,Action action)
        {
            var row = Rect(parent,value,min,max);
            var fill = row.gameObject.AddComponent<Image>();
            fill.color = new Color(.25f,.30f,.31f);
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            button.onClick.AddListener(() => action());
            Label(row,value,new(.045f,.08f),new(.96f,.92f),23);
            return button;
        }
    }
}
