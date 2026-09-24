using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RPG.EditorTools
{
    /// <summary>Character, prop and gameplay prefabs.</summary>
    public static class PrefabFactory
    {
        public const string CharFolder = "Assets/Prefabs/Characters";
        public const string PropFolder = "Assets/Prefabs/Props";
        public const string GameplayFolder = "Assets/Prefabs/Gameplay";

        public static readonly Dictionary<string, GameObject> Props = new Dictionary<string, GameObject>();

        public static GameObject Player, NetHero, Chief, Girl, Slime, Shroom, Bear, Boulder, Loot;
        // Đầm Lầy Sương Mù
        public static GameObject Toad, Leech, MudMan, Mudling, ToadKing, Snake, WaterSnake, Dragonfly, Wisp;

        /// <summary>Abilities on Q W E R A S D Space.</summary>
        static readonly string[] DefaultSlots = { "slash", "fireball", "ice", "lightning", "heal", "shield", "bladestorm", "dash" };

        [MenuItem("Tools/RPG/Steps/5. Character + Prop Prefabs", priority = 105)]
        public static void BuildAll()
        {
            EditorUtil.EnsureFolder(CharFolder);
            EditorUtil.EnsureFolder(PropFolder);
            EditorUtil.EnsureFolder(GameplayFolder);
            Player = BuildPlayer();
            Chief = BuildNPC("Chief", "chief", "Trưởng Làng", "chief_idle", "chief_talk", "npc_chief_idle_0");
            Girl = BuildNPC("Girl", "girl", "Bé Mai", "girl_idle", "girl_idle", "npc_girl_idle_0");
            Slime = BuildSlime();
            Shroom = BuildShroom();
            Bear = BuildBear();
            Toad = BuildToad();
            Leech = BuildLeech();
            MudMan = BuildMudMan();
            Mudling = BuildMudling();
            ToadKing = BuildToadKing();
            Snake = BuildSnake();
            WaterSnake = BuildWaterSnake();
            Dragonfly = BuildDragonfly();
            Wisp = BuildWisp();
            Boulder = BuildBoulder();
            Loot = BuildLoot();
            BuildProps();
            UpgradePrefabs();
            var db = AssetFactory.Database;
            EditorUtil.Assign(ref db.boulderPrefab, Boulder);
            EditorUtil.Assign(ref db.lootPrefab, Loot);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            BuildOnline();
        }

        /// <summary>
        /// Online phase 1 (Docs/KeHoach-Online.md): the NetHero prefab, FishNet's list of network
        /// prefabs, the NetworkManager prefab, and the GameDatabase links to them.
        /// </summary>
        public static void BuildOnline()
        {
            NetHero = BuildNetHero();
            // FishNet's import hook lists every NetworkObject prefab in DefaultPrefabObjects
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var prefabs = AssetDatabase.LoadAssetAtPath<FishNet.Managing.Object.DefaultPrefabObjects>(NetworkPrefabsPath);
            var nob = NetHero != null ? NetHero.GetComponent<FishNet.Object.NetworkObject>() : null;
            if (prefabs != null && nob != null && !prefabs.Prefabs.Contains(nob))
            {
                Debug.LogWarning("[RPG] FishNet did not list NetHero in DefaultPrefabObjects; adding it");
                prefabs.AddObject(nob, true);
                EditorUtility.SetDirty(prefabs);
            }
            var db = AssetFactory.Database;
            EditorUtil.Assign(ref db.netHeroPrefab, NetHero);
            EditorUtil.Assign(ref db.networkManagerPrefab, BuildNetworkManager(prefabs));
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Where FishNet keeps the prefabs the network may spawn.</summary>
        public const string NetworkPrefabsPath = "Assets/DefaultPrefabObjects.asset";
        public const string NetFolder = "Assets/Prefabs/Net";

        /// <summary>
        /// FishNet's NetworkManager, its UDP transport (Tugboat) and the list of network prefabs,
        /// instantiated when an online session starts. A prefab rather than components added at
        /// runtime: the manager must already know its prefab list when it wakes up.
        /// </summary>
        static GameObject BuildNetworkManager(FishNet.Managing.Object.PrefabObjects prefabs)
        {
            string path = $"{NetFolder}/NetworkManager.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (EditorUtil.Keep(existing)) return existing;
            var go = new GameObject("NetworkManager");
            var tugboat = go.AddComponent<FishNet.Transporting.Tugboat.Tugboat>();
            go.AddComponent<FishNet.Managing.Transporting.TransportManager>().Transport = tugboat;
            go.AddComponent<FishNet.Managing.NetworkManager>().SpawnablePrefabs = prefabs;
            return EditorUtil.SavePrefab(go, path);
        }

        /// <summary>
        /// The hero an online session spawns for every player: a variant of the Player prefab with a
        /// NetworkObject (global, so it outlives zone changes), a NetworkTransform (its owner sends
        /// where it walks; every other copy turns kinematic and follows, and long jumps snap) and a
        /// NetworkHero.
        /// </summary>
        static GameObject BuildNetHero()
        {
            string path = $"{CharFolder}/NetHero.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (EditorUtil.Keep(existing)) return existing;
            var basePrefab = Player != null ? Player : AssetDatabase.LoadAssetAtPath<GameObject>($"{CharFolder}/Player.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            var nob = go.AddComponent<FishNet.Object.NetworkObject>();
            var nobSo = new SerializedObject(nob);
            nobSo.FindProperty("_isGlobal").boolValue = true;
            nobSo.ApplyModifiedPropertiesWithoutUndo();
            var nt = go.AddComponent<FishNet.Component.Transforming.NetworkTransform>();
            var ntSo = new SerializedObject(nt);
            ntSo.FindProperty("_clientAuthoritative").boolValue = true;
            ntSo.FindProperty("_synchronizeRotation").boolValue = false;
            ntSo.FindProperty("_synchronizeScale").boolValue = false;
            ntSo.FindProperty("_componentConfiguration").intValue = (int)FishNet.Component.Transforming.NetworkTransform.ComponentConfigurationType.Rigidbody2D;
            ntSo.FindProperty("_enableTeleport").boolValue = true;
            ntSo.FindProperty("_teleportThreshold").floatValue = 2f;   // a dash moves under 1 unit per tick; cheats and zone entries jump
            ntSo.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<NetworkHero>();
            go.name = "NetHero";
            EditorUtil.Written++;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        /// <summary>Adds components introduced after the prefabs were first generated, keeping hand edits.</summary>
        static void UpgradePrefabs()
        {
            EditorUtil.UpgradePrefab($"{CharFolder}/Player.prefab", root =>
            {
                var pc = root.GetComponent<PlayerController>();
                if (pc == null) return false;
                bool changed = false;
                if (root.GetComponent<PlayerStats>() == null)
                {
                    pc.stats = root.AddComponent<PlayerStats>();
                    changed = true;
                }
                // Đá Truyền Tống the hero has woken (T49)
                if (root.GetComponent<WaystoneLog>() == null)
                {
                    pc.waystones = root.AddComponent<WaystoneLog>();
                    changed = true;
                }
                // prefabs from before Ability System v2 lost their (SkillDef) slots: fill them with the ported abilities
                var skills = root.GetComponent<PlayerSkills>();
                if (skills != null && System.Array.TrueForAll(skills.slots, s => s == null))
                {
                    var db = AssetFactory.Database;
                    for (int i = 0; i < skills.slots.Length && i < DefaultSlots.Length; i++) skills.slots[i] = db.Ability(DefaultSlots[i]);
                    changed = true;
                }
                return changed;
            });
            void YarnNode(string file, string node) => EditorUtil.UpgradePrefab($"{CharFolder}/{file}.prefab", root =>
            {
                var npc = root.GetComponent<NPC>();
                if (npc == null || !string.IsNullOrEmpty(npc.yarnNode)) return false;
                npc.yarnNode = node;
                return true;
            });
            YarnNode("Chief", "Chief");
            YarnNode("Girl", "Mai");
            // characters get the dissolve / outline sprite (T22)
            foreach (var file in new[] { "Slime", "Shroom", "BossBear", "Chief", "Girl" })
            {
                EditorUtil.UpgradePrefab($"{CharFolder}/{file}.prefab", root =>
                {
                    if (root.GetComponentInChildren<SpriteStyle>(true) != null) return false;
                    var body = root.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r => r.name == "Body");
                    var fx = AssetFactory.SpriteLitFX;
                    if (body == null || fx == null) return false;
                    body.sharedMaterial = fx;
                    var style = root.AddComponent<SpriteStyle>();
                    style.target = body;
                    var enemy = root.GetComponent<EnemyBase>();
                    if (enemy != null) enemy.style = style;
                    var boss = root.GetComponent<BossBear>();
                    if (boss != null) boss.style = style;
                    return true;
                });
            }
            EditorUtil.UpgradePrefab($"{CharFolder}/BossBear.prefab", root =>
            {
                var boss = root.GetComponent<BossBear>();
                if (boss == null || root.GetComponent<Poise>() != null) return false;
                boss.poise = root.AddComponent<Poise>();
                boss.poise.maxPoise = 300f;
                return true;
            });
        }

        /// <summary>Loads the generated prefabs from disk (used when only the scene is rebuilt).</summary>
        public static void LoadExisting()
        {
            GameObject L(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p + ".prefab");
            Player = L($"{CharFolder}/Player");
            NetHero = L($"{CharFolder}/NetHero");
            Chief = L($"{CharFolder}/Chief");
            Girl = L($"{CharFolder}/Girl");
            Slime = L($"{CharFolder}/Slime");
            Shroom = L($"{CharFolder}/Shroom");
            Bear = L($"{CharFolder}/BossBear");
            Toad = L($"{CharFolder}/Toad");
            Leech = L($"{CharFolder}/Leech");
            MudMan = L($"{CharFolder}/MudMan");
            Mudling = L($"{CharFolder}/Mudling");
            ToadKing = L($"{CharFolder}/BossToadKing");
            Snake = L($"{CharFolder}/BossSnakeMother");
            WaterSnake = L($"{CharFolder}/WaterSnake");
            Dragonfly = L($"{CharFolder}/Dragonfly");
            Wisp = L($"{CharFolder}/Wisp");
            Boulder = L($"{GameplayFolder}/TangDaLon");
            Loot = L($"{GameplayFolder}/Loot");
            Props.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropFolder }))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) Props[p.name] = p;
            }
        }

        // ================================================================== helpers
        static SpriteRenderer Sprite(GameObject parent, string name, string sprite, Material mat, string layer = SortingLayerNames.Default, int order = 0)
        {
            var go = EditorUtil.Child(parent, name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = string.IsNullOrEmpty(sprite) ? null : ArtImporter.S(sprite);
            sr.sharedMaterial = mat;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer Shadow(GameObject parent, float width, float y = 0.02f)
        {
            var sr = Sprite(parent, "Shadow", "shadow", AssetFactory.SpriteUnlit, SortingLayerNames.Default, -10);
            sr.transform.localPosition = new Vector3(0, y, 0);
            sr.transform.localScale = new Vector3(width / 2f, width / 2f, 1f); // shadow sprite is 2 units wide
            return sr;
        }

        static HitFlash Flash(SpriteRenderer body)
        {
            var o = Sprite(body.gameObject, "Flash", null, AssetFactory.Silhouette, body.sortingLayerName, body.sortingOrder + 1);
            o.enabled = false;
            var f = body.gameObject.AddComponent<HitFlash>();
            f.source = body;
            f.overlay = o;
            return f;
        }

        static Transform Head(GameObject parent, float y)
        {
            return EditorUtil.Child(parent, "Head", new Vector3(0, y, 0)).transform;
        }

        static Rigidbody2D Body(GameObject go, float mass)
        {
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.mass = mass;
            rb.freezeRotation = true;
            rb.linearDamping = 0f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            return rb;
        }

        static Light2D PointLight(GameObject parent, Color c, float radius, float intensity, Vector3 pos)
        {
            var go = EditorUtil.Child(parent, "Light", pos);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.1f;
            l.falloffIntensity = 0.6f;
            l.shadowsEnabled = false;
            l.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
            return l;
        }

        static SpriteAnimator Animator(SpriteRenderer sr, string set, string start)
        {
            var a = sr.gameObject.AddComponent<SpriteAnimator>();
            a.set = ArtImporter.AnimSet(set);
            a.target = sr;
            a.startClip = start;
            return a;
        }

        static void Group(GameObject go)
        {
            var g = go.AddComponent<SortingGroup>();
            g.sortingLayerName = SortingLayerNames.Default;
        }

        // ================================================================== characters
        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            root.layer = Layers.Player;
            Group(root);
            Body(root, 1f);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.26f;
            col.offset = new Vector2(0, 0.22f);
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = 4.6f;
            motor.acceleration = 60f;
            var health = root.AddComponent<Health>();
            health.team = Team.Player;
            health.displayName = "Anh Hùng";
            health.maxHp = 149;
            health.hp = 149;
            var status = root.AddComponent<StatusEffects>();
            Shadow(root, 0.9f);
            var body = Sprite(root, "Body", "player_idle_down_0", AssetFactory.SpriteLit);
            var anim = Animator(body, "player", "idle_down");
            var flash = Flash(body);
            health.head = Head(root, 1.55f);
            var ghost = root.AddComponent<AfterImageSpawner>();
            ghost.source = body;
            var skills = root.AddComponent<PlayerSkills>();
            var db = AssetFactory.Database;
            for (int i = 0; i < 8; i++) skills.slots[i] = db.Ability(DefaultSlots[i]);
            var pc = root.AddComponent<PlayerController>();
            pc.motor = motor;
            pc.anim = anim;
            pc.health = health;
            pc.status = status;
            pc.flash = flash;
            pc.afterImages = ghost;
            pc.body = body;
            pc.skills = skills;
            pc.perfectDodge = root.AddComponent<PerfectDodge>();
            pc.energy = 50;
            pc.maxEnergy = 63;
            // the hero carries its own bag (the prefab's is a new character's kit), quest log and bestiary
            pc.inventory = root.AddComponent<Inventory>();
            StartingKit(pc.inventory);
            pc.quests = root.AddComponent<QuestSystem>();
            pc.bestiary = root.AddComponent<Bestiary>();
            // a soft personal light so the hero is readable at night
            var l = PointLight(root, new Color(1f, 0.85f, 0.65f), 4.5f, 0.4f, new Vector3(0, 0.6f, 0));
            var nl = l.gameObject.AddComponent<NightLight>();
            nl.target = l;
            nl.dayIntensity = 0f;
            nl.nightIntensity = 0.75f;
            nl.flicker = 0.03f;
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Player.prefab");
        }

        /// <summary>What a new character starts with: 25 gold, potions, a sword and apples.</summary>
        public static void StartingKit(Inventory bag)
        {
            var db = AssetFactory.Database;
            bag.gold = 25;
            bag.stacks.Clear();
            bag.stacks.Add(new Inventory.Stack { item = db.Item("potion_red"), count = 4 });
            bag.stacks.Add(new Inventory.Stack { item = db.Item("potion_blue"), count = 4 });
            bag.stacks.Add(new Inventory.Stack { item = db.Item("potion_green"), count = 2 });
            bag.stacks.Add(new Inventory.Stack { item = db.Item("sword"), count = 1 });
            bag.stacks.Add(new Inventory.Stack { item = db.Item("apple"), count = 3 });
        }

        static GameObject BuildNPC(string file, string id, string display, string idle, string talk, string portraitSprite)
        {
            var root = new GameObject(file);
            root.layer = Layers.NPC;
            Group(root);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
            col.offset = new Vector2(0, 0.25f);
            Shadow(root, 0.9f);
            var body = Sprite(root, "Body", portraitSprite, AssetFactory.SpriteLit);
            var anim = Animator(body, "npc", idle);
            var marker = Sprite(root, "QuestMarker", "quest_excl", AssetFactory.SpriteUnlit, SortingLayerNames.Top, 5);
            marker.transform.localPosition = new Vector3(0, 2.5f, 0);
            marker.transform.localScale = Vector3.one * 1.3f;
            var bob = marker.gameObject.AddComponent<Bobber>();
            bob.amplitude = 0.12f;
            bob.speed = 3.5f;
            var glow = Sprite(marker.gameObject, "Glow", "glow", AssetFactory.Additive, SortingLayerNames.Top, 4);
            glow.color = new Color(1f, 0.85f, 0.3f, 0.45f);
            glow.transform.localScale = Vector3.one * 0.9f;
            var npc = root.AddComponent<NPC>();
            npc.npcId = id;
            npc.displayName = display;
            npc.anim = anim;
            npc.body = body;
            npc.questMarker = marker;
            npc.idleClip = idle;
            npc.talkClip = talk;
            npc.portrait = ArtImporter.S(portraitSprite);
            return EditorUtil.SavePrefab(root, $"{CharFolder}/{file}.prefab");
        }

        static void EnemyCommon(GameObject root, EnemyBase e, SpriteRenderer body, string set, float headY, float hp)
        {
            var motor = root.GetComponent<CharacterMotor>();
            var health = root.GetComponent<Health>();
            health.team = Team.Enemy;
            health.maxHp = hp;
            health.hp = hp;
            health.head = Head(root, headY);
            e.motor = motor;
            e.health = health;
            e.status = root.GetComponent<StatusEffects>();
            e.body = body;
            e.anim = Animator(body, set, "idle");
            e.flash = Flash(body);
            e.maxHp = hp;
        }

        static GameObject BuildSlime()
        {
            var root = new GameObject("Slime");
            root.layer = Layers.Enemy;
            Group(root);
            Body(root, 0.8f);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.32f;
            col.offset = new Vector2(0, 0.25f);
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = 2.6f;
            root.AddComponent<Health>();
            root.AddComponent<StatusEffects>();
            Shadow(root, 1f);
            var body = Sprite(root, "Body", "slime_idle_0", AssetFactory.SpriteLit);
            var ai = root.AddComponent<SlimeAI>();
            EnemyCommon(root, ai, body, "slime", 1.05f, 60f);
            ai.enemyId = "slime";
            ai.displayName = "Slime Rêu";
            ai.level = 2;
            ai.contactDamage = 6f;
            ai.attackRange = 1.2f;
            ai.loot = new List<LootEntry>
            {
                new LootEntry { itemId = "gel", chance = 0.6f, min = 1, max = 1 },
                new LootEntry { itemId = "coin", chance = 0.8f, min = 1, max = 3 },
                new LootEntry { itemId = "potion_red", chance = 0.08f, min = 1, max = 1 },
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Slime.prefab");
        }

        static GameObject BuildShroom()
        {
            var root = new GameObject("Shroom");
            root.layer = Layers.Enemy;
            Group(root);
            Body(root, 0.8f);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
            col.offset = new Vector2(0, 0.25f);
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = 1.9f;
            root.AddComponent<Health>();
            root.AddComponent<StatusEffects>();
            Shadow(root, 0.9f);
            var body = Sprite(root, "Body", "shroom_idle_0", AssetFactory.SpriteLit);
            var ai = root.AddComponent<ShroomAI>();
            EnemyCommon(root, ai, body, "shroom", 1.35f, 48f);
            root.GetComponent<Health>().resistances.fire = -0.3f;   // weak to fire: the damage example of plan §04
            ai.enemyId = "shroom";
            ai.displayName = "Nấm Độc";
            ai.level = 3;
            ai.contactDamage = 0f;
            ai.attackRange = 6f;
            ai.attackCooldown = 2.2f;
            ai.aggroRange = 7.5f;
            ai.loot = new List<LootEntry>
            {
                new LootEntry { itemId = "shroom_cap", chance = 0.75f, min = 1, max = 1 },
                new LootEntry { itemId = "coin", chance = 0.8f, min = 1, max = 3 },
                new LootEntry { itemId = "potion_blue", chance = 0.1f, min = 1, max = 1 },
                new LootEntry { itemId = "herb", chance = 0.25f, min = 1, max = 1 },
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Shroom.prefab");
        }

        static GameObject BuildBear()
        {
            var root = new GameObject("BossBear");
            root.layer = Layers.Enemy;
            Group(root);
            var rb = Body(root, 40f);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 1.05f;
            col.offset = new Vector2(0, 0.75f);
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = 2.3f;
            motor.knockbackResist = 0.1f;
            var health = root.AddComponent<Health>();
            health.team = Team.Enemy;
            health.maxHp = 3200;
            health.hp = 3200;
            var status = root.AddComponent<StatusEffects>();
            status.stunResist = 0.5f;
            Shadow(root, 3.2f, 0.05f);
            var bodyRoot = EditorUtil.Child(root, "BodyRoot");
            var body = Sprite(bodyRoot, "Body", "bear_idle_0", AssetFactory.SpriteLit);
            var anim = Animator(body, "bear", "idle");
            var flash = Flash(body);
            health.head = Head(root, 3.9f);
            var eyes = PointLight(bodyRoot, new Color(1f, 0.55f, 0.2f), 1.6f, 0.9f, new Vector3(0, 2.75f, 0));
            var nl = eyes.gameObject.AddComponent<NightLight>();
            nl.target = eyes;
            nl.dayIntensity = 0.35f;
            nl.nightIntensity = 1.2f;
            nl.flicker = 0.15f;
            var ghost = root.AddComponent<AfterImageSpawner>();
            ghost.source = body;
            var boss = root.AddComponent<BossBear>();
            boss.motor = motor;
            boss.anim = anim;
            boss.health = health;
            boss.status = status;
            boss.flash = flash;
            boss.bodyRoot = bodyRoot.transform;
            boss.body = body;
            boss.afterImages = ghost;
            boss.loot = new List<LootEntry>
            {
                new LootEntry { itemId = "claw", chance = 1f, min = 1, max = 1 },
                new LootEntry { itemId = "pelt", chance = 1f, min = 1, max = 1 },
                new LootEntry { itemId = "gem_red", chance = 0.7f, min = 1, max = 1 },
                new LootEntry { itemId = "gem_blue", chance = 0.7f, min = 1, max = 1 },
                new LootEntry { itemId = "potion_red", chance = 1f, min = 2, max = 2 },
                new LootEntry { itemId = "meat", chance = 1f, min = 1, max = 2 },
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/BossBear.prefab");
        }

        // ================================================================== Đầm Lầy Sương Mù
        /// <summary>The body gets the dissolve / outline material and a SpriteStyle (T22).</summary>
        static SpriteStyle Style(GameObject root, SpriteRenderer body)
        {
            var fx = AssetFactory.SpriteLitFX;
            if (fx != null) body.sharedMaterial = fx;
            var style = root.AddComponent<SpriteStyle>();
            style.target = body;
            return style;
        }

        static LootEntry Drop(string item, float chance, int min = 1, int max = 1) =>
            new LootEntry { itemId = item, chance = chance, min = min, max = max };

        /// <summary>The frame of every swamp creature: body, collider, motor, health, statuses.</summary>
        static (GameObject root, SpriteRenderer body) Creature(string name, string firstFrame, float mass, float radius, float colliderY,
                                                             float speed, bool swims, float shadow)
        {
            var root = new GameObject(name);
            root.layer = Layers.Enemy;
            Group(root);
            Body(root, mass);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = radius;
            col.offset = new Vector2(0, colliderY);
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = speed;
            motor.slowedByWater = !swims;
            root.AddComponent<Health>();
            root.AddComponent<StatusEffects>();
            if (shadow > 0f) Shadow(root, shadow);
            var body = Sprite(root, "Body", firstFrame, AssetFactory.SpriteLit);
            return (root, body);
        }

        static GameObject BuildToad()
        {
            var (root, body) = Creature("Toad", "toad_idle_0", 0.8f, 0.32f, 0.22f, 2.4f, true, 1f);
            var ai = root.AddComponent<ToadAI>();
            EnemyCommon(root, ai, body, "toad", 1.05f, 150f);
            root.GetComponent<Health>().resistances.poison = 0.5f;
            ai.style = Style(root, body);
            ai.enemyId = "toad";
            ai.displayName = "Cóc Độc";
            ai.level = 8;
            ai.contactDamage = 0f;
            ai.attackRange = 1.1f;
            ai.attackCooldown = 1.8f;
            ai.aggroRange = 6.5f;
            ai.leashRange = 14f;
            ai.loot = new List<LootEntry>
            {
                Drop("toad_skin", 0.55f), Drop("poison_gland", 0.22f), Drop("coin", 0.85f, 2, 5), Drop("lotus", 0.06f), Drop("potion_green", 0.04f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Toad.prefab");
        }

        static GameObject BuildLeech()
        {
            var (root, body) = Creature("Leech", "leech_idle_0", 0.5f, 0.28f, 0.15f, 3.2f, true, 0f);
            var ai = root.AddComponent<LeechAI>();
            EnemyCommon(root, ai, body, "leech", 0.8f, 120f);
            ai.style = Style(root, body);
            ai.enemyId = "leech";
            ai.displayName = "Đỉa Bùn";
            ai.level = 9;
            ai.contactDamage = 0f;
            ai.attackRange = 1f;
            ai.attackCooldown = 2.2f;
            ai.aggroRange = 4.5f;
            ai.leashRange = 7f;
            ai.wanderRadius = 1.2f;
            ai.loot = new List<LootEntry>
            {
                Drop("leech_tooth", 0.5f), Drop("coin", 0.8f, 1, 4), Drop("potion_red", 0.1f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Leech.prefab");
        }

        static GameObject BuildMudMan()
        {
            var (root, body) = Creature("MudMan", "mudman_idle_0", 3f, 0.45f, 0.35f, 1.5f, false, 1.4f);
            root.GetComponent<CharacterMotor>().knockbackResist = 0.6f;
            var ai = root.AddComponent<MudManAI>();
            EnemyCommon(root, ai, body, "mudman", 2.1f, 420f);
            var h = root.GetComponent<Health>();
            h.resistances.physical = 0.15f;
            h.resistances.fire = -0.2f;   // the mud dries and cracks
            ai.style = Style(root, body);
            ai.enemyId = "mudman";
            ai.displayName = "Người Bùn";
            ai.level = 11;
            ai.contactDamage = 6f;
            ai.attackCooldown = 2.6f;
            ai.aggroRange = 6f;
            ai.leashRange = 12f;
            ai.loot = new List<LootEntry>
            {
                Drop("mud_core", 0.45f), Drop("coin", 0.9f, 3, 7), Drop("herb", 0.2f), Drop("potion_red", 0.12f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/MudMan.prefab");
        }

        static GameObject BuildMudling()
        {
            var (root, body) = Creature("Mudling", "mudling_idle_0", 0.7f, 0.3f, 0.22f, 2.8f, false, 0.9f);
            var ai = root.AddComponent<SlimeAI>();
            EnemyCommon(root, ai, body, "mudling", 1f, 70f);
            ai.style = Style(root, body);
            ai.enemyId = "mudling";
            ai.displayName = "Bùn Con";
            ai.level = 9;
            ai.contactDamage = 5f;
            ai.attackRange = 1.2f;
            ai.lungeDamage = 12f;
            ai.aggroRange = 7f;
            ai.loot = new List<LootEntry> { Drop("coin", 0.5f, 1, 2) };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Mudling.prefab");
        }

        static GameObject BuildWaterSnake()
        {
            // no shadow: it lives in the water
            var (root, body) = Creature("WaterSnake", "watersnake_idle_0", 0.7f, 0.34f, 0.2f, 3.4f, true, 0f);
            var ai = root.AddComponent<WaterSnakeAI>();
            EnemyCommon(root, ai, body, "watersnake", 1f, 110f);
            root.GetComponent<Health>().resistances.poison = 0.5f;
            ai.style = Style(root, body);
            ai.enemyId = "watersnake";
            ai.displayName = "Rắn Nước";
            ai.level = 10;
            ai.contactDamage = 0f;
            ai.attackRange = 1f;
            ai.attackCooldown = 2.6f;
            ai.aggroRange = 5.5f;
            ai.leashRange = 9f;
            ai.wanderRadius = 1.2f;
            ai.loot = new List<LootEntry>
            {
                Drop("wsnake_skin", 0.5f), Drop("venom_sac", 0.08f), Drop("coin", 0.85f, 2, 5), Drop("potion_green", 0.05f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/WaterSnake.prefab");
        }

        /// <summary>
        /// A flying or floating creature: its body is a trigger (hit by attacks, in nobody's way,
        /// over water and reeds) and its sprite hangs above the ground on a bobbing child.
        /// </summary>
        static (GameObject root, SpriteRenderer body, GameObject air) Flyer(string name, string firstFrame, float mass, float radius, float colliderY,
                                                                          float speed, float height, float bob, float bobSpeed, float shadow, Material mat)
        {
            var root = new GameObject(name);
            root.layer = Layers.Enemy;
            Group(root);
            Body(root, mass);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = radius;
            col.offset = new Vector2(0, colliderY);
            col.isTrigger = true;
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = speed;
            motor.slowedByWater = false;
            root.AddComponent<Health>();
            root.AddComponent<StatusEffects>();
            if (shadow > 0f) Shadow(root, shadow);
            var air = EditorUtil.Child(root, "Air", new Vector3(0, height, 0));
            var b = air.AddComponent<Bobber>();
            b.amplitude = bob;
            b.speed = bobSpeed;
            var body = Sprite(air, "Body", firstFrame, mat);
            return (root, body, air);
        }

        static GameObject BuildDragonfly()
        {
            var (root, body, air) = Flyer("Dragonfly", "dragonfly_idle_0", 0.3f, 0.42f, 0.75f, 4.2f, 0.55f, 0.1f, 6f, 0.6f, AssetFactory.SpriteLit);
            root.GetComponent<CharacterMotor>().acceleration = 60f;
            var ai = root.AddComponent<DragonflyAI>();
            EnemyCommon(root, ai, body, "dragonfly", 1.7f, 80f);
            ai.flight = air.transform;
            ai.style = Style(root, body);
            ai.enemyId = "dragonfly";
            ai.displayName = "Chuồn Chuồn Kim";
            ai.level = 9;
            ai.contactDamage = 0f;
            ai.attackCooldown = 2.2f;
            ai.aggroRange = 7f;
            ai.leashRange = 14f;
            ai.wanderRadius = 3f;
            ai.loot = new List<LootEntry>
            {
                Drop("dragonfly_wing", 0.55f), Drop("coin", 0.8f, 2, 4), Drop("potion_blue", 0.06f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Dragonfly.prefab");
        }

        static GameObject BuildWisp()
        {
            // unlit: a light of its own in the dark swamp night
            var (root, body, air) = Flyer("Wisp", "wisp_idle_0", 0.2f, 0.4f, 0.8f, 3.6f, 0.25f, 0.12f, 2.4f, 0f, AssetFactory.SpriteUnlit);
            air.GetComponent<Bobber>().pulse = 0.03f;
            root.GetComponent<CharacterMotor>().knockbackResist = 0.5f;
            var ai = root.AddComponent<WispAI>();
            EnemyCommon(root, ai, body, "wisp", 1.9f, 70f);
            var h = root.GetComponent<Health>();
            h.resistances.physical = 0.25f;   // a flame: blades pass half through it
            h.resistances.ice = 0.5f;
            h.resistances.holy = -0.3f;
            ai.glow = PointLight(air, new Color(0.45f, 0.85f, 1f), 3.2f, 1.2f, new Vector3(0, 0.6f, 0));
            var nl = ai.glow.gameObject.AddComponent<NightLight>();
            nl.target = ai.glow;
            nl.dayIntensity = 0.5f;
            nl.nightIntensity = 1.3f;
            nl.flicker = 0.25f;
            nl.flickerSpeed = 5f;
            ai.enemyId = "wisp";
            ai.displayName = "Ma Trơi";
            ai.level = 11;
            ai.contactDamage = 0f;
            ai.attackCooldown = 1.5f;
            ai.aggroRange = 7f;
            ai.leashRange = 13f;
            ai.wanderRadius = 2.5f;
            ai.loot = new List<LootEntry>
            {
                Drop("wisp_essence", 0.6f), Drop("coin", 0.9f, 3, 6), Drop("potion_blue", 0.1f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Wisp.prefab");
        }

        /// <summary>The frame every boss shares (the bear's own builder predates it).</summary>
        static (GameObject root, T boss) Boss<T>(string name, string set, float mass, float radius, float colliderY, float speed, bool swims,
                                                 float hp, float shadow, float headY) where T : BossBase
        {
            var root = new GameObject(name);
            root.layer = Layers.Enemy;
            Group(root);
            Body(root, mass);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = radius;
            col.offset = new Vector2(0, colliderY);
            var motor = root.AddComponent<CharacterMotor>();
            motor.moveSpeed = speed;
            motor.knockbackResist = 0.1f;
            motor.slowedByWater = !swims;
            var health = root.AddComponent<Health>();
            health.team = Team.Enemy;
            health.maxHp = hp;
            health.hp = hp;
            var status = root.AddComponent<StatusEffects>();
            status.stunResist = 0.5f;
            if (shadow > 0f) Shadow(root, shadow, 0.05f);
            var bodyRoot = EditorUtil.Child(root, "BodyRoot");
            var body = Sprite(bodyRoot, "Body", set + "_idle_0", AssetFactory.SpriteLit);
            var anim = Animator(body, set, "idle");
            var flash = Flash(body);
            health.head = Head(root, headY);
            var ghost = root.AddComponent<AfterImageSpawner>();
            ghost.source = body;
            var boss = root.AddComponent<T>();
            boss.motor = motor;
            boss.anim = anim;
            boss.health = health;
            boss.status = status;
            boss.flash = flash;
            boss.bodyRoot = bodyRoot.transform;
            boss.body = body;
            boss.afterImages = ghost;
            boss.maxHp = hp;
            boss.walkSpeed = speed;
            boss.style = Style(root, body);
            boss.poise = root.AddComponent<Poise>();
            return (root, boss);
        }

        static GameObject BuildToadKing()
        {
            var (root, boss) = Boss<BossToadKing>("BossToadKing", "toadking", 25f, 0.95f, 0.6f, 2f, true, 2400f, 2.6f, 3.1f);
            var crown = PointLight(boss.bodyRoot.gameObject, new Color(1f, 0.8f, 0.4f), 1.4f, 0.5f, new Vector3(0, 2.7f, 0));
            var nl = crown.gameObject.AddComponent<NightLight>();
            nl.target = crown;
            nl.dayIntensity = 0.2f;
            nl.nightIntensity = 0.8f;
            nl.flicker = 0.1f;
            root.GetComponent<Health>().resistances.poison = 0.6f;
            boss.poise.maxPoise = 220f;
            boss.bossId = "toadking";
            boss.displayName = "Cóc Tía";
            boss.title = "Chúa Ao Độc";
            boss.level = 11;
            boss.rank = EnemyRank.MiniBoss;
            boss.homeName = "Ao Cóc Tía";
            boss.arenaRadius = 8f;
            boss.wakeRadius = 6f;
            boss.coins = 8;
            boss.loot = new List<LootEntry>
            {
                Drop("toad_crown", 1f), Drop("poison_gland", 1f, 2, 3), Drop("toad_skin", 1f, 2, 3), Drop("venom_sac", 0.5f),
                Drop("lotus", 0.8f, 1, 2), Drop("gem_blue", 0.3f),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/BossToadKing.prefab");
        }

        static GameObject BuildSnake()
        {
            // no shadow: it rises out of the water
            var (root, boss) = Boss<BossSnakeMother>("BossSnakeMother", "snake", 45f, 1.2f, 0.7f, 2.2f, true, 5200f, 0f, 4.2f);
            root.GetComponent<CharacterMotor>().knockbackResist = 0.08f;
            var eyes = PointLight(boss.bodyRoot.gameObject, new Color(1f, 0.35f, 0.3f), 1.8f, 0.6f, new Vector3(0, 3.1f, 0));
            var nl = eyes.gameObject.AddComponent<NightLight>();
            nl.target = eyes;
            nl.dayIntensity = 0.3f;
            nl.nightIntensity = 1.2f;
            nl.flicker = 0.12f;
            root.GetComponent<Health>().resistances.poison = 0.75f;
            boss.poise.maxPoise = 360f;
            boss.bossId = "snake";
            boss.displayName = "Xà Mẫu Đầm Lầy";
            boss.title = "Mẹ Của Đầm Sâu";
            boss.level = 14;
            boss.rank = EnemyRank.Boss;
            boss.homeName = "Đầm Xà Mẫu";
            boss.arenaRadius = 11f;
            boss.wakeRadius = 8f;
            boss.coins = 16;
            boss.loot = new List<LootEntry>
            {
                Drop("snake_fang", 1f), Drop("snake_scale", 1f, 2, 3), Drop("venom_sac", 1f, 2, 2), Drop("gem_red", 0.6f),
                Drop("gem_blue", 0.6f), Drop("lotus", 1f, 2, 2), Drop("potion_red", 1f, 2, 2),
            };
            return EditorUtil.SavePrefab(root, $"{CharFolder}/BossSnakeMother.prefab");
        }

        static GameObject BuildBoulder()
        {
            var root = new GameObject("TangDaLon");
            root.layer = Layers.Obstacle;
            Group(root);
            var col = root.AddComponent<CircleCollider2D>();
            col.radius = 0.72f;
            col.offset = new Vector2(0, 0.45f);
            var health = root.AddComponent<Health>();
            health.team = Team.Neutral;
            health.displayName = "Tảng Đá Lớn";
            health.maxHp = 120;
            health.hp = 120;
            health.head = Head(root, 1.6f);
            Shadow(root, 2f, 0.05f);
            var body = Sprite(root, "Body", "boulder", AssetFactory.SpriteLit);
            Flash(body);
            var b = root.AddComponent<Boulder>();
            b.sr = body;
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/TangDaLon.prefab");
        }

        static GameObject BuildLoot()
        {
            var root = new GameObject("Loot");
            root.layer = Layers.Pickup;
            Group(root);
            var shadow = Shadow(root, 0.5f);
            var glow = Sprite(root, "Glow", "glow", AssetFactory.Additive, SortingLayerNames.Default, 1);
            glow.transform.localPosition = new Vector3(0, 0.35f, 0);
            glow.transform.localScale = Vector3.one * 0.9f;
            var beam = Sprite(root, "Beam", "beam", AssetFactory.Additive, SortingLayerNames.Default, 0);
            beam.transform.localPosition = new Vector3(0, 1.1f, 0);
            beam.transform.localScale = new Vector3(0.6f, 1.2f, 1f);
            var icon = Sprite(root, "Icon", "coin", AssetFactory.SpriteUnlit, SortingLayerNames.Default, 2);
            icon.transform.localPosition = new Vector3(0, 0.35f, 0);
            icon.transform.localScale = Vector3.one * 0.85f;
            var lp = root.AddComponent<LootPickup>();
            lp.icon = icon;
            lp.glow = glow;
            lp.beam = beam;
            lp.shadow = shadow;
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/Loot.prefab");
        }

        // ================================================================== props
        static GameObject Prop(string name, string sprite, Vector2? colliderSize = null, Vector2 colliderOffset = default,
                               float shadowWidth = 0f, bool fade = false, System.Action<GameObject, SpriteRenderer> extra = null)
        {
            var root = new GameObject(name);
            root.layer = Layers.Obstacle;
            Group(root);
            if (shadowWidth > 0) Shadow(root, shadowWidth, 0.04f);
            var sr = Sprite(root, "Sprite", sprite, AssetFactory.SpriteLit);
            if (colliderSize.HasValue)
            {
                var c = root.AddComponent<CapsuleCollider2D>();
                c.direction = colliderSize.Value.x >= colliderSize.Value.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                c.size = colliderSize.Value;
                c.offset = colliderOffset;
            }
            if (fade)
            {
                var f = root.AddComponent<FadeWhenBehind>();
                f.sr = sr;
                var b = sr.sprite != null ? sr.sprite.bounds : new Bounds(Vector3.zero, Vector3.one);
                f.areaSize = new Vector2(b.size.x * 0.8f, b.size.y * 0.8f);
                f.areaOffset = new Vector2(0, b.center.y + 0.2f);
            }
            extra?.Invoke(root, sr);
            var prefab = EditorUtil.SavePrefab(root, $"{PropFolder}/{name}.prefab");
            Props[name] = prefab;
            return prefab;
        }

        static void BuildProps()
        {
            Props.Clear();
            for (int i = 0; i < 3; i++) Prop($"pine_{i}", $"pine_{i}", new Vector2(0.55f, 0.35f), new Vector2(0, 0.15f), 1.8f, true);
            for (int i = 0; i < 2; i++) Prop($"oak_{i}", $"oak_{i}", new Vector2(0.7f, 0.4f), new Vector2(0, 0.18f), 2.4f, true);
            for (int i = 0; i < 2; i++)
            {
                Prop($"bush_big_{i}", $"bush_big_{i}", new Vector2(1.2f, 0.45f), new Vector2(0, 0.2f), 1.6f);
                Prop($"bush_small_{i}", $"bush_small_{i}", null, default, 1.0f);
            }
            for (int i = 0; i < 2; i++)
            {
                Prop($"rock_big_{i}", $"rock_big_{i}", new Vector2(1.1f, 0.5f), new Vector2(0, 0.25f), 1.4f);
                Prop($"rock_small_{i}", $"rock_small_{i}", null, default, 0.7f);
            }
            Prop("log", "log", new Vector2(1.7f, 0.45f), new Vector2(0, 0.2f), 2f);
            Prop("stump", "stump", new Vector2(0.8f, 0.4f), new Vector2(0, 0.2f), 1f);
            Prop("mushrooms", "mushrooms");
            Prop("tent", "tent", new Vector2(1.9f, 0.6f), new Vector2(0, 0.3f), 2.3f, true);
            Prop("hut", "hut", new Vector2(2.6f, 0.9f), new Vector2(0, 0.45f), 3.2f, true, (go, sr) =>
            {
                // warm windows at night
                foreach (var x in new[] { -0.9f, 0.9f })
                {
                    var l = PointLight(go, new Color(1f, 0.75f, 0.4f), 2.4f, 0f, new Vector3(x, 1.0f, 0));
                    var nl = l.gameObject.AddComponent<NightLight>();
                    nl.target = l;
                    nl.dayIntensity = 0f;
                    nl.nightIntensity = 1.1f;
                    nl.flicker = 0.05f;
                }
            });
            Prop("well", "well", new Vector2(1.4f, 0.6f), new Vector2(0, 0.3f), 1.8f);
            Prop("fence_h", "fence_h", new Vector2(1f, 0.25f), new Vector2(0, 0.15f));
            Prop("fence_post", "fence_post", new Vector2(0.3f, 0.25f), new Vector2(0, 0.12f));
            Prop("signpost", "signpost", new Vector2(0.35f, 0.25f), new Vector2(0, 0.12f), 0.7f);
            Prop("crate", "crate", new Vector2(0.9f, 0.45f), new Vector2(0, 0.2f), 1f);
            Prop("barrel", "barrel", new Vector2(0.75f, 0.4f), new Vector2(0, 0.2f), 0.9f);
            for (int i = 0; i < 2; i++)
            {
                Prop($"pillar_{i}", $"pillar_{i}", new Vector2(0.9f, 0.45f), new Vector2(0, 0.2f), 1.3f, true, (go, sr) =>
                {
                    var l = PointLight(go, new Color(0.45f, 1f, 0.85f), 2.2f, 0.4f, new Vector3(0, 1.2f, 0));
                    var nl = l.gameObject.AddComponent<NightLight>();
                    nl.target = l;
                    nl.dayIntensity = 0.25f;
                    nl.nightIntensity = 0.9f;
                    nl.flicker = 0.2f;
                    nl.flickerSpeed = 2f;
                });
            }
            // Đầm Lầy Sương Mù
            for (int i = 0; i < 2; i++)
            {
                Prop($"deadtree_{i}", $"deadtree_{i}", new Vector2(0.5f, 0.3f), new Vector2(0, 0.15f), 1.6f, true);
                Prop($"willow_{i}", $"willow_{i}", new Vector2(0.7f, 0.4f), new Vector2(0, 0.18f), 2.4f, true);
                Prop($"cattails_{i}", $"cattails_{i}");
                Prop($"swamprock_big_{i}", $"swamprock_big_{i}", new Vector2(1.2f, 0.5f), new Vector2(0, 0.25f), 1.5f);
                Prop($"swamprock_small_{i}", $"swamprock_small_{i}", null, default, 0.7f);
            }
            Prop("mossylog", "mossylog", new Vector2(1.9f, 0.4f), new Vector2(0, 0.18f), 2f);
            Prop("bones", "bones");
            Prop("stilthut", "stilthut", new Vector2(2.4f, 0.8f), new Vector2(0, 0.4f), 3f, true, (go, sr) =>
            {
                var l = PointLight(go, new Color(1f, 0.75f, 0.4f), 2.6f, 0f, new Vector3(0.2f, 1.6f, 0));
                var nl = l.gameObject.AddComponent<NightLight>();
                nl.target = l;
                nl.dayIntensity = 0.05f;
                nl.nightIntensity = 1.1f;
                nl.flicker = 0.06f;
            });
            Prop("waystone", "waystone", new Vector2(0.7f, 0.35f), new Vector2(0, 0.15f), 0.9f, false, (go, sr) =>
            {
                var w = go.AddComponent<Waystone>();
                w.sprite = sr;
                w.dark = ArtImporter.S("waystone");
                w.lit = ArtImporter.S("waystone_lit");
                w.glow = PointLight(go, new Color(0.45f, 1f, 0.95f), 3.6f, 1.1f, new Vector3(0, 1.4f, 0));
                w.glow.enabled = false;
            });
            Prop("lantern", "lantern", new Vector2(0.3f, 0.25f), new Vector2(0, 0.12f), 0.6f, false, (go, sr) =>
            {
                var l = PointLight(go, new Color(1f, 0.78f, 0.45f), 4.2f, 0.3f, new Vector3(0.3f, 1.2f, 0));
                var nl = l.gameObject.AddComponent<NightLight>();
                nl.target = l;
                nl.dayIntensity = 0.1f;
                nl.nightIntensity = 1.3f;
                nl.flicker = 0.08f;
            });
            Prop("campfire", "campfire", new Vector2(1.1f, 0.5f), new Vector2(0, 0.2f), 0f, false, (go, sr) =>
            {
                var flame = Sprite(go, "Flame", "flame_0", AssetFactory.SpriteUnlit, SortingLayerNames.Default, 1);
                flame.transform.localPosition = new Vector3(0, 0.25f, 0);
                Animator(flame, "props", "flame");
                var glow = Sprite(go, "Glow", "glow", AssetFactory.Additive, SortingLayerNames.Default, 2);
                glow.color = new Color(1f, 0.55f, 0.2f, 0.55f);
                glow.transform.localPosition = new Vector3(0, 0.55f, 0);
                glow.transform.localScale = Vector3.one * 1.6f;
                var gb = glow.gameObject.AddComponent<Bobber>();
                gb.amplitude = 0;
                gb.pulse = 0.06f;
                gb.speed = 9f;
                var l = PointLight(go, new Color(1f, 0.62f, 0.3f), 7.5f, 0.6f, new Vector3(0, 0.6f, 0));
                var nl = l.gameObject.AddComponent<NightLight>();
                nl.target = l;
                nl.dayIntensity = 0.45f;
                nl.nightIntensity = 1.8f;
                nl.flicker = 0.22f;
                nl.flickerSpeed = 9f;
                // rising embers
                var pgo = EditorUtil.Child(go, "Embers", new Vector3(0, 0.4f, 0));
                var ps = pgo.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f), new Color(1f, 0.45f, 0.15f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 60;
                var em = ps.emission;
                em.rateOverTime = 7f;
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = 18f;
                sh.radius = 0.2f;
                sh.rotation = new Vector3(-90, 0, 0);
                var noise = ps.noise;
                noise.enabled = true;
                noise.separateAxes = true;
                noise.strengthX = 0.5f;
                noise.strengthY = 0.3f;
                noise.strengthZ = 0f;
                noise.frequency = 0.8f;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(EditorUtil.Grad((0f, Color.white), (0.6f, Color.white), (1f, new Color(1, 1, 1, 0))));
                var r = pgo.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VFX/px_square_add_2_6.mat");
                r.sortingLayerName = SortingLayerNames.VFX;
            });
        }
    }
}
