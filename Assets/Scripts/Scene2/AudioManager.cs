using UnityEngine;

namespace NightChase.Station
{
    public sealed partial class AudioManager : MonoBehaviour
    {
        public AudioClip lamps, russianRadio, traffic, carpet, laminate, phone;
        public AudioClip paper, pickup, lockClick, metal, warning, caught, exitDoor;
        public AudioClip chair, interviewDoor, corridorCough;
        [SerializeField] AudioClip chairSlide, fanLoop;

        internal void ChairSlideAt(Vector3 position)
        {
            if (chairSlide) AudioSource.PlayClipAtPoint(chairSlide, position, .58f);
        }

        void StartOfficeFans()
        {
            if (!fanLoop) return;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var fan in root.GetComponentsInChildren<Fan>())
                {
                    var source = PlayLoop(fanLoop, "Desk fan", fan.transform.position, .34f);
                    source.transform.SetParent(fan.transform, true);
                    source.minDistance = 2f;
                }
            // [ИМПРОВИЗАЦИЯ] Тихая вытяжка над кабинетом начальника.
            var exhaust = PlayLoop(fanLoop, "Office ventilation", new Vector3(.75f, 2.6f, -1.8f), .20f);
            exhaust.time = fanLoop.length * .43f;
        }

        AudioSource effects, radio;
        Transform listener;
        DialogueSystem dialogue;
        float nextCall;

        AudioSource Source(string label, Vector3 point, bool spatial)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform);
            source.transform.position = point;
            source.playOnAwake = false;
            source.spatialBlend = spatial ? 1 : 0;
            source.maxDistance = 18;
            return source;
        }
        public AudioSource PlayLoop(AudioClip clip, string label, Vector3 point, float volume, bool spatial = true)
        {
            var source = Source(label, point, spatial);
            source.loop = true;
            source.clip = clip;
            source.volume = volume;
            if (clip) source.Play();
            return source;
        }
        public void StopLoop(AudioSource source) { if (source) source.Stop(); }
        public void PlayOneShot(AudioClip clip, float volume = 1f) { if (clip && effects) effects.PlayOneShot(clip, volume); }
        internal void Bind(Transform player)
        {
            listener = player;
            StartOfficeFans();
            
            PlayLoop(lamps,"Лампы",Vector3.zero,.14f,false);
            radio = PlayLoop(russianRadio,"Радио за стеной",new Vector3(-16,1,6),.23f);
            radio.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1700;
            var window = PlayLoop(traffic,"Улица за окном",new Vector3(-20,2,14),.2f);
            window.gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 900;
            nextCall = Time.time + Random.Range(30,65);
            var ambient = gameObject.AddComponent<RandomAudioPlayer>();
            ambient.Bind(new[]{chair, interviewDoor, corridorCough});
        }
        void Update()
        {
            if (!listener) return;
            if (!dialogue) dialogue = FindAnyObjectByType<DialogueSystem>();
            // [УГЛУБЛЕНИЕ] В паузах между разговорами снова слышны слова из соседней комнаты.
            radio.volume = Mathf.MoveTowards(radio.volume, dialogue && dialogue.Active ? .085f : .23f, Time.deltaTime * .3f);
            if (Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(35,90);
            if (phone) AudioSource.PlayClipAtPoint(phone,new Vector3(-9,1,12),.45f);
        }
    }
}
