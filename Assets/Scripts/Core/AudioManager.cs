using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RPG
{
    /// <summary>What a sound belongs to for the volume settings (plan §15: Master · Nhạc · SFX · UI · Môi trường · Blip thoại).</summary>
    public enum AudioBus
    {
        Master,
        Music,
        Sfx,
        UI,
        Ambience,
        Voice
    }

    /// <summary>
    /// Pooled one-shot SFX, crossfading music and an ambience bed, each on a bus with its own
    /// volume setting (saved in PlayerPrefs, see <see cref="SetVolume"/>). Music ducks while a
    /// conversation is open and for a moment when a boss announces a skill (plan T23). With a
    /// mixer assigned, the sources also play through its groups of the same names (Music, SFX,
    /// UI, Ambience, Voice), ready for effects and snapshots.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        public AudioLibrary library;
        [Header("Mix")]
        [Range(0, 1)] public float sfxVolume = 0.8f;
        [Range(0, 1)] public float musicVolume = 0.45f;
        [Range(0, 1)] public float ambienceVolume = 0.5f;
        [Tooltip("Optional: sources play through this mixer's Music / SFX / UI / Ambience / Voice groups.")]
        public AudioMixer mixer;

        [Header("Ducking")]
        [Tooltip("Music level while a conversation is open.")]
        [Range(0, 1)] public float dialogueDuck = 0.45f;
        [Tooltip("Music level for a moment when a boss announces a skill.")]
        [Range(0, 1)] public float skillDuck = 0.7f;
        public float skillDuckSeconds = 0.9f;
        [Tooltip("Seconds from full music to silence; ducking moves at this speed.")]
        public float duckFade = 0.3f;

        const string VolumeKey = "rtt.audio.";
        static float[] volumes;

        readonly List<AudioSource> sources = new List<AudioSource>();
        AudioSource musicA, musicB, ambience;
        float levelA, levelB;   // crossfade position of each music source, 0..1
        bool aIsCurrent = true;
        string currentMusic;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        Coroutine fadeRoutine;
        bool talking;
        float skillDuckUntil = -1f;
        AudioMixerGroup sfxGroup, uiGroup, voiceGroup;

        /// <summary>Music level from ducking right now: 1 = not ducked.</summary>
        public float Duck { get; private set; } = 1f;

        void Awake()
        {
            I = this;
            for (int i = 0; i < 20; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                sources.Add(s);
            }
            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            ambience = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB, ambience })
            {
                m.loop = true;
                m.playOnAwake = false;
                m.volume = 0;
            }
            if (mixer != null)
            {
                sfxGroup = Group("SFX");
                uiGroup = Group("UI");
                voiceGroup = Group("Voice");
                musicA.outputAudioMixerGroup = musicB.outputAudioMixerGroup = Group("Music");
                ambience.outputAudioMixerGroup = Group("Ambience");
            }
        }

        AudioMixerGroup Group(string groupName)
        {
            foreach (var g in mixer.FindMatchingGroups(groupName))
                if (g.name == groupName) return g;
            return null;
        }

        void OnEnable()
        {
            GameEvents.DialogueStarted += OnDialogueStarted;
            GameEvents.DialogueEnded += OnDialogueEnded;
            GameEvents.SkillAnnounced += OnSkillAnnounced;
        }

        void OnDisable()
        {
            GameEvents.DialogueStarted -= OnDialogueStarted;
            GameEvents.DialogueEnded -= OnDialogueEnded;
            GameEvents.SkillAnnounced -= OnSkillAnnounced;
        }

        void OnDialogueStarted(string npcId) => talking = true;

        void OnDialogueEnded(string npcId) => talking = false;

        void OnSkillAnnounced(Health owner, string text)
        {
            if (owner != null && owner.team != Team.Player) skillDuckUntil = Time.unscaledTime + skillDuckSeconds;
        }

        void Update()
        {
            float target = 1f;
            if (talking) target = Mathf.Min(target, dialogueDuck);
            if (Time.unscaledTime < skillDuckUntil) target = Mathf.Min(target, skillDuck);
            Duck = Mathf.MoveTowards(Duck, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, duckFade));
            float music = musicVolume * Gain(AudioBus.Music) * Duck;
            musicA.volume = levelA * music;
            musicB.volume = levelB * music;
            ambience.volume = ambienceVolume * Gain(AudioBus.Ambience);
        }

        // ------------------------------------------------------------------ volume settings
        /// <summary>The player's setting for a bus, 0..1 (1 = as mixed). Saved in PlayerPrefs.</summary>
        public static float GetVolume(AudioBus bus) => Volumes[(int)bus];

        public static void SetVolume(AudioBus bus, float value)
        {
            Volumes[(int)bus] = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey + bus, Volumes[(int)bus]);
        }

        /// <summary>A bus's setting times the master setting.</summary>
        public static float Gain(AudioBus bus) => bus == AudioBus.Master ? GetVolume(AudioBus.Master) : GetVolume(bus) * GetVolume(AudioBus.Master);

        /// <summary>The bus of a sound id: sfx_ui_* is UI, *blip* is Voice (dialogue blips), everything else SFX.</summary>
        public static AudioBus BusFor(string id)
        {
            if (id.StartsWith("sfx_ui")) return AudioBus.UI;
            return id.Contains("blip") ? AudioBus.Voice : AudioBus.Sfx;
        }

        static float[] Volumes
        {
            get
            {
                if (volumes == null)
                {
                    volumes = new float[System.Enum.GetValues(typeof(AudioBus)).Length];
                    for (int i = 0; i < volumes.Length; i++) volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey + (AudioBus)i, 1f));
                }
                return volumes;
            }
        }

        // ------------------------------------------------------------------ playback
        /// <summary>Plays a one-shot by clip name. World position attenuates volume by camera distance.</summary>
        public static void Play(string id, float volume = 1f, float pitchVariance = 0.06f, Vector3? worldPos = null, float minInterval = 0.03f)
        {
            if (I == null || I.library == null || string.IsNullOrEmpty(id)) return;
            var clip = I.library.Get(id);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (I.lastPlayed.TryGetValue(id, out float last) && now - last < minInterval) return;
            I.lastPlayed[id] = now;
            var bus = BusFor(id);
            float vol = volume * I.sfxVolume * Gain(bus);
            if (worldPos.HasValue && CameraRig.MainCam != null)
            {
                float d = Vector2.Distance(worldPos.Value, CameraRig.MainCam.transform.position);
                vol *= Mathf.Clamp01(1.2f - d / 18f);
            }
            if (vol <= 0.01f) return;
            AudioSource src = null;
            foreach (var s in I.sources)
                if (!s.isPlaying) { src = s; break; }
            if (src == null) src = I.sources[Random.Range(0, I.sources.Count)];
            src.outputAudioMixerGroup = bus == AudioBus.UI ? I.uiGroup : bus == AudioBus.Voice ? I.voiceGroup : I.sfxGroup;
            src.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
            src.volume = vol;
            src.clip = clip;
            src.Play();
        }

        public static void PlayMusic(string id, float fade = 1.5f)
        {
            if (I == null || I.library == null || I.currentMusic == id) return;
            var clip = I.library.Get(id);
            if (clip == null) return;
            I.currentMusic = id;
            if (I.fadeRoutine != null) I.StopCoroutine(I.fadeRoutine);
            I.fadeRoutine = I.StartCoroutine(I.Crossfade(clip, fade));
        }

        public static void PlayAmbience(string id)
        {
            if (I == null || I.library == null) return;
            var clip = I.library.Get(id);
            if (clip == null) return;
            I.ambience.clip = clip;
            I.ambience.volume = I.ambienceVolume * Gain(AudioBus.Ambience);
            I.ambience.Play();
        }

        /// <summary>Fades the new clip in on the idle source and the playing one out; Update turns levels into volumes.</summary>
        IEnumerator Crossfade(AudioClip clip, float fade)
        {
            bool toA = !aIsCurrent;
            aIsCurrent = toA;
            var to = toA ? musicA : musicB;
            var from = toA ? musicB : musicA;
            to.clip = clip;
            SetLevel(toA, 0f);
            to.Play();
            float startFrom = toA ? levelB : levelA;
            float t = 0;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fade);
                SetLevel(toA, k);
                SetLevel(!toA, startFrom * (1 - k));
                yield return null;
            }
            from.Stop();
            SetLevel(!toA, 0f);
            SetLevel(toA, 1f);
        }

        void SetLevel(bool a, float level)
        {
            if (a) levelA = level;
            else levelB = level;
        }
    }
}
