using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG.EditorTools
{
    /// <summary>
    /// Builds every visual effect as an editable prefab (Assets/Prefabs/VFX) and registers it
    /// in the VFX library. Effects combine particle systems, flipbook sprites, 2D lights and
    /// HDR additive materials (picked up by Bloom).
    /// </summary>
    public static class VFXFactory
    {
        const string Folder = "Assets/Prefabs/VFX";
        const string GameplayFolder = "Assets/Prefabs/Gameplay";
        const string MatFolder = "Assets/Materials/VFX";
        static VFXLibrary lib;
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        // palette
        static readonly Color FireCore = new Color(1f, 0.92f, 0.55f);
        static readonly Color FireMid = new Color(1f, 0.55f, 0.15f);
        static readonly Color FireDark = new Color(0.85f, 0.22f, 0.05f);
        static readonly Color IceA = new Color(0.55f, 0.88f, 1f);
        static readonly Color IceB = new Color(0.9f, 1f, 1f);
        static readonly Color Volt = new Color(0.72f, 0.62f, 1f);
        static readonly Color HealC = new Color(0.45f, 1f, 0.5f);
        static readonly Color Holy = new Color(1f, 0.88f, 0.45f);
        static readonly Color Enrage = new Color(0.72f, 0.3f, 1f);
        static readonly Color Dust = new Color(0.62f, 0.5f, 0.36f);
        static readonly Color Wind = new Color(0.7f, 1f, 0.95f);

        public static VFXLibrary Library => AssetDatabase.LoadAssetAtPath<VFXLibrary>("Assets/Data/VFXLibrary.asset");

        [MenuItem("Tools/RPG/Steps/4. VFX Prefabs", priority = 104)]
        public static void BuildAll()
        {
            Mats.Clear();
            EditorUtil.EnsureFolder(Folder);
            EditorUtil.EnsureFolder(MatFolder);
            EditorUtil.EnsureFolder(GameplayFolder);
            lib = Library;
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<VFXLibrary>();
                AssetDatabase.CreateAsset(lib, "Assets/Data/VFXLibrary.asset");
            }
            // authoring mode keeps the library as is (entries may be re-pointed or added by hand)
            if (EditorUtil.Overwrite) lib.entries.Clear();

            // hits & statuses
            Build("hit_spark", HitSpark, 0.6f);
            Build("hit_fire", HitFire, 0.8f);
            Build("hit_ice", HitIce, 0.8f);
            Build("hit_lightning", HitLightning, 0.6f);
            Build("stun_stars", StunStars, 0f);
            Build("burning", Burning, 0f);
            // player skills
            Build("slash", r => Slash(r, false), 0.5f);
            Build("slash_big", r => Slash(r, true), 0.7f);
            Build("cast_fire", CastFire, 0.6f);
            Build("fire_explosion", FireExplosion, 3f);
            Build("cast_ice", CastIce, 0.7f);
            Build("ice_spike", IceSpike, 1.1f);
            Build("storm_circle", StormCircle, 0f);
            Build("cast_lightning", CastLightning, 0.6f);
            Build("lightning_strike", LightningStrike, 2f);
            Build("heal_burst", HealBurst, 1.4f);
            Build("heal_aura", HealAura, 0f);
            Build("shield_cast", ShieldCast, 0.8f);
            Build("shield_bubble", ShieldBubble, 0f);
            Build("shield_break", ShieldBreak, 0.8f);
            Build("blade_storm", BladeStorm, 0f);
            Build("dash_burst", DashBurst, 0.6f);
            Build("step_dust", StepDust, 0.45f);
            Build("potion_red", r => Potion(r, new Color(1f, 0.35f, 0.3f)), 1.4f);
            Build("potion_blue", r => Potion(r, new Color(0.4f, 0.7f, 1f)), 1.4f);
            Build("potion_green", r => Potion(r, new Color(0.5f, 1f, 0.45f)), 1.4f);
            Build("click_marker", ClickMarker, 0.45f);
            Build("respawn", Respawn, 2f);
            Build("quest_complete", QuestComplete, 2.2f);
            // world
            Build("rock_chips", RockChips, 0.9f);
            Build("boulder_break", BoulderBreak, 1.8f);
            Build("pickup_coin", r => Pickup(r, Holy), 0.6f);
            Build("pickup_item", r => Pickup(r, Color.white), 0.6f);
            Build("enemy_death", EnemyDeath, 1.6f);
            Build("slime_splat", SlimeSplat, 1.2f);
            Build("spore_puff", SporePuff, 0.8f);
            Build("spore_hit", SporeHit, 1f);
            // swamp (Đầm Lầy Sương Mù)
            Build("venom_puff", VenomPuff, 0.8f);
            Build("venom_hit", VenomHit, 1f);
            Build("venom_pool", VenomPool, HazardZone.PoolSeconds + 0.2f);
            Build("water_splash", WaterSplash, 1.2f);
            Build("water_ripple", WaterRipple, 1f);
            Build("water_step", WaterStep, 0.6f);
            Build("mud_splat", MudSplat, 1.2f);
            Build("tongue_lash", TongueLash, 0.4f);
            Build("waystone_wake", WaystoneWake, 2.2f);
            Build("wisp_blink", WispBlink, 0.9f);
            Build("wisp_burst", WispBurst, 1.4f);
            // cave (Hang Pha Lê)
            Build("web_hit", WebHit, 1.2f);
            Build("crystal_burst", CrystalBurst, 1.2f);
            Build("crystal_hit", CrystalHit, 0.8f);
            Build("crystal_shatter", CrystalShatter, 1.6f);
            Build("crystal_beam", CrystalBeam, 0f);
            Build("dig_dust", DigDust, 1.8f);
            Build("coin_burst", CoinBurst, 1.4f);
            // a fallen boss's treasure chest
            Build("chest_appear", ChestAppear, 1.4f);
            Build("chest_open", ChestOpen, 2f);
            // the classes' skills
            Build("holy_strike", r => Pillar(r, Holy), 1.4f);
            Build("dark_strike", r => Pillar(r, DarkC), 1.4f);
            Build("holy_hit", r => SmallBurst(r, Holy), 0.9f);
            Build("dark_hit", r => SmallBurst(r, DarkC), 0.9f);
            Build("arcane_hit", r => SmallBurst(r, ArcaneC), 0.9f);
            Build("note_hit", r => SmallBurst(r, PinkC), 0.9f);
            Build("nature_burst", NatureBurst, 1.6f);
            Build("smoke_cloud", SmokeCloud, 3f);
            Build("holy_aura", r => Aura(r, Holy), 0f);
            Build("dark_aura", r => Aura(r, DarkC), 0f);
            // boss
            Build("boss_roar", BossRoar, 1.6f);
            Build("enrage_burst", EnrageBurst, 1.8f);
            Build("enrage_aura", EnrageAura, 0f);
            Build("claw_swipe", ClawSwipe, 0.6f);
            Build("stomp_shockwave", r => Shockwave(r, 1f), 3f);
            Build("pounce_land", r => Shockwave(r, 0.6f), 2.5f);
            Build("rock_impact", RockImpact, 2.5f);
            Build("boss_death", BossDeath, 3.5f);

            EditorUtility.SetDirty(lib);

            // gameplay prefabs built from VFX parts
            var db = AssetFactory.Database;
            EditorUtil.Assign(ref db.fireballPrefab, BuildFireball());
            EditorUtil.Assign(ref db.sporePrefab, BuildSpore());
            EditorUtil.Assign(ref db.venomPrefab, BuildVenom());
            EditorUtil.Assign(ref db.venomArcPrefab, BuildVenomGlob());
            EditorUtil.Assign(ref db.webPrefab, BuildWebShot());
            EditorUtil.Assign(ref db.shardPrefab, BuildShardShot());
            // the classes' projectiles (the abilities find them by path)
            var arrow = BuildBolt("Arrow", "proj_arrow", false, Color.white, new Color(1f, 0.9f, 0.7f), 1f, true, 0f, 0f);
            BuildBolt("ThrownKnife", "proj_knife", false, Color.white, new Color(0.85f, 0.9f, 1f), 1f, true, 0f, 0f);
            BuildBolt("ThrownAxe", "proj_axe", false, Color.white, new Color(1f, 0.8f, 0.6f), 1.1f, false, -900f, 0f);
            BuildBolt("NoteBolt", "proj_note", false, PinkC, PinkC, 0.9f, false, 0f, 1.1f);
            BuildBolt("HolyBolt", "glow_hard", true, Holy, Holy, 0.5f, false, 0f, 1.5f);
            BuildBolt("DarkBolt", "glow_hard", true, DarkC, DarkC, 0.5f, false, 0f, 1.5f);
            BuildBolt("ArcaneBolt", "spark4", true, ArcaneC, ArcaneC, 0.45f, false, 540f, 1.1f);
            BuildBolt("FireBolt", "glow_hard", true, new Color(1f, 0.55f, 0.2f), new Color(1f, 0.45f, 0.15f), 0.45f, false, 0f, 1.3f);
            BuildBolt("ChaosBolt", "spark4", true, new Color(1f, 0.6f, 1f), new Color(0.9f, 0.4f, 1f), 0.6f, false, -720f, 1.6f);
            EditorUtil.Assign(ref db.mistMaterial, Mat("smoke", false, 1f));
            EditorUtil.Assign(ref db.windMaterial, Mat("streak", true, 1.1f));
            // Hắc Phong's shots: the archers' arrows are the heroes' own, the chief's blades and whirlwinds
            EditorUtil.Assign(ref db.arrowPrefab, arrow);
            EditorUtil.Assign(ref db.windBladePrefab, BuildWindBlade());
            EditorUtil.Assign(ref db.tornadoPrefab, BuildTornado());
            EditorUtil.Assign(ref db.rockProjectilePrefab, BuildRockProjectile());
            EditorUtil.Assign(ref db.telegraphPrefab, BuildTelegraph());
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RPG] VFX library: {lib.entries.Count} effects");
        }

        static void Build(string id, Action<GameObject> make, float lifetime)
        {
            var root = new GameObject(id);
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = lifetime;
            make(root);
            var prefab = EditorUtil.SavePrefab(root, $"{Folder}/{id}.prefab");
            var entry = lib.entries.Find(e => e.id == id);
            if (entry == null) lib.entries.Add(new VFXLibrary.Entry { id = id, prefab = prefab });
            else if (entry.prefab == null) entry.prefab = prefab;
        }

        // ================================================================== materials
        static Texture2D Tex(string sprite)
        {
            var s = ArtImporter.S(sprite);
            return s != null ? s.texture : null;
        }

        /// <param name="tex">sprite name whose texture is used (flipbooks: first frame name)</param>
        static Material Mat(string tex, bool additive, float intensity = 1.6f)
        {
            string key = $"{tex}_{(additive ? "add" : "alpha")}_{intensity:0.0}".Replace(".", "_");
            if (Mats.TryGetValue(key, out var m) && m != null) return m;
            string path = $"{MatFolder}/{key}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            // keep materials tuned by hand (e.g. a stronger _Intensity for more bloom)
            if (EditorUtil.Keep(m))
            {
                Mats[key] = m;
                return m;
            }
            EditorUtil.Written++;
            var shader = Shader.Find(additive ? "RPG/VFX Additive" : "RPG/VFX Alpha");
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            m.SetTexture("_MainTex", Tex(tex));
            m.SetFloat("_Intensity", intensity);
            m.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(m);
            Mats[key] = m;
            return m;
        }

        // ================================================================== builders
        class P
        {
            public readonly ParticleSystem ps;
            public readonly ParticleSystemRenderer r;

            public P(GameObject parent, string name, Material mat, string layer = SortingLayerNames.VFX, int order = 0)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent.transform, false);
                ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.sortingLayerName = layer;
                r.sortingOrder = order;
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.maxParticleSize = 3f;
                var main = ps.main;
                main.playOnAwake = true;
                main.loop = false;
                main.duration = 1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.startLifetime = 0.5f;
                main.startSpeed = 0f;
                main.startSize = 0.3f;
                main.maxParticles = 200;
                var em = ps.emission;
                em.rateOverTime = 0f;
                var sh = ps.shape;
                sh.enabled = false;
            }

            public P Life(float a, float b) { var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(a, b); return this; }
            public P Speed(float a, float b) { var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(a, b); return this; }
            public P Size(float a, float b) { var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(a, b); return this; }
            public P Col(Color a) { var m = ps.main; m.startColor = a; return this; }
            public P Col(Color a, Color b) { var m = ps.main; m.startColor = new ParticleSystem.MinMaxGradient(a, b); return this; }
            public P Rot(float a = 0, float b = 360) { var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(a * Mathf.Deg2Rad, b * Mathf.Deg2Rad); return this; }
            public P Grav(float g) { var m = ps.main; m.gravityModifier = g; return this; }
            public P Max(int n) { var m = ps.main; m.maxParticles = n; return this; }
            public P Delay(float d) { var m = ps.main; m.startDelay = d; return this; }
            public P Local() { var m = ps.main; m.simulationSpace = ParticleSystemSimulationSpace.Local; return this; }

            public P Loop(float duration = 1f)
            {
                var m = ps.main;
                m.loop = true;
                m.duration = duration;
                return this;
            }

            public P Dur(float d) { var m = ps.main; m.duration = d; return this; }

            public P Burst(int n, float t = 0f)
            {
                var em = ps.emission;
                var list = new ParticleSystem.Burst[em.burstCount + 1];
                em.GetBursts(list);
                list[em.burstCount] = new ParticleSystem.Burst(t, (short)n);
                em.SetBursts(list);
                return this;
            }

            public P Rate(float r) { var em = ps.emission; em.rateOverTime = r; return this; }

            public P Circle(float radius, float arc = 360f, float thickness = 1f)
            {
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Circle;
                sh.radius = radius;
                sh.arc = arc;
                sh.radiusThickness = thickness;
                sh.rotation = Vector3.zero;
                return this;
            }

            /// <summary>Cone emitting along +X (2D), angle in degrees.</summary>
            public P ConeX(float angle, float radius = 0.05f)
            {
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = angle;
                sh.radius = radius;
                sh.rotation = new Vector3(0, 90, 0);
                return this;
            }

            /// <summary>Cone emitting along +Y (up on screen).</summary>
            public P ConeUp(float angle, float radius = 0.05f)
            {
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = angle;
                sh.radius = radius;
                sh.rotation = new Vector3(-90, 0, 0);
                return this;
            }

            public P Box(float x, float y)
            {
                var sh = ps.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Box;
                sh.scale = new Vector3(x, y, 0.01f);
                return this;
            }

            public P ColLife(Gradient g)
            {
                var c = ps.colorOverLifetime;
                c.enabled = true;
                c.color = new ParticleSystem.MinMaxGradient(g);
                return this;
            }

            public P Fade(float fadeInEnd = 0f)
            {
                var keys = fadeInEnd > 0
                    ? new[] { (0f, new Color(1, 1, 1, 0)), (fadeInEnd, Color.white), (0.6f, Color.white), (1f, new Color(1, 1, 1, 0)) }
                    : new[] { (0f, Color.white), (0.55f, Color.white), (1f, new Color(1, 1, 1, 0)) };
                return ColLife(EditorUtil.Grad(keys));
            }

            public P SizeLife(params float[] kv)
            {
                var s = ps.sizeOverLifetime;
                s.enabled = true;
                s.size = new ParticleSystem.MinMaxCurve(1f, EditorUtil.Curve(kv));
                return this;
            }

            public P Spin(float a, float b)
            {
                var ro = ps.rotationOverLifetime;
                ro.enabled = true;
                ro.z = new ParticleSystem.MinMaxCurve(a * Mathf.Deg2Rad, b * Mathf.Deg2Rad);
                return this;
            }

            public P Vel(float x0, float x1, float y0, float y1, bool local = false)
            {
                var v = ps.velocityOverLifetime;
                v.enabled = true;
                v.space = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
                v.x = new ParticleSystem.MinMaxCurve(x0, x1);
                v.y = new ParticleSystem.MinMaxCurve(y0, y1);
                v.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                return this;
            }

            public P Orbit(float z, float radial = 0f)
            {
                var v = ps.velocityOverLifetime;
                v.enabled = true;
                v.orbitalX = new ParticleSystem.MinMaxCurve(0f);
                v.orbitalY = new ParticleSystem.MinMaxCurve(0f);
                v.orbitalZ = new ParticleSystem.MinMaxCurve(z);
                v.radial = new ParticleSystem.MinMaxCurve(radial);
                if (v.x.mode != ParticleSystemCurveMode.Constant)
                {
                    v.x = new ParticleSystem.MinMaxCurve(0f);
                    v.y = new ParticleSystem.MinMaxCurve(0f);
                    v.z = new ParticleSystem.MinMaxCurve(0f);
                }
                return this;
            }

            public P Drag(float d)
            {
                var l = ps.limitVelocityOverLifetime;
                l.enabled = true;
                l.drag = d;
                l.multiplyDragByParticleSize = false;
                l.multiplyDragByParticleVelocity = true;
                return this;
            }

            public P Noise(float strength, float freq = 0.5f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.separateAxes = true;
                n.strengthX = strength;
                n.strengthY = strength;
                n.strengthZ = 0f;
                n.frequency = freq;
                n.scrollSpeed = 0.3f;
                n.quality = ParticleSystemNoiseQuality.Medium;
                return this;
            }

            public P Sheet(int tiles, float cycles = 1f)
            {
                var t = ps.textureSheetAnimation;
                t.enabled = true;
                t.mode = ParticleSystemAnimationMode.Grid;
                t.numTilesX = tiles;
                t.numTilesY = 1;
                t.animation = ParticleSystemAnimationType.WholeSheet;
                t.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
                t.cycleCount = Mathf.Max(1, Mathf.RoundToInt(cycles));
                return this;
            }

            public P Stretch(float length = 2f, float velocityScale = 0.04f)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = length;
                r.velocityScale = velocityScale;
                return this;
            }

            public P Trails(Material m, float life = 0.25f, float width = 1f)
            {
                var t = ps.trails;
                t.enabled = true;
                t.ratio = 1f;
                t.lifetime = life;
                t.dieWithParticles = true;
                t.inheritParticleColor = true;
                t.widthOverTrail = new ParticleSystem.MinMaxCurve(width, AnimationCurve.Linear(0, 1, 1, 0));
                r.trailMaterial = m;
                return this;
            }

            public P At(float x, float y)
            {
                ps.transform.localPosition = new Vector3(x, y, 0);
                return this;
            }

            public P Scale(float sx, float sy)
            {
                ps.transform.localScale = new Vector3(sx, sy, 1);
                return this;
            }
        }

        static P PS(GameObject parent, string name, Material mat, string layer = SortingLayerNames.VFX, int order = 0) => new P(parent, name, mat, layer, order);

        static SpriteRenderer Spr(GameObject parent, string name, string sprite, Material mat, Color color,
                                  string layer = SortingLayerNames.VFX, int order = 0, float scale = 1f, float x = 0, float y = 0)
        {
            var go = EditorUtil.Child(parent, name, new Vector3(x, y, 0));
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ArtImporter.S(sprite);
            sr.sharedMaterial = mat;
            sr.color = color;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteFX SFX(SpriteRenderer sr, float life, AnimationCurve scale, AnimationCurve alpha, float spin = 0f)
        {
            var fx = sr.gameObject.AddComponent<SpriteFX>();
            fx.sr = sr;
            fx.lifetime = life;
            fx.scale = scale;
            fx.alpha = alpha;
            fx.spinSpeed = spin;
            return fx;
        }

        static SpriteAnimator Flip(SpriteRenderer sr, string clip, float speed = 1f, bool hideWhenDone = true)
        {
            var a = sr.gameObject.AddComponent<SpriteAnimator>();
            a.set = ArtImporter.AnimSet("vfx");
            a.target = sr;
            a.startClip = clip;
            a.speed = speed;
            a.hideWhenDone = hideWhenDone;
            sr.sprite = ArtImporter.S(clip + "_0");
            return a;
        }

        static Light2D Light(GameObject parent, Color c, float radius, float intensity, float duration = 0.4f, bool pulse = true, float flicker = 0f)
        {
            var go = EditorUtil.Child(parent, "Light");
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.08f;
            l.falloffIntensity = 0.65f;
            l.shadowsEnabled = false;
            l.targetSortingLayers = SortingLayer.layers.Select(x => x.id).ToArray();
            if (pulse)
            {
                var lp = go.AddComponent<LightPulse>();
                lp.target = l;
                lp.peakIntensity = intensity;
                lp.duration = duration;
                lp.curve = EditorUtil.Curve(0, 1, 0.25f, 0.7f, 1, 0);
                lp.flicker = flicker;
            }
            else if (flicker > 0)
            {
                var lp = go.AddComponent<LightPulse>();
                lp.target = l;
                lp.peakIntensity = intensity;
                lp.duration = 1f;
                lp.loop = true;
                lp.curve = AnimationCurve.Constant(0, 1, 1);
                lp.flicker = flicker;
            }
            return l;
        }

        static AnimationCurve C(params float[] kv) => EditorUtil.Curve(kv);
        static Gradient G(params (float, Color)[] k) => EditorUtil.Grad(k);
        static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

        static void Ring(GameObject root, string name, Color c, float from, float to, float life, string tex = "ring", string layer = SortingLayerNames.VFX, float squashY = 1f, bool additive = true)
        {
            var sr = Spr(root, name, tex, Mat(tex, additive, 1.8f), c, layer, -1);
            sr.transform.localScale = new Vector3(1, squashY, 1);
            SFX(sr, life, C(0, from, 1, to), C(0, 1, 0.5f, 0.7f, 1, 0));
        }

        // ================================================================== hits
        static void HitSpark(GameObject r)
        {
            var sr = Spr(r, "Flash", "hit_0", Mat("hit_0", true, 2.2f), new Color(1f, 0.95f, 0.8f), scale: 1.1f);
            Flip(sr, "hit");
            PS(r, "Sparks", Mat("px_square", true, 2.5f)).Burst(8).Life(0.12f, 0.3f).Speed(5f, 11f).Size(0.07f, 0.14f)
                .Col(Color.white, new Color(1f, 0.8f, 0.4f)).Circle(0.05f).Drag(4f).Stretch(1.6f, 0.035f).Fade();
        }

        static void HitFire(GameObject r)
        {
            var sr = Spr(r, "Flash", "hit_0", Mat("hit_0", true, 2.4f), FireCore, scale: 1.2f);
            Flip(sr, "hit");
            PS(r, "Embers", Mat("px_square", true, 2.6f)).Burst(12).Life(0.25f, 0.6f).Speed(3f, 8f).Size(0.07f, 0.14f)
                .Col(FireCore, FireMid).Circle(0.1f).Drag(3f).Grav(-0.3f).Stretch(1.4f, 0.03f).Fade();
            PS(r, "Flames", Mat("fire_0", true, 1.8f)).Burst(5).Life(0.3f, 0.5f).Speed(0.5f, 1.5f).Size(0.4f, 0.7f)
                .Circle(0.2f).Sheet(8).Grav(-0.25f).Rot();
        }

        static void HitIce(GameObject r)
        {
            var sr = Spr(r, "Flash", "hit_0", Mat("hit_0", true, 2.2f), IceB, scale: 1.1f);
            Flip(sr, "hit");
            PS(r, "Shards", Mat("ice_shard", false, 1.3f)).Burst(9).Life(0.35f, 0.7f).Speed(3f, 7f).Size(0.25f, 0.4f)
                .Col(IceB, IceA).Circle(0.1f).Grav(1.8f).Rot().Spin(-360, 360).Fade();
            PS(r, "Frost", Mat("glow", true, 1.4f)).Burst(6).Life(0.3f, 0.6f).Speed(0.3f, 1.2f).Size(0.3f, 0.6f)
                .Col(A(IceA, 0.6f)).Circle(0.3f).Fade();
        }

        static void HitLightning(GameObject r)
        {
            var sr = Spr(r, "Bolt", "bolt_hit_0", Mat("bolt_hit_0", true, 2.6f), Volt, scale: 1f);
            Flip(sr, "bolt_hit");
            PS(r, "Sparks", Mat("px_square", true, 3f)).Burst(10).Life(0.1f, 0.3f).Speed(6f, 12f).Size(0.06f, 0.12f)
                .Col(Color.white, Volt).Circle(0.05f).Drag(5f).Stretch(2f, 0.04f).Fade();
        }

        static void StunStars(GameObject r)
        {
            var ring = EditorUtil.Child(r, "Orbit");
            var o = ring.AddComponent<OrbitRing>();
            o.radiusX = 0.38f;
            o.radiusY = 0.12f;
            o.degreesPerSecond = 240f;
            o.childSpin = 180f;
            for (int i = 0; i < 3; i++)
                Spr(ring, "Star" + i, "spark4", Mat("spark4", true, 2f), new Color(1f, 0.9f, 0.35f), scale: 0.6f);
        }

        static void Burning(GameObject r)
        {
            PS(r, "Flames", Mat("fire_0", true, 1.9f), SortingLayerNames.VFX, 2).Loop().Rate(16).Life(0.35f, 0.6f).Speed(0.2f, 0.6f)
                .Size(0.35f, 0.6f).Circle(0.28f).Sheet(8).Vel(0, 0, 1.2f, 2f).Rot().Local();
            PS(r, "Embers", Mat("px_square", true, 2.5f)).Loop().Rate(8).Life(0.4f, 0.8f).Speed(0.2f, 0.8f).Size(0.05f, 0.09f)
                .Col(FireCore, FireMid).Circle(0.3f).Vel(-0.3f, 0.3f, 1.5f, 2.5f).Noise(0.6f).Fade();
            Light(r, FireMid, 2.2f, 0.9f, pulse: false, flicker: 0.3f);
        }

        // ================================================================== player skills
        static void Slash(GameObject r, bool big)
        {
            Color c = big ? new Color(1f, 0.9f, 0.55f) : new Color(0.75f, 1f, 0.97f);
            var sr = Spr(r, "Arc", "slash_0", Mat("slash_0", true, big ? 2.6f : 2.1f), c, scale: big ? 1.15f : 1f);
            Flip(sr, "slash", big ? 0.9f : 1.1f);
            var glow = Spr(r, "ArcGlow", "slash_0", Mat("slash_0", true, 1.2f), A(c, 0.5f), scale: big ? 1.3f : 1.15f, order: -1);
            Flip(glow, "slash", big ? 0.9f : 1.1f);
            PS(r, "Sparks", Mat("px_square", true, 2.4f)).Burst(big ? 18 : 10).Life(0.15f, 0.35f).Speed(1.5f, 4f).Size(0.05f, 0.11f)
                .Col(Color.white, c).Circle(1.2f, 170f, 0f).Drag(3f).Fade();
            PS(r, "Wind", Mat("streak", true, 1.4f)).Burst(big ? 6 : 3).Life(0.15f, 0.3f).Speed(4f, 7f).Size(0.5f, 0.9f)
                .Col(A(Wind, 0.5f)).ConeX(35f, 0.3f).Stretch(1.2f, 0.02f).Fade();
            if (big)
            {
                Ring(r, "Shock", A(Holy, 0.8f), 0.3f, 1.6f, 0.3f, "ring");
                Light(r, Holy, 3.5f, 1.4f, 0.25f);
            }
            else Light(r, Wind, 2.2f, 0.6f, 0.2f);
        }

        static void CastFire(GameObject r)
        {
            var g = Spr(r, "Glow", "glow", Mat("glow", true, 2f), FireMid, scale: 1.4f);
            SFX(g, 0.25f, C(0, 0.4f, 0.4f, 1.2f, 1, 0.8f), C(0, 1, 1, 0));
            PS(r, "Flames", Mat("fire_0", true, 1.8f)).Burst(7).Life(0.2f, 0.4f).Speed(1f, 3f).Size(0.3f, 0.5f).Circle(0.15f).Sheet(8).Rot();
        }

        static void FireExplosion(GameObject r)
        {
            var boom = Spr(r, "Boom", "explosion_0", Mat("explosion_0", false, 1.7f), Color.white, scale: 1.25f);
            Flip(boom, "explosion");
            var core = Spr(r, "Core", "glow_hard", Mat("glow_hard", true, 3f), FireCore, scale: 3.2f);
            SFX(core, 0.3f, C(0, 0.5f, 0.3f, 1.1f, 1, 0.9f), C(0, 1, 1, 0));
            Ring(r, "Shock", A(FireMid, 0.9f), 0.2f, 2.2f, 0.35f, "ring_thick");
            PS(r, "Flames", Mat("fire_0", true, 2f)).Burst(16).Life(0.3f, 0.7f).Speed(2f, 6f).Size(0.5f, 0.9f)
                .Circle(0.3f).Sheet(8).Drag(3f).Rot();
            PS(r, "Embers", Mat("px_square", true, 3f)).Burst(26).Life(0.4f, 1f).Speed(4f, 10f).Size(0.06f, 0.13f)
                .Col(FireCore, FireMid).Circle(0.2f).Drag(2.5f).Grav(0.7f).Stretch(1.5f, 0.03f).Fade();
            PS(r, "Smoke", Mat("smoke", false, 1f), SortingLayerNames.VFX, -2).Burst(8).Delay(0.12f).Life(1f, 1.8f).Speed(0.5f, 1.5f)
                .Size(1.1f, 1.9f).Col(new Color(0.22f, 0.18f, 0.2f, 0.7f)).Circle(0.5f).Vel(0, 0, 0.3f, 0.8f).Rot().Spin(-40, 40)
                .ColLife(G((0f, new Color(1, 1, 1, 0)), (0.15f, Color.white), (1f, new Color(1, 1, 1, 0))));
            var scorch = Spr(r, "Scorch", "glow", Mat("glow", false, 1f), new Color(0.05f, 0.03f, 0.03f, 0.6f), SortingLayerNames.Decal, 0, 2.4f);
            scorch.transform.localScale = new Vector3(2.4f, 1.3f, 1);
            SFX(scorch, 3f, C(0, 1, 1, 1), C(0, 1, 0.7f, 1, 1, 0));
            Light(r, FireMid, 6f, 3.2f, 0.5f);
        }

        static void CastIce(GameObject r)
        {
            Ring(r, "Ring", IceA, 0.2f, 1.1f, 0.35f, "ring", SortingLayerNames.VFX, 0.55f);
            PS(r, "Shards", Mat("ice_shard", false, 1.3f)).Burst(8).Life(0.3f, 0.6f).Speed(1.5f, 3.5f).Size(0.2f, 0.35f)
                .Col(IceB, IceA).Circle(0.2f).Rot().Spin(-200, 200).Fade();
            Light(r, IceA, 2.5f, 1.2f, 0.3f);
        }

        static void IceSpike(GameObject r)
        {
            var glow = Spr(r, "Glow", "glow", Mat("glow", true, 1.4f), A(IceA, 0.7f), SortingLayerNames.VFX, -2, 1.6f, 0, 0.9f);
            SFX(glow, 0.9f, C(0, 0.5f, 0.2f, 1f, 1, 0.8f), C(0, 1, 0.7f, 0.8f, 1, 0));
            var spike = Spr(r, "Spike", "ice_spike_0", AssetFactory.SpriteUnlit, Color.white, SortingLayerNames.Default, 0);
            Flip(spike, "ice_spike", 1f, false);
            var fade = spike.gameObject.AddComponent<SpriteFX>();
            fade.sr = spike;
            fade.lifetime = 1f;
            fade.scale = C(0, 1, 1, 1);
            fade.alpha = C(0, 1, 0.75f, 1, 1, 0);
            PS(r, "Burst", Mat("ice_shard", false, 1.4f)).Burst(6).Life(0.3f, 0.6f).Speed(2f, 4.5f).Size(0.2f, 0.35f)
                .Col(IceB, IceA).ConeUp(40f, 0.3f).Grav(2f).Rot().Spin(-300, 300).Fade();
            PS(r, "Shatter", Mat("ice_shard", false, 1.4f)).Burst(8, 0.3f).Life(0.35f, 0.7f).Speed(1.5f, 4f).Size(0.18f, 0.3f)
                .Col(IceB, IceA).Circle(0.4f).Grav(2.5f).Rot().Spin(-300, 300).Fade().At(0, 0.8f);
            PS(r, "Mist", Mat("smoke", true, 0.8f), SortingLayerNames.VFX, -1).Burst(4).Life(0.6f, 1f).Speed(0.2f, 0.6f).Size(0.8f, 1.2f)
                .Col(A(IceA, 0.35f)).Circle(0.4f).Fade(0.2f);
            Light(r, IceA, 2.4f, 1.3f, 0.7f);
        }

        static void StormCircle(GameObject r)
        {
            var circle = Spr(r, "Circle", "magic_circle", Mat("magic_circle", true, 1.8f), Volt, SortingLayerNames.Decal, 2, 1.25f);
            var fx = SFX(circle, 0.5f, C(0, 0.3f, 0.3f, 1.05f, 1, 1f), C(0, 0, 1, 1));
            circle.gameObject.AddComponent<Spinner>().degreesPerSecond = 45f;
            var inner = Spr(r, "Inner", "magic_circle", Mat("magic_circle", true, 1.2f), A(Volt, 0.6f), SortingLayerNames.Decal, 1, 0.7f);
            inner.gameObject.AddComponent<Spinner>().degreesPerSecond = -80f;
            var glow = Spr(r, "Glow", "glow", Mat("glow", true, 1f), A(Volt, 0.45f), SortingLayerNames.Decal, 0, 5f);
            PS(r, "Motes", Mat("px_square", true, 2.5f)).Loop().Rate(26).Life(0.6f, 1.2f).Speed(0f, 0.2f).Size(0.05f, 0.1f)
                .Col(Color.white, Volt).Circle(2.2f).Vel(0, 0, 1f, 2.2f).Fade(0.2f);
            PS(r, "Clouds", Mat("smoke", false, 1f), SortingLayerNames.Top, 0).Loop().Rate(5).Life(1.2f, 1.8f).Size(1.5f, 2.4f)
                .Col(new Color(0.18f, 0.14f, 0.3f, 0.45f)).Circle(1.8f).Rot().Spin(-20, 20).Fade(0.3f).At(0, 3.5f);
            Light(r, Volt, 4.5f, 0.8f, pulse: false, flicker: 0.25f);
        }

        static void CastLightning(GameObject r)
        {
            Ring(r, "Ring", Volt, 0.2f, 1f, 0.3f);
            PS(r, "Sparks", Mat("px_square", true, 3f)).Burst(12).Life(0.1f, 0.3f).Speed(3f, 7f).Size(0.05f, 0.1f)
                .Col(Color.white, Volt).Circle(0.2f).Drag(4f).Stretch(1.5f, 0.03f).Fade();
            Light(r, Volt, 2.5f, 1.4f, 0.3f);
        }

        static void LightningStrike(GameObject r)
        {
            var boltGo = EditorUtil.Child(r, "Bolt");
            var bolt = boltGo.AddComponent<LightningBolt>();
            LineRenderer Line(string name, float width, Color c, int order)
            {
                var go = EditorUtil.Child(boltGo, name);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = width;
                lr.widthCurve = C(0, 0.6f, 0.5f, 1f, 1, 0.8f);
                lr.startColor = c;
                lr.endColor = c;
                lr.sharedMaterial = Mat("streak", true, 3f);
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 2;
                lr.sortingLayerName = SortingLayerNames.Top;
                lr.sortingOrder = order;
                return lr;
            }
            bolt.core = Line("Core", 0.35f, Color.white, 2);
            bolt.glow = Line("Glow", 1.3f, Volt, 1);
            bolt.lifetime = 0.32f;
            var hit = Spr(r, "Impact", "bolt_hit_0", Mat("bolt_hit_0", true, 3f), Color.white, scale: 1.6f);
            Flip(hit, "bolt_hit");
            var flash = Spr(r, "Flash", "glow_hard", Mat("glow_hard", true, 3f), Volt, scale: 3.5f);
            SFX(flash, 0.25f, C(0, 1.1f, 1, 0.8f), C(0, 1, 1, 0));
            Ring(r, "Shock", A(Volt, 0.9f), 0.2f, 1.4f, 0.3f, "ring", SortingLayerNames.VFX, 0.6f);
            PS(r, "Sparks", Mat("px_square", true, 3f)).Burst(16).Life(0.15f, 0.4f).Speed(4f, 10f).Size(0.05f, 0.12f)
                .Col(Color.white, Volt).Circle(0.2f).Drag(4f).Grav(0.6f).Stretch(2f, 0.04f).Fade();
            PS(r, "Debris", Mat("debris_0", false, 1f)).Burst(6).Life(0.4f, 0.7f).Speed(2f, 5f).Size(0.2f, 0.3f)
                .ConeUp(50f, 0.2f).Grav(2.5f).Rot().Spin(-300, 300).Fade();
            var scorch = Spr(r, "Scorch", "crack", Mat("crack", false, 1f), new Color(0.2f, 0.15f, 0.25f, 0.9f), SortingLayerNames.Decal, 0, 0.5f);
            SFX(scorch, 2f, C(0, 1, 1, 1), C(0, 1, 0.6f, 1, 1, 0));
            Light(r, Volt, 7f, 4f, 0.3f);
        }

        static void HealBurst(GameObject r)
        {
            Ring(r, "Ring", HealC, 0.3f, 1.6f, 0.5f, "ring", SortingLayerNames.VFX, 0.5f);
            var g = Spr(r, "Glow", "glow", Mat("glow", true, 1.6f), A(HealC, 0.7f), scale: 2.2f, y: 0.7f);
            SFX(g, 0.6f, C(0, 0.5f, 0.3f, 1f, 1, 1.1f), C(0, 1, 1, 0));
            PS(r, "Plus", Mat("plus", true, 2f)).Burst(12).Life(0.7f, 1.2f).Speed(0.4f, 1f).Size(0.25f, 0.4f)
                .Col(HealC, Color.white).Circle(0.6f).Vel(0, 0, 1f, 2f).Fade(0.1f).At(0, 0.3f);
            PS(r, "Sparkle", Mat("spark4", true, 2f)).Burst(10).Life(0.4f, 0.9f).Speed(0.5f, 2f).Size(0.2f, 0.35f)
                .Col(Color.white, HealC).Circle(0.4f).Fade().At(0, 0.6f);
            Light(r, HealC, 3.5f, 1.8f, 0.6f);
        }

        static void HealAura(GameObject r)
        {
            var ring = Spr(r, "Ring", "ring", Mat("ring", true, 1.4f), A(HealC, 0.8f), SortingLayerNames.Decal, 1);
            ring.transform.localScale = new Vector3(0.8f, 0.4f, 1);
            var b = ring.gameObject.AddComponent<Bobber>();
            b.amplitude = 0;
            b.pulse = 0.08f;
            b.speed = 5f;
            PS(r, "Rise", Mat("plus", true, 1.8f)).Loop().Rate(7).Life(0.8f, 1.2f).Size(0.15f, 0.25f).Col(HealC, Color.white)
                .Circle(0.45f).Vel(0, 0, 0.8f, 1.4f).Fade(0.2f).Local();
            PS(r, "Motes", Mat("px_square", true, 2f)).Loop().Rate(12).Life(0.5f, 1f).Size(0.04f, 0.08f).Col(HealC, Color.white)
                .Circle(0.5f).Vel(0, 0, 1f, 2f).Fade().Local();
            Light(r, HealC, 2.5f, 0.8f, pulse: false, flicker: 0.1f);
        }

        static void ShieldCast(GameObject r)
        {
            Ring(r, "Ring", Holy, 0.2f, 1.4f, 0.4f, "ring_thick");
            PS(r, "Sparkle", Mat("spark4", true, 2.2f)).Burst(14).Life(0.3f, 0.7f).Speed(1.5f, 4f).Size(0.2f, 0.35f)
                .Col(Color.white, Holy).Circle(0.3f).Drag(3f).Fade().At(0, 0.6f);
            Light(r, Holy, 3.5f, 1.8f, 0.4f);
        }

        /// <summary>Khiên Thánh while it lasts: a few holy glints on the hero, no bubble or rune circle around them (players found those in the way).</summary>
        static void ShieldBubble(GameObject r)
        {
            PS(r, "Sparkles", Mat("spark4", true, 2f)).Loop().Rate(5).Life(0.4f, 0.8f).Size(0.08f, 0.16f).Col(Color.white, Holy)
                .Circle(0.35f, 360f, 0f).Fade(0.2f).Local().At(0, 0.65f);
            Light(r, Holy, 1.6f, 0.5f, pulse: false, flicker: 0.08f);
        }

        static void ShieldBreak(GameObject r)
        {
            PS(r, "Shards", Mat("spark4", true, 2.2f)).Burst(18).Life(0.3f, 0.7f).Speed(2f, 5f).Size(0.15f, 0.3f)
                .Col(Color.white, Holy).Circle(0.8f, 360f, 0f).Drag(2f).Grav(0.8f).Fade();
            Ring(r, "Ring", Holy, 0.9f, 1.3f, 0.3f);
        }

        static void BladeStorm(GameObject r)
        {
            var orbit = EditorUtil.Child(r, "Blades", new Vector3(0, 0.45f, 0));
            var o = orbit.AddComponent<OrbitRing>();
            o.radiusX = 1.7f;
            o.radiusY = 1.1f;
            o.degreesPerSecond = 540f;
            o.rotateChildren = true;
            o.depthSort = false;
            for (int i = 0; i < 4; i++)
            {
                var sr = Spr(orbit, "Blade" + i, "sword", Mat("sword", true, 1.6f), new Color(1f, 0.75f, 0.78f), scale: 1.2f);
                var tr = sr.gameObject.AddComponent<TrailRenderer>();
                tr.time = 0.14f;
                tr.widthMultiplier = 0.45f;
                tr.widthCurve = C(0, 1, 1, 0);
                tr.colorGradient = G((0f, new Color(1f, 0.55f, 0.6f, 0.8f)), (1f, new Color(1f, 0.3f, 0.4f, 0f)));
                tr.sharedMaterial = Mat("streak", true, 2.2f);
                tr.sortingLayerName = SortingLayerNames.VFX;
                tr.minVertexDistance = 0.05f;
            }
            var swirl = Spr(r, "Swirl", "slash_3", Mat("slash_3", true, 1.4f), new Color(1f, 0.6f, 0.65f, 0.55f), scale: 1.5f, y: 0.45f);
            swirl.gameObject.AddComponent<Spinner>().degreesPerSecond = -900f;
            PS(r, "Wind", Mat("streak", true, 1.2f)).Loop().Rate(24).Life(0.3f, 0.5f).Size(0.4f, 0.7f).Col(new Color(1f, 0.7f, 0.75f, 0.5f))
                .Circle(1.8f, 360f, 0.2f).Orbit(-9f).Stretch(1f, 0.05f).Fade().Local().At(0, 0.45f);
            Light(r, new Color(1f, 0.5f, 0.55f), 3f, 0.8f, pulse: false, flicker: 0.2f);
        }

        static void DashBurst(GameObject r)
        {
            var d = Spr(r, "Dust", "dust_0", Mat("dust_0", false, 1f), Color.white, SortingLayerNames.Decal, 3, 1.3f, 0, -0.25f);
            Flip(d, "dust");
            PS(r, "Streaks", Mat("streak", true, 1.6f)).Burst(7).Life(0.15f, 0.3f).Speed(6f, 10f).Size(0.4f, 0.8f)
                .Col(A(Wind, 0.7f)).ConeX(12f, 0.35f).Stretch(1.5f, 0.03f).Fade().Scale(-1, 1);
            Ring(r, "Ring", A(Wind, 0.7f), 0.2f, 0.9f, 0.25f, "ring", SortingLayerNames.VFX, 0.6f);
        }

        static void StepDust(GameObject r)
        {
            var d = Spr(r, "Dust", "dust_0", Mat("dust_0", false, 1f), new Color(1, 1, 1, 0.75f), SortingLayerNames.Decal, 3, 0.55f);
            Flip(d, "dust", 1.2f);
        }

        static void Potion(GameObject r, Color c)
        {
            PS(r, "Bubbles", Mat("glow", true, 1.8f)).Burst(14).Life(0.6f, 1f).Speed(0.3f, 0.8f).Size(0.12f, 0.25f)
                .Col(c, Color.white).Circle(0.4f).Vel(0, 0, 1.2f, 2.2f).Fade().At(0, 0.3f).Local();
            PS(r, "Sparkle", Mat("spark4", true, 2f)).Burst(8, 0.1f).Life(0.4f, 0.8f).Speed(0.2f, 0.8f).Size(0.18f, 0.3f)
                .Col(Color.white, c).Circle(0.5f).Fade().At(0, 0.7f).Local();
            Ring(r, "Ring", c, 0.2f, 1.1f, 0.4f, "ring", SortingLayerNames.Decal, 0.5f);
            Light(r, c, 2.5f, 1.2f, 0.7f);
        }

        static void ClickMarker(GameObject r)
        {
            var sr = Spr(r, "Ring", "ring", Mat("ring", true, 1.4f), new Color(0.85f, 1f, 0.8f, 0.9f), SortingLayerNames.Decal, 5);
            sr.transform.localScale = new Vector3(1f, 0.5f, 1f);
            SFX(sr, 0.4f, C(0, 0.45f, 1, 0.08f), C(0, 1, 1, 0.2f));
        }

        static void Respawn(GameObject r)
        {
            var beam = Spr(r, "Beam", "beam", Mat("beam", true, 2f), Holy, SortingLayerNames.VFX, 0, 1f, 0, 2f);
            beam.transform.localScale = new Vector3(3f, 2.2f, 1f);
            SFX(beam, 1.4f, C(0, 1, 1, 1), C(0, 0, 0.1f, 1, 1, 0));
            Ring(r, "Ring", Holy, 0.2f, 2f, 0.7f, "ring", SortingLayerNames.Decal, 0.5f);
            PS(r, "Sparkle", Mat("spark4", true, 2.2f)).Burst(20).Life(0.6f, 1.4f).Speed(0.3f, 1.2f).Size(0.18f, 0.32f)
                .Col(Color.white, Holy).Circle(0.6f).Vel(0, 0, 1f, 2.5f).Fade();
            Light(r, Holy, 4f, 2.2f, 1.2f);
        }

        static void QuestComplete(GameObject r)
        {
            var beam = Spr(r, "Beam", "beam", Mat("beam", true, 2f), Holy, SortingLayerNames.VFX, 0, 1f, 0, 2f);
            beam.transform.localScale = new Vector3(2f, 2.5f, 1f);
            SFX(beam, 1.8f, C(0, 1, 1, 1), C(0, 0, 0.15f, 1, 1, 0));
            PS(r, "Stars", Mat("spark4", true, 2.4f)).Burst(24).Life(0.8f, 1.6f).Speed(1f, 3f).Size(0.2f, 0.35f)
                .Col(Color.white, Holy).ConeUp(35f, 0.3f).Grav(0.5f).Drag(1.5f).Fade().At(0, 0.5f);
            Ring(r, "Ring", Holy, 0.3f, 2.4f, 0.6f, "ring", SortingLayerNames.Decal, 0.5f);
            Light(r, Holy, 4f, 2f, 1.5f);
        }

        // ================================================================== world
        static void RockChips(GameObject r)
        {
            PS(r, "Chips", Mat("debris_0", false, 1f)).Burst(5).Life(0.3f, 0.6f).Speed(2f, 4f).Size(0.18f, 0.28f)
                .ConeUp(60f, 0.3f).Grav(2.5f).Rot().Spin(-400, 400).Fade();
            PS(r, "Dust", Mat("smoke", false, 1f)).Burst(2).Life(0.4f, 0.6f).Size(0.5f, 0.8f).Col(A(Dust, 0.5f)).Circle(0.3f).Fade();
        }

        static void BoulderBreak(GameObject r)
        {
            PS(r, "Chunks", Mat("debris_0", false, 1f)).Burst(14).Life(0.5f, 0.9f).Speed(3f, 7f).Size(0.3f, 0.55f)
                .ConeUp(70f, 0.5f).Grav(3f).Rot().Spin(-500, 500).Fade();
            PS(r, "Chips", Mat("debris_1", false, 1f)).Burst(16).Life(0.4f, 0.8f).Speed(2f, 6f).Size(0.15f, 0.25f)
                .Circle(0.4f).Grav(2f).Rot().Spin(-500, 500).Fade();
            PS(r, "Dust", Mat("smoke", false, 1f), SortingLayerNames.VFX, -1).Burst(8).Life(0.7f, 1.2f).Speed(0.8f, 2f).Size(0.9f, 1.5f)
                .Col(A(Dust, 0.6f)).Circle(0.6f).Drag(2f).Rot().Fade(0.1f);
            for (int i = 0; i < 3; i++)
            {
                var d = Spr(r, "Puff" + i, "dust_0", Mat("dust_0", false, 1f), Color.white, SortingLayerNames.VFX, 1, 1.4f, (i - 1) * 0.6f, -0.2f + (i % 2) * 0.3f);
                Flip(d, "dust", 0.8f);
            }
        }

        static void Pickup(GameObject r, Color c)
        {
            PS(r, "Sparkle", Mat("spark4", true, 2.2f)).Burst(7).Life(0.25f, 0.5f).Speed(1.5f, 3f).Size(0.15f, 0.25f)
                .Col(Color.white, c).Circle(0.1f).Drag(3f).Fade();
            Ring(r, "Ring", c, 0.1f, 0.7f, 0.25f);
        }

        static void EnemyDeath(GameObject r)
        {
            PS(r, "Pixels", Mat("px_square", true, 1.8f)).Burst(18).Life(0.4f, 0.9f).Speed(2f, 5f).Size(0.07f, 0.14f)
                .Col(new Color(0.9f, 0.85f, 1f), new Color(0.6f, 0.5f, 0.8f)).Circle(0.2f).Drag(2f).Grav(0.8f).Fade();
            PS(r, "Soul", Mat("glow", true, 1.8f)).Burst(3, 0.15f).Life(0.8f, 1.3f).Speed(0.1f, 0.3f).Size(0.3f, 0.5f)
                .Col(new Color(0.75f, 0.65f, 1f, 0.8f)).Circle(0.2f).Vel(-0.2f, 0.2f, 1.5f, 2.2f).Noise(0.4f, 1f).Fade(0.1f);
            PS(r, "Smoke", Mat("smoke", false, 1f)).Burst(4).Life(0.5f, 0.9f).Speed(0.3f, 1f).Size(0.6f, 1f)
                .Col(new Color(0.3f, 0.28f, 0.35f, 0.6f)).Circle(0.3f).Fade();
            Light(r, new Color(0.7f, 0.6f, 1f), 2.5f, 1f, 0.5f);
        }

        static void SlimeSplat(GameObject r)
        {
            PS(r, "Drops", Mat("px_square", false, 1.1f)).Burst(14).Life(0.4f, 0.8f).Speed(2f, 5f).Size(0.1f, 0.2f)
                .Col(new Color(0.55f, 0.9f, 0.45f), new Color(0.25f, 0.6f, 0.3f)).ConeUp(70f, 0.3f).Grav(2.5f).Fade();
            var p = Spr(r, "Puff", "poison_0", Mat("poison_0", false, 1f), Color.white, SortingLayerNames.VFX, 0, 1.2f);
            Flip(p, "poison", 1.4f);
        }

        static void SporePuff(GameObject r)
        {
            var p = Spr(r, "Puff", "poison_0", Mat("poison_0", false, 1.1f), Color.white, SortingLayerNames.VFX, 0, 0.9f);
            Flip(p, "poison", 1.4f);
            PS(r, "Spores", Mat("glow", true, 1.6f)).Burst(6).Life(0.4f, 0.8f).Speed(0.5f, 1.5f).Size(0.1f, 0.18f)
                .Col(new Color(0.6f, 1f, 0.35f)).Circle(0.25f).Fade();
        }

        static void SporeHit(GameObject r)
        {
            var p = Spr(r, "Puff", "poison_0", Mat("poison_0", false, 1.1f), Color.white, SortingLayerNames.VFX, 0, 1.4f);
            Flip(p, "poison");
            PS(r, "Spores", Mat("glow", true, 1.6f)).Burst(10).Life(0.5f, 1f).Speed(0.8f, 2f).Size(0.12f, 0.22f)
                .Col(new Color(0.6f, 1f, 0.35f), new Color(0.9f, 1f, 0.5f)).Circle(0.3f).Drag(2f).Fade();
            Light(r, new Color(0.6f, 1f, 0.35f), 2f, 0.8f, 0.4f);
        }

        // ================================================================== swamp
        static readonly Color Venom = new Color(0.72f, 0.32f, 0.95f);
        static readonly Color VenomGreen = new Color(0.55f, 0.95f, 0.35f);
        static readonly Color Water = new Color(0.7f, 0.93f, 1f);
        static readonly Color Mud = new Color(0.45f, 0.34f, 0.22f);
        static readonly Color WispBlue = new Color(0.45f, 0.85f, 1f);
        static readonly Color WispPale = new Color(0.78f, 1f, 1f);

        static readonly Color Silk = new Color(0.92f, 0.94f, 1f);
        static readonly Color CrystalCyan = new Color(0.4f, 0.95f, 1f);

        /// <summary>A ball of web splatting: pale strands flung out and a wisp of silk left hanging.</summary>
        static void WebHit(GameObject r)
        {
            PS(r, "Strands", Mat("px_square", false, 1f)).Burst(14).Life(0.35f, 0.7f).Speed(2f, 4.5f).Size(0.05f, 0.1f)
                .Col(Silk, new Color(0.75f, 0.8f, 0.9f)).Circle(0.2f).Drag(3f).Stretch(1.6f, 0.03f).Fade();
            var splat = Spr(r, "Splat", "ring", Mat("ring", false, 1f), A(Silk, 0.8f), SortingLayerNames.Decal, 2);
            splat.transform.localScale = new Vector3(1f, 0.6f, 1f);
            SFX(splat, 1.1f, C(0, 0.2f, 0.12f, 0.9f, 1, 1f), C(0, 1, 0.6f, 0.8f, 1, 0));
        }

        /// <summary>Crystal shattering (a golem crumbling, a crystal cracked): bright shards and a flash.</summary>
        static void CrystalBurst(GameObject r)
        {
            PS(r, "Shards", Mat("px_square", true, 2f)).Burst(18).Life(0.4f, 0.9f).Speed(2.5f, 6f).Size(0.07f, 0.15f)
                .Col(Color.white, CrystalCyan).ConeUp(80f, 0.3f).Grav(2.2f).Fade();
            PS(r, "Glints", Mat("spark4", true, 2.2f)).Burst(8).Life(0.3f, 0.6f).Speed(0.5f, 1.5f).Size(0.14f, 0.26f)
                .Col(Color.white, CrystalCyan).Circle(0.3f).Fade();
            Light(r, CrystalCyan, 3f, 1.4f, 0.5f);
        }

        /// <summary>A shot glancing off crystal, a pillar struck: a spray of bright splinters.</summary>
        static void CrystalHit(GameObject r)
        {
            PS(r, "Splinters", Mat("px_square", true, 2f)).Burst(10).Life(0.2f, 0.45f).Speed(2.5f, 5.5f).Size(0.05f, 0.1f)
                .Col(Color.white, CrystalCyan).Circle(0.15f).Drag(3f).Stretch(1.4f, 0.03f).Fade();
            var g = Spr(r, "Glint", "spark4", Mat("spark4", true, 2.4f), CrystalCyan, scale: 0.9f);
            SFX(g, 0.25f, C(0, 0.4f, 0.3f, 1.2f, 1, 0.6f), C(0, 1, 1, 0));
            Light(r, CrystalCyan, 2f, 1.2f, 0.25f);
        }

        /// <summary>A crystal pillar (or a crystal slime) shattering: big shards thrown out and falling.</summary>
        static void CrystalShatter(GameObject r)
        {
            PS(r, "Shards", Mat("ice_shard", true, 1.8f)).Burst(16).Life(0.5f, 1f).Speed(3f, 7f).Size(0.25f, 0.5f)
                .Col(Color.white, CrystalCyan).ConeUp(75f, 0.4f).Grav(2.6f).Rot().Spin(-500, 500).Fade();
            PS(r, "Dust", Mat("px_square", true, 1.8f)).Burst(22).Life(0.4f, 0.9f).Speed(1f, 3.5f).Size(0.05f, 0.1f)
                .Col(Color.white, CrystalCyan).Circle(0.5f).Drag(2f).Fade();
            var flash = Spr(r, "Flash", "glow_hard", Mat("glow_hard", true, 2.4f), CrystalCyan, scale: 3f);
            SFX(flash, 0.3f, C(0, 1.1f, 1, 0.8f), C(0, 1, 1, 0));
            Light(r, CrystalCyan, 4.5f, 2.2f, 0.5f);
        }

        /// <summary>Something digging into the ground or bursting out of it (Mimic Tham Lam): clods and a cloud of dust.</summary>
        static void DigDust(GameObject r)
        {
            PS(r, "Clods", Mat("debris_0", false, 1f)).Burst(12).Life(0.4f, 0.8f).Speed(2f, 4.5f).Size(0.18f, 0.32f)
                .ConeUp(70f, 0.4f).Grav(2.6f).Rot().Spin(-300, 300).Fade();
            PS(r, "Dust", Mat("smoke", false, 1f)).Burst(7).Life(0.6f, 1.1f).Speed(0.4f, 1.3f).Size(0.7f, 1.2f)
                .Col(new Color(0.35f, 0.33f, 0.4f, 0.7f)).Circle(0.5f).Fade();
            var crack = Spr(r, "Crack", "crack", Mat("crack", false, 1f), new Color(0.15f, 0.14f, 0.2f, 0.9f), SortingLayerNames.Decal, 0, 0.9f);
            SFX(crack, 1.6f, C(0, 1, 1, 1), C(0, 1, 0.6f, 1, 1, 0));
        }

        /// <summary>Gold taken or given back: coins of light bursting up and falling.</summary>
        static void CoinBurst(GameObject r)
        {
            PS(r, "Coins", Mat("spark4", true, 2.4f)).Burst(18).Life(0.6f, 1.1f).Speed(2f, 4.5f).Size(0.14f, 0.26f)
                .Col(Color.white, Holy).ConeUp(45f, 0.2f).Grav(2.6f).Fade();
            Light(r, Holy, 2.5f, 1.2f, 0.5f);
        }

        /// <summary>A beam of light between two points (Mắt Hang, Tia Pha Lê): LineRenderers set up by <see cref="BeamFX"/>.</summary>
        static void CrystalBeam(GameObject r)
        {
            var beam = r.AddComponent<BeamFX>();
            LineRenderer Line(string name, Color c, int order)
            {
                var go = EditorUtil.Child(r, name);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.widthMultiplier = 0.3f;
                lr.startColor = c;
                lr.endColor = c;
                lr.sharedMaterial = Mat("streak", true, 3f);
                lr.textureMode = LineTextureMode.Stretch;
                lr.numCapVertices = 4;
                lr.sortingLayerName = SortingLayerNames.Top;
                lr.sortingOrder = order;
                return lr;
            }
            beam.glow = Line("Glow", CrystalCyan, 1);
            beam.core = Line("Core", Color.white, 2);
        }

        /// <summary>A crescent of wind cut loose by Thủ Lĩnh Hắc Phong's blades (<see cref="EnemyShots.WindBlade"/>).</summary>
        static GameObject BuildWindBlade()
        {
            var root = new GameObject("WindBlade");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.4f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = true;
            Spr(root, "Core", "proj_windblade", Mat("proj_windblade", true, 1.8f), Color.white, scale: 1.5f, order: 1);
            Spr(root, "Glow", "glow", Mat("glow", true, 1.3f), A(Wind, 0.4f), scale: 1.4f, order: -1);
            PS(root, "Trail", Mat("streak", true, 1.4f), SortingLayerNames.VFX, -2).Loop().Rate(40).Life(0.15f, 0.3f).Size(0.1f, 0.22f)
                .Col(A(Wind, 0.8f), A(Color.white, 0.3f)).Circle(0.35f).Fade();
            Light(root, Wind, 1.6f, 0.7f, pulse: false);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/WindBlade.prefab");
        }

        /// <summary>
        /// Lốc Xoáy (<see cref="EnemyShots.Tornado"/>): a funnel of dust swirling up and out from a
        /// narrow foot, grit whipping round it, a trail of dust left on the ground behind.
        /// </summary>
        static GameObject BuildTornado()
        {
            var root = new GameObject("Tornado");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.9f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = false;
            var sand = new Color(0.94f, 0.84f, 0.62f);
            var cream = new Color(1f, 0.96f, 0.86f);
            // a dark whirl of dust at its foot, turning
            var foot = Spr(root, "Foot", "smoke", Mat("smoke", false, 1f), new Color(0.3f, 0.24f, 0.16f, 0.55f), SortingLayerNames.VFX, -1, 1.5f);
            foot.transform.localScale = new Vector3(1.5f, 0.75f, 1f);
            foot.gameObject.AddComponent<Spinner>().degreesPerSecond = -540f;
            // the funnel itself, spinning in four frames (Tools/ArtGen: gen_steppe_creatures.tornado)
            var body = Spr(root, "Funnel", "tornado_spin_0", AssetFactory.SpriteLit, Color.white, SortingLayerNames.Default, 0, 1.3f);
            var spin = body.gameObject.AddComponent<SpriteAnimator>();
            spin.set = ArtImporter.AnimSet("tornado");
            spin.target = body;
            spin.startClip = "spin";
            PS(root, "Dust", Mat("smoke", false, 1f), SortingLayerNames.VFX, 1).Loop().Local().Rate(40).Life(0.8f, 1.1f).Size(0.4f, 0.7f)
                .Col(A(sand, 0.95f), A(cream, 0.8f)).Circle(0.2f).Vel(-0.25f, 0.25f, 2.4f, 3.2f, true).Noise(1.1f, 1.6f)
                .SizeLife(0, 0.45f, 1, 2.4f).Rot().Fade(0.1f);
            PS(root, "Grit", Mat("px_square", true, 1.5f), SortingLayerNames.VFX, 2).Loop().Local().Rate(40).Life(0.35f, 0.7f).Size(0.06f, 0.12f)
                .Col(sand, Color.white).Circle(0.8f).Orbit(9f, -0.4f).Fade();
            PS(root, "Streaks", Mat("streak", true, 1.6f), SortingLayerNames.VFX, 3).Loop().Local().Rate(30).Life(0.4f, 0.7f).Size(0.12f, 0.22f)
                .Col(A(Color.white, 0.85f), A(cream, 0.6f)).Circle(0.3f).Vel(-0.6f, 0.6f, 1.8f, 3f, true).Noise(1.6f, 2.2f).Fade();
            PS(root, "Trail", Mat("smoke", false, 1f), SortingLayerNames.VFX, -1).Loop().Rate(10).Life(0.6f, 1f).Size(0.5f, 0.9f)
                .Col(A(Dust, 0.45f), A(sand, 0.3f)).Circle(0.4f).SizeLife(0, 0.7f, 1, 1.3f).Rot().Fade(0.15f);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/Tornado.prefab");
        }

        /// <summary>A splinter of crystal in flight (<see cref="EnemyShots.Shard"/>).</summary>
        static GameObject BuildShardShot()
        {
            var root = new GameObject("ShardShot");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.4f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = true;
            Spr(root, "Core", "proj_shard", Mat("proj_shard", false, 1f), Color.white, scale: 1f, order: 1);
            Spr(root, "Glow", "glow", Mat("glow", true, 1.6f), A(CrystalCyan, 0.6f), scale: 0.9f, order: -1);
            PS(root, "Trail", Mat("px_square", true, 1.8f), SortingLayerNames.VFX, -2).Loop().Rate(30).Life(0.15f, 0.3f).Size(0.04f, 0.08f)
                .Col(A(CrystalCyan, 0.9f), A(Color.white, 0.5f)).Circle(0.05f).Fade();
            Light(root, CrystalCyan, 1.6f, 0.8f, pulse: false);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/ShardShot.prefab");
        }

        static readonly Color DarkC = new Color(0.62f, 0.35f, 1f);
        static readonly Color ArcaneC = new Color(0.55f, 0.62f, 1f);
        static readonly Color PinkC = new Color(1f, 0.5f, 0.85f);
        static readonly Color NatureC = new Color(0.45f, 0.85f, 0.35f);

        /// <summary>A pillar of light striking down (Cột Sáng, Phán Quyết Trời Cao; in purple, the dark storms).</summary>
        static void Pillar(GameObject r, Color c)
        {
            var beam = Spr(r, "Beam", "beam", Mat("beam", true, 2f), A(c, 0.9f), scale: 1f, y: 1.6f);
            beam.transform.localScale = new Vector3(0.9f, 3.2f, 1f);
            SFX(beam, 0.6f, C(0, 1.2f, 0.3f, 1f, 1, 0.4f), C(0, 1, 0.5f, 0.8f, 1, 0));
            Ring(r, "Ring", c, 0.2f, 1.6f, 0.5f, "ring", SortingLayerNames.Decal, 0.5f);
            PS(r, "Motes", Mat("spark4", true, 2.2f)).Burst(16).Life(0.4f, 0.9f).Speed(0.5f, 2.2f).Size(0.12f, 0.24f)
                .Col(Color.white, c).Circle(0.5f).Vel(0, 0, 1f, 2.5f).Fade();
            Light(r, c, 4f, 2.2f, 0.6f);
        }

        /// <summary>A small burst where a bolt lands (holy, dark, arcane, a bard's note).</summary>
        static void SmallBurst(GameObject r, Color c)
        {
            var g = Spr(r, "Glow", "glow", Mat("glow", true, 1.8f), A(c, 0.8f), scale: 1.3f);
            SFX(g, 0.35f, C(0, 0.4f, 0.3f, 1.1f, 1, 0.9f), C(0, 1, 1, 0));
            PS(r, "Sparks", Mat("spark4", true, 2.2f)).Burst(12).Life(0.25f, 0.55f).Speed(1.5f, 4f).Size(0.1f, 0.2f)
                .Col(Color.white, c).Circle(0.2f).Drag(2f).Fade();
            Light(r, c, 2.5f, 1.4f, 0.35f);
        }

        /// <summary>Roots and leaves bursting from the ground (Rễ Trói, Bão Gai, Bẫy Gai).</summary>
        static void NatureBurst(GameObject r)
        {
            Ring(r, "Ring", NatureC, 0.3f, 2f, 0.6f, "ring", SortingLayerNames.Decal, 0.5f);
            PS(r, "Leaves", Mat("leaf_green", false, 1f)).Burst(14).Life(0.6f, 1.2f).Speed(1f, 3f).Size(0.2f, 0.35f)
                .ConeUp(70f, 0.4f).Grav(1.5f).Rot().Fade();
            PS(r, "Chips", Mat("debris_0", false, 1f)).Burst(8).Life(0.4f, 0.8f).Speed(1.5f, 3.5f).Size(0.15f, 0.25f)
                .Col(new Color(0.45f, 0.32f, 0.2f)).ConeUp(60f, 0.3f).Grav(3f).Fade();
            Light(r, NatureC, 2.5f, 1f, 0.5f);
        }

        /// <summary>A bomb of thick smoke (Bom Khói).</summary>
        static void SmokeCloud(GameObject r)
        {
            PS(r, "Smoke", Mat("smoke", false, 1f)).Burst(24).Life(1.6f, 2.6f).Speed(0.4f, 1.6f).Size(1.2f, 2.2f)
                .Col(new Color(0.45f, 0.45f, 0.52f, 0.85f), new Color(0.25f, 0.25f, 0.3f, 0.7f)).Circle(0.8f).Drag(1.5f)
                .SizeLife(0, 0.6f, 1, 1.4f).Rot().Fade(0.2f);
            Ring(r, "Ring", new Color(0.8f, 0.8f, 0.85f), 0.3f, 2.8f, 0.4f, "ring", SortingLayerNames.Decal, 0.5f);
        }

        /// <summary>A ring of light turning on the ground while a power lasts (Tinh Linh Hộ Vệ, Hố Đen).</summary>
        static void Aura(GameObject r, Color c)
        {
            var ring = Spr(r, "Ring", "magic_circle", Mat("magic_circle", true, 1.4f), A(c, 0.7f), SortingLayerNames.Decal, 1, 1f);
            ring.transform.localScale = new Vector3(1.4f, 0.7f, 1);
            ring.gameObject.AddComponent<Spinner>().degreesPerSecond = 45f;
            PS(r, "Motes", Mat("spark4", true, 2f)).Loop().Rate(14).Life(0.6f, 1.1f).Size(0.1f, 0.2f).Col(Color.white, c)
                .Circle(1.2f, 360f, 0f).Vel(0, 0, 0.6f, 1.4f).Fade(0.2f).Local();
            Light(r, c, 3f, 1f, pulse: false, flicker: 0.1f);
        }

        /// <summary>A class skill's projectile: its sprite (turned to its flight, or spinning), a glow, a trail and a light.</summary>
        static GameObject BuildBolt(string name, string sprite, bool additive, Color tint, Color glowColor, float scale, bool rotate, float spin, float glowScale)
        {
            var root = new GameObject(name);
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.4f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = rotate;
            var core = Spr(root, "Core", sprite, Mat(sprite, additive, additive ? 2.2f : 1f), tint, scale: scale, order: 1);
            if (spin != 0f) core.gameObject.AddComponent<Spinner>().degreesPerSecond = spin;
            if (glowScale > 0f) Spr(root, "Glow", "glow", Mat("glow", true, 1.6f), A(glowColor, 0.65f), scale: glowScale, order: -1);
            PS(root, "Trail", Mat("glow", true, 1.4f), SortingLayerNames.VFX, -2).Loop().Rate(26).Life(0.18f, 0.35f).Size(0.08f, 0.2f)
                .Col(A(glowColor, 0.75f), A(Color.white, 0.35f)).Circle(0.05f).Fade();
            if (glowScale > 0f) Light(root, glowColor, 1.8f, 0.9f, pulse: false);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/{name}.prefab");
        }

        /// <summary>A boss's chest appearing in the middle of its arena: a golden ring and a puff of glints.</summary>
        static void ChestAppear(GameObject r)
        {
            Ring(r, "Ring", Holy, 0.3f, 1.8f, 0.6f, "ring", SortingLayerNames.Decal, 0.5f);
            PS(r, "Glints", Mat("spark4", true, 2.2f)).Burst(14).Life(0.5f, 1f).Speed(0.6f, 2f).Size(0.14f, 0.26f)
                .Col(Color.white, Holy).Circle(0.6f).Vel(0, 0, 0.5f, 1.2f).Fade().At(0, 0.4f);
            PS(r, "Dust", Mat("smoke", true, 1.2f)).Burst(6).Life(0.4f, 0.7f).Speed(0.4f, 1.2f).Size(0.4f, 0.7f)
                .Col(A(Holy, 0.5f), A(Color.white, 0.2f)).Circle(0.5f).Fade();
            Light(r, Holy, 3.5f, 1.6f, 0.6f);
        }

        /// <summary>A chest bursting open: a column of gold light, coins of light flying up and falling.</summary>
        static void ChestOpen(GameObject r)
        {
            var g = Spr(r, "Glow", "glow", Mat("glow", true, 1.8f), A(Holy, 0.85f), scale: 2.6f, y: 0.2f);
            SFX(g, 1f, C(0, 0.4f, 0.15f, 1.2f, 1, 1f), C(0, 1, 1, 0));
            PS(r, "Coins", Mat("spark4", true, 2.4f)).Burst(26).Life(0.7f, 1.3f).Speed(2.5f, 5f).Size(0.16f, 0.3f)
                .Col(Color.white, Holy).ConeUp(40f, 0.2f).Grav(2.6f).Fade();
            PS(r, "Motes", Mat("px_square", true, 2f)).Burst(20).Life(0.6f, 1.2f).Speed(0.5f, 1.6f).Size(0.05f, 0.1f)
                .Col(Holy, Color.white).Circle(0.5f).Vel(0, 0, 0.8f, 1.8f).Fade();
            Light(r, Holy, 5f, 2.4f, 1f);
        }

        /// <summary>A Ma Trơi guttering out or flaring up: a cold puff and a few rising motes.</summary>
        static void WispBlink(GameObject r)
        {
            PS(r, "Flare", Mat("smoke", true, 1.6f)).Burst(6).Life(0.3f, 0.55f).Speed(0.3f, 1f).Size(0.5f, 0.9f)
                .Col(A(WispBlue, 0.8f), A(WispPale, 0.6f)).Circle(0.2f).SizeLife(0, 0.4f, 1, 1.3f).Rot().Fade();
            PS(r, "Motes", Mat("spark4", true, 2f)).Burst(10).Life(0.4f, 0.8f).Speed(0.5f, 1.8f).Size(0.1f, 0.2f)
                .Col(Color.white, WispBlue).Circle(0.3f).Vel(0, 0, 0.3f, 1f).Fade();
            Light(r, WispBlue, 2.5f, 1.2f, 0.5f);
        }

        /// <summary>A Ma Trơi bursting (radius 2.4 at scale 1): a cold ring, blue flames and frost.</summary>
        static void WispBurst(GameObject r)
        {
            Ring(r, "Ring", WispBlue, 0.3f, 4.8f, 0.5f, "ring_thick");
            Ring(r, "Ground", A(WispPale, 0.7f), 0.3f, 4.6f, 0.7f, "ring", SortingLayerNames.Decal, 0.55f);
            PS(r, "Flames", Mat("smoke", true, 1.8f)).Burst(22).Life(0.45f, 0.9f).Speed(2.5f, 6f).Size(0.7f, 1.2f)
                .Col(WispBlue, WispPale).Circle(0.4f).Drag(3f).Rot().Fade();
            PS(r, "Sparks", Mat("spark4", true, 2.4f)).Burst(24).Life(0.5f, 1.1f).Speed(3f, 7f).Size(0.12f, 0.24f)
                .Col(Color.white, WispBlue).Circle(0.3f).Drag(2f).Fade();
            PS(r, "Frost", Mat("px_square", true, 2f)).Burst(20).Life(0.6f, 1.2f).Speed(1f, 3f).Size(0.06f, 0.12f)
                .Col(Color.white, WispPale).Circle(1f).Grav(-0.3f).Fade();
            Light(r, WispBlue, 7f, 3f, 0.7f);
        }

        static void VenomPuff(GameObject r)
        {
            PS(r, "Puff", Mat("smoke", false, 1f)).Burst(5).Life(0.35f, 0.6f).Speed(0.4f, 1.2f).Size(0.35f, 0.6f)
                .Col(A(Venom, 0.8f), A(VenomGreen, 0.6f)).Circle(0.15f).Rot().SizeLife(0, 0.5f, 1, 1.4f).Fade();
            PS(r, "Drops", Mat("px_square", true, 1.6f)).Burst(6).Life(0.3f, 0.6f).Speed(1f, 2.5f).Size(0.06f, 0.12f)
                .Col(Venom, VenomGreen).Circle(0.1f).Grav(1.5f).Fade();
        }

        static void VenomHit(GameObject r)
        {
            var splash = Spr(r, "Splash", "smoke", Mat("smoke", false, 1f), A(Venom, 0.85f), SortingLayerNames.VFX, 0, 1f);
            SFX(splash, 0.6f, C(0, 0.3f, 1, 1.2f), C(0, 1, 0.5f, 0.8f, 1, 0), 60f);
            PS(r, "Drops", Mat("px_square", true, 1.7f)).Burst(14).Life(0.35f, 0.8f).Speed(2f, 4.5f).Size(0.08f, 0.16f)
                .Col(Venom, VenomGreen).ConeUp(70f, 0.25f).Grav(2.5f).Fade();
            PS(r, "Bubbles", Mat("bubble", false, 1f)).Burst(6).Life(0.5f, 0.9f).Speed(0.3f, 1f).Size(0.12f, 0.24f)
                .Col(A(Venom, 0.9f), A(VenomGreen, 0.8f)).Circle(0.4f).Vel(0, 0, 0.4f, 1f).Fade();
            Light(r, Venom, 2f, 0.9f, 0.4f);
        }

        /// <summary>A poison pool on the ground (radius 1.4 at scale 1), bubbling for as long as the hazard lasts.</summary>
        static void VenomPool(GameObject r)
        {
            float life = HazardZone.PoolSeconds;
            float rad = HazardZone.PoolEffectRadius;
            // a dark, murky puddle with a faint green rim: it must not hide the ground's warnings
            var pool = Spr(r, "Pool", "tele_fill", Mat("tele_fill", false, 1.1f), new Color(0.42f, 0.16f, 0.55f, 0.42f), SortingLayerNames.Decal, 4);
            SFX(pool, life, C(0, 0.35f * rad, 0.04f, rad, 1, rad), C(0, 0, 0.03f, 1, 0.88f, 1, 1, 0));
            var rim = Spr(r, "Rim", "tele_ring", Mat("tele_ring", false, 1.2f), A(VenomGreen, 0.3f), SortingLayerNames.Decal, 5);
            SFX(rim, life, C(0, 0.35f * rad, 0.04f, rad, 1, rad), C(0, 0, 0.03f, 1, 0.88f, 1, 1, 0));
            PS(r, "Bubbles", Mat("bubble", false, 1f)).Loop(life).Rate(7).Life(0.5f, 1f).Size(0.1f, 0.22f)
                .Col(A(Venom, 0.95f), A(VenomGreen, 0.9f)).Circle(1.1f).Vel(0, 0, 0.2f, 0.6f).Fade(0.2f);
            PS(r, "Fumes", Mat("smoke", false, 1f), SortingLayerNames.VFX, -1).Loop(life).Rate(2.5f).Life(1.2f, 2f).Size(0.6f, 1f)
                .Col(A(Venom, 0.25f), A(VenomGreen, 0.2f)).Circle(0.9f).Vel(-0.1f, 0.1f, 0.3f, 0.6f).Rot().Fade(0.25f);
            Light(r, Venom, 2.6f, 0.55f, pulse: false, flicker: 0.15f);
        }

        static void WaterSplash(GameObject r)
        {
            PS(r, "Drops", Mat("px_square", true, 1.4f)).Burst(18).Life(0.4f, 0.8f).Speed(2f, 5f).Size(0.07f, 0.14f)
                .Col(Color.white, Water).ConeUp(55f, 0.35f).Grav(2.6f).Fade();
            PS(r, "Spray", Mat("smoke", false, 1f)).Burst(4).Life(0.4f, 0.7f).Speed(0.5f, 1.5f).Size(0.5f, 0.9f)
                .Col(A(Water, 0.45f)).Circle(0.3f).Vel(0, 0, 0.5f, 1.2f).Rot().Fade();
            Ring(r, "Ring", A(Water, 0.8f), 0.3f, 1.8f, 0.7f, "ring", SortingLayerNames.Decal, 0.6f);
        }

        static void WaterRipple(GameObject r)
        {
            Ring(r, "Ring1", A(Water, 0.7f), 0.2f, 1.3f, 0.9f, "ring", SortingLayerNames.Decal, 0.55f);
            var second = Spr(r, "Ring2", "ring", Mat("ring", true, 1.8f), A(Water, 0.5f), SortingLayerNames.Decal, -1);
            second.transform.localScale = new Vector3(1f, 0.55f, 1f);
            SFX(second, 1f, C(0, 0.05f, 0.3f, 0.1f, 1, 0.9f), C(0, 0, 0.3f, 1, 1, 0));
        }

        static void WaterStep(GameObject r)
        {
            Ring(r, "Ring", A(Water, 0.6f), 0.1f, 0.6f, 0.5f, "ring", SortingLayerNames.Decal, 0.55f);
            PS(r, "Drops", Mat("px_square", true, 1.3f)).Burst(4).Life(0.2f, 0.4f).Speed(1f, 2f).Size(0.05f, 0.09f)
                .Col(Color.white, Water).ConeUp(40f, 0.1f).Grav(2f).Fade();
        }

        static void MudSplat(GameObject r)
        {
            PS(r, "Clods", Mat("px_square", false, 1f)).Burst(16).Life(0.4f, 0.8f).Speed(2f, 5f).Size(0.1f, 0.22f)
                .Col(Mud, new Color(0.32f, 0.24f, 0.16f)).ConeUp(70f, 0.35f).Grav(3f).Fade();
            PS(r, "Muck", Mat("smoke", false, 1f), SortingLayerNames.VFX, -1).Burst(6).Life(0.5f, 0.9f).Speed(1f, 2.5f).Size(0.6f, 1f)
                .Col(A(Mud, 0.7f)).Circle(0.4f, 360, 0f).Drag(3f).Rot().Fade(0.05f);
            var puddle = Spr(r, "Puddle", "tele_fill", Mat("tele_fill", false, 1f), A(Mud, 0.6f), SortingLayerNames.Decal, 3);
            SFX(puddle, 1.2f, C(0, 0.4f, 0.1f, 1.1f, 1, 1.2f), C(0, 1, 0.6f, 0.8f, 1, 0));
        }

        static void TongueLash(GameObject r)
        {
            var tip = Spr(r, "Tongue", "glow_hard", Mat("glow_hard", false, 1.1f), new Color(1f, 0.42f, 0.58f, 0.95f), SortingLayerNames.VFX, 2, 0.45f);
            SFX(tip, 0.35f, C(0, 0.6f, 0.3f, 1f, 1, 0.8f), C(0, 1, 0.6f, 1, 1, 0));
            PS(r, "Spit", Mat("px_square", true, 1.4f)).Burst(3).Life(0.15f, 0.3f).Speed(1f, 2f).Size(0.05f, 0.08f)
                .Col(new Color(1f, 0.7f, 0.8f)).Circle(0.1f).Fade();
        }

        static void WaystoneWake(GameObject r)
        {
            var c = new Color(0.45f, 1f, 0.95f);
            var beam = Spr(r, "Beam", "beam", Mat("beam", true, 2f), c, SortingLayerNames.VFX, 0, 1f, 0, 1.4f);
            beam.transform.localScale = new Vector3(1.6f, 2.4f, 1f);
            SFX(beam, 1.6f, C(0, 1, 1, 1), C(0, 0, 0.1f, 1, 1, 0));
            Ring(r, "Ring", c, 0.3f, 3f, 0.9f, "ring", SortingLayerNames.Decal, 0.55f);
            PS(r, "Sparkle", Mat("spark4", true, 2.2f)).Burst(22).Life(0.7f, 1.5f).Speed(0.4f, 1.6f).Size(0.16f, 0.3f)
                .Col(Color.white, c).Circle(0.5f).Vel(0, 0, 1f, 2.4f).Fade();
            Light(r, c, 4.5f, 2f, 1.4f);
        }

        // ================================================================== boss
        static void BossRoar(GameObject r)
        {
            PS(r, "Waves", Mat("ring", true, 1.6f)).Burst(1).Burst(1, 0.15f).Burst(1, 0.3f).Life(0.55f, 0.55f).Size(1f, 1f)
                .Col(new Color(1f, 0.5f, 0.45f, 0.8f)).SizeLife(0, 0.5f, 1, 6f).Fade();
            PS(r, "Dust", Mat("smoke", false, 1f), SortingLayerNames.VFX, -1).Burst(16).Life(0.8f, 1.4f).Speed(2f, 4f).Size(0.9f, 1.4f)
                .Col(A(Dust, 0.55f)).Circle(1.5f, 360, 0f).Drag(2f).Rot().Fade(0.1f).At(0, -2.2f);
            PS(r, "Leaves", Mat("leaf_autumn", false, 1f)).Burst(14).Life(1.2f, 2f).Speed(2f, 5f).Size(0.25f, 0.35f)
                .Circle(1f).Drag(2f).Grav(0.25f).Rot().Spin(-300, 300).Noise(0.5f).Fade();
            Light(r, new Color(1f, 0.4f, 0.3f), 6f, 2f, 0.8f);
        }

        static void EnrageBurst(GameObject r)
        {
            Ring(r, "Ring1", Enrage, 0.3f, 4f, 0.5f, "ring_thick");
            Ring(r, "Ring2", new Color(1f, 0.5f, 1f), 0.2f, 2.8f, 0.4f, "ring");
            PS(r, "Flames", Mat("smoke", true, 1.8f)).Burst(24).Life(0.5f, 1f).Speed(3f, 7f).Size(0.8f, 1.4f)
                .Col(Enrage, new Color(1f, 0.4f, 0.8f)).Circle(0.6f).Drag(3f).Rot().Fade();
            PS(r, "Sparks", Mat("px_square", true, 3f)).Burst(30).Life(0.4f, 1f).Speed(5f, 11f).Size(0.07f, 0.14f)
                .Col(Color.white, Enrage).Circle(0.4f).Drag(2f).Stretch(1.8f, 0.03f).Fade();
            Light(r, Enrage, 9f, 3.5f, 1.2f);
        }

        static void EnrageAura(GameObject r)
        {
            PS(r, "Flames", Mat("smoke", true, 1.5f), SortingLayerNames.VFX, -1).Loop().Rate(22).Life(0.6f, 1f).Size(0.7f, 1.2f)
                .Col(A(Enrage, 0.7f), new Color(1f, 0.35f, 0.7f, 0.6f)).Circle(1.3f, 360, 0.3f).Vel(0, 0, 1.5f, 2.8f)
                .SizeLife(0, 0.6f, 0.4f, 1f, 1, 0f).Rot().Spin(-60, 60).Fade(0.15f).At(0, 0.6f);
            PS(r, "Motes", Mat("px_square", true, 2.6f)).Loop().Rate(16).Life(0.6f, 1.2f).Size(0.06f, 0.12f)
                .Col(Color.white, Enrage).Circle(1.5f).Vel(-0.3f, 0.3f, 1.5f, 3f).Noise(0.5f).Fade().At(0, 0.8f);
            var ground = Spr(r, "Ground", "glow", Mat("glow", true, 1.3f), A(Enrage, 0.6f), SortingLayerNames.Decal, 0, 1f);
            ground.transform.localScale = new Vector3(4.5f, 2.2f, 1f);
            Light(r, Enrage, 5f, 1.3f, pulse: false, flicker: 0.3f);
        }

        static void ClawSwipe(GameObject r)
        {
            var c = Spr(r, "Claw", "claw_0", Mat("claw_0", true, 2.4f), new Color(1f, 0.55f, 0.5f), scale: 1f);
            Flip(c, "claw");
            PS(r, "Sparks", Mat("px_square", true, 2.5f)).Burst(12).Life(0.15f, 0.35f).Speed(3f, 7f).Size(0.06f, 0.12f)
                .Col(Color.white, new Color(1f, 0.5f, 0.4f)).Circle(0.8f).Drag(3f).Stretch(1.4f, 0.03f).Fade();
            Light(r, new Color(1f, 0.45f, 0.4f), 3f, 1.5f, 0.3f);
        }

        static void Shockwave(GameObject r, float scale)
        {
            float s = scale;
            var crack = Spr(r, "Crack", "crack", Mat("crack", false, 1f), Color.white, SortingLayerNames.Decal, 1, 1.6f * s);
            SFX(crack, 3f, C(0, 0.8f, 0.1f, 1f, 1, 1f), C(0, 1, 0.7f, 1, 1, 0));
            var ring = Spr(r, "Wave", "ring_thick", Mat("ring_thick", false, 1.2f), new Color(0.95f, 0.88f, 0.75f, 0.85f), SortingLayerNames.Decal, 2);
            ring.transform.localScale = new Vector3(1f, 0.62f, 1f);
            SFX(ring, 0.45f, C(0, 0.4f * s, 1, 5.4f * s), C(0, 1, 0.6f, 0.6f, 1, 0));
            var ring2 = Spr(r, "Wave2", "ring", Mat("ring", true, 1.6f), new Color(1f, 0.85f, 0.6f, 0.8f), SortingLayerNames.VFX, -1);
            ring2.transform.localScale = new Vector3(1f, 0.62f, 1f);
            SFX(ring2, 0.3f, C(0, 0.3f * s, 1, 4.6f * s), C(0, 1, 1, 0));
            PS(r, "Debris", Mat("debris_0", false, 1f)).Burst(Mathf.RoundToInt(26 * s)).Life(0.5f, 1f).Speed(3f, 8f).Size(0.2f, 0.4f)
                .Circle(0.8f * s).Grav(3f).Vel(0, 0, 2f, 5f).Rot().Spin(-500, 500).Fade();
            PS(r, "DustRing", Mat("smoke", false, 1f), SortingLayerNames.VFX, -2).Burst(Mathf.RoundToInt(22 * s)).Life(0.7f, 1.3f)
                .Speed(4f * s, 7f * s).Size(1f * s, 1.7f * s).Col(A(Dust, 0.6f)).Circle(1f * s, 360, 0f).Drag(4f).Rot().Spin(-40, 40)
                .Fade(0.05f).Scale(1f, 0.62f);
            PS(r, "Leaves", Mat("leaf_green", false, 1f)).Burst(Mathf.RoundToInt(10 * s)).Life(1f, 1.8f).Speed(2f, 5f).Size(0.25f, 0.35f)
                .Circle(1.5f * s).Drag(2f).Grav(0.3f).Rot().Spin(-300, 300).Fade();
            Light(r, new Color(1f, 0.8f, 0.55f), 6f * s, 2.2f, 0.4f);
        }

        static void RockImpact(GameObject r)
        {
            var crack = Spr(r, "Crack", "crack", Mat("crack", false, 1f), Color.white, SortingLayerNames.Decal, 1, 0.8f);
            SFX(crack, 2.4f, C(0, 1, 1, 1), C(0, 1, 0.7f, 1, 1, 0));
            var ring = Spr(r, "Wave", "ring_thick", Mat("ring_thick", false, 1.1f), new Color(0.95f, 0.88f, 0.75f, 0.8f), SortingLayerNames.Decal, 2);
            ring.transform.localScale = new Vector3(1f, 0.62f, 1f);
            SFX(ring, 0.35f, C(0, 0.3f, 1, 2.4f), C(0, 1, 1, 0));
            PS(r, "Debris", Mat("debris_0", false, 1f)).Burst(14).Life(0.5f, 0.9f).Speed(3f, 6f).Size(0.2f, 0.35f)
                .ConeUp(70f, 0.4f).Grav(3f).Rot().Spin(-500, 500).Fade();
            PS(r, "Dust", Mat("smoke", false, 1f), SortingLayerNames.VFX, -1).Burst(10).Life(0.6f, 1.1f).Speed(2f, 4f).Size(0.8f, 1.3f)
                .Col(A(Dust, 0.6f)).Circle(0.5f, 360, 0f).Drag(3f).Rot().Fade(0.05f);
        }

        static void BossDeath(GameObject r)
        {
            var beam = Spr(r, "Pillar", "beam", Mat("beam", true, 2.4f), Enrage, SortingLayerNames.Top, 0, 1f, 0, 1.5f);
            beam.transform.localScale = new Vector3(5f, 4f, 1f);
            SFX(beam, 2.5f, C(0, 0.6f, 0.2f, 1f, 1, 1.2f), C(0, 0, 0.1f, 1, 0.6f, 0.8f, 1, 0));
            Ring(r, "Ring1", Enrage, 0.3f, 7f, 0.8f, "ring_thick");
            Ring(r, "Ring2", Color.white, 0.2f, 5f, 0.5f, "ring");
            PS(r, "Sparks", Mat("px_square", true, 3f)).Burst(60).Life(0.6f, 1.6f).Speed(4f, 12f).Size(0.08f, 0.16f)
                .Col(Color.white, Enrage).Circle(0.8f).Drag(1.8f).Grav(0.4f).Stretch(1.6f, 0.03f).Fade();
            PS(r, "Souls", Mat("glow", true, 2f)).Burst(12, 0.3f).Life(1.5f, 2.5f).Speed(0.3f, 1f).Size(0.4f, 0.8f)
                .Col(new Color(0.8f, 0.6f, 1f, 0.9f)).Circle(1f).Vel(-0.5f, 0.5f, 2f, 3.5f).Noise(0.7f, 0.8f).Fade(0.1f);
            PS(r, "Smoke", Mat("smoke", false, 1f), SortingLayerNames.VFX, -2).Burst(18).Life(1.2f, 2.2f).Speed(1f, 3f).Size(1.5f, 2.6f)
                .Col(new Color(0.2f, 0.12f, 0.28f, 0.7f)).Circle(1.2f).Drag(1.5f).Rot().Spin(-30, 30).Fade(0.1f);
            PS(r, "Gold", Mat("spark4", true, 2.4f)).Burst(24, 1f).Life(1f, 2f).Speed(1f, 3f).Size(0.2f, 0.35f)
                .Col(Color.white, Holy).ConeUp(40f, 0.5f).Drag(1f).Grav(0.3f).Fade();
            Light(r, Enrage, 12f, 4.5f, 2f);
        }

        // ================================================================== ambient (scene object, not pooled)
        /// <summary>Fireflies, falling leaves and pollen that follow the camera.</summary>
        public static AmbientParticles BuildAmbient(GameObject root)
        {
            Mats.Clear();
            var amb = root.AddComponent<AmbientParticles>();
            amb.fireflies = PS(root, "Fireflies", Mat("glow", true, 2.2f), SortingLayerNames.VFX, 0).Loop(4f).Rate(10).Life(4f, 7f)
                .Size(0.1f, 0.2f).Col(new Color(0.85f, 1f, 0.45f), new Color(1f, 0.95f, 0.55f)).Box(40f, 24f)
                .Noise(0.5f, 0.35f).Max(160).Fade(0.25f).ps;
            amb.leaves = PS(root, "Leaves", Mat("leaf_autumn", false, 1f), SortingLayerNames.Top, 0).Loop(5f).Rate(2.5f).Life(6f, 9f)
                .Size(0.28f, 0.4f).Box(42f, 26f).Vel(-0.6f, -0.2f, -0.9f, -0.5f).Rot().Spin(-120, 120).Noise(0.4f, 0.4f).Max(60).Fade(0.1f).ps;
            amb.motes = PS(root, "Motes", Mat("px_square", true, 1.6f), SortingLayerNames.VFX, 0).Loop(4f).Rate(6).Life(4f, 8f)
                .Size(0.04f, 0.07f).Col(new Color(1f, 1f, 0.9f, 0.7f)).Box(40f, 24f).Vel(0.1f, 0.3f, 0.05f, 0.2f).Noise(0.3f, 0.3f)
                .Max(80).Fade(0.25f).ps;
            return amb;
        }

        // ================================================================== gameplay prefabs
        static GameObject BuildFireball()
        {
            var root = new GameObject("Fireball");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.6f;
            root.AddComponent<Projectile>();
            var core = Spr(root, "Core", "fireball_0", Mat("fireball_0", true, 2.4f), Color.white, scale: 1.1f);
            Flip(core, "fireball");
            var glow = Spr(root, "Glow", "glow", Mat("glow", true, 1.8f), FireMid, scale: 1.8f, order: -1);
            PS(root, "Trail", Mat("fire_0", true, 1.9f), SortingLayerNames.VFX, -2).Loop().Rate(55).Life(0.2f, 0.4f).Size(0.35f, 0.6f)
                .SizeLife(0, 1f, 1, 0.2f).Circle(0.12f).Sheet(8).Rot();
            PS(root, "Sparks", Mat("px_square", true, 2.6f)).Loop().Rate(30).Life(0.2f, 0.45f).Speed(0.5f, 2f).Size(0.05f, 0.1f)
                .Col(FireCore, FireMid).Circle(0.15f).Fade();
            PS(root, "Smoke", Mat("smoke", false, 1f), SortingLayerNames.VFX, -3).Loop().Rate(10).Life(0.4f, 0.7f).Size(0.4f, 0.7f)
                .Col(new Color(0.25f, 0.2f, 0.22f, 0.4f)).Circle(0.1f).Fade(0.1f);
            Light(root, FireMid, 3.5f, 1.6f, pulse: false, flicker: 0.25f);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/Fireball.prefab");
        }

        static GameObject BuildSpore()
        {
            var root = new GameObject("Spore");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.5f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = false;
            var core = Spr(root, "Core", "glow_hard", Mat("glow_hard", true, 2f), new Color(0.65f, 1f, 0.35f), scale: 0.55f);
            var puff = Spr(root, "Puff", "poison_1", Mat("poison_1", false, 1.1f), Color.white, scale: 0.5f, order: 1);
            puff.gameObject.AddComponent<Spinner>().degreesPerSecond = 200f;
            PS(root, "Trail", Mat("glow", true, 1.4f), SortingLayerNames.VFX, -1).Loop().Rate(30).Life(0.3f, 0.5f).Size(0.12f, 0.25f)
                .Col(new Color(0.55f, 0.95f, 0.3f, 0.8f)).Circle(0.08f).Fade();
            Light(root, new Color(0.6f, 1f, 0.35f), 1.8f, 0.8f, pulse: false);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/Spore.prefab");
        }

        static GameObject BuildVenom()
        {
            var root = new GameObject("Venom");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.5f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = false;
            Spr(root, "Core", "glow_hard", Mat("glow_hard", true, 2f), Venom, scale: 0.55f);
            var drop = Spr(root, "Drop", "bubble", Mat("bubble", false, 1.1f), new Color(0.85f, 0.6f, 1f), scale: 0.45f, order: 1);
            drop.gameObject.AddComponent<Spinner>().degreesPerSecond = 160f;
            PS(root, "Trail", Mat("glow", true, 1.4f), SortingLayerNames.VFX, -1).Loop().Rate(30).Life(0.3f, 0.5f).Size(0.12f, 0.25f)
                .Col(A(Venom, 0.8f), A(VenomGreen, 0.7f)).Circle(0.08f).Fade();
            Light(root, Venom, 1.8f, 0.8f, pulse: false);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/Venom.prefab");
        }

        /// <summary>A ball of sticky web (Nhện Hang's), spinning as it flies.</summary>
        static GameObject BuildWebShot()
        {
            var root = new GameObject("WebShot");
            root.layer = Layers.Projectile;
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.4f;
            var p = root.AddComponent<Projectile>();
            p.rotateToDirection = false;
            var ball = Spr(root, "Ball", "ring", Mat("ring", false, 1.1f), Silk, scale: 0.5f);
            ball.gameObject.AddComponent<Spinner>().degreesPerSecond = 420f;
            Spr(root, "Core", "glow_hard", Mat("glow_hard", false, 1f), A(Silk, 0.85f), scale: 0.3f, order: 1);
            PS(root, "Trail", Mat("px_square", false, 1f), SortingLayerNames.VFX, -1).Loop().Rate(26).Life(0.25f, 0.45f).Size(0.04f, 0.08f)
                .Col(A(Silk, 0.9f), new Color(0.7f, 0.75f, 0.85f, 0.6f)).Circle(0.1f).Fade();
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/WebShot.prefab");
        }

        static GameObject BuildVenomGlob()
        {
            var root = new GameObject("VenomGlob");
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.3f;
            var arc = root.AddComponent<ArcProjectile>();
            var shadow = Spr(root, "Shadow", "shadow", AssetFactory.SpriteUnlit, new Color(1, 1, 1, 0.7f), SortingLayerNames.Decal, 4);
            shadow.transform.localScale = new Vector3(0.9f, 0.9f, 1);
            var visual = EditorUtil.Child(root, "Visual");
            Spr(visual, "Core", "glow_hard", Mat("glow_hard", true, 2f), Venom, SortingLayerNames.VFX, 5, 0.7f);
            Spr(visual, "Drop", "bubble", Mat("bubble", false, 1.1f), new Color(0.85f, 0.6f, 1f), SortingLayerNames.VFX, 6, 0.55f);
            PS(visual, "Trail", Mat("glow", true, 1.4f)).Loop().Rate(26).Life(0.25f, 0.45f).Size(0.12f, 0.24f)
                .Col(A(Venom, 0.8f), A(VenomGreen, 0.7f)).Circle(0.1f).Fade();
            Light(visual, Venom, 1.8f, 0.7f, pulse: false);
            arc.visual = visual.transform;
            arc.shadow = shadow;
            arc.height = 2.6f;
            arc.spin = 0f;
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/VenomGlob.prefab");
        }

        static GameObject BuildRockProjectile()
        {
            var root = new GameObject("BossRock");
            var fx = root.AddComponent<PooledFX>();
            fx.lifetime = 0f;
            fx.stopLinger = 0.2f;
            var arc = root.AddComponent<ArcProjectile>();
            var shadow = Spr(root, "Shadow", "shadow", AssetFactory.SpriteUnlit, new Color(1, 1, 1, 0.8f), SortingLayerNames.Decal, 4);
            shadow.transform.localScale = new Vector3(1.4f, 1.4f, 1);
            var visual = EditorUtil.Child(root, "Visual");
            var rock = Spr(visual, "Rock", "boulder", AssetFactory.SpriteLit, Color.white, SortingLayerNames.VFX, 5);
            rock.transform.localPosition = new Vector3(0, -0.6f, 0);
            PS(visual, "Dust", Mat("debris_1", false, 1f)).Loop().Rate(14).Life(0.3f, 0.5f).Size(0.12f, 0.2f).Grav(1.5f).Circle(0.4f).Rot().Fade();
            arc.visual = visual.transform;
            arc.shadow = shadow;
            arc.height = 3.2f;
            arc.spin = 0f;
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/BossRock.prefab");
        }

        static GameObject BuildTelegraph()
        {
            var root = new GameObject("Telegraph");
            var tg = root.AddComponent<Telegraph>();
            var mat = Mat("tele_ring", false, 1.3f);
            tg.ring = Spr(root, "Ring", "tele_ring", Mat("tele_ring", true, 1.5f), Palette.Telegraph, SortingLayerNames.Decal, 10);
            tg.fill = Spr(root, "Fill", "tele_fill", Mat("tele_fill", false, 1.2f), Palette.Telegraph, SortingLayerNames.Decal, 9);
            tg.coneOutline = Spr(root, "ConeOutline", "tele_cone", Mat("tele_cone", false, 1.2f), Palette.Telegraph, SortingLayerNames.Decal, 9);
            tg.coneFill = Spr(root, "ConeFill", "tele_cone", Mat("tele_cone", true, 1.4f), Palette.Telegraph, SortingLayerNames.Decal, 10);
            return EditorUtil.SavePrefab(root, $"{GameplayFolder}/Telegraph.prefab");
        }
    }
}
