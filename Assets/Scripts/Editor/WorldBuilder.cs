using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.EditorTools
{
    /// <summary>
    /// Generates the world as one seamless map (plan §10: new regions join the same map). The
    /// southern band (<see cref="LowH"/> high): in the west the village (Làng Lá Xanh), the forest
    /// paths with enemy camps and the bear's arena (Rừng Già Cổ Thụ); in the east Đầm Lầy Sương Mù —
    /// water that slows whoever wades through it, mud trails, dead trees and willows, toad, leech,
    /// mud-man and water-snake camps (dragonflies by day, wisps at night), Cóc Tía's pond and Xà
    /// Mẫu's lake with its four mounds — reached by a road through the forest's eastern edge to a
    /// stilt-hut outpost. North of it all a wall of rock; behind the swamp a trail climbs to the
    /// mouth of Hang Pha Lê, a dark cave of chambers and tunnels (solid rock between them) lit by
    /// crystals. Đá Truyền Tống stand in the village, at the arena's gate, around the swamp and in
    /// the cave. Tiles are painted into Tilemaps, props are prefab instances — everything can be
    /// edited by hand afterwards.
    /// </summary>
    public static class WorldBuilder
    {
        public const int W = 200, H = 128;
        /// <summary>The southern band of the forest and the swamp; the cave and the mountain rock lie north of it.</summary>
        public const int LowH = 64;
        /// <summary>Roughly where the forest ends and the swamp begins (the real edge wanders, <see cref="SwampEdge"/>).</summary>
        public const int ForestW = 100;
        const string TileFolder = "Assets/Tiles";

        public class Result
        {
            public Tilemap ground, tall, dirt, mud, water, details, walls;
            public Transform props;
            public Transform playerSpawn, chief, girl, forestSpot, bossSpot;
            public Transform outpost, swampSpot, mudField, toadPond, snakeLair, snakePools, dragonflies, wisps;
            /// <summary>More places quests point at (the cave's chambers), by id.</summary>
            public readonly List<(string id, Transform point)> spots = new List<(string, Transform)>();
            public BossBear boss;
            /// <summary>Per grid corner: <see cref="ZoneRoot.Water"/>, <see cref="ZoneRoot.Mud"/>, <see cref="ZoneRoot.Wall"/>.</summary>
            public byte[] terrain;
            public int terrainWidth;
        }

        // ================================================================== places
        // the forest (west)
        static readonly Vector2 Village = new Vector2(18, 20);
        static readonly Vector2 Arena = new Vector2(84, 45);
        static readonly Vector2[] Camps = { new Vector2(35.5f, 22), new Vector2(47, 27.5f), new Vector2(53, 15), new Vector2(62, 29.5f), new Vector2(71.5f, 35) };
        static readonly float[] CampRadius = { 2.5f, 4f, 3.5f, 3.5f, 3f };

        static readonly Vector2[] MainPath =
        {
            new Vector2(18, 20), new Vector2(26, 19), new Vector2(33, 22), new Vector2(40, 25), new Vector2(47, 27),
            new Vector2(54, 24), new Vector2(60, 28), new Vector2(66, 32), new Vector2(72, 36), new Vector2(78, 40), new Vector2(84, 45)
        };
        static readonly Vector2[] SidePath = { new Vector2(40, 25), new Vector2(44, 19), new Vector2(53, 15) };
        static readonly Vector2[] NorthPath = { new Vector2(18, 20), new Vector2(17, 27), new Vector2(15, 34) };
        /// <summary>The road out of the forest to the swamp's outpost.</summary>
        static readonly Vector2[] EastRoad =
        {
            new Vector2(66, 32), new Vector2(72, 29.5f), new Vector2(80, 27.5f), new Vector2(88, 27.5f), new Vector2(96, 29),
            new Vector2(104, 29.5f), new Vector2(110, 29.5f)
        };

        // the swamp (east)
        static readonly Vector2 Outpost = new Vector2(113, 30);
        static readonly Vector2 ToadPond = new Vector2(152.5f, 50.5f);
        static readonly Vector2 SnakeLair = new Vector2(182, 30);
        static readonly Vector2 MudField = new Vector2(141, 18);
        /// <summary>Raised mud trails between the pools.</summary>
        static readonly Vector2[][] Trails =
        {
            new[] { new Vector2(110, 29.5f), new Vector2(118, 27), new Vector2(126, 24), new Vector2(134, 22), new Vector2(141, 21.5f),
                    new Vector2(149, 22.5f), new Vector2(157, 25.5f), new Vector2(164, 28.5f), new Vector2(170, 30) },
            new[] { new Vector2(113, 33), new Vector2(115, 36), new Vector2(120, 40.5f), new Vector2(127, 44.5f), new Vector2(136, 47),
                    new Vector2(143, 48) },
            new[] { new Vector2(134, 22), new Vector2(136, 15), new Vector2(142, 9), new Vector2(152, 8), new Vector2(162, 10),
                    new Vector2(168, 14), new Vector2(168, 21), new Vector2(164, 28.5f) },
            // north through the wisps' haunt to the mouth of Hang Pha Lê
            new[] { new Vector2(164, 28.5f), new Vector2(166, 36), new Vector2(167, 44), new Vector2(168, 52), new Vector2(168, 60),
                    new Vector2(168, 64) },
        };
        static readonly (Vector2 c, float rx, float ry)[] Ponds =
        {
            (new Vector2(124, 33), 3.4f, 2.4f),      // leeches (west)
            (new Vector2(133, 35.5f), 4.5f, 3f),
            (new Vector2(156, 36), 5f, 3.4f),        // leeches (middle)
            (new Vector2(146, 15), 4f, 2.6f),
            (new Vector2(175, 44), 4.2f, 3.4f),      // leeches (deep)
            (new Vector2(153, 51.5f), 6.5f, 4.6f),   // Ao Cóc Tía
            (new Vector2(182, 30), 10.5f, 8.5f),     // Đầm Xà Mẫu
        };
        /// <summary>The lake's four mounds: dry islands with a rock the snake can crash into.</summary>
        static readonly Vector2[] Mounds = { new Vector2(176.5f, 33.2f), new Vector2(187.5f, 33.2f), new Vector2(176.5f, 26.8f), new Vector2(187.5f, 26.8f) };
        const float MoundRadius = 2.3f;

        class SwampCamp
        {
            public string name;
            public Vector2 at;
            public float radius;
            public int count;
            public string kind;   // toad, leech, mud, watersnake, dragonfly, wisp
            public DayPart when = DayPart.Always;
            /// <summary>Flying or floating: the water around it is kept (dragonflies over the pools, wisps over the mire).</summary>
            public bool flies;
        }

        static readonly SwampCamp[] SwampCamps =
        {
            new SwampCamp { name = "ToadCamp_Edge", at = new Vector2(121, 20.5f), radius = 2.4f, count = 3, kind = "toad" },
            new SwampCamp { name = "ToadCamp_Reeds", at = new Vector2(130, 40.5f), radius = 2.2f, count = 3, kind = "toad" },
            new SwampCamp { name = "ToadCamp_South", at = new Vector2(152, 13), radius = 2.2f, count = 3, kind = "toad" },
            new SwampCamp { name = "ToadCamp_East", at = new Vector2(172, 18.5f), radius = 2f, count = 2, kind = "toad" },
            new SwampCamp { name = "LeechPool_West", at = new Vector2(124, 33), radius = 1.2f, count = 2, kind = "leech" },
            new SwampCamp { name = "LeechPool_Mid", at = new Vector2(156, 36), radius = 1.8f, count = 3, kind = "leech" },
            new SwampCamp { name = "LeechPool_Deep", at = new Vector2(175, 44), radius = 1.4f, count = 2, kind = "leech" },
            new SwampCamp { name = "MudCamp_Field", at = MudField, radius = 2f, count = 2, kind = "mud" },
            new SwampCamp { name = "MudCamp_North", at = new Vector2(163, 45), radius = 2f, count = 2, kind = "mud" },
            // water snakes in the two pools nobody else lives in
            new SwampCamp { name = "SnakePool_Reeds", at = new Vector2(133, 35.5f), radius = 1.6f, count = 2, kind = "watersnake" },
            new SwampCamp { name = "SnakePool_South", at = new Vector2(146, 15), radius = 1.4f, count = 2, kind = "watersnake" },
            // by day dragonflies over the open mire; at night wisps, near the leech pools they lure heroes into
            new SwampCamp { name = "Dragonflies_Reeds", at = new Vector2(139, 29), radius = 3f, count = 3, kind = "dragonfly", when = DayPart.Day, flies = true },
            new SwampCamp { name = "Dragonflies_East", at = new Vector2(165, 25), radius = 3f, count = 3, kind = "dragonfly", when = DayPart.Day, flies = true },
            new SwampCamp { name = "Dragonflies_West", at = new Vector2(116, 42), radius = 2.5f, count = 2, kind = "dragonfly", when = DayPart.Day, flies = true },
            new SwampCamp { name = "Wisps_Middle", at = new Vector2(147, 40), radius = 2.5f, count = 2, kind = "wisp", when = DayPart.Night, flies = true },
            new SwampCamp { name = "Wisps_Deep", at = new Vector2(168, 52), radius = 2.5f, count = 2, kind = "wisp", when = DayPart.Night, flies = true },
            new SwampCamp { name = "Wisps_South", at = new Vector2(126, 11), radius = 2.5f, count = 2, kind = "wisp", when = DayPart.Night, flies = true },
        };

        /// <summary>The cave's camps: bats in their cave and among the crystals, spiders by their nest, golems in the old mine.</summary>
        static readonly SwampCamp[] CaveCamps =
        {
            new SwampCamp { name = "Bats_West", at = new Vector2(142.5f, 78.5f), radius = 2.4f, count = 3, kind = "bat", flies = true },
            new SwampCamp { name = "Bats_East", at = new Vector2(150, 74.5f), radius = 2.2f, count = 3, kind = "bat", flies = true },
            new SwampCamp { name = "Bats_Crystals", at = new Vector2(129, 98), radius = 2.4f, count = 3, kind = "bat", flies = true },
            new SwampCamp { name = "Spiders_Nest", at = new Vector2(155, 107.5f), radius = 2.2f, count = 3, kind = "spider" },
            new SwampCamp { name = "Spiders_Deep", at = new Vector2(160.5f, 104), radius = 1.8f, count = 2, kind = "spider" },
            new SwampCamp { name = "Spiders_Forest", at = new Vector2(143.5f, 99.5f), radius = 1.8f, count = 2, kind = "spider" },
            new SwampCamp { name = "Golems_MineWest", at = new Vector2(171.5f, 91), radius = 2f, count = 2, kind = "golem" },
            new SwampCamp { name = "Golems_MineEast", at = new Vector2(182.5f, 89.5f), radius = 2f, count = 2, kind = "golem" },
            new SwampCamp { name = "Golems_Hall", at = new Vector2(186, 111), radius = 2.2f, count = 2, kind = "golem" },
        };

        /// <summary>Đá Truyền Tống: id, name, where.</summary>
        static readonly (string id, string name, Vector2 at)[] Stones =
        {
            ("village", "Làng Lá Xanh", new Vector2(21.8f, 14.6f)),
            ("rung_gia", "Cửa Rừng Già", new Vector2(74.8f, 41.2f)),
            ("outpost", "Trạm Nhà Sàn", new Vector2(110.5f, 26.8f)),
            ("toadpond", "Ao Cóc Tía", new Vector2(144f, 45f)),
            ("snakelair", "Đầm Xà Mẫu", new Vector2(166.5f, 33f)),
            ("cuahang", "Cửa Hang Pha Lê", new Vector2(172.6f, 70.6f)),
            ("rungphale", "Rừng Pha Lê", new Vector2(131f, 90.5f)),
            ("nhenchua", "Cổng Hang Nhện Chúa", new Vector2(125f, 103.8f)),
        };

        // the cave (north of the swamp): chambers joined by tunnels, solid rock everywhere else
        static readonly Vector2 CaveMouth = new Vector2(168, 64);
        static readonly Vector2 MinersCamp = new Vector2(168, 72.5f);
        static readonly Vector2 BatCave = new Vector2(146, 77);
        static readonly Vector2 Mine = new Vector2(177, 90);
        static readonly Vector2 CrystalForest = new Vector2(136, 95);
        static readonly Vector2 SpiderNest = new Vector2(157, 106);
        static readonly Vector2 CrystalHall = new Vector2(186, 112);
        static readonly Vector2 QueenHall = new Vector2(117, 115);
        static readonly (Vector2 c, float rx, float ry)[] Chambers =
        {
            (MinersCamp, 7.5f, 5.2f),       // Cửa Hang: the miners' camp
            (BatCave, 9f, 6f),              // Hang Dơi
            (Mine, 10.5f, 6f),              // Mỏ Bỏ Hoang
            (CrystalForest, 12.5f, 8.5f),   // Rừng Pha Lê
            (SpiderNest, 8.5f, 6.2f),       // Tổ Nhện
            (CrystalHall, 9f, 7f),          // Điện Pha Lê
            (QueenHall, 11.5f, 9.5f),       // Hang Nhện Chúa
        };
        static readonly Vector2[][] Tunnels =
        {
            new[] { new Vector2(168, 58), new Vector2(168, 68) },                                         // the mouth
            new[] { new Vector2(162, 73), new Vector2(156, 75), new Vector2(150, 76.5f) },                // camp → bats
            new[] { new Vector2(170, 77), new Vector2(173, 82), new Vector2(175, 86) },                   // camp → mine
            new[] { new Vector2(143, 82), new Vector2(141, 86), new Vector2(139, 88) },                   // bats → crystal forest
            new[] { new Vector2(147, 99), new Vector2(150, 102), new Vector2(152, 104) },                 // crystal forest → nest
            new[] { new Vector2(185, 95), new Vector2(187, 100), new Vector2(186, 106) },                 // mine → crystal hall
            new[] { new Vector2(163, 108), new Vector2(170, 110.5f), new Vector2(178, 111.5f) },          // nest → hall: a loop
            new[] { new Vector2(127, 101), new Vector2(124, 105), new Vector2(121, 107) },                // forest → the queen's hall
        };
        const float TunnelHalfWidth = 2.1f;

        static System.Random rnd;

        public static Result Build(Transform worldRoot)
        {
            rnd = new System.Random(1234);
            var res = new Result();
            BuildTilemaps(worldRoot, res);
            res.props = new GameObject("Props").transform;
            res.props.SetParent(worldRoot, false);
            PlaceVillage(res);
            PlaceArena(res);
            PlaceOutpost(res);
            PlaceSwampArenas(res);
            PlaceStones(res);
            PlaceForest(res);
            PlaceSwamp(res);
            PlaceCave(res);
            PlaceBounds(worldRoot);
            PlaceActors(worldRoot, res);
            PlaceZones(worldRoot);
            return res;
        }

        // ================================================================== terrain
        static float Noise(float x, float y, float s) => Mathf.PerlinNoise(x * s + 13.1f, y * s + 7.7f);

        static float DistToPath(Vector2 p, Vector2[] path)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector2 a = path[i], b = path[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static float DistToTrails(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (var t in Trails) d = Mathf.Min(d, DistToPath(p, t));
            return d;
        }

        /// <summary>Where the swamp begins on row <paramref name="y"/> (the forest thins out into it).</summary>
        static float SwampEdge(float y) => 102f + 2f * Mathf.Sin(y * 0.23f) + (Noise(3f, y, 0.2f) - 0.5f) * 3f;

        static bool InSwamp(Vector2 p) => p.y < LowH && p.x > SwampEdge(p.y);

        /// <summary>The cave's floor: inside a chamber or along a tunnel, the edges wobbling a little.</summary>
        static bool CaveFloor(Vector2 p)
        {
            float wob = (Noise(p.x * 1.3f + 500f, p.y * 1.3f, 0.25f) - 0.5f) * 0.5f;
            foreach (var (c, rx, ry) in Chambers)
            {
                float dx = (p.x - c.x) / rx, dy = (p.y - c.y) / ry;
                if (dx * dx + dy * dy < 1f + wob) return true;
            }
            foreach (var t in Tunnels)
                if (DistToPath(p, t) < TunnelHalfWidth + wob * 1.5f) return true;
            return false;
        }

        /// <summary>
        /// Solid rock at a grid corner: the whole north of the map but the cave's chambers and
        /// tunnels, and a cliff along the top of the forest and the swamp.
        /// </summary>
        static bool IsWallAt(Vector2 p)
        {
            float edge = LowH - 1.2f + (Noise(p.x, 900f, 0.35f) - 0.5f) * 2.2f;
            if (p.y < edge) return false;
            return !CaveFloor(p);
        }

        static bool InCave(Vector2 p) => p.y >= LowH && p.x > ForestW;

        static bool IsDirt(Vector2 p)
        {
            float wob = (Noise(p.x, p.y, 0.35f) - 0.5f) * 1.2f;
            if (Vector2.Distance(p, Village) < 7.2f + wob) return true;
            if (Vector2.Distance(p, Arena) < 7f + wob) return true;
            if (DistToPath(p, MainPath) < 1.55f + wob * 0.5f) return true;
            if (DistToPath(p, SidePath) < 1.2f + wob * 0.5f) return true;
            if (DistToPath(p, NorthPath) < 1.05f + wob * 0.4f) return true;
            if (DistToPath(p, EastRoad) < 1.3f + wob * 0.5f) return true;
            if (Vector2.Distance(p, Outpost) < 5.2f + wob) return true;
            for (int i = 1; i < Camps.Length; i++)
                if (Vector2.Distance(p, Camps[i]) < CampRadius[i] * 0.65f + wob) return true;
            return false;
        }

        static float DirtDistance(Vector2 p)
        {
            float d = Mathf.Min(DistToPath(p, MainPath) - 1.55f, DistToPath(p, SidePath) - 1.2f, DistToPath(p, NorthPath) - 1.05f);
            d = Mathf.Min(d, DistToPath(p, EastRoad) - 1.3f);
            d = Mathf.Min(d, Vector2.Distance(p, Village) - 7.2f, Vector2.Distance(p, Arena) - 7f, Vector2.Distance(p, Outpost) - 5.2f);
            for (int i = 1; i < Camps.Length; i++) d = Mathf.Min(d, Vector2.Distance(p, Camps[i]) - CampRadius[i] * 0.65f);
            return d;
        }

        static bool IsTallGrass(Vector2 p)
        {
            if (p.x < 27 || p.y > LowH - 2.5f) return false;
            if (p.x > SwampEdge(p.y) - 4f) return false;
            if (Vector2.Distance(p, Arena) < 11.5f) return false;
            if (DirtDistance(p) < 1.6f) return false;
            foreach (var c in Camps)
                if (Vector2.Distance(p, c) < 4.5f) return false;
            foreach (var s in Stones)
                if (Vector2.Distance(p, s.at) < 2f) return false;
            return Noise(p.x, p.y, 0.11f) > 0.57f;
        }

        static bool NearSwampCamp(Vector2 p, float extra, bool groundOnly = false)
        {
            foreach (var c in SwampCamps)
                if ((!groundOnly || !c.flies) && Vector2.Distance(p, c.at) < c.radius + extra) return true;
            return false;
        }

        /// <summary>Swamp water at a grid corner: the pools, the lake, scattered puddles; never on trails, mounds or the outpost.</summary>
        static bool IsWaterAt(Vector2 p)
        {
            if (p.x < 106f || p.y > LowH - 3f || !InSwamp(p)) return false;
            if (Vector2.Distance(p, Outpost) < 7.5f) return false;
            if (DistToTrails(p) < 1.7f) return false;
            foreach (var m in Mounds)
                if (Vector2.Distance(p, m) < MoundRadius + (Noise(p.x * 2f, p.y * 2f, 0.5f) - 0.5f) * 0.6f) return false;
            foreach (var s in Stones)
                if (Vector2.Distance(p, s.at) < 2.2f) return false;
            float wob = (Noise(p.x * 1.7f, p.y * 1.7f, 0.3f) - 0.5f) * 0.35f;
            foreach (var (c, rx, ry) in Ponds)
            {
                float dx = (p.x - c.x) / rx, dy = (p.y - c.y) / ry;
                if (dx * dx + dy * dy < 1f + wob) return true;
            }
            // scattered puddles and channels, away from the camps, the arenas and the edges of the map
            if (p.y < 5f || p.y > LowH - 5f || p.x > W - 5f) return false;
            if (NearSwampCamp(p, 2.5f, true) || Vector2.Distance(p, ToadPond) < 11f || Vector2.Distance(p, SnakeLair) < 14.5f) return false;
            if (DistToTrails(p) < 3f) return false;
            return Noise(p.x + 400f, p.y, 0.09f) > 0.67f;
        }

        static Tile MakeTile(string sprite) => MakeTile(sprite, false);

        /// <summary>A tile asset of a sprite; a <paramref name="solid"/> one fills its cell with a collider (the cave's rock).</summary>
        static Tile MakeTile(string sprite, bool solid)
        {
            EditorUtil.EnsureFolder(TileFolder);
            string path = $"{TileFolder}/{sprite}.asset";
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (EditorUtil.Keep(t)) return t;
            EditorUtil.Written++;
            if (t == null)
            {
                t = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(t, path);
            }
            t.sprite = ArtImporter.S(sprite);
            t.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            EditorUtility.SetDirty(t);
            return t;
        }

        static Tilemap MakeMap(Transform grid, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid, false);
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingLayerName = SortingLayerNames.Ground;
            tr.sortingOrder = order;
            tr.sharedMaterial = AssetFactory.SpriteLit;
            tr.mode = TilemapRenderer.Mode.Chunk;
            return tm;
        }

        /// <summary>
        /// Makes a tilemap's solid tiles one merged collider on the obstacle layer. Called once its
        /// tiles are down: a tilemap collider shapes later tiles a frame later, a frame a batch build
        /// never gives it before it saves the scene (the rock would then load with no collider).
        /// </summary>
        static void MakeSolid(Tilemap map)
        {
            var go = map.gameObject;
            go.layer = Layers.Obstacle;
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var tiles = go.AddComponent<TilemapCollider2D>();
            // compositeOperation = Merge, through its field: the compile check's Unity reference predates the property
            var so = new SerializedObject(tiles);
            so.FindProperty("m_CompositeOperation").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            var shape = go.AddComponent<CompositeCollider2D>();
            shape.geometryType = CompositeCollider2D.GeometryType.Polygons;
            shape.GenerateGeometry();
            if (shape.pathCount == 0) Debug.LogError($"[RPG] {go.name}: the solid tiles made no collider");
        }

        static Tile[] Tiles(string prefix, int count, bool firstEmpty = false) =>
            Enumerable.Range(0, count).Select(i => firstEmpty && i == 0 ? null : MakeTile($"{prefix}_{i}")).ToArray();

        static int Corners(bool[,] v, int x, int y) =>
            (v[x, y + 1] ? 8 : 0) | (v[x + 1, y + 1] ? 4 : 0) | (v[x + 1, y] ? 2 : 0) | (v[x, y] ? 1 : 0);

        static void BuildTilemaps(Transform root, Result res)
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root, false);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            res.ground = MakeMap(gridGo.transform, "Ground", 0);
            res.tall = MakeMap(gridGo.transform, "TallGrass", 1);
            res.dirt = MakeMap(gridGo.transform, "Dirt", 2);
            res.mud = MakeMap(gridGo.transform, "Mud", 3);
            res.water = MakeMap(gridGo.transform, "Water", 4);
            res.details = MakeMap(gridGo.transform, "Details", 5);
            res.walls = MakeMap(gridGo.transform, "Walls", 6);

            var grass = Tiles("grass", 8);
            var swampGround = Tiles("swamp", 8);
            var tallMs = Tiles("tall", 16, true);
            var tallFull = Tiles("tallfull", 4);
            var dirtMs = Tiles("dirt", 16, true);
            var dirtFull = Tiles("dirtfull", 4);
            var mudMs = Tiles("mud", 16, true);
            var mudFull = Tiles("mudfull", 4);
            var waterMs = Tiles("water", 16, true);
            var waterFull = Tiles("waterfull", 4);
            string[] detailNames = { "tuft_0", "tuft_1", "tuft_2", "flowers_0", "flowers_1", "flowers_2", "leaves_0", "leaves_1", "pebbles_0", "pebbles_1", "clover_0", "mushrooms_0" };
            var details = detailNames.Select(MakeTile).ToArray();
            string[] swampDetailNames = { "reeds_0", "reeds_1", "reeds_0", "reeds_1", "puddle_0", "rotleaf_0", "rotleaf_0", "tuft_0", "tuft_2", "pebbles_1" };
            var swampDetails = swampDetailNames.Select(MakeTile).ToArray();
            var lilies = new[] { MakeTile("lily_0"), MakeTile("lily_1"), MakeTile("lily_2"), MakeTile("lotus_0") };
            var caveFloor = Tiles("cave", 8);
            // a wall cell is solid when at least two of its corners are rock (its face and top fill most of it)
            var wallMs = Enumerable.Range(0, 16).Select(i => i == 0 ? null : MakeTile($"cavewall_{i}", Bits(i) >= 2)).ToArray();
            var wallFull = Enumerable.Range(0, 4).Select(i => MakeTile($"cavewallfull_{i}", true)).ToArray();
            string[] caveDetailNames = { "shards_0", "shards_1", "shards_2", "pebbles_c", "pebbles_c", "crack_0", "crack_1", "glowcap_0", "cavepuddle_0" };
            var caveDetails = caveDetailNames.Select(n => MakeTile(n)).ToArray();

            var dirtV = new bool[W + 1, H + 1];
            var tallV = new bool[W + 1, H + 1];
            var waterV = new bool[W + 1, H + 1];
            var mudV = new bool[W + 1, H + 1];
            var wallV = new bool[W + 1, H + 1];
            for (int y = 0; y <= H; y++)
                for (int x = 0; x <= W; x++)
                {
                    var p = new Vector2(x, y);
                    wallV[x, y] = IsWallAt(p);
                    if (y > LowH + 2) continue;   // the cave and the mountain: rock and cave floor only
                    dirtV[x, y] = !wallV[x, y] && IsDirt(p);
                    tallV[x, y] = !dirtV[x, y] && !wallV[x, y] && IsTallGrass(p);
                    waterV[x, y] = !dirtV[x, y] && !wallV[x, y] && IsWaterAt(p);
                }
            for (int y = 0; y <= H; y++)
                for (int x = 0; x <= W; x++)
                {
                    if (waterV[x, y] || dirtV[x, y] || wallV[x, y] || y > LowH) continue;
                    var p = new Vector2(x, y);
                    float wob = (Noise(p.x, p.y, 0.4f) - 0.5f) * 1.2f;
                    bool mud = Mathf.Abs(p.x - SwampEdge(p.y)) < 1.1f + wob;   // hides where the grass turns into swamp
                    if (!mud && InSwamp(p))
                    {
                        mud = DistToTrails(p) < 1.45f + wob * 0.5f
                              || Vector2.Distance(p, MudField) < 4.5f + wob
                              || NearSwampCamp(p, -0.6f)
                              || Noise(p.x + 200f, p.y + 50f, 0.16f) > 0.74f;
                        // a muddy shore around the water
                        for (int dy = -1; dy <= 1 && !mud; dy++)
                            for (int dx = -1; dx <= 1 && !mud; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && ny >= 0 && nx <= W && ny <= H && waterV[nx, ny]) mud = true;
                            }
                    }
                    mudV[x, y] = mud;
                }

            res.terrainWidth = W + 1;
            res.terrain = new byte[(W + 1) * (H + 1)];
            for (int y = 0; y <= H; y++)
                for (int x = 0; x <= W; x++)
                    res.terrain[y * (W + 1) + x] = (byte)((waterV[x, y] ? ZoneRoot.Water : 0) | (mudV[x, y] ? ZoneRoot.Mud : 0) |
                                                          (wallV[x, y] ? ZoneRoot.Wall : 0));

            var gPos = new List<Vector3Int>();
            var gTiles = new List<TileBase>();
            var tPos = new List<Vector3Int>();
            var tTiles = new List<TileBase>();
            var dPos = new List<Vector3Int>();
            var dTiles = new List<TileBase>();
            var mPos = new List<Vector3Int>();
            var mTiles = new List<TileBase>();
            var wPos = new List<Vector3Int>();
            var wTiles = new List<TileBase>();
            var xPos = new List<Vector3Int>();
            var xTiles = new List<TileBase>();
            var kPos = new List<Vector3Int>();
            var kTiles = new List<TileBase>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = new Vector3Int(x, y, 0);
                    var center = new Vector2(x + 0.5f, y + 0.5f);
                    int ki = Corners(wallV, x, y);
                    if (ki == 15)
                    {
                        // solid rock: nothing shows under it
                        kPos.Add(c);
                        kTiles.Add(wallFull[Pick(wallFull.Length, x, y)]);
                        continue;
                    }
                    if (ki > 0)
                    {
                        kPos.Add(c);
                        kTiles.Add(wallMs[ki]);
                    }
                    if (y >= LowH)
                    {
                        // the cave floor, a crystal splinter or a pebble here and there
                        gPos.Add(c);
                        gTiles.Add(caveFloor[Pick(caveFloor.Length, x, y)]);
                        if (ki == 0 && rnd.NextDouble() < 0.09)
                        {
                            xPos.Add(c);
                            xTiles.Add(caveDetails[rnd.Next(caveDetails.Length)]);
                        }
                        continue;
                    }
                    bool swamp = InSwamp(center);
                    gPos.Add(c);
                    gTiles.Add(swamp ? swampGround[Pick(swampGround.Length, x, y)] : grass[Pick(grass.Length, x, y)]);
                    int ti = Corners(tallV, x, y);
                    if (ti == 15) { tPos.Add(c); tTiles.Add(tallFull[Pick(tallFull.Length, x, y)]); }
                    else if (ti > 0) { tPos.Add(c); tTiles.Add(tallMs[ti]); }
                    int di = Corners(dirtV, x, y);
                    if (di == 15) { dPos.Add(c); dTiles.Add(dirtFull[Pick(dirtFull.Length, x, y)]); }
                    else if (di > 0) { dPos.Add(c); dTiles.Add(dirtMs[di]); }
                    int mi = Corners(mudV, x, y);
                    if (mi == 15) { mPos.Add(c); mTiles.Add(mudFull[Pick(mudFull.Length, x, y)]); }
                    else if (mi > 0) { mPos.Add(c); mTiles.Add(mudMs[mi]); }
                    int wi = Corners(waterV, x, y);
                    if (wi == 15) { wPos.Add(c); wTiles.Add(waterFull[Pick(waterFull.Length, x, y)]); }
                    else if (wi > 0) { wPos.Add(c); wTiles.Add(waterMs[wi]); }
                    if (wi == 15)
                    {
                        // lily pads and the odd lotus on open water
                        if (rnd.NextDouble() < 0.14)
                        {
                            xPos.Add(c);
                            xTiles.Add(rnd.NextDouble() < 0.2 ? lilies[3] : lilies[rnd.Next(3)]);
                        }
                    }
                    else if (di == 0 && ti == 0 && mi == 0 && wi == 0 && ki == 0 && rnd.NextDouble() < 0.1)
                    {
                        xPos.Add(c);
                        xTiles.Add(swamp ? swampDetails[rnd.Next(swampDetails.Length)] : details[rnd.Next(details.Length)]);
                    }
                }
            res.ground.SetTiles(gPos.ToArray(), gTiles.ToArray());
            res.tall.SetTiles(tPos.ToArray(), tTiles.ToArray());
            res.dirt.SetTiles(dPos.ToArray(), dTiles.ToArray());
            res.mud.SetTiles(mPos.ToArray(), mTiles.ToArray());
            res.water.SetTiles(wPos.ToArray(), wTiles.ToArray());
            res.details.SetTiles(xPos.ToArray(), xTiles.ToArray());
            res.walls.SetTiles(kPos.ToArray(), kTiles.ToArray());
            MakeSolid(res.walls);
            waterAt =(x, y) => x >= 0 && y >= 0 && x <= W && y <= H && waterV[x, y];
            wallAt = (x, y) => x < 0 || y < 0 || x > W || y > H || wallV[x, y];
        }

        /// <summary>Water at a grid corner of the map being built (props keep out of it).</summary>
        static System.Func<int, int, bool> waterAt = (x, y) => false;

        /// <summary>Rock at a grid corner of the map being built.</summary>
        static System.Func<int, int, bool> wallAt = (x, y) => false;

        /// <summary>Whether a prop at <paramref name="p"/> would stand in or against the rock (a margin of <paramref name="reach"/> corners).</summary>
        static bool NearWall(Vector2 p, int reach)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                    if (wallAt(cx + dx, cy + dy)) return true;
            return false;
        }

        static int Bits(int i) => (i & 1) + ((i >> 1) & 1) + ((i >> 2) & 1) + ((i >> 3) & 1);

        /// <summary>Whether a prop at <paramref name="p"/> would stand in water.</summary>
        static bool Wet(Vector2 p) => waterAt(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));

        static bool NearWater(Vector2 p, int reach)
        {
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                    if (waterAt(cx + dx, cy + dy)) return true;
            return false;
        }

        static int Pick(int n, int x, int y)
        {
            // mostly the first variants, occasionally the busier ones
            double r = rnd.NextDouble();
            if (n >= 8) return r < 0.55 ? rnd.Next(3) : rnd.Next(n);
            return rnd.Next(n);
        }

        // ================================================================== props
        static GameObject Place(string prop, Vector2 pos, Transform parent)
        {
            if (!PrefabFactory.Props.TryGetValue(prop, out var prefab) || prefab == null)
            {
                Debug.LogWarning("[RPG] Missing prop prefab " + prop);
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = new Vector3(pos.x, pos.y, 0);
            return go;
        }

        static readonly List<Vector2> Occupied = new List<Vector2>();

        static bool Free(Vector2 p, float minDist)
        {
            foreach (var o in Occupied)
                if ((o - p).sqrMagnitude < minDist * minDist) return false;
            return true;
        }

        static void Mark(Vector2 p) => Occupied.Add(p);

        static void PlaceVillage(Result res)
        {
            Occupied.Clear();
            var t = res.props;
            void P(string n, float x, float y) { Place(n, new Vector2(x, y), t); Mark(new Vector2(x, y)); }
            P("hut", 13f, 24.2f);
            P("hut", 23.5f, 25.2f);
            P("tent", 25.8f, 21.4f);
            P("campfire", 18.5f, 20.4f);
            P("well", 11f, 16.8f);
            P("signpost", 27.6f, 18.1f);
            P("lantern", 14.8f, 17.4f);
            P("lantern", 22.6f, 17.2f);
            P("lantern", 26.9f, 19.7f);
            P("crate", 10.6f, 23.1f);
            P("crate", 11.5f, 22.4f);
            P("barrel", 16.2f, 23.4f);
            P("barrel", 21f, 24.3f);
            P("log", 20.3f, 18.4f);
            P("stump", 16.4f, 18.8f);
            // the smithy: the smith's fire and his stock
            P("campfire", 25.9f, 15.1f);
            P("barrel", 26.4f, 16.5f);
            // fences around the northern gardens
            for (float x = 8.5f; x <= 15.5f; x += 1f) P("fence_h", x, 27.4f);
            P("fence_post", 16.2f, 27.4f);
            for (float x = 19.5f; x <= 26.5f; x += 1f) P("fence_h", x, 28.2f);
            P("fence_post", 27.2f, 28.2f);
            // flowers/bushes to dress it
            P("bush_small_0", 9.4f, 20.2f);
            P("bush_big_1", 28.6f, 24.2f);
            P("mushrooms", 9.8f, 14.5f);
        }

        static void PlaceArena(Result res)
        {
            var t = res.props;
            float[] angles = { 15, 60, 105, 150, 195, 300, 345 };
            for (int i = 0; i < angles.Length; i++)
            {
                var d = Util.FromAngle(angles[i]);
                var p = Arena + d * 9.3f;
                Place($"pillar_{i % 2}", p, t);
                Mark(p);
            }
            var bouldersRoot = new GameObject("ArenaBoulders").transform;
            bouldersRoot.SetParent(res.props.parent, false);
            foreach (var off in new[] { new Vector2(-4.2f, -2.2f), new Vector2(4.6f, 1.2f), new Vector2(-1.2f, 4.4f) })
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(PrefabFactory.Boulder, bouldersRoot);
                go.transform.position = Arena + off;
                Mark(Arena + off);
            }
            // ruin dressing
            Place("rock_big_0", Arena + new Vector2(7.5f, -5.5f), t);
            Place("rock_small_1", Arena + new Vector2(-8.2f, 3.5f), t);
            Place("log", Arena + new Vector2(6.8f, 6.2f), t);
        }

        /// <summary>Trạm Nhà Sàn: the fishers' stilt huts on a dry clearing at the swamp's edge (no enemies come here).</summary>
        static void PlaceOutpost(Result res)
        {
            var t = res.props;
            void P(string n, float x, float y) { Place(n, Outpost + new Vector2(x, y), t); Mark(Outpost + new Vector2(x, y)); }
            P("stilthut", -3f, 3.8f);
            P("stilthut", 3.8f, 3.2f);
            P("campfire", 0.2f, -0.2f);
            P("lantern", -4.6f, 0.6f);
            P("lantern", 4.6f, -0.6f);
            P("crate", 2.6f, -2.8f);
            P("crate", 3.3f, -2.1f);
            P("barrel", -5.2f, 2.8f);
            P("barrel", 5.4f, 1.2f);
            P("log", -1.8f, -1.6f);
            P("signpost", -6.5f, -1.4f);
            P("cattails_0", 6.6f, 4.8f);
            P("cattails_1", -6.8f, 5.2f);
        }

        static void PlaceSwampArenas(Result res)
        {
            var t = res.props;
            // Ao Cóc Tía: reeds around the pond, a rock at its head, old bones on the shore
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f + 8f;
                if (a > 190f && a < 250f) continue;   // the way in, from the south-west
                var p = new Vector2(153f, 51.5f) + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * 7.8f, Mathf.Sin(a * Mathf.Deg2Rad) * 5.9f);
                Place(i % 2 == 0 ? "cattails_0" : "cattails_1", p, t);
                Mark(p);
            }
            Place("swamprock_big_1", new Vector2(153.5f, 57.8f), t);
            Mark(new Vector2(153.5f, 57.8f));
            Place("bones", new Vector2(147.2f, 46.4f), t);
            Place("bones", new Vector2(159.4f, 47.6f), t);
            // Đầm Xà Mẫu: four mounds in the lake (crash the snake into them), dead trees around the shore
            for (int i = 0; i < Mounds.Length; i++)
            {
                Place($"swamprock_big_{i % 2}", Mounds[i], t);
                Mark(Mounds[i]);
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i * 36f + 18f;
                if (a > 150f && a < 215f) continue;   // the way in, from the west
                var p = SnakeLair + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * 12.4f, Mathf.Sin(a * Mathf.Deg2Rad) * 10.2f);
                Place($"deadtree_{i % 2}", p, t);
                Mark(p);
            }
            Place("bones", SnakeLair + new Vector2(-11.5f, 3.6f), t);
            Place("mossylog", SnakeLair + new Vector2(-12f, -4.4f), t);
        }

        static void PlaceStones(Result res)
        {
            foreach (var (id, name, at) in Stones)
            {
                var go = Place("waystone", at, res.props);
                if (go == null) continue;
                go.name = "DaTruyenTong_" + id;
                var w = go.GetComponent<Waystone>();
                if (w != null)
                {
                    w.stoneId = id;
                    w.displayName = name;
                    EditorUtility.SetDirty(w);
                }
                Mark(at);
            }
        }

        static bool Reserved(Vector2 p)
        {
            if (Vector2.Distance(p, Village) < 10.5f) return true;
            if (Vector2.Distance(p, Arena) < 11.8f) return true;
            for (int i = 0; i < Camps.Length; i++)
                if (Vector2.Distance(p, Camps[i]) < CampRadius[i] + 1.2f) return true;
            foreach (var s in Stones)
                if (Vector2.Distance(p, s.at) < 2.6f) return true;
            return false;
        }

        static bool SwampReserved(Vector2 p)
        {
            if (Vector2.Distance(p, Outpost) < 8f) return true;
            if (Vector2.Distance(p, ToadPond) < 9.5f) return true;
            if (Vector2.Distance(p, SnakeLair) < 13.5f) return true;
            if (NearSwampCamp(p, 1.6f)) return true;
            foreach (var s in Stones)
                if (Vector2.Distance(p, s.at) < 2.6f) return true;
            return false;
        }

        static void PlaceForest(Result res)
        {
            var t = res.props;
            float R() => (float)rnd.NextDouble();
            // dense border around the whole map: forest trees in the west, dead trees and willows in the swamp
            for (float y = 0.8f; y < LowH; y += 1.7f)
                for (float x = 0.8f; x < W; x += 1.7f)
                {
                    float edge = Mathf.Min(x, y, W - x, LowH - y);
                    if (edge > 3.6f) continue;
                    var p = new Vector2(x + (R() - 0.5f) * 0.8f, y + (R() - 0.5f) * 0.8f);
                    if (DirtDistance(p) < 0.8f && edge > 1.5f) continue;
                    if (Vector2.Distance(p, CaveMouth) < 4.5f || NearWall(p, 0)) continue;   // the way up to the cave stays open
                    Place(InSwamp(p) ? SwampTree(p) : Tree(p, 0.8f), p, t);
                    Mark(p);
                }
            // forest fill, thinning out where the swamp begins
            for (int i = 0; i < 9000; i++)
            {
                var p = new Vector2(3 + R() * (ForestW + 4), 3 + R() * (LowH - 6));
                if (InSwamp(p) || Reserved(p)) continue;
                float dd = DirtDistance(p);
                if (dd < 1.7f) continue;
                float edgeGap = SwampEdge(p.y) - p.x;
                float density = p.x < 30 ? 0.3f : edgeGap < 5f ? 0.55f : 0.95f;
                if (R() > density) continue;
                if (!Free(p, 2.1f)) continue;
                Place(edgeGap < 5f && R() < 0.4f ? $"deadtree_{rnd.Next(2)}" : Tree(p, p.x < 30 ? 0.35f : 0.75f), p, t);
                Mark(p);
            }
            // undergrowth
            string[] small = { "bush_big_0", "bush_big_1", "bush_small_0", "bush_small_1", "rock_big_0", "rock_big_1", "rock_small_0",
                               "rock_small_1", "stump", "log", "mushrooms", "mushrooms", "bush_small_0", "bush_small_1" };
            int placed = 0;
            for (int i = 0; i < 6000 && placed < 230; i++)
            {
                var p = new Vector2(3 + R() * (ForestW - 6), 3 + R() * (LowH - 6));
                if (InSwamp(p)) continue;
                if (Vector2.Distance(p, Village) < 9f || Vector2.Distance(p, Arena) < 9.5f) continue;
                if (DirtDistance(p) < 0.9f) continue;
                bool nearCamp = false;
                for (int c = 0; c < Camps.Length; c++) nearCamp |= Vector2.Distance(p, Camps[c]) < CampRadius[c] * 0.6f;
                if (nearCamp) continue;
                if (!Free(p, 1.3f)) continue;
                Place(small[rnd.Next(small.Length)], p, t);
                Mark(p);
                placed++;
            }
        }

        /// <summary>Dead trees and weeping willows over the water and the mud; a stray pine near the forest.</summary>
        static void PlaceSwamp(Result res)
        {
            var t = res.props;
            float R() => (float)rnd.NextDouble();
            for (int i = 0; i < 8000; i++)
            {
                var p = new Vector2(ForestW - 2 + R() * (W - ForestW - 1), 3 + R() * (LowH - 6));
                if (!InSwamp(p) || SwampReserved(p)) continue;
                if (DistToTrails(p) < 1.9f || DirtDistance(p) < 1.6f) continue;
                bool wet = Wet(p);
                if (wet && R() > 0.12f) continue;   // the odd dead tree stands in the water
                if (R() > 0.5f) continue;
                if (!Free(p, 2.4f)) continue;
                Place(wet ? $"deadtree_{rnd.Next(2)}" : SwampTree(p), p, t);
                Mark(p);
            }
            // reeds along the water, rocks, logs, bones and a few bushes
            string[] small = { "swamprock_small_0", "swamprock_small_1", "swamprock_big_0", "swamprock_big_1", "mossylog", "bones",
                               "bush_small_0", "bush_small_1", "mushrooms", "stump" };
            int placed = 0;
            for (int i = 0; i < 9000 && placed < 320; i++)
            {
                var p = new Vector2(ForestW + R() * (W - ForestW - 3), 3 + R() * (LowH - 6));
                if (!InSwamp(p) || Wet(p)) continue;
                if (Vector2.Distance(p, Outpost) < 7f || Vector2.Distance(p, ToadPond) < 9f || Vector2.Distance(p, SnakeLair) < 12.5f) continue;
                if (DistToTrails(p) < 1.2f || DirtDistance(p) < 0.9f || NearSwampCamp(p, 0f)) continue;
                bool shore = NearWater(p, 1);
                if (!shore && R() < 0.35f) continue;
                if (!Free(p, 1.3f)) continue;
                Place(shore && R() < 0.75f ? $"cattails_{rnd.Next(2)}" : small[rnd.Next(small.Length)], p, t);
                Mark(p);
                placed++;
            }
        }

        /// <summary>Near the middle of a tunnel (props keep them open).</summary>
        static bool InTunnel(Vector2 p, float extra)
        {
            foreach (var t in Tunnels)
                if (DistToPath(p, t) < TunnelHalfWidth * 0.6f + extra) return true;
            return false;
        }

        /// <summary>
        /// Hang Pha Lê: the miners' camp at the mouth, stalagmites in the bats' cave, rails, carts and
        /// timber in the old mine, a thicket of glowing crystals, webs over the spiders' nest, amber
        /// crystals in the crystal hall and the ring of crystal pillars in the spider queen's hall.
        /// The middle of every chamber and every tunnel stays open to fight and walk through.
        /// </summary>
        static void PlaceCave(Result res)
        {
            var t = res.props;
            float R() => (float)rnd.NextDouble();
            void P(string n, Vector2 at)
            {
                if (Place(n, at, t) != null) Mark(at);
            }
            // the camps' middles stay clear
            foreach (var camp in CaveCamps) Mark(camp.at);
            // Cửa Hang: the miners' camp around a fire, their carts on a stretch of rails
            var c = MinersCamp;
            P("campfire", c + new Vector2(-1.8f, -0.4f));
            P("lantern", c + new Vector2(-5.4f, 1.4f));
            P("lantern", c + new Vector2(3.2f, -3.2f));
            P("crate", c + new Vector2(-5f, -2.2f));
            P("crate", c + new Vector2(-4.2f, -2.9f));
            P("barrel", c + new Vector2(-3.4f, 3.3f));
            P("barrel", c + new Vector2(-2.5f, 3.7f));
            P("rails_h", c + new Vector2(2.2f, 3.3f));
            P("minecart", c + new Vector2(2.2f, 3.5f));
            P("signpost", c + new Vector2(-2f, -4.4f));
            P("timber", new Vector2(170.6f, 78.2f));      // the way into the mine
            P("timber", new Vector2(168f, 66.6f));        // the mouth
            // Mỏ Bỏ Hoang: a line of track across it, carts left behind, ore in the rock
            for (float x = Mine.x - 7.5f; x <= Mine.x + 7.5f; x += 2f) P("rails_h", new Vector2(x, Mine.y - 0.6f));
            P("minecart", new Vector2(Mine.x - 3.4f, Mine.y - 0.4f));
            P("minecart_empty", new Vector2(Mine.x + 4.6f, Mine.y - 0.4f));
            P("orevein", new Vector2(Mine.x - 6.5f, Mine.y + 3.6f));
            P("orevein", new Vector2(Mine.x + 7.2f, Mine.y + 3.2f));
            P("lantern", new Vector2(Mine.x - 1f, Mine.y + 4.2f));
            P("timber", new Vector2(185.2f, 96.2f));      // on to the crystal hall
            // Hang Nhện Chúa: six crystal pillars in a ring (her beam bounces off them)
            for (int i = 0; i < 6; i++)
            {
                var d = Util.FromAngle(i * 60f + 30f);
                P("crystal_pillar", QueenHall + new Vector2(d.x * 7.2f, d.y * 5.8f));
            }
            P("cobweb_0", QueenHall + new Vector2(-8.5f, 5.5f));
            P("cobweb_1", QueenHall + new Vector2(8f, 6.2f));
            P("bones", QueenHall + new Vector2(-6f, -6.5f));
            // the chambers' edges: what grows in each, the middle kept open
            void Dress(Vector2 center, float rx, float ry, int count, string[] kinds, float minSpacing)
            {
                int placed = 0;
                for (int i = 0; i < 400 && placed < count; i++)
                {
                    float a = R() * Mathf.PI * 2f;
                    float r = Mathf.Lerp(0.5f, 0.9f, R());
                    var p = center + new Vector2(Mathf.Cos(a) * rx * r, Mathf.Sin(a) * ry * r);
                    if (!CaveFloor(p) || NearWall(p, 1) || InTunnel(p, 1.5f) || !Free(p, minSpacing)) continue;
                    bool nearStone = false;
                    foreach (var st in Stones) nearStone |= Vector2.Distance(p, st.at) < 2.6f;
                    if (nearStone) continue;
                    P(kinds[rnd.Next(kinds.Length)], p);
                    placed++;
                }
            }
            string[] rocks = { "stalagmite_0", "stalagmite_1", "caverock_big", "caverock_small" };
            Dress(MinersCamp, 7.5f, 5.2f, 3, new[] { "caverock_small", "stalagmite_1", "crystal_small_cyan" }, 2.2f);
            Dress(BatCave, 9f, 6f, 9, new[] { "stalagmite_0", "stalagmite_0", "stalagmite_1", "caverock_big", "crystal_small_cyan", "crystal_big_cyan" }, 2.2f);
            Dress(Mine, 10.5f, 6f, 5, new[] { "caverock_big", "caverock_small", "stalagmite_1", "crystal_small_amber" }, 2.4f);
            Dress(CrystalForest, 12.5f, 8.5f, 16, new[] { "crystal_big_cyan", "crystal_big_pink", "crystal_big_amber", "crystal_small_cyan",
                                                          "crystal_small_pink", "crystal_small_amber", "stalagmite_0" }, 2.6f);
            Dress(SpiderNest, 8.5f, 6.2f, 7, new[] { "cobweb_0", "cobweb_1", "bones", "crystal_small_pink", "caverock_small" }, 2.2f);
            Dress(CrystalHall, 9f, 7f, 8, new[] { "crystal_big_amber", "crystal_big_amber", "crystal_small_amber", "crystal_big_pink", "stalagmite_0" }, 2.6f);
            Dress(QueenHall, 11.5f, 9.5f, 4, rocks, 2.8f);
            // the tunnels: the odd rock or crystal against their walls
            foreach (var tun in Tunnels)
            {
                for (int i = 0; i + 1 < tun.Length; i++)
                {
                    var p = Vector2.Lerp(tun[i], tun[i + 1], 0.5f);
                    var side = Vector2.Perpendicular((tun[i + 1] - tun[i]).normalized) * (TunnelHalfWidth - 0.4f) * (R() < 0.5f ? 1f : -1f);
                    var q = p + side;
                    if (q.y < LowH + 2f || !CaveFloor(q) || !Free(q, 2f)) continue;
                    P(R() < 0.5f ? "caverock_small" : "crystal_small_cyan", q);
                }
            }
        }

        static string Tree(Vector2 p, float pineChance)
        {
            if (rnd.NextDouble() < pineChance) return $"pine_{rnd.Next(3)}";
            return $"oak_{rnd.Next(2)}";
        }

        static string SwampTree(Vector2 p)
        {
            double r = rnd.NextDouble();
            if (p.x < SwampEdge(p.y) + 6f && r < 0.2) return $"pine_{rnd.Next(3)}";
            return r < 0.6 ? $"deadtree_{rnd.Next(2)}" : $"willow_{rnd.Next(2)}";
        }

        static void PlaceBounds(Transform root)
        {
            var go = new GameObject("MapBounds");
            go.transform.SetParent(root, false);
            go.layer = Layers.Obstacle;
            void Box(float x, float y, float w, float h)
            {
                var b = go.AddComponent<BoxCollider2D>();
                b.offset = new Vector2(x, y);
                b.size = new Vector2(w, h);
            }
            Box(W / 2f, -0.5f, W + 2, 1);
            Box(W / 2f, H + 0.5f, W + 2, 1);
            Box(-0.5f, H / 2f, 1, H + 2);
            Box(W + 0.5f, H / 2f, 1, H + 2);
        }

        // ================================================================== actors
        static GameObject Spawn(GameObject prefab, Vector2 at, Transform parent, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = at;
            if (name != null) go.name = name;
            return go;
        }

        /// <summary>Enemies waiting switched off until something calls them (a mud man's split, a boss's summons).</summary>
        static Brood MakeBrood(Transform parent, string name, GameObject prefab, int count, Vector2 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var brood = go.AddComponent<Brood>();
            for (int i = 0; i < count; i++)
            {
                var e = Spawn(prefab, at + new Vector2((i % 2 - 0.5f) * 0.8f, (i / 2 - 0.5f) * 0.8f), go.transform);
                brood.members.Add(e.GetComponent<EnemyBase>());
                e.SetActive(false);
            }
            EditorUtility.SetDirty(brood);
            return brood;
        }

        static Transform Marker(Transform parent, string name, Vector2 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = at;
            return t;
        }

        static void PlaceActors(Transform root, Result res)
        {
            var actors = new GameObject("Actors").transform;
            actors.SetParent(root, false);
            res.playerSpawn = new GameObject("PlayerSpawn").transform;
            res.playerSpawn.SetParent(actors, false);
            res.playerSpawn.position = new Vector3(18.5f, 15.2f, 0);   // the hero lives in the Core scene

            res.chief = Spawn(PrefabFactory.Chief, new Vector2(20.4f, 21.1f), actors, "Truong Lang").transform;
            res.girl = Spawn(PrefabFactory.Girl, new Vector2(12.6f, 15.4f), actors, "Be Mai").transform;
            if (PrefabFactory.Smith != null) Spawn(PrefabFactory.Smith, new Vector2(24.3f, 15.6f), actors, "Tho Ren");

            var camps = new GameObject("EnemyCamps").transform;
            camps.SetParent(root, false);
            GameObject Camp(string name, GameObject prefab, Vector2 at, int count, float radius)
            {
                var go = new GameObject(name);
                go.transform.SetParent(camps, false);
                go.transform.position = at;
                var sp = go.AddComponent<EnemySpawner>();
                sp.prefab = prefab;
                sp.count = count;
                sp.radius = radius;
                for (int i = 0; i < count; i++)
                {
                    float a = i * Mathf.PI * 2f / count + 0.4f;
                    Spawn(prefab, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * 0.6f, go.transform);
                }
                return go;
            }
            Camp("SlimeCamp_Edge", PrefabFactory.Slime, Camps[0], 2, 1.8f);
            Camp("SlimeCamp_Clearing", PrefabFactory.Slime, Camps[1], 4, 2.6f);
            Camp("SlimeCamp_South", PrefabFactory.Slime, Camps[2], 3, 2.2f);
            Camp("ShroomCamp_East", PrefabFactory.Shroom, Camps[3], 3, 2.2f);
            Camp("ShroomCamp_Path", PrefabFactory.Shroom, Camps[4], 2, 1.8f);
            res.forestSpot = camps.Find("SlimeCamp_Clearing");

            // the swamp's camps (some only by day or at night); each mud man camp keeps its Bùn Con in a brood beside it
            foreach (var c in SwampCamps)
            {
                var prefab = c.kind == "toad" ? PrefabFactory.Toad : c.kind == "leech" ? PrefabFactory.Leech
                           : c.kind == "watersnake" ? PrefabFactory.WaterSnake : c.kind == "dragonfly" ? PrefabFactory.Dragonfly
                           : c.kind == "wisp" ? PrefabFactory.Wisp : PrefabFactory.MudMan;
                if (prefab == null) continue;
                var go = Camp(c.name, prefab, c.at, c.count, c.radius);
                go.GetComponent<EnemySpawner>().activeAt = c.when;
                if (c.kind != "mud" || PrefabFactory.Mudling == null) continue;
                var brood = MakeBrood(camps, c.name + "_Brood", PrefabFactory.Mudling, c.count * 2, c.at);
                foreach (var m in go.GetComponentsInChildren<MudManAI>(true))
                {
                    m.brood = brood;
                    EditorUtility.SetDirty(m);
                }
            }
            // the cave's camps
            foreach (var c in CaveCamps)
            {
                var prefab = c.kind == "bat" ? PrefabFactory.Bat : c.kind == "spider" ? PrefabFactory.CaveSpider : PrefabFactory.Golem;
                if (prefab != null) Camp(c.name, prefab, c.at, c.count, c.radius);
            }
            res.swampSpot = camps.Find("ToadCamp_Edge");
            res.mudField = camps.Find("MudCamp_Field");
            res.snakePools = camps.Find("SnakePool_Reeds");
            res.dragonflies = camps.Find("Dragonflies_Reeds");
            res.wisps = camps.Find("Wisps_Middle");

            var arena = new GameObject("BossArena").transform;
            arena.SetParent(root, false);
            arena.position = Arena;
            res.bossSpot = arena;
            var bear = Spawn(PrefabFactory.Bear, Arena + new Vector2(0, 1.2f), arena, "Gau Ma Rung Gia");
            res.boss = bear.GetComponent<BossBear>();
            res.boss.arenaCenter = arena;
            EditorUtility.SetDirty(res.boss);

            if (PrefabFactory.ToadKing != null)
            {
                var pond = new GameObject("ToadPondArena").transform;
                pond.SetParent(root, false);
                pond.position = ToadPond;
                res.toadPond = pond;
                var king = Spawn(PrefabFactory.ToadKing, ToadPond + new Vector2(0, 0.8f), pond, "Coc Tia").GetComponent<BossToadKing>();
                king.arenaCenter = pond;
                if (PrefabFactory.Toad != null) king.brood = MakeBrood(pond, "Brood", PrefabFactory.Toad, 4, ToadPond + new Vector2(0, 1.5f));
                EditorUtility.SetDirty(king);
            }
            if (PrefabFactory.Snake != null)
            {
                var lair = new GameObject("SnakeLairArena").transform;
                lair.SetParent(root, false);
                lair.position = SnakeLair;
                res.snakeLair = lair;
                var snake = Spawn(PrefabFactory.Snake, SnakeLair + new Vector2(1f, 0f), lair, "Xa Mau Dam Lay").GetComponent<BossSnakeMother>();
                snake.arenaCenter = lair;
                if (PrefabFactory.Leech != null) snake.brood = MakeBrood(lair, "Brood", PrefabFactory.Leech, 4, SnakeLair + new Vector2(0f, -1.5f));
                EditorUtility.SetDirty(snake);
            }
            res.outpost = Marker(actors, "OutpostSpot", Outpost + new Vector2(0f, -2f));
            res.spots.Add(("cavemouth", Marker(actors, "CaveMouthSpot", MinersCamp)));
            res.spots.Add(("batcave", Marker(actors, "BatCaveSpot", BatCave)));
            res.spots.Add(("mine", Marker(actors, "MineSpot", Mine)));
            res.spots.Add(("crystalforest", Marker(actors, "CrystalForestSpot", CrystalForest)));
            res.spots.Add(("spidernest", Marker(actors, "SpiderNestSpot", SpiderNest)));
            res.spots.Add(("crystalhall", Marker(actors, "CrystalHallSpot", CrystalHall)));
            res.spots.Add(("queenhall", Marker(actors, "QueenHallSpot", QueenHall)));
            if (res.toadPond == null) res.toadPond = Marker(actors, "ToadPondSpot", ToadPond);
            if (res.snakeLair == null) res.snakeLair = Marker(actors, "SnakeLairSpot", SnakeLair);
        }

        static void PlaceZones(Transform root)
        {
            var zones = new GameObject("Zones").transform;
            zones.SetParent(root, false);
            ZoneArea Z(string name, Vector2 at, float r, int prio)
            {
                var go = new GameObject("Zone_" + name);
                go.transform.SetParent(zones, false);
                go.transform.position = at;
                var z = go.AddComponent<ZoneArea>();
                z.zoneName = name;
                z.radius = r;
                z.priority = prio;
                return z;
            }
            Z("Làng Lá Xanh", Village, 11.5f, 2);
            var forest = Z("Rừng Thì Thầm", new Vector2(ForestW / 2f + 1f, LowH / 2f), 0f, 0);
            forest.size = new Vector2(ForestW + 2f, LowH);
            forest.ambience = "amb_forest";
            Z("Rừng Già Cổ Thụ", Arena, 13f, 3);
            var swamp = Z("Đầm Lầy Sương Mù", new Vector2((ForestW + 2f + W) / 2f, LowH / 2f), 0f, 0);
            swamp.size = new Vector2(W - ForestW - 2f, LowH);
            swamp.music = "music_swamp";
            swamp.ambience = "amb_swamp";
            swamp.tint = new Color(0.8f, 0.9f, 0.84f);
            swamp.mist = 0.85f;
            var outpost = Z("Trạm Nhà Sàn", Outpost, 7.5f, 2);
            outpost.music = "";
            var mud = Z("Bãi Bùn", MudField, 7f, 2);
            mud.music = "";
            var pond = Z("Ao Cóc Tía", ToadPond, 10f, 3);
            pond.music = "music_swamp";
            var lair = Z("Đầm Xà Mẫu", SnakeLair, 13.5f, 3);
            lair.music = "music_swamp";
            // Hang Pha Lê: under the rock, its own dim blue light whatever the hour
            var cave = Z("Hang Pha Lê", new Vector2((ForestW + W) / 2f, (LowH + H) / 2f), 0f, 0);
            cave.size = new Vector2(W - ForestW, H - LowH);
            cave.music = "music_cave";
            cave.ambience = "amb_cave";
            cave.tint = new Color(0.55f, 0.62f, 0.95f);
            cave.underground = 1f;
            foreach (var (name, at, r, prio) in new[] { ("Cửa Hang", MinersCamp, 8f, 2), ("Hang Dơi", BatCave, 9.5f, 2), ("Mỏ Bỏ Hoang", Mine, 10.5f, 2),
                                                         ("Rừng Pha Lê", CrystalForest, 12.5f, 2), ("Tổ Nhện", SpiderNest, 9f, 2),
                                                         ("Điện Pha Lê", CrystalHall, 9.5f, 3), ("Hang Nhện Chúa", QueenHall, 12f, 3) })
                Z(name, at, r, prio).music = "";   // the cave's music goes on (a boss brings its own)
        }
    }
}
