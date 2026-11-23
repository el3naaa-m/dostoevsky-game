using UnityEngine;

namespace NightChase.Station
{
    public abstract class StationInteractable : MonoBehaviour
    {
        Renderer highlight;
        MaterialPropertyBlock normal, lit;

        internal void BindHighlight(Renderer target, Color color)
        {
            highlight = target;
            normal = new MaterialPropertyBlock();
            lit = new MaterialPropertyBlock();
            normal.SetColor("_BaseColor", color);
            lit.SetColor("_BaseColor", Color.Lerp(color, new Color(1, .78f, .3f), .66f));
            SetHighlighted(false);
        }

        internal void SetHighlighted(bool active)
        {
            if (highlight) highlight.SetPropertyBlock(active ? lit : normal);
        }

        public abstract string Prompt(Scene2FlowManager flow);
        public abstract void Use(Scene2FlowManager flow);
    }
}
