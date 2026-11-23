using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightChase.Station
{
    public sealed partial class AudioManager
    {
        public static AudioManager Instance { get; private set; }
        [SerializeField] bool bureau;
        [SerializeField] AudioClip[] beginning, action, office, city, cars;
        [SerializeField] AudioClip[] asphalt, softFloor, metalSteps, stepRecordings, jumps, landings, breath, impacts;
        [SerializeField] AudioClip miss, handcuffs, dialogueClick;
        AudioSource music, streetMusic, feet, breathing;
        CharacterController motor;
        FirstPersonController movement;
        bool grounded, pursuing;
        float stride;

        void Awake()
        {
            Instance = this;
            effects = Source("UI", transform.position, false);
        }

        void Start()
        {
            movement = FindAnyObjectByType<FirstPersonController>();
            if (movement)
            {
                motor = movement.GetComponent<CharacterController>();
                feet = Source("Footsteps", movement.transform.position, true);
                feet.transform.SetParent(movement.transform, false);
                feet.transform.localPosition = Vector3.zero;
                breathing = Source("Breathing", movement.transform.position, false);
                grounded = motor && motor.isGrounded;
            }
            music = PlayLoop(Pick(bureau ? office : beginning), "Music", transform.position, bureau ? .10f : .32f, false);
        }

        static AudioClip Pick(AudioClip[] clips) =>
            clips != null && clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;

        public void PlayLoop(AudioClip clip, AudioSource source)
        {
            if (!source || !clip) return;
            source.clip = clip;
            source.loop = true;
            source.Play();
        }

        public void FadeVolume(AudioSource source, float target, float duration)
        {
            if (source) StartCoroutine(Fade(source, Mathf.Clamp01(target), duration));
        }

        IEnumerator Fade(AudioSource source, float target, float duration)
        {
            float start = source.volume;
            for (float t = 0; t < duration && source; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            if (!source) yield break;
            source.volume = target;
            if (target == 0) StopLoop(source);
        }

        internal void BeginPursuit()
        {
            if (pursuing) return;
            pursuing = true;
            FadeVolume(music, 0, 1.3f);
            streetMusic = PlayLoop(Pick(action), "Action", transform.position, 0, false);
            FadeVolume(streetMusic, .36f, 1.3f);
            var ambience = PlayLoop(Pick(city), "City", transform.position, 0, false);
            FadeVolume(ambience, .13f, 1.3f);
            StartCoroutine(TrafficSounds());
        }

        IEnumerator TrafficSounds()
        {
            var source = Source("Passing cars", transform.position, true);
            source.minDistance = 5f;
            while (movement)
            {
                yield return new WaitForSeconds(Random.Range(3f, 8f));
                if (!movement) yield break;
                var actor = movement.transform;
                source.transform.position = actor.position + actor.right * (Random.value < .5f ? -4 : 4);
                var clip = Pick(cars);
                if (clip) source.PlayOneShot(clip, .88f);
            }
        }

        internal bool PlayDialogueClick()
        {
            if (!dialogueClick || !effects) return false;
            PlayOneShot(dialogueClick,.13f);
            return true;
        }

        internal void PlayHandcuffs() => PlayOneShot(handcuffs, .57f);

        internal void CounterImpact() => StartCoroutine(PlayCounterImpact());

        IEnumerator PlayCounterImpact()
        {
            yield return new WaitForSeconds(.14f);
            PlayOneShot(Pick(impacts), .36f);
        }

        internal void FightCue(bool hit) => PlayOneShot(hit ? Pick(impacts) : miss, .40f);

        void LateUpdate()
        {
            if (!motor || !movement.isActiveAndEnabled || !motor.enabled || Time.deltaTime <= 0)
            {
                stride = 0;
                return;
            }
            var velocity = motor.velocity;
            bool onFloor = motor.isGrounded;
            if (grounded && !onFloor && velocity.y > 1 && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                FootCue(jumps, .55f);
            if (!grounded && onFloor) FootCue(landings, .40f);
            grounded = onFloor;
            velocity.y = 0;
            float speed = velocity.magnitude;
            if (!onFloor || speed < .15f) { stride = 0; return; }
            bool running = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed && speed > 3.6f;
            stride += speed * Time.deltaTime;
            if (stride < (running ? 1.9f : 1.45f)) return;
            stride = 0;
            bool carpet = bureau && movement.transform.position.x < -12;
            FootCue(carpet ? softFloor : asphalt, carpet ? .22f : .30f);
            if (running && !breathing.isPlaying && Random.value < .30f)
            {
                var clip = Pick(breath);
                if (clip) breathing.PlayOneShot(clip, .24f);
            }
        }

        void FootCue(AudioClip[] clips, float gain)
        {
            var clip = Pick(clips);
            if (clip) feet.PlayOneShot(clip, gain);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
