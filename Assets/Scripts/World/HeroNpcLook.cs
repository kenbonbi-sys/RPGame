using UnityEngine;

namespace RPG
{
    /// <summary>
    /// An NPC drawn like a hero (<see cref="HeroArt"/>) from a look instead of its own sprite sheet:
    /// the village smith is a dwarf fighter with a long beard and a hammer. Screens draw it; the
    /// server has nothing to draw.
    /// </summary>
    public class HeroNpcLook : MonoBehaviour
    {
        public HeroLook look = new HeroLook();

        void Start()
        {
            if (!GameSession.HasScreen) return;
            var npc = GetComponent<NPC>();
            var anim = npc != null ? npc.anim : GetComponentInChildren<SpriteAnimator>();
            if (anim == null) return;
            var set = HeroArt.SetFor(look);
            if (set == null) return;
            anim.set = set;
            if (npc != null)
            {
                npc.idleClip = "idle_down";
                npc.talkClip = "idle_down";
            }
            anim.Play("idle_down", true);
        }
    }
}
