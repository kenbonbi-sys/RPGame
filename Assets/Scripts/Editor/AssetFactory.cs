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

        [MenuItem("Tools/RPG/Steps/3. Data (font, items, skills, volumes, audio)", priority = 103)]
        public static void CreateAll()
        {
            CreateMaterials();
            CreateFont();
            CreateVolumes();
            ConfigureAudio();
            var items = CreateItems();
            var skills = CreateSkills();
            var progression = CreateProgression();
            var quests = CreateQuests();
            CreateDatabase(items, skills, progression, quests);
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
            if (EditorUtil.Keep(m)) return m;
            if (m == null)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            EditorUtil.Written++;
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
                if (EditorUtil.Keep(m)) return m;
                EditorUtil.Written++;
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
                if (p != null && (p.objectReferenceValue == null || EditorUtil.Overwrite)) p.objectReferenceValue = fa;
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
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (EditorUtil.Keep(existing)) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(path);
            EditorUtil.Written++;
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
                // hand-tuned clips keep their settings; only new clips get the defaults
                if (ai.userData == ConfiguredMark && !EditorUtil.Overwrite) continue;
                ai.userData = ConfiguredMark;
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

        const string ConfiguredMark = "rpg:configured";

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
            var found = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" })
                .Select(g => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null);
            // authoring mode keeps clips added by hand (from other folders) and only appends new ones
            var keep = EditorUtil.Overwrite || lib.clips == null ? new List<AudioClip>() : lib.clips.Where(c => c != null).ToList();
            lib.clips = keep.Union(found).OrderBy(c => c.name).ToList();
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
            if (EditorUtil.Keep(it)) return it;
            EditorUtil.Written++;
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
            if (EditorUtil.Keep(s)) return s;
            EditorUtil.Written++;
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

        // ------------------------------------------------------------------ progression
        /// <summary>Assets/Data/Progression.asset with the default numbers of plan §05.</summary>
        static ProgressionConfig CreateProgression()
        {
            string path = DataFolder + "/Progression.asset";
            var c = AssetDatabase.LoadAssetAtPath<ProgressionConfig>(path);
            if (EditorUtil.Keep(c)) return c;
            EditorUtil.Written++;
            var fresh = ScriptableObject.CreateInstance<ProgressionConfig>();
            if (c == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            EditorUtility.CopySerialized(fresh, c);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(c);
            return c;
        }

        // ------------------------------------------------------------------ quests
        public const string DialogueProject = "Assets/Dialogue/RungThiTham.yarnproject";

        /// <summary>The prototype's quest line as QuestDef assets (Assets/Data/Quests). Dialogue lives in Assets/Dialogue.</summary>
        static List<QuestDef> CreateQuests()
        {
            var written = new HashSet<QuestDef>();
            QuestDef Quest(string id, string title, QuestKind kind, System.Action<QuestDef> fill)
            {
                string path = $"{DataFolder}/Quests/{id}.asset";
                EditorUtil.EnsureFolder(DataFolder + "/Quests");
                var q = AssetDatabase.LoadAssetAtPath<QuestDef>(path);
                if (EditorUtil.Keep(q)) return q;
                EditorUtil.Written++;
                var fresh = ScriptableObject.CreateInstance<QuestDef>();
                if (q == null)
                {
                    q = fresh;
                    AssetDatabase.CreateAsset(q, path);
                }
                else
                {
                    EditorUtility.CopySerialized(fresh, q);
                    Object.DestroyImmediate(fresh);
                }
                q.id = id;
                q.title = title;
                q.kind = kind;
                fill(q);
                written.Add(q);
                EditorUtility.SetDirty(q);
                return q;
            }
            QuestObjective Obj(ObjectiveKind kind, string target, int count, string text, string marker = null, bool countPrevious = false) =>
                new QuestObjective { kind = kind, target = target, count = count, text = text, marker = marker, countPrevious = countPrevious };
            ItemReward Reward(string item, int count) =>
                new ItemReward { item = AssetDatabase.LoadAssetAtPath<ItemDef>($"{DataFolder}/Items/{item}.asset"), count = count };

            var talk = Quest("talk_chief", "Lời Nhờ Của Trưởng Làng", QuestKind.Main, q =>
            {
                q.summary = "Trưởng Làng đang tìm bạn bên đống lửa giữa làng.";
                q.giver = q.turnIn = "chief";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Talk, "chief", 1, "Nói chuyện với Trưởng Làng"));
                q.xp = 20;
                q.items.Add(Reward("potion_red", 2));
                q.items.Add(Reward("potion_blue", 1));
            });
            var forest = Quest("clear_forest", "Dọn Dẹp Rừng Thì Thầm", QuestKind.Main, q =>
            {
                q.summary = "Lũ Slime Rêu và Nấm Độc tràn ra khắp Rừng Thì Thầm, phía đông làng.";
                q.giver = "chief";
                q.objectives.Add(Obj(ObjectiveKind.Kill, "slime", 4, "Hạ Slime Rêu", "forest"));
                q.objectives.Add(Obj(ObjectiveKind.Kill, "shroom", 2, "Nấm Độc", "forest"));
                q.xp = 120;
            });
            var bear = Quest("slay_bear", "Gấu Ma Rừng Già", QuestKind.Main, q =>
            {
                q.summary = "Gấu Ma Rừng Già ngự ở Rừng Già Cổ Thụ, phía đông bắc.";
                q.giver = q.turnIn = "chief";
                q.turnInText = "Báo tin cho Trưởng Làng";
                q.objectives.Add(Obj(ObjectiveKind.Kill, "bear", 1, "Đánh bại Gấu Ma Rừng Già", "boss", true));
                q.xp = 400;
                q.items.Add(Reward("gold", 1));
                q.items.Add(Reward("ring", 1));
                q.items.Add(Reward("potion_red", 3));
                q.setFlags.Add("forest_saved");
            });
            var mushrooms = Quest("mushrooms", "Nấm Cho Bé Mai", QuestKind.Side, q =>
            {
                q.summary = "Mẹ Bé Mai bị ốm, cô bé cần 3 Mũ Nấm Đỏ để nấu thuốc.";
                q.giver = q.turnIn = "girl";
                q.availableText = "Nói chuyện với Bé Mai";
                q.turnInText = "Mang nấm về cho Bé Mai";
                q.objectives.Add(Obj(ObjectiveKind.Deliver, "shroom_cap", 3, "Nhặt Mũ Nấm Đỏ", "forest"));
                q.xp = 80;
                q.items.Add(Reward("potion_green", 3));
            });

            // links between quests (only on assets written in this run)
            void Link(QuestDef q, QuestDef requires, QuestDef followUp)
            {
                if (!written.Contains(q)) return;
                if (requires != null) q.requires.Add(requires);
                if (followUp != null) q.followUps.Add(followUp);
            }
            Link(talk, null, forest);
            Link(forest, talk, bear);
            Link(bear, forest, null);
            Link(mushrooms, talk, null);
            return new List<QuestDef> { talk, forest, bear, mushrooms };
        }

        // ------------------------------------------------------------------ database
        static void CreateDatabase(List<ItemDef> items, List<SkillDef> skills, ProgressionConfig progression, List<QuestDef> quests)
        {
            string path = DataFolder + "/GameDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(path);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, path);
            }
            // authoring mode appends missing entries and keeps whatever was added or re-pointed by hand
            db.items = Merge(db.items, items);
            db.skills = Merge(db.skills, skills);
            EditorUtil.Assign(ref db.progression, progression);
            db.quests = Merge(db.quests, quests);
            EditorUtil.Assign(ref db.dialogue, AssetDatabase.LoadAssetAtPath<Yarn.Unity.YarnProject>(DialogueProject));
            EditorUtil.Assign(ref db.shadowSprite, ArtImporter.S("shadow"));
            EditorUtil.Assign(ref db.whiteSprite, ArtImporter.S("white"));
            EditorUtil.Assign(ref db.starIcon, ArtImporter.S("icon_star"));
            EditorUtil.Assign(ref db.skullIcon, ArtImporter.S("icon_skull"));
            EditorUtil.Assign(ref db.questExclaim, ArtImporter.S("quest_excl"));
            EditorUtil.Assign(ref db.questQuestion, ArtImporter.S("quest_ques"));
            EditorUtil.Assign(ref db.glowSprite, ArtImporter.S("glow"));
            EditorUtil.Assign(ref db.beamSprite, ArtImporter.S("beam"));
            EditorUtil.Assign(ref db.cursorDefault, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/cursor.png"));
            EditorUtil.Assign(ref db.cursorAttack, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/cursor_attack.png"));
            EditorUtil.Assign(ref db.stunIcon, ArtImporter.S("st_stun"));
            EditorUtil.Assign(ref db.slowIcon, ArtImporter.S("st_slow"));
            EditorUtil.Assign(ref db.burnIcon, ArtImporter.S("st_burn"));
            EditorUtil.Assign(ref db.shieldIcon, ArtImporter.S("st_shield"));
            EditorUtil.Assign(ref db.regenIcon, ArtImporter.S("st_regen"));
            EditorUtil.Assign(ref db.spriteLit, SpriteLit);
            EditorUtil.Assign(ref db.spriteUnlit, SpriteUnlit);
            EditorUtil.Assign(ref db.additive, Additive);
            EditorUtil.Assign(ref db.silhouette, Silhouette);
            EditorUtility.SetDirty(db);
        }

        /// <summary>Forced: the generated list. Authoring: the current list plus generated entries it lacks.</summary>
        static List<T> Merge<T>(List<T> current, List<T> generated) where T : Object
        {
            if (EditorUtil.Overwrite || current == null) return generated;
            var list = current.Where(x => x != null).ToList();
            foreach (var g in generated)
                if (g != null && !list.Contains(g)) list.Add(g);
            return list;
        }

        public static GameDatabase Database => AssetDatabase.LoadAssetAtPath<GameDatabase>(DataFolder + "/GameDatabase.asset");
    }
}
