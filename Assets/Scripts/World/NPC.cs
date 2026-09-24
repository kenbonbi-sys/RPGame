using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea(2, 4)] public string text;

        public DialogueLine(string s, string t)
        {
            speaker = s;
            text = t;
        }
    }

    /// <summary>A talkable character. Dialogue comes from its Yarn node (or the fallback lines).</summary>
    public class NPC : MonoBehaviour
    {
        public static readonly List<NPC> All = new List<NPC>();

        public string npcId = "chief";
        public string displayName = "Trưởng Làng";
        public Sprite portrait;
        public SpriteAnimator anim;
        public SpriteRenderer body;
        public SpriteRenderer questMarker;
        public string idleClip = "chief_idle";
        public string talkClip = "chief_talk";
        [Tooltip("Yarn node started when the player talks to this NPC (Assets/Dialogue/*.yarn).")]
        public string yarnNode = "";
        [Tooltip("Used when there is no Yarn node.")]
        public List<DialogueLine> fallbackLines = new List<DialogueLine>();

        NameplateUI plate;
        bool talking;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Start()
        {
            if (HUD.I != null) plate = HUD.I.CreateNameplate(transform, displayName, false, Color.white, null, 1.42f);
            MinimapUI.Register(transform, MinimapUI.MarkerKind.NPC);
        }

        public static NPC Nearest(Vector2 p, float maxDist)
        {
            NPC best = null;
            float bd = maxDist;
            foreach (var n in All)
            {
                float d = Vector2.Distance(p, n.transform.position);
                if (d < bd)
                {
                    bd = d;
                    best = n;
                }
            }
            return best;
        }

        public void Interact(PlayerController p)
        {
            if (talking || DialogueDirector.I == null) return;
            bool started = DialogueDirector.I.Talk(this, () =>
            {
                talking = false;
                if (anim != null) anim.Play(idleClip);
            });
            if (!started) return;
            talking = true;
            if (body != null && p != null) body.flipX = p.transform.position.x < transform.position.x;
            if (anim != null) anim.Play(talkClip);
        }

        void Update()
        {
            if (questMarker != null && QuestSystem.I != null)
            {
                var m = QuestSystem.I.MarkerFor(npcId, out var kind);
                questMarker.gameObject.SetActive(m != QuestSystem.Marker.None && !talking);
                if (m != QuestSystem.Marker.None)
                {
                    var db = GameManager.I.db;
                    questMarker.sprite = m == QuestSystem.Marker.Exclaim ? db.questExclaim : db.questQuestion;
                    // plan §09: gold for the main story, silver for side quests
                    questMarker.color = kind == QuestKind.Main ? Color.white : new Color(0.78f, 0.84f, 0.95f);
                }
            }
            if (plate != null && GameManager.I != null && GameManager.I.player != null)
            {
                float d = Vector2.Distance(GameManager.I.player.transform.position, transform.position);
                plate.SetPrompt(d < GameManager.I.player.interactRadius && !talking ? "[F] Nói chuyện" : null);
            }
        }
    }
}
