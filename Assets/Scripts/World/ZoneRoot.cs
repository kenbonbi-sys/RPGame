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
        public Tilemap mud;
        public Tilemap water;
        [Tooltip("The cave's rock (solid): its own colour on the map.")]
        public Tilemap walls;
        [Tooltip("Khe Vực, the steppe's ravine (solid for walkers, open for shots): its own colour on the map.")]
        public Tilemap chasm;
        [Tooltip("Parent of trees and rocks drawn as dots on the minimap.")]
        public Transform obstacles;

        [Header("Terrain")]
        [Tooltip("One byte per grid corner, row by row from the bottom left (terrainWidth per row): " +
                 "Water (1) marks swamp water, which characters wade through slower; Wall (4) the cave's solid rock.")]
        [HideInInspector] public byte[] terrain;
        public int terrainWidth;

        /// <summary>Flags of <see cref="terrain"/>.</summary>
        public const byte Water = 1, Mud = 2, Wall = 4, Chasm = 8;
        /// <summary>Share of their speed characters keep while wading (swimmers keep all of it).</summary>
        public const float WaterSpeed = 0.6f;

        /// <summary>The zone loaded right now.</summary>
        public static ZoneRoot Current { get; private set; }

        public const string CoreScene = "Core";

        public Scene Scene => gameObject.scene;

        /// <summary>Whether a point is in swamp water: the corners' flags blended across the tile, as the water tiles are drawn.</summary>
        public bool IsWater(Vector2 p) => Has(p, Water);

        public bool IsMud(Vector2 p) => Has(p, Mud);

        /// <summary>Whether a point is inside the solid rock of the cave (or the mountains around it).</summary>
        public bool IsWall(Vector2 p) => Has(p, Wall);

        /// <summary>Whether a point is over Khe Vực, the steppe's ravine (nobody stands there; a Cột Gió flies over it).</summary>
        public bool IsChasm(Vector2 p) => Has(p, Chasm);

        /// <summary>Share of their speed a wader keeps at a point.</summary>
        public float SpeedAt(Vector2 p) => IsWater(p) ? WaterSpeed : 1f;

        bool Has(Vector2 p, byte flag)
        {
            if (terrain == null || terrainWidth <= 1) return false;
            int rows = terrain.Length / terrainWidth;
            float fx = p.x - bounds.xMin, fy = p.y - bounds.yMin;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            if (x0 < 0 || y0 < 0 || x0 + 1 >= terrainWidth || y0 + 1 >= rows) return false;
            float tx = fx - x0, ty = fy - y0;
            float Corner(int x, int y) => (terrain[y * terrainWidth + x] & flag) != 0 ? 1f : 0f;
            float bottom = Mathf.Lerp(Corner(x0, y0), Corner(x0 + 1, y0), tx);
            float top = Mathf.Lerp(Corner(x0, y0 + 1), Corner(x0 + 1, y0 + 1), tx);
            return Mathf.Lerp(bottom, top, ty) > 0.5f;
        }

        /// <summary>The flag of <see cref="terrain"/> set at a grid corner (tests, tools).</summary>
        public bool CornerHas(int x, int y, byte flag)
        {
            if (terrain == null || terrainWidth <= 0 || x < 0 || y < 0 || x >= terrainWidth) return false;
            int i = y * terrainWidth + x;
            return i < terrain.Length && (terrain[i] & flag) != 0;
        }

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
