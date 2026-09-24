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

        public static GameObject Player, Chief, Girl, Slime, Shroom, Bear, Boulder, Loot;

        [MenuItem("Tools/RPG/Steps/5. Rebuild Character + Prop Prefabs", priority = 105)]
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
            Boulder = BuildBoulder();
            Loot = BuildLoot();
            BuildProps();
            var db = AssetFactory.Database;
            db.boulderPrefab = Boulder;
            db.lootPrefab = Loot;
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Loads the generated prefabs from disk (used when only the scene is rebuilt).</summary>
        public static void LoadExisting()
        {
            GameObject L(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p + ".prefab");
            Player = L($"{CharFolder}/Player");
            Chief = L($"{CharFolder}/Chief");
            Girl = L($"{CharFolder}/Girl");
            Slime = L($"{CharFolder}/Slime");
            Shroom = L($"{CharFolder}/Shroom");
            Bear = L($"{CharFolder}/BossBear");
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
            string[] order = { "slash", "fireball", "ice", "lightning", "heal", "shield", "bladestorm", "dash" };
            for (int i = 0; i < 8; i++) skills.slots[i] = db.Skill(order[i]);
            var pc = root.AddComponent<PlayerController>();
            pc.motor = motor;
            pc.anim = anim;
            pc.health = health;
            pc.status = status;
            pc.flash = flash;
            pc.afterImages = ghost;
            pc.body = body;
            pc.skills = skills;
            pc.energy = 50;
            pc.maxEnergy = 63;
            // a soft personal light so the hero is readable at night
            var l = PointLight(root, new Color(1f, 0.85f, 0.65f), 4.5f, 0.4f, new Vector3(0, 0.6f, 0));
            var nl = l.gameObject.AddComponent<NightLight>();
            nl.target = l;
            nl.dayIntensity = 0f;
            nl.nightIntensity = 0.75f;
            nl.flicker = 0.03f;
            return EditorUtil.SavePrefab(root, $"{CharFolder}/Player.prefab");
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
