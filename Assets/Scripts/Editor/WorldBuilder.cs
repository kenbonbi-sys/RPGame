using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.EditorTools
{
    /// <summary>
    /// Generates the demo map "Rừng Thì Thầm": village (Làng Lá Xanh), forest paths with enemy camps
    /// and the boss arena (Rừng Già Cổ Thụ). Tiles are painted into Tilemaps, props are prefab
    /// instances — everything can be edited by hand afterwards.
    /// </summary>
    public static class WorldBuilder
    {
        public const int W = 100, H = 64;
        const string TileFolder = "Assets/Tiles";

        public class Result
        {
            public Tilemap ground, tall, dirt, details;
            public Transform props;
            public Transform playerSpawn, chief, girl, forestSpot, bossSpot;
            public GameObject player;
            public BossBear boss;
        }

        // key locations
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
            PlaceForest(res);
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

        static bool IsDirt(Vector2 p)
        {
            float wob = (Noise(p.x, p.y, 0.35f) - 0.5f) * 1.2f;
            if (Vector2.Distance(p, Village) < 7.2f + wob) return true;
            if (Vector2.Distance(p, Arena) < 7f + wob) return true;
            if (DistToPath(p, MainPath) < 1.55f + wob * 0.5f) return true;
            if (DistToPath(p, SidePath) < 1.2f + wob * 0.5f) return true;
            if (DistToPath(p, NorthPath) < 1.05f + wob * 0.4f) return true;
            for (int i = 1; i < Camps.Length; i++)
                if (Vector2.Distance(p, Camps[i]) < CampRadius[i] * 0.65f + wob) return true;
            return false;
        }

        static float DirtDistance(Vector2 p)
        {
            float d = Mathf.Min(DistToPath(p, MainPath) - 1.55f, DistToPath(p, SidePath) - 1.2f, DistToPath(p, NorthPath) - 1.05f);
            d = Mathf.Min(d, Vector2.Distance(p, Village) - 7.2f, Vector2.Distance(p, Arena) - 7f);
            for (int i = 1; i < Camps.Length; i++) d = Mathf.Min(d, Vector2.Distance(p, Camps[i]) - CampRadius[i] * 0.65f);
            return d;
        }

        static bool IsTallGrass(Vector2 p)
        {
            if (p.x < 27) return false;
            if (Vector2.Distance(p, Arena) < 11.5f) return false;
            if (DirtDistance(p) < 1.6f) return false;
            foreach (var c in Camps)
                if (Vector2.Distance(p, c) < 4.5f) return false;
            return Noise(p.x, p.y, 0.11f) > 0.57f;
        }

        static Tile MakeTile(string sprite)
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
            t.colliderType = Tile.ColliderType.None;
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

        static void BuildTilemaps(Transform root, Result res)
        {
            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root, false);
            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            res.ground = MakeMap(gridGo.transform, "Ground", 0);
            res.tall = MakeMap(gridGo.transform, "TallGrass", 1);
            res.dirt = MakeMap(gridGo.transform, "Dirt", 2);
            res.details = MakeMap(gridGo.transform, "Details", 3);

            var grass = Enumerable.Range(0, 8).Select(i => MakeTile($"grass_{i}")).ToArray();
            var tallMs = Enumerable.Range(0, 16).Select(i => i == 0 ? null : MakeTile($"tall_{i}")).ToArray();
            var tallFull = Enumerable.Range(0, 4).Select(i => MakeTile($"tallfull_{i}")).ToArray();
            var dirtMs = Enumerable.Range(0, 16).Select(i => i == 0 ? null : MakeTile($"dirt_{i}")).ToArray();
            var dirtFull = Enumerable.Range(0, 4).Select(i => MakeTile($"dirtfull_{i}")).ToArray();
            string[] detailNames = { "tuft_0", "tuft_1", "tuft_2", "flowers_0", "flowers_1", "flowers_2", "leaves_0", "leaves_1", "pebbles_0", "pebbles_1", "clover_0", "mushrooms_0" };
            var details = detailNames.Select(MakeTile).ToArray();

            var dirtV = new bool[W + 1, H + 1];
            var tallV = new bool[W + 1, H + 1];
            for (int y = 0; y <= H; y++)
                for (int x = 0; x <= W; x++)
                {
                    var p = new Vector2(x, y);
                    dirtV[x, y] = IsDirt(p);
                    tallV[x, y] = !dirtV[x, y] && IsTallGrass(p);
                }

            var gPos = new List<Vector3Int>();
            var gTiles = new List<TileBase>();
            var tPos = new List<Vector3Int>();
            var tTiles = new List<TileBase>();
            var dPos = new List<Vector3Int>();
            var dTiles = new List<TileBase>();
            var xPos = new List<Vector3Int>();
            var xTiles = new List<TileBase>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = new Vector3Int(x, y, 0);
                    gPos.Add(c);
                    gTiles.Add(grass[Pick(grass.Length, x, y)]);
                    int ti = (tallV[x, y + 1] ? 8 : 0) | (tallV[x + 1, y + 1] ? 4 : 0) | (tallV[x + 1, y] ? 2 : 0) | (tallV[x, y] ? 1 : 0);
                    if (ti == 15) { tPos.Add(c); tTiles.Add(tallFull[Pick(tallFull.Length, x, y)]); }
                    else if (ti > 0) { tPos.Add(c); tTiles.Add(tallMs[ti]); }
                    int di = (dirtV[x, y + 1] ? 8 : 0) | (dirtV[x + 1, y + 1] ? 4 : 0) | (dirtV[x + 1, y] ? 2 : 0) | (dirtV[x, y] ? 1 : 0);
                    if (di == 15) { dPos.Add(c); dTiles.Add(dirtFull[Pick(dirtFull.Length, x, y)]); }
                    else if (di > 0) { dPos.Add(c); dTiles.Add(dirtMs[di]); }
                    if (di == 0 && ti == 0 && rnd.NextDouble() < 0.1)
                    {
                        xPos.Add(c);
                        xTiles.Add(details[rnd.Next(details.Length)]);
                    }
                }
            res.ground.SetTiles(gPos.ToArray(), gTiles.ToArray());
            res.tall.SetTiles(tPos.ToArray(), tTiles.ToArray());
            res.dirt.SetTiles(dPos.ToArray(), dTiles.ToArray());
            res.details.SetTiles(xPos.ToArray(), xTiles.ToArray());
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

        static bool Reserved(Vector2 p)
        {
            if (Vector2.Distance(p, Village) < 10.5f) return true;
            if (Vector2.Distance(p, Arena) < 11.8f) return true;
            for (int i = 0; i < Camps.Length; i++)
                if (Vector2.Distance(p, Camps[i]) < CampRadius[i] + 1.2f) return true;
            return false;
        }

        static void PlaceForest(Result res)
        {
            var t = res.props;
            float R() => (float)rnd.NextDouble();
            // dense border
            for (float y = 0.8f; y < H; y += 1.7f)
                for (float x = 0.8f; x < W; x += 1.7f)
                {
                    float edge = Mathf.Min(x, y, W - x, H - y);
                    if (edge > 3.6f) continue;
                    var p = new Vector2(x + (R() - 0.5f) * 0.8f, y + (R() - 0.5f) * 0.8f);
                    if (DirtDistance(p) < 0.8f && edge > 1.5f) continue;
                    Place(Tree(p, 0.8f), p, t);
                    Mark(p);
                }
            // forest fill
            for (int i = 0; i < 9000; i++)
            {
                var p = new Vector2(3 + R() * (W - 6), 3 + R() * (H - 6));
                if (Reserved(p)) continue;
                float dd = DirtDistance(p);
                if (dd < 1.7f) continue;
                float density = p.x < 30 ? 0.3f : 0.95f;
                if (R() > density) continue;
                if (!Free(p, 2.1f)) continue;
                Place(Tree(p, p.x < 30 ? 0.35f : 0.75f), p, t);
                Mark(p);
            }
            // undergrowth
            string[] small = { "bush_big_0", "bush_big_1", "bush_small_0", "bush_small_1", "rock_big_0", "rock_big_1", "rock_small_0",
                               "rock_small_1", "stump", "log", "mushrooms", "mushrooms", "bush_small_0", "bush_small_1" };
            int placed = 0;
            for (int i = 0; i < 6000 && placed < 230; i++)
            {
                var p = new Vector2(3 + R() * (W - 6), 3 + R() * (H - 6));
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

        static string Tree(Vector2 p, float pineChance)
        {
            if (rnd.NextDouble() < pineChance) return $"pine_{rnd.Next(3)}";
            return $"oak_{rnd.Next(2)}";
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

        static void PlaceActors(Transform root, Result res)
        {
            var actors = new GameObject("Actors").transform;
            actors.SetParent(root, false);
            res.playerSpawn = new GameObject("PlayerSpawn").transform;
            res.playerSpawn.SetParent(actors, false);
            res.playerSpawn.position = new Vector3(18.5f, 15.2f, 0);
            res.player = Spawn(PrefabFactory.Player, res.playerSpawn.position, null, "Player");

            res.chief = Spawn(PrefabFactory.Chief, new Vector2(20.4f, 21.1f), actors, "Truong Lang").transform;
            res.girl = Spawn(PrefabFactory.Girl, new Vector2(12.6f, 15.4f), actors, "Be Mai").transform;

            var camps = new GameObject("EnemyCamps").transform;
            camps.SetParent(root, false);
            void Camp(string name, GameObject prefab, Vector2 at, int count, float radius)
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
            }
            Camp("SlimeCamp_Edge", PrefabFactory.Slime, Camps[0], 2, 1.8f);
            Camp("SlimeCamp_Clearing", PrefabFactory.Slime, Camps[1], 4, 2.6f);
            Camp("SlimeCamp_South", PrefabFactory.Slime, Camps[2], 3, 2.2f);
            Camp("ShroomCamp_East", PrefabFactory.Shroom, Camps[3], 3, 2.2f);
            Camp("ShroomCamp_Path", PrefabFactory.Shroom, Camps[4], 2, 1.8f);
            res.forestSpot = camps.Find("SlimeCamp_Clearing");

            var arena = new GameObject("BossArena").transform;
            arena.SetParent(root, false);
            arena.position = Arena;
            res.bossSpot = arena;
            var bear = Spawn(PrefabFactory.Bear, Arena + new Vector2(0, 1.2f), arena, "Gau Ma Rung Gia");
            res.boss = bear.GetComponent<BossBear>();
            res.boss.arenaCenter = arena;
            EditorUtility.SetDirty(res.boss);
        }

        static void PlaceZones(Transform root)
        {
            var zones = new GameObject("Zones").transform;
            zones.SetParent(root, false);
            void Z(string name, Vector2 at, float r, int prio)
            {
                var go = new GameObject("Zone_" + name);
                go.transform.SetParent(zones, false);
                go.transform.position = at;
                var z = go.AddComponent<ZoneArea>();
                z.zoneName = name;
                z.radius = r;
                z.priority = prio;
            }
            Z("Làng Lá Xanh", Village, 11.5f, 2);
            Z("Rừng Thì Thầm", new Vector2(55, 32), 70f, 0);
            Z("Rừng Già Cổ Thụ", Arena, 13f, 3);
        }
    }
}
