using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{

    /// <summary>Pooled one-shot SFX, crossfading music and an ambience bed.</summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        public AudioLibrary library;
        [Range(0, 1)] public float sfxVolume = 0.8f;
        [Range(0, 1)] public float musicVolume = 0.45f;
        [Range(0, 1)] public float ambienceVolume = 0.5f;

        readonly List<AudioSource> sources = new List<AudioSource>();
        AudioSource musicA, musicB, ambience;
        bool aIsCurrent = true;
        string currentMusic;
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        Coroutine fadeRoutine;

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
        }

        /// <summary>Plays a one-shot by clip name. World position attenuates volume by camera distance.</summary>
        public static void Play(string id, float volume = 1f, float pitchVariance = 0.06f, Vector3? worldPos = null, float minInterval = 0.03f)
        {
            if (I == null || I.library == null || string.IsNullOrEmpty(id)) return;
            var clip = I.library.Get(id);
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (I.lastPlayed.TryGetValue(id, out float last) && now - last < minInterval) return;
            I.lastPlayed[id] = now;
            float vol = volume * I.sfxVolume;
            if (worldPos.HasValue && CameraRig.MainCam != null)
            {
                float d = Vector2.Distance(worldPos.Value, CameraRig.MainCam.transform.position);
                vol *= Mathf.Clamp01(1.2f - d / 18f);
                if (vol <= 0.01f) return;
            }
            AudioSource src = null;
            foreach (var s in I.sources)
                if (!s.isPlaying) { src = s; break; }
            if (src == null) src = I.sources[Random.Range(0, I.sources.Count)];
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
            I.ambience.volume = I.ambienceVolume;
            I.ambience.Play();
        }

        IEnumerator Crossfade(AudioClip clip, float fade)
        {
            var from = aIsCurrent ? musicA : musicB;
            var to = aIsCurrent ? musicB : musicA;
            aIsCurrent = !aIsCurrent;
            to.clip = clip;
            to.volume = 0;
            to.Play();
            float t = 0;
            float startFrom = from.volume;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fade);
                to.volume = musicVolume * k;
                from.volume = startFrom * (1 - k);
                yield return null;
            }
            from.Stop();
            to.volume = musicVolume;
        }
    }
}
