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

        internal void Bind(RectTransform surface, Action<Vector2> savePosition, Action open)
        {
            board = surface;
            card = (RectTransform)transform;
            save = savePosition;
            inspect = open;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
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

        public void OnEndDrag(PointerEventData eventData) => eventData.eligibleForClick = false;
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
