using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;

namespace RPG.EditorTools
{
    /// <summary>Creates shared assets: materials, font, items, skills, database, audio library, volume profiles.</summary>
    public static class AssetFactory
    {
        public const string MatFolder = "Assets/Materials";
        public const string DataFolder = "Assets/Data";
        public const string VolumeFolder = "Assets/Settings/Volumes";

        public static Material SpriteLit => AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
        public static Material SpriteUnlit => AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
        public static Material Silhouette => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/Silhouette.mat");
        public static Material Additive => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/SpriteAdditive.mat");
        public static Material AlphaUnlit => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/SpriteAlphaUnlit.mat");
        public static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Inter SDF.asset");
        public static Material FontOutline => AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Inter SDF - Outline.mat");
        public static Material FontShadow => AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Inter SDF - Shadow.mat");

        [MenuItem("Tools/RPG/Steps/3. Rebuild Data (font, items, skills, volumes, audio)", priority = 103)]
        public static void CreateAll()
        {
            CreateMaterials();
            CreateFont();
            CreateVolumes();
            ConfigureAudio();
            var items = CreateItems();
            var skills = CreateSkills();
            CreateDatabase(items, skills);
            CreateAudioLibrary();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ materials
        static Material MakeMat(string name, string shader, System.Action<Material> setup = null)
        {
            string path = $"{MatFolder}/{name}.mat";
            EditorUtil.EnsureFolder(MatFolder);
            var sh = Shader.Find(shader);
            if (sh == null)
            {
                Debug.LogError("[RPG] Shader not found: " + shader);
                return null;
            }
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void CreateMaterials()
        {
            MakeMat("Silhouette", "RPG/Sprite Silhouette", m => m.SetFloat("_Intensity", 1.35f));
            MakeMat("SpriteAdditive", "RPG/VFX Additive", m => m.SetFloat("_Intensity", 1.6f));
            MakeMat("SpriteAlphaUnlit", "RPG/VFX Alpha", m => m.SetFloat("_Intensity", 1f));
        }

        // ------------------------------------------------------------------ font
        static void CreateFont()
        {
            const string path = "Assets/Fonts/Inter SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Inter-SemiBold.ttf");
                fa = TMP_FontAsset.CreateFontAsset(font, 64, 7, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                fa.name = "Inter SDF";
                AssetDatabase.CreateAsset(fa, path);
                fa.atlasTextures[0].name = "Inter SDF Atlas";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
                fa.material.name = "Inter SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                // pre-populate the Vietnamese alphabet so the atlas is ready in builds
                fa.TryAddCharacters(VietnameseCharset(), out string missing);
                EditorUtility.SetDirty(fa);
                AssetDatabase.SaveAssets();
            }
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset");
            if (fallback != null)
            {
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(fallback)) fa.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(fa);
            }

            // presets: outline and soft drop shadow
            var sdf = Shader.Find("TextMeshPro/Distance Field");
            Material Preset(string name, System.Action<Material> setup)
            {
                string p = $"Assets/Fonts/Inter SDF - {name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null)
                {
                    m = new Material(fa.material);
                    AssetDatabase.CreateAsset(m, p);
                }
                else m.CopyPropertiesFromMaterial(fa.material);
                if (sdf != null) m.shader = sdf;
                m.SetTexture("_MainTex", fa.atlasTextures[0]);
                setup(m);
                EditorUtility.SetDirty(m);
                return m;
            }
            Preset("Outline", m =>
            {
                m.SetFloat("_OutlineWidth", 0.24f);
                m.SetColor("_OutlineColor", new Color(0.06f, 0.04f, 0.08f, 1f));
                m.SetFloat("_FaceDilate", 0.18f);
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.55f));
                m.SetFloat("_UnderlayOffsetX", 0.55f);
                m.SetFloat("_UnderlayOffsetY", -0.65f);
                m.SetFloat("_UnderlayDilate", 0.2f);
                m.SetFloat("_UnderlaySoftness", 0.15f);
            });
            Preset("Shadow", m =>
            {
                m.SetFloat("_OutlineWidth", 0.08f);
                m.SetColor("_OutlineColor", new Color(0.05f, 0.03f, 0.07f, 1f));
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0, 0, 0, 0.6f));
                m.SetFloat("_UnderlayOffsetX", 0.45f);
                m.SetFloat("_UnderlayOffsetY", -0.5f);
                m.SetFloat("_UnderlaySoftness", 0.3f);
            });

            // make it the project default
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var p = so.FindProperty("m_defaultFontAsset");
                if (p != null) p.objectReferenceValue = fa;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static string VietnameseCharset()
        {
            const string basic = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            const string viet = "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯưẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ";
            const string symbols = "◆◇►»•★☆✓♥·…→←↑↓×";
            return basic + viet + symbols;
        }

        // ------------------------------------------------------------------ post processing
        static VolumeProfile Profile(string name, System.Action<VolumeProfile> fill)
        {
            EditorUtil.EnsureFolder(VolumeFolder);
            string path = $"{VolumeFolder}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            fill(p);
            foreach (var c in p.components)
            {
                c.name = c.GetType().Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        static void CreateVolumes()
        {
            Profile("GameVolume", p =>
            {
                var bloom = p.Add<Bloom>();
                bloom.threshold.Override(0.92f);
                bloom.intensity.Override(1.1f);
                bloom.scatter.Override(0.62f);
                bloom.highQualityFiltering.Override(true);
                var ca = p.Add<ColorAdjustments>();
                ca.contrast.Override(8f);
                ca.saturation.Override(10f);
                var vig = p.Add<Vignette>();
                vig.intensity.Override(0.24f);
                vig.smoothness.Override(0.45f);
                vig.color.Override(new Color(0.05f, 0.02f, 0.08f));
            });
            Profile("ImpactVolume", p =>
            {
                var ch = p.Add<ChromaticAberration>();
                ch.intensity.Override(0.85f);
                var ld = p.Add<LensDistortion>();
                ld.intensity.Override(-0.22f);
                ld.scale.Override(1.02f);
            });
            Profile("DangerVolume", p =>
            {
                var vig = p.Add<Vignette>();
                vig.intensity.Override(0.5f);
                vig.smoothness.Override(0.65f);
                vig.color.Override(new Color(0.55f, 0.02f, 0.04f));
                var ca = p.Add<ColorAdjustments>();
                ca.saturation.Override(-25f);
            });
        }

        public static VolumeProfile LoadProfile(string name) => AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{VolumeFolder}/{name}.asset");

        // ------------------------------------------------------------------ audio
        static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ai = AssetImporter.GetAtPath(p) as AudioImporter;
                if (ai == null) continue;
                bool music = p.Contains("/Music/");
                var s = ai.defaultSampleSettings;
                s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
                s.quality = 0.6f;
                ai.defaultSampleSettings = s;
                ai.forceToMono = !music;
                ai.loadInBackground = music;
                ai.SaveAndReimport();
            }
        }

        static void CreateAudioLibrary()
        {
            string path = DataFolder + "/AudioLibrary.asset";
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(path);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                EditorUtil.EnsureFolder(DataFolder);
                AssetDatabase.CreateAsset(lib, path);
            }
            lib.clips = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).OrderBy(c => c.name).ToList();
            EditorUtility.SetDirty(lib);
            Debug.Log($"[RPG] Audio library: {lib.clips.Count} clips");
        }

        // ------------------------------------------------------------------ items
        static ItemDef Item(string id, string name, ItemKind kind, ItemRarity rarity, string desc, int value = 1,
                            float heal = 0, float energy = 0, bool cleanse = false, int maxStack = 99)
        {
            string path = $"{DataFolder}/Items/{id}.asset";
            EditorUtil.EnsureFolder(DataFolder + "/Items");
            var it = AssetDatabase.LoadAssetAtPath<ItemDef>(path);
            if (it == null)
            {
                it = ScriptableObject.CreateInstance<ItemDef>();
                AssetDatabase.CreateAsset(it, path);
            }
            it.id = id;
            it.displayName = name;
            it.kind = kind;
            it.rarity = rarity;
            it.description = desc;
            it.value = value;
            it.healAmount = heal;
            it.energyAmount = energy;
            it.cleanse = cleanse;
            it.maxStack = maxStack;
            it.icon = ArtImporter.S(id == "gold" ? "gold" : id);
            EditorUtility.SetDirty(it);
            return it;
        }

        static List<ItemDef> CreateItems()
        {
            var C = ItemKind.Consumable;
            var M = ItemKind.Material;
            return new List<ItemDef>
            {
                Item("coin", "Đồng Vàng", ItemKind.Currency, ItemRarity.Common, "Tiền tệ của Làng Lá Xanh.", 1),
                Item("gold", "Túi Vàng", ItemKind.Currency, ItemRarity.Uncommon, "Một túi vàng nặng trĩu.", 50),
                Item("potion_red", "Bình Máu", C, ItemRarity.Common, "Hồi phục 60 Máu.", 10, heal: 60, maxStack: 20),
                Item("potion_blue", "Bình Năng Lượng", C, ItemRarity.Common, "Hồi phục 40 Năng lượng.", 10, energy: 40, maxStack: 20),
                Item("potion_green", "Thuốc Thảo Mộc", C, ItemRarity.Uncommon, "Hồi 30 Máu, 25 Năng lượng và giải mọi hiệu ứng xấu.", 25, heal: 30, energy: 25, cleanse: true, maxStack: 20),
                Item("gel", "Gel Slime", M, ItemRarity.Common, "Chất nhầy xanh lấp lánh. Thợ rèn rất thích thứ này.", 2),
                Item("shroom_cap", "Mũ Nấm Đỏ", M, ItemRarity.Common, "Mũ nấm đỏ chót của Nấm Độc. Bé Mai đang cần.", 3),
                Item("claw", "Vuốt Gấu Ma", M, ItemRarity.Rare, "Móng vuốt của Gấu Ma Rừng Già, vẫn còn tỏa khí lạnh.", 80),
                Item("pelt", "Da Gấu Ma", M, ItemRarity.Rare, "Tấm da đen tuyền, ấm và dai như thép.", 120),
                Item("gem_red", "Hồng Ngọc", M, ItemRarity.Rare, "Viên đá đỏ rực, ẩn chứa sức mạnh lửa.", 150),
                Item("gem_blue", "Lam Ngọc", M, ItemRarity.Rare, "Viên đá xanh thẳm, mát lạnh khi cầm.", 150),
                Item("ring", "Nhẫn Trưởng Làng", ItemKind.Equipment, ItemRarity.Epic, "Chiếc nhẫn gia truyền, dành cho người hùng của làng.", 500, maxStack: 1),
                Item("meat", "Thịt Nướng", C, ItemRarity.Common, "Thơm lừng. Hồi 20 Máu.", 4, heal: 20),
                Item("apple", "Táo Đỏ", C, ItemRarity.Common, "Giòn ngọt. Hồi 10 Máu.", 2, heal: 10),
                Item("bread", "Bánh Mì", C, ItemRarity.Common, "Bánh mì nóng giòn. Hồi 15 Máu.", 3, heal: 15),
                Item("herb", "Cỏ Thuốc", M, ItemRarity.Common, "Loại cỏ dùng để chế thuốc.", 2),
                Item("bone", "Xương", M, ItemRarity.Common, "Một khúc xương cũ.", 1),
                Item("key", "Chìa Khóa Cổ", ItemKind.Quest, ItemRarity.Uncommon, "Chìa khóa bằng đồng, khắc hoa văn lạ.", 0, maxStack: 1),
                Item("scroll", "Cuộn Giấy Cổ", ItemKind.Quest, ItemRarity.Uncommon, "Ghi chép về Rừng Già Cổ Thụ.", 0, maxStack: 1),
                Item("sword", "Kiếm Sắt", ItemKind.Equipment, ItemRarity.Common, "Thanh kiếm sắt đáng tin cậy.", 30, maxStack: 1),
                Item("shield", "Khiên Gỗ", ItemKind.Equipment, ItemRarity.Common, "Chiếc khiên gỗ bọc sắt.", 25, maxStack: 1),
            };
        }

        // ------------------------------------------------------------------ skills
        static T Skill<T>(string id, string name, string icon, float cd, float cost, string anim, float lockTime, string desc, System.Action<T> tune = null) where T : SkillDef
        {
            string path = $"{DataFolder}/Skills/{id}.asset";
            EditorUtil.EnsureFolder(DataFolder + "/Skills");
            var s = AssetDatabase.LoadAssetAtPath<T>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(s, path);
            }
            s.id = id;
            s.displayName = name;
            s.icon = ArtImporter.S(icon);
            s.cooldown = cd;
            s.energyCost = cost;
            s.animBase = anim;
            s.lockTime = lockTime;
            s.description = desc;
            tune?.Invoke(s);
            EditorUtility.SetDirty(s);
            return s;
        }

        static List<SkillDef> CreateSkills()
        {
            return new List<SkillDef>
            {
                Skill<SlashSkill>("slash", "Chém Gió", "sk_slash", 0.42f, 0f, "attack", 0.26f,
                    "Vung kiếm tạo luồng gió chém hình vòng cung. Đòn thứ 3 liên tiếp gây sát thương cực mạnh.",
                    s => { s.moveWhileCasting = 0.35f; s.castSfx = ""; }),
                Skill<FireballSkill>("fireball", "Cầu Lửa", "sk_fireball", 3f, 12f, "cast", 0.3f,
                    "Phóng cầu lửa nổ tung khi trúng mục tiêu, thiêu đốt kẻ địch trong 3 giây.",
                    s => s.castSfx = "sfx_fireball_cast"),
                Skill<IceSpikeSkill>("ice", "Mũi Băng", "sk_ice", 6f, 15f, "cast", 0.35f,
                    "Gọi hàng gai băng trồi lên theo hướng chuột, làm chậm kẻ địch 50%.",
                    s => s.castSfx = "sfx_ice_cast"),
                Skill<LightningSkill>("lightning", "Lôi Phạt", "sk_lightning", 16f, 28f, "cast", 0.5f,
                    "Triệu hồi bão sét tại vị trí chuột. Mỗi tia sét gây choáng ngắn.",
                    s => s.castSfx = ""),
                Skill<HealSkill>("heal", "Hồi Phục", "sk_heal", 14f, 18f, "cast", 0.35f,
                    "Hồi ngay 40 Máu và hồi thêm máu liên tục trong 4 giây.",
                    s => s.castSfx = "sfx_heal"),
                Skill<ShieldSkill>("shield", "Khiên Thánh", "sk_shield", 16f, 16f, "cast", 0.3f,
                    "Tạo lá chắn thánh giảm 60% sát thương nhận vào và miễn choáng trong 5 giây.",
                    s => s.castSfx = "sfx_shield"),
                Skill<BladeStormSkill>("bladestorm", "Bão Kiếm", "sk_bladestorm", 10f, 20f, "attack", 0.2f,
                    "Kiếm ảnh xoay quanh bản thân trong 3 giây, chém mọi kẻ địch lại gần.",
                    s => { s.moveWhileCasting = 0.9f; }),
                Skill<DashSkill>("dash", "Lướt", "sk_dash", 2.2f, 0f, "", 0.05f,
                    "Lướt nhanh về phía trước và bất tử trong khoảnh khắc. Dùng để né vòng cảnh báo đỏ!",
                    s => { s.moveWhileCasting = 1f; s.castSfx = "sfx_dash"; }),
            };
        }

        // ------------------------------------------------------------------ database
        static void CreateDatabase(List<ItemDef> items, List<SkillDef> skills)
        {
            string path = DataFolder + "/GameDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }
            db.items = items;
            db.skills = skills;
            db.shadowSprite = ArtImporter.S("shadow");
            db.whiteSprite = ArtImporter.S("white");
            db.starIcon = ArtImporter.S("icon_star");
            db.skullIcon = ArtImporter.S("icon_skull");
            db.questExclaim = ArtImporter.S("quest_excl");
            db.questQuestion = ArtImporter.S("quest_ques");
            db.glowSprite = ArtImporter.S("glow");
            db.beamSprite = ArtImporter.S("beam");
            db.cursorDefault = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/cursor.png");
            db.cursorAttack = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/cursor_attack.png");
            db.stunIcon = ArtImporter.S("st_stun");
            db.slowIcon = ArtImporter.S("st_slow");
            db.burnIcon = ArtImporter.S("st_burn");
            db.shieldIcon = ArtImporter.S("st_shield");
            db.regenIcon = ArtImporter.S("st_regen");
            db.spriteLit = SpriteLit;
            db.spriteUnlit = SpriteUnlit;
            db.additive = Additive;
            db.silhouette = Silhouette;
            EditorUtility.SetDirty(db);
        }

        public static GameDatabase Database => AssetDatabase.LoadAssetAtPath<GameDatabase>(DataFolder + "/GameDatabase.asset");
    }
}
