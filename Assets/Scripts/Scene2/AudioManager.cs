using UnityEngine;

namespace NightChase.Station
{
    public sealed class AudioManager : MonoBehaviour
    {
        public AudioClip lamps, russianRadio, traffic, carpet, laminate, phone;
        public AudioClip paper, pickup, lockClick, metal, warning, caught, exitDoor;
        public AudioClip chair, interviewDoor, corridorCough;
        AudioSource effects, radio;
        Transform listener;
        DialogueSystem dialogue;
        Vector3 previous;
        float walked, nextCall;

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
        public void PlayOneShot(AudioClip clip) { if (clip && effects) effects.PlayOneShot(clip); }
        internal void Bind(Transform player)
        {
            listener = player;
            previous = player.position;
            effects = Source("Действия", player.position, false);
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
            if (!dialogue) dialogue = GetComponent<DialogueSystem>();
            // [УГЛУБЛЕНИЕ] В паузах между разговорами снова слышны слова из соседней комнаты.
            radio.volume = Mathf.MoveTowards(radio.volume, dialogue && dialogue.Active ? .085f : .23f, Time.deltaTime * .3f);
            var delta = listener.position - previous;
            previous = listener.position;
            delta.y = 0;
            if (delta.magnitude < .9f) walked += delta.magnitude;
            if (walked > 1.45f) { walked = 0; PlayOneShot(listener.position.x < -12 ? carpet : laminate); }
            if (Time.time < nextCall) return;
            nextCall = Time.time + Random.Range(35,90);
            if (phone) AudioSource.PlayClipAtPoint(phone,new Vector3(-9,1,12),.45f);
        }
    }
}
