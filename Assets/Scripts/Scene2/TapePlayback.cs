using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NightChase.Station
{
    internal sealed class TapePlayback : StationInteractable
    {
        internal const string TapeId = "Tape44B";
        static readonly string[] Lines =
        {
            "Я передумал. Там будут люди. Там будут дети.",
            "Мы не выбираем войну. Они бомбили наших. Ты видел, что они сделали с Белградом? Ты видел? Твой отец воевал, он знает. Мы просто напомним им, что такое страх. У нас есть план. Есть люди, которые помогут.",
            "Кто?",
            "Те, кто не забыл. Из порта. Они дают нам всё. Ты со мной?",
            "А если я уйду?",
            "Тогда я пойду без тебя."
        };

        bool inserted;
        Coroutine playback;
        Transform eyes;
        Vector3 savedPosition;
        Quaternion savedRotation;
        RawImage signal;
        Texture2D noise;
        readonly Color32[] pixels = new Color32[128 * 72];
        AudioSource sound;
        AudioClip hiss, click, chair;

        public override string Prompt(Scene2FlowManager flow) => !flow.InvestigationUnlocked ? "" : inserted ? "E — Воспроизвести кассету 44-Б"
            : flow.Inventory.Has(TapeId) ? "E — Вставить кассету 44-Б" : "E — Осмотреть видеомагнитофон";

        public override void Use(Scene2FlowManager flow)
        {
            if (!flow.InvestigationUnlocked || playback != null) return;
            bool first = !inserted;
            if (first && !flow.Inventory.Remove(TapeId))
            {
                flow.Notify("Нужна кассета.", 3);
                return;
            }
            inserted = true;
            flow.Lock(true);
            var camera = flow.Player.GetComponentInChildren<Camera>();
            eyes = camera ? camera.transform : null;
            if (eyes)
            {
                savedPosition = eyes.localPosition;
                savedRotation = eyes.localRotation;
            }
            var ui = flow.UI.Workspace;
            var screen = ui.Page("Кассета 44-Б",() => Finish(flow,false),"Выйти  [Esc]");
            var glass = EvidenceWorkspace.Rect(screen,"Экран VHS",new(.07f,.13f),new(.93f,.88f));
            glass.gameObject.AddComponent<Image>().color = Color.black;
            signal = EvidenceWorkspace.Rect(glass,"Помехи",new(.017f,.03f),new(.983f,.97f)).gameObject.AddComponent<RawImage>();
            if (!noise) noise = new Texture2D(128,72,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            signal.texture = noise;
            signal.color = Color.black;
            var plate = EvidenceWorkspace.Rect(glass,"Подложка",new(.08f,.06f),new(.92f,.40f));
            plate.gameObject.AddComponent<Image>().color = new Color(0,0,0,.87f);
            var captions = ui.Label(plate,"",new(.045f,.10f),new(.955f,.90f),30);
            plate.gameObject.SetActive(false);
            playback = StartCoroutine(Play(flow,captions,plate.gameObject,first));
        }

        IEnumerator Play(Scene2FlowManager flow,TMP_Text captions,GameObject plate,bool first)
        {
            if (!sound)
            {
                sound = gameObject.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                hiss = MakeSound("TV static",22050,0);
                click = MakeSound("VHS mechanism",1800,1);
                chair = MakeSound("Chair scrape",7500,2);
            }
            if (first) sound.PlayOneShot(click,.65f);
            if (eyes)
            {
                Vector3 origin = eyes.position;
                Quaternion rotation = eyes.rotation;
                var collider = GetComponentInChildren<Collider>();
                Vector3 toward = (collider ? collider.bounds.center : transform.position) - origin;
                if (toward.sqrMagnitude > .01f)
                {
                    Vector3 target = origin + toward.normalized * Mathf.Clamp(toward.magnitude - .65f,0,.65f);
                    for (float t = 0; t < .32f; t += Time.unscaledDeltaTime)
                    {
                        float blend = Mathf.SmoothStep(0,1,t/.32f);
                        eyes.SetPositionAndRotation(Vector3.Lerp(origin,target,blend),Quaternion.Slerp(rotation,Quaternion.LookRotation(toward),blend));
                        yield return null;
                    }
                }
            }
            sound.clip = hiss;
            sound.loop = true;
            sound.Play();
            yield return Interference(1,.4f);
            for (int i = 0; i < Lines.Length; i++)
            {
                if (!signal || !signal.gameObject.activeInHierarchy) { Abort(); yield break; }
                if (i > 0)
                {
                    plate.SetActive(false);
                    if (i == 2) sound.PlayOneShot(chair);
                    yield return Interference(i == 2 ? 1.6f : .7f,.31f);
                }
                if (!signal || !signal.gameObject.activeInHierarchy) { Abort(); yield break; }
                captions.text = Lines[i];
                captions.fontStyle = i % 2 == 0 ? FontStyles.Italic : FontStyles.Bold;
                plate.SetActive(true);
                yield return Interference(Mathf.Max(2.4f,Lines[i].Length / 15f),.14f);
            }
            if (!signal || !signal.gameObject.activeInHierarchy) { Abort(); yield break; }
            plate.SetActive(false);
            yield return Interference(.4f,.65f);
            if (!signal || !signal.gameObject.activeInHierarchy) { Abort(); yield break; }
            signal.color = Color.black;
            sound.Stop();
            yield return new WaitForSecondsRealtime(.28f);
            playback = null;
            Finish(flow,true);
        }

        IEnumerator Interference(float duration,float strength)
        {
            signal.color = Color.white;
            sound.volume = strength * .38f;
            float until = Time.unscaledTime + duration;
            while (Time.unscaledTime < until && signal && signal.gameObject.activeInHierarchy)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    byte shade = (byte)Random.Range(40,200);
                    pixels[i] = new Color32(shade,shade,shade,255);
                }
                noise.SetPixels32(pixels);
                noise.Apply(false);
                yield return new WaitForSecondsRealtime(.065f);
            }
        }

        void Abort()
        {
            playback = null;
            if (sound) sound.Stop();
            if (eyes) eyes.SetLocalPositionAndRotation(savedPosition,savedRotation);
            eyes = null;
        }

        void Finish(Scene2FlowManager flow,bool completed)
        {
            if (playback != null) StopCoroutine(playback);
            Abort();
            flow.ClosePanel();
            if (!completed) return;
            InvestigationNotes.For(flow).Tape44BHeard = true;
            flow.Notify("Это его голос. Отец знал, кто будет в здании. И люди из порта тоже знали.",6);
        }

        static AudioClip MakeSound(string name,int frames,int kind)
        {
            var clip = AudioClip.Create(name,frames,1,22050,false);
            var samples = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                float envelope = kind == 0 ? .42f : Mathf.Exp(-i / (kind == 1 ? 340f : 1900f));
                float value = Random.Range(-1f,1f);
                if (kind == 2) value = value * .42f + Mathf.Sin(i * .061f) * .45f;
                samples[i] = value * envelope;
            }
            clip.SetData(samples,0);
            return clip;
        }

        void OnDestroy()
        {
            if (noise) Destroy(noise);
            if (hiss) Destroy(hiss);
            if (click) Destroy(click);
            if (chair) Destroy(chair);
        }
    }
}
