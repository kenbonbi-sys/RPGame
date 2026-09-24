using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace RPG
{
    /// <summary>
    /// The root of a zone scene. Tells the Core scene what it cannot reference directly (scenes
    /// cannot point into each other): named spots (entries, respawn, quest places), the
    /// tilemaps for the minimap and the camera bounds. Pressing Play with only a zone open
    /// loads the Core scene next to it, so zones can be edited and tested on their own.
    /// </summary>
    public class ZoneRoot : MonoBehaviour
    {
        [Serializable]
        public class Spot
        {
            public string id;
            public Transform point;
        }

        public ZoneDef def;
        [Tooltip("Camera limits and minimap size, in world units.")]
        public Rect bounds = new Rect(0, 0, 100, 64);
        [Tooltip("spawn (entry + respawn), and places quests point at: forest, boss…")]
        public List<Spot> spots = new List<Spot>();

        [Header("Minimap")]
        public Tilemap ground;
        public Tilemap tallGrass;
        public Tilemap dirt;
        [Tooltip("Parent of trees and rocks drawn as dots on the minimap.")]
        public Transform obstacles;

        /// <summary>The zone loaded right now.</summary>
        public static ZoneRoot Current { get; private set; }

        public const string CoreScene = "Core";

        public Scene Scene => gameObject.scene;

        public Transform SpotOf(string id)
        {
            foreach (var s in spots)
                if (s != null && s.id == id && s.point != null) return s.point;
            return null;
        }

        void Awake()
        {
            Current = this;
            // opened on its own in the editor: bring in the Core scene (managers, hero, UI)
            if (GameManager.I == null && !SceneManager.GetSceneByName(CoreScene).isLoaded)
                SceneManager.LoadScene(CoreScene, LoadSceneMode.Additive);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
