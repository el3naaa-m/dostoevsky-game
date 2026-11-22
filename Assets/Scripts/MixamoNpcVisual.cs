using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NightChase
{
    sealed class MixamoNpcVisual : MonoBehaviour
    {
        const string Root = "MixamoNpc/";
        readonly List<Material> materials = new();
        readonly AnimationClipPlayable[] motions = new AnimationClipPlayable[4];
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        int current = -1;
        Transform modelRoot;
        Quaternion modelRestRotation;
        float attackUntil, reactionUntil, fallStarted = -1;

        [System.Serializable] sealed class Profile { public Slot[] slots; }
        [System.Serializable] sealed class Slot
        {
            public string mesh, texture;
            public int slot;
            public bool alpha;
        }

        public Animator Animator { get; private set; }

        public static MixamoNpcVisual Create(Transform parent, string path, Color shirt, float phase, bool normalize = true)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (!prefab)
            {
                Debug.LogWarning($"Не найдена модель NPC: {path}.", parent);
                return null;
            }

            var mount = new GameObject("NPC Visual");
            mount.transform.SetParent(parent, false);
            var pivot = new GameObject("Reaction pivot").transform;
            pivot.SetParent(mount.transform, false);
            var model = Instantiate(prefab, pivot, false);
            var visual = mount.AddComponent<MixamoNpcVisual>();
            visual.Configure(model, path, shirt, phase, normalize);
            return visual;
        }

        void Configure(GameObject model, string path, Color shirt, float phase, bool normalize = true)
        {
            modelRoot = model.transform.parent;
            modelRestRotation = modelRoot.localRotation;
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (normalize && bounds.size.y > .01f)
                {
                    float scale = 1.74f / bounds.size.y;
                    transform.localScale = Vector3.one * scale;
                    transform.position += Vector3.up * ((transform.position.y - bounds.min.y) * scale);
                }
                ApplyMaterials(renderers, path, shirt);
            }

            Animator = model.GetComponentInChildren<Animator>();
            if (!Animator || !Animator.avatar || !Animator.avatar.isValid || !Animator.isHuman)
            {
                Debug.LogWarning($"Для {model.name} нужен валидный Humanoid Avatar.", model);
                return;
            }

            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (Animator.runtimeAnimatorController) return;

            graph = PlayableGraph.Create("Mixamo NPC");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, motions.Length);
            AnimationPlayableOutput.Create(graph, "Mixamo", Animator).SetSourcePlayable(mixer);
            AddMotion(0, "Idle", phase);
            AddMotion(1, "Walk", phase);
            AddMotion(2, "Run", phase);
            AddMotion(3, "Punch", 0);
            graph.Play();
            Pose(0f, 0f);
        }

        void ApplyMaterials(SkinnedMeshRenderer[] renderers, string path, Color shirt)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) return;

            string modelName = path.Substring(path.LastIndexOf('/') + 1);
            var data = Resources.Load<TextAsset>(Root + "Profiles/" + modelName);
            if (data)
            {
                var profile = JsonUtility.FromJson<Profile>(data.text);
                var cache = new Dictionary<string, Material>();
                foreach (var renderer in renderers)
                {
                    var slots = renderer.sharedMaterials;
                    foreach (var rule in profile.slots)
                    {
                        if (rule.mesh != renderer.name || rule.slot >= slots.Length) continue;
                        bool blue = modelName == "Ch31" && renderer.name.Contains("Sweater");
                        string texture = blue ? "Ch31SweaterGray" : rule.texture;
                        string key = texture + "|" + rule.alpha + "|" + blue;
                        if (!cache.TryGetValue(key, out var material))
                        {
                            material = CreateMaterial(shader, texture, blue ? new Color(.12f, .38f, 1f) : Color.white, rule.alpha);
                            cache.Add(key, material);
                        }
                        slots[rule.slot] = material;
                    }
                    renderer.sharedMaterials = slots;
                }
                return;
            }

            bool civilian = modelName == "StreetCivilian";
            var body = CreateMaterial(shader, civilian ? "CivilianBody" : "PartnerBody", Color.white);
            var hair = CreateMaterial(shader, civilian ? "CivilianHair" : "PartnerBody", Color.white, true);
            var top = civilian ? CreateMaterial(shader, "CivilianShirt", shirt) : body;
            foreach (var renderer in renderers)
            {
                string name = renderer.name;
                Material replacement = name.Contains("Shirt") ? top
                    : name.Contains("Hair") || name.Contains("Lashes") || name.Contains("Eyelashes") ? hair : body;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = replacement;
                renderer.sharedMaterials = slots;
            }
        }

        Material CreateMaterial(Shader shader, string texture, Color color, bool alphaClip = false)
        {
            var material = new Material(shader) { name = "Mixamo " + texture };
            if (!string.IsNullOrEmpty(texture)) material.SetTexture("_BaseMap", Resources.Load<Texture2D>(Root + "Textures/" + texture));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .22f);
            if (alphaClip)
            {
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", .38f);
                material.SetFloat("_Cull", 0);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            materials.Add(material);
            return material;
        }

        void AddMotion(int index, string name, float phase)
        {
            foreach (var clip in Resources.LoadAll<AnimationClip>(Root + "Motion/" + name))
            {
                if (!clip.isHumanMotion || clip.legacy || clip.length <= 0 ||
                    clip.name.StartsWith("__preview__", System.StringComparison.Ordinal)) continue;
                var motion = AnimationClipPlayable.Create(graph, clip);
                motion.SetApplyFootIK(true);
                motion.SetTime(Mathf.Repeat(phase, clip.length));
                graph.Connect(motion, 0, mixer, index);
                motions[index] = motion;
                return;
            }
            Debug.LogWarning($"Не найден Humanoid-клип {name}.", this);
        }

        public void Pose(float speed, float delta)
        {
            float hit = Mathf.Clamp01((reactionUntil - Time.time) / .42f);
            float fall = fallStarted < 0 ? 0 : Mathf.SmoothStep(0, 1, (Time.time - fallStarted) / .55f);
            modelRoot.localRotation = modelRestRotation * Quaternion.Euler(-12f * Mathf.Sin(Mathf.PI * hit), 0, 85f * fall);
            if (!graph.IsValid()) return;
            int selected = Time.time < attackUntil && motions[3].IsValid() ? 3
                : speed < .12f ? 0 : speed < 3.25f ? 1 : 2;
            if (!motions[selected].IsValid())
                selected = motions[1].IsValid() ? 1 : motions[0].IsValid() ? 0 : 2;
            if (!motions[selected].IsValid()) return;

            if (current != selected)
            {
                for (int i = 0; i < motions.Length; i++) mixer.SetInputWeight(i, i == selected ? 1 : 0);
                current = selected;
            }

            var motion = motions[selected];
            float rate = selected == 0 || selected == 3 ? 1 : Mathf.Clamp(speed / (selected == 1 ? 1.4f : 4.6f), .65f, 1.55f);
            motion.SetTime(Mathf.Repeat((float)motion.GetTime() + delta * rate, motion.GetAnimationClip().length));
            graph.Evaluate(0);
        }

        public void FightMotion(string name, bool receivedHit)
        {
            if (receivedHit) reactionUntil = Time.time + .42f;
            if (name != "Punch" || !motions[3].IsValid()) return;
            motions[3].SetTime(0);
            attackUntil = Time.time + Mathf.Min(.55f, motions[3].GetAnimationClip().length);
        }

        public void Fall() => fallStarted = Time.time;

        public Transform Bone(HumanBodyBones key, string suffix)
        {
            if (Animator && Animator.avatar && Animator.avatar.isValid && Animator.isHuman)
                return Animator.GetBoneTransform(key);
            foreach (var bone in GetComponentsInChildren<Transform>())
                if (bone.name.EndsWith(suffix, System.StringComparison.Ordinal)) return bone;
            return null;
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            foreach (var material in materials) Destroy(material);
        }
    }
}
