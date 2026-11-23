using System.Collections.Generic;
using UnityEngine;

namespace NightChase.Station
{
    public sealed class WhiteboardGenerator : MonoBehaviour
    {
        [SerializeField] Texture2D[] photoTextures, newspaperClippings, casePrintouts;
        [SerializeField] Vector2 size = new Vector2(1.32f,.88f);
        readonly List<Material> owned = new();
        bool built;
        void Start() => Build();

        internal void Build()
        {
            if (built) return;
            built = true;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return;
            Quad("Рама",Vector2.zero,size + Vector2.one*.055f,.007f,new Color(.20f,.22f,.23f),null,shader);
            Quad("Белая доска",Vector2.zero,size,0,new Color(.87f,.88f,.84f),null,shader);
            var photos = photoTextures is { Length: > 0 } ? photoTextures :
                new[] { Load("CourtCase0"),Load("CourtCase1") };
            var news = newspaperClippings is { Length: > 0 } ? newspaperClippings :
                new[] { Load("CourtCase2"),Load("CourtCase3") };
            var reports = casePrintouts is { Length: > 0 } ? casePrintouts :
                new[] { Load("ArchivePaper"),Load("CourtCase4") };
            int slot = 0;
            foreach (var group in new[] { photos,news,reports })
                foreach (var texture in group)
                {
                    if (!texture || slot >= 9) continue;
                    int index = slot++;
                    var position = new Vector2((index%3-1)*size.x*.30f,(1-index/3)*size.y*.28f);
                    var dimensions = new Vector2(size.x*.25f,size.y*.22f);
                    var photoSize = dimensions;
                    float aspect = (float)texture.width/texture.height;
                    photoSize.x = Mathf.Min(dimensions.x,dimensions.y*aspect);
                    photoSize.y = photoSize.x/aspect;
                    Quad("Архивный лист",position,dimensions,-.003f,new Color(.93f,.9f,.80f),null,shader);
                    var doc = Quad(texture.name,position,photoSize*.89f,-.005f,Color.white,texture,shader);
                    doc.localRotation = Quaternion.Euler(0,0,index%2==0 ? 3 : -3);
                    Quad("Магнит",position+Vector2.up*dimensions.y*.5f,new Vector2(.021f,.021f),-.008f,new Color(.40f,.12f,.10f),null,shader);
                }

            var label = new GameObject("Подпись").AddComponent<TextMesh>();
            label.transform.SetParent(transform,false);
            label.transform.localPosition = new Vector3(-.56f,-.34f,-.009f);
            label.text = "ДЕЛО № 94-17   /   МИЛЛЕР";
            label.fontSize = 44;
            label.characterSize = .009f;
            label.color = new Color(.15f,.18f,.22f);
        }

        Transform Quad(string title, Vector2 point, Vector2 dimensions, float depth, Color tint, Texture texture, Shader shader)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = title;
            quad.transform.SetParent(transform,false);
            quad.transform.localPosition = new Vector3(point.x,point.y,depth);
            quad.transform.localScale = new Vector3(dimensions.x,dimensions.y,1);
            var collider = quad.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var material = new Material(shader) { color = tint };
            if (texture) material.mainTexture = texture;
            owned.Add(material);
            quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            return quad.transform;
        }

        static Texture2D Load(string name) => Resources.Load<Texture2D>("StationEvidence/" + name);
        void OnDestroy() { foreach (var material in owned) Destroy(material); }
    }
}
