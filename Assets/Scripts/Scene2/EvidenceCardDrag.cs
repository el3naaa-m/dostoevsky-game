using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NightChase.Station
{
    sealed class EvidenceCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        RectTransform board, card;
        Vector2 grabOffset;
        Action<Vector2> save;
        Action inspect;
        Func<PointerEventData,bool> drop;
        Vector2 start;

        internal void Bind(RectTransform surface, Action<Vector2> savePosition, Action open, Func<PointerEventData,bool> onDrop = null)
        {
            board = surface;
            card = (RectTransform)transform;
            save = savePosition;
            inspect = open;
            drop = onDrop;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            start = card.anchorMin;
            grabOffset = card.anchorMin - Pointer(eventData);
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Vector2 size = card.anchorMax - card.anchorMin;
            Vector2 next = Pointer(eventData) + grabOffset;
            next = new Vector2(Mathf.Clamp(next.x,0,1-size.x), Mathf.Clamp(next.y,0,1-size.y));
            card.anchorMin = next;
            card.anchorMax = next + size;
            save(next);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            eventData.eligibleForClick = false;
            if (eventData.button != PointerEventData.InputButton.Left || drop?.Invoke(eventData) != true) return;
            var size = card.anchorMax - card.anchorMin;
            card.anchorMin = start;
            card.anchorMax = start + size;
            save(start);
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging) inspect();
        }

        Vector2 Pointer(PointerEventData data)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board,data.position,data.pressEventCamera,out var point);
            return Rect.PointToNormalized(board.rect,point);
        }
    }
}
