using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>T23: sound buses and their volume settings (plain EditMode, no scene).</summary>
    public class AudioBusTests
    {
        float[] saved;

        [SetUp]
        public void SetUp() =>
            saved = System.Enum.GetValues(typeof(AudioBus)).Cast<AudioBus>().Select(AudioManager.GetVolume).ToArray();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < saved.Length; i++) AudioManager.SetVolume((AudioBus)i, saved[i]);
        }

        [Test]
        public void SoundIdsPickTheirBus()
        {
            Assert.AreEqual(AudioBus.UI, AudioManager.BusFor("sfx_ui_click"));
            Assert.AreEqual(AudioBus.Voice, AudioManager.BusFor("sfx_dialogue_blip"));
            Assert.AreEqual(AudioBus.Sfx, AudioManager.BusFor("sfx_boss_stomp"));
        }

        [Test]
        public void VolumesAreSavedAndScaledByMaster()
        {
            AudioManager.SetVolume(AudioBus.Master, 0.5f);
            AudioManager.SetVolume(AudioBus.Music, 0.6f);
            Assert.AreEqual(0.3f, AudioManager.Gain(AudioBus.Music), 1e-5f);
            Assert.AreEqual(0.5f, AudioManager.Gain(AudioBus.Master), 1e-5f);
            Assert.AreEqual(0.6f, PlayerPrefs.GetFloat("rtt.audio.Music"), 1e-5f, "saved");
            AudioManager.SetVolume(AudioBus.Sfx, 3f);
            Assert.AreEqual(1f, AudioManager.GetVolume(AudioBus.Sfx), "clamped to 0..1");
        }
    }
}
