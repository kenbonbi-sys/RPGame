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
        public static Material SpriteLitFX => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/SpriteLitFX.mat");
        public static Material Additive => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/SpriteAdditive.mat");
        public static Material AlphaUnlit => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/SpriteAlphaUnlit.mat");
        /// <summary>Thảo Nguyên Gió's tall grass: sways with the wind (RPG/Sprite Lit Wind).</summary>
        public static Material GrassWind => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/GrassWind.mat");
        /// <summary>Thảo Nguyên Gió's ground: light bands roll over it with the wind.</summary>
        public static Material GroundWind => AssetDatabase.LoadAssetAtPath<Material>(MatFolder + "/GroundWind.mat");
        public static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Inter SDF.asset");
        public static Material FontOutline => AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Inter SDF - Outline.mat");
        public static Material FontShadow => AssetDatabase.LoadAssetAtPath<Material>("Assets/Fonts/Inter SDF - Shadow.mat");

        [MenuItem("Tools/RPG/Steps/3. Data (font, items, abilities, quests, volumes, audio)", priority = 103)]
        public static void CreateAll()
        {
            CreateMaterials();
            CreateFont();
            CreateVolumes();
            ConfigureAudio();
            var items = CreateItems();
            var abilities = CreateAbilities();
            var progression = CreateProgression();
            var combat = CreateConfig<CombatConfig>("Combat");
            var quests = CreateQuests();
            var zones = CreateZones();
            CreateDatabase(items, abilities, progression, combat, quests, zones);
            var db = Database;
            if (db != null)
            {
                // the character creator's peoples and classes (after the abilities their skill bars name)
                db.races = Merge(db.races, HeroDataFactory.CreateRaces());
                db.classes = Merge(db.classes, HeroDataFactory.CreateClasses());
                EditorUtility.SetDirty(db);
            }
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
            MakeMat("SpriteLitFX", "RPG/Sprite Lit FX");
            MakeMat("SpriteAdditive", "RPG/VFX Additive", m => m.SetFloat("_Intensity", 1.6f));
            MakeMat("SpriteAlphaUnlit", "RPG/VFX Alpha", m => m.SetFloat("_Intensity", 1f));
            MakeMat("GrassWind", "RPG/Sprite Lit Wind", m =>
            {
                m.SetFloat("_Sway", 0.22f);
                m.SetFloat("_Wave", 0f);
            });
            MakeMat("GroundWind", "RPG/Sprite Lit Wind", m =>
            {
                m.SetFloat("_Sway", 0f);
                m.SetFloat("_Wave", 0.14f);
            });
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

        /// <summary>The 134 letters of Vietnamese beyond ASCII (all vowels with every tone, and Đ/đ).</summary>
        public const string VietnameseLetters = "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯưẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợỤụỦủỨứỪừỬửỮữỰựỲỳỴỵỶỷỸỹ";

        /// <summary>ASCII, the Vietnamese letters and the UI symbols: what every font atlas is pre-filled with.</summary>
        public static string VietnameseCharset()
        {
            const string basic = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            const string symbols = "◆◇►»•★☆✓♥·…→←↑↓×";
            return basic + VietnameseLetters + symbols;
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
            var items = BaseItems();
            items.AddRange(GearItems());
            DressGear(items);
            return items;
        }

        static List<ItemDef> BaseItems()
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
                Item("sword", "Kiếm Sắt", ItemKind.Equipment, ItemRarity.Common, "Thanh kiếm sắt đáng tin cậy, cầm ở tay phụ: đánh nhanh hơn một chút.", 30, maxStack: 1),
                Item("shield", "Khiên Gỗ", ItemKind.Equipment, ItemRarity.Common, "Chiếc khiên gỗ bọc sắt, cầm ở tay phụ.", 25, maxStack: 1),
                // Đầm Lầy Sương Mù
                Item("toad_skin", "Da Cóc", M, ItemRarity.Common, "Da cóc sần sùi, vẫn còn rịn nhựa độc.", 4),
                Item("poison_gland", "Tuyến Độc", M, ItemRarity.Uncommon, "Túi độc nhỏ của Cóc Độc. Bà lang nào cũng muốn có.", 10),
                Item("leech_tooth", "Răng Đỉa", M, ItemRarity.Common, "Chiếc răng cưa li ti, sắc như dao cạo.", 5),
                Item("mud_core", "Lõi Bùn", M, ItemRarity.Uncommon, "Hòn bùn cứng như đá, vẫn còn ấm. Người Bùn sống nhờ nó.", 12),
                Item("venom_sac", "Túi Nọc", M, ItemRarity.Uncommon, "Túi nọc đặc quánh, lấp lánh ánh tím.", 20),
                Item("lotus", "Sen Đầm", C, ItemRarity.Uncommon, "Hoa sen trắng mọc giữa đầm lầy. Hồi 40 Máu và 20 Năng lượng.", 8, heal: 40, energy: 20, maxStack: 20),
                Item("toad_crown", "Vương Miện Cóc Tía", M, ItemRarity.Rare, "Chiếc vương miện vàng méo mó của Cóc Tía, dính đầy nhựa độc.", 150),
                Item("snake_scale", "Vảy Xà Mẫu", M, ItemRarity.Rare, "Vảy lục thẫm cứng hơn thép, phản chiếu ánh sương.", 160),
                Item("snake_fang", "Nanh Xà Mẫu", M, ItemRarity.Epic, "Chiếc nanh dài của Xà Mẫu Đầm Lầy, nọc vẫn còn nhỏ giọt.", 320),
                Item("wsnake_skin", "Da Rắn Nước", M, ItemRarity.Common, "Lớp da lột xanh ô liu, còn nguyên những khoanh vằn sẫm.", 6),
                Item("dragonfly_wing", "Cánh Chuồn Chuồn", M, ItemRarity.Common, "Cánh mỏng như sương, gân xanh chằng chịt. Chạm nhẹ là rung.", 5),
                Item("wisp_essence", "Tinh Chất Ma Trơi", M, ItemRarity.Uncommon, "Một đốm lửa lạnh nhốt trong bình, không bao giờ tắt.", 18),
                // Hang Pha Lê
                Item("crystal_shard", "Mảnh Pha Lê", M, ItemRarity.Common, "Mảnh pha lê trong vắt, tự phát ra ánh sáng xanh nhạt.", 6),
                Item("bat_wing", "Cánh Dơi Pha Lê", M, ItemRarity.Common, "Cánh dơi mỏng, mép cánh lấm tấm những hạt pha lê.", 7),
                Item("spider_silk", "Tơ Nhện Hang", M, ItemRarity.Uncommon, "Cuộn tơ dai như dây thừng, dính tay khó gỡ.", 12),
                Item("golem_core", "Lõi Golem", M, ItemRarity.Uncommon, "Trái tim đá của Golem, một con mắt xanh vẫn lập lòe.", 24),
                Item("beetle_shell", "Vỏ Bọ Giáp", M, ItemRarity.Uncommon, "Mảnh mai đá của Bọ Giáp Đá, cứng như khiên, lấm tấm pha lê.", 16),
                Item("crystal_jelly", "Nhân Slime Pha Lê", M, ItemRarity.Common, "Giọt nhớt xanh trong, giữa lòng có một hạt pha lê nhỏ.", 9),
                Item("eye_lens", "Thủy Tinh Thể Mắt Hang", M, ItemRarity.Uncommon, "Thấu kính trong vắt của Mắt Hang. Nhìn qua nó, bóng tối như sáng lên.", 22),
                Item("ancient_core", "Lõi Pha Lê Cổ", M, ItemRarity.Rare, "Trái tim hổ phách của Golem Pha Lê Cổ, vẫn còn ấm và phát sáng.", 180),
                Item("queen_eye", "Mắt Nhện Chúa", M, ItemRarity.Epic, "Con mắt đỏ rực của Nhện Chúa Pha Lê, nằm giữa một vòng pha lê.", 360),
                Item("crystal_silk", "Tơ Pha Lê", M, ItemRarity.Rare, "Tơ của Nhện Chúa, óng ánh bụi pha lê, không lưỡi dao nào cắt đứt.", 120),
                Item("mimic_tooth", "Răng Mimic", M, ItemRarity.Epic, "Chiếc răng cong của Mimic Tham Lam, một đồng vàng vẫn còn dính trên đó.", 300),
                // Thảo Nguyên Gió
                Item("hyena_fang", "Nanh Linh Cẩu", M, ItemRarity.Common, "Chiếc nanh vàng ố của Linh Cẩu Gió, mòn vì gặm xương.", 14),
                Item("eagle_feather", "Lông Ưng Đá", M, ItemRarity.Uncommon, "Chiếc lông xám như đá của Chim Ưng Đá. Thả ra là nó cưỡi gió bay đi.", 22),
                Item("bison_horn", "Sừng Bò Rừng", M, ItemRarity.Uncommon, "Chiếc sừng cong, gốc to bằng cổ tay, còn vết húc vào đá.", 30),
                Item("bison_hide", "Da Bò Rừng", M, ItemRarity.Common, "Tấm da dày lông xù của Bò Rừng, gió thảo nguyên không lọt qua.", 18),
            };
        }

        // ------------------------------------------------------------------ gear
        /// <summary>The gear the smith makes (Lò Rèn, tab Chế Tạo), a set for each region's materials.</summary>
        static List<ItemDef> GearItems()
        {
            var E = ItemKind.Equipment;
            return new List<ItemDef>
            {
                // Rừng Thì Thầm
                Item("helm_leather", "Mũ Da", E, ItemRarity.Common, "Mũ da thuộc khâu tay, hai vạt che tai. Thợ Rèn phết gel slime cho da không nứt.", 40, maxStack: 1),
                Item("boots_leather", "Giày Da", E, ItemRarity.Common, "Đôi giày da mềm, đế mỏng: bước nhẹ, lướt nhanh.", 40, maxStack: 1),
                Item("armor_bear", "Áo Da Gấu", E, ItemRarity.Uncommon, "Áo may từ da Gấu Ma, cổ viền lông đen. Ấm, và dai như thép.", 180, maxStack: 1),
                // Đầm Lầy Sương Mù
                Item("boots_toad", "Giày Da Cóc", E, ItemRarity.Uncommon, "Giày da cóc sần sùi, không thấm nước: lướt trên bùn như trên đất khô.", 160, maxStack: 1),
                Item("armor_scale", "Giáp Vảy Xà", E, ItemRarity.Rare, "Giáp lưới đan từ vảy Xà Mẫu và da Rắn Nước, cứng hơn thép mà nhẹ như vải.", 420, maxStack: 1),
                Item("crown_toad", "Vương Miện Cóc", E, ItemRarity.Rare, "Vương miện của Cóc Tía, Thợ Rèn nắn lại cho vừa đầu người. Vàng tự tìm tới người đội nó.", 380, maxStack: 1),
                // Hang Pha Lê
                Item("helm_crystal", "Mũ Pha Lê", E, ItemRarity.Rare, "Mũ thép gắn mào pha lê, sáng mờ trong bóng tối. Đầu óc tỉnh táo lạ thường.", 460, maxStack: 1),
                Item("shield_beetle", "Khiên Vỏ Bọ", E, ItemRarity.Rare, "Khiên làm từ mai Bọ Giáp Đá, lấm tấm pha lê.", 480, maxStack: 1),
                Item("armor_silk", "Áo Tơ Pha Lê", E, ItemRarity.Epic, "Áo dệt từ tơ của Nhện Chúa: không lưỡi dao nào cắt đứt, phép thuật trượt đi trên nó.", 900, maxStack: 1),
                Item("ring_eye", "Nhẫn Mắt Hang", E, ItemRarity.Rare, "Nhẫn bạc gắn thủy tinh thể của Mắt Hang. Nhìn qua nó, thấy ngay chỗ yếu.", 400, maxStack: 1),
                Item("ring_mimic", "Nhẫn Răng Mimic", E, ItemRarity.Epic, "Răng của Mimic Tham Lam trên một vòng vàng. Vàng cứ thế rơi vào túi.", 600, maxStack: 1),
                // Thảo Nguyên Gió
                Item("helm_eagle", "Mũ Lông Ưng", E, ItemRarity.Rare, "Mũ thép cài hai chiếc lông Ưng Đá. Người đội nó nhìn thấy chỗ hở trước khi đối thủ kịp che.", 560, maxStack: 1),
                Item("armor_bison", "Áo Da Bò Rừng", E, ItemRarity.Epic, "Áo da bò rừng dày, vai đính sừng. Gió thảo nguyên và nanh vuốt đều trượt đi.", 980, maxStack: 1),
                Item("boots_wind", "Giày Gió", E, ItemRarity.Epic, "Giày khâu bằng lông ưng: bước đi như có gió đẩy sau lưng.", 760, maxStack: 1),
            };
        }

        static StatModifier Plus(StatId stat, float value) => new StatModifier(stat, ModKind.Flat, value);

        static ItemCount[] Parts(params (string id, int n)[] parts)
        {
            var list = new ItemCount[parts.Length];
            for (int i = 0; i < parts.Length; i++) list[i] = new ItemCount(parts[i].id, parts[i].n);
            return list;
        }

        /// <summary>
        /// Where each piece of gear is worn, what it adds and what the smith asks to make it. Also
        /// run over the items made before gear existed (the ring, the sword, the shield): authoring
        /// mode fills them once, while their slot is still empty, and keeps hand edits after that.
        /// </summary>
        static void DressGear(List<ItemDef> items)
        {
            void G(string id, EquipSlot slot, int gold, ItemCount[] parts, params StatModifier[] bonuses)
            {
                var it = items.Find(x => x != null && x.id == id);
                if (it == null || (it.slot != EquipSlot.None && !EditorUtil.Overwrite)) return;
                it.kind = ItemKind.Equipment;
                it.maxStack = 1;
                it.slot = slot;
                it.bonuses = bonuses;
                it.craftGold = gold;
                it.craftItems = parts ?? new ItemCount[0];
                EditorUtility.SetDirty(it);
            }
            var none = new ItemCount[0];
            // Rừng Thì Thầm (levels 1-8)
            G("helm_leather", EquipSlot.Head, 60, Parts(("gel", 6), ("shroom_cap", 2)), Plus(StatId.Armor, 2), Plus(StatId.MaxHp, 15));
            G("boots_leather", EquipSlot.Feet, 60, Parts(("gel", 4), ("herb", 3)), Plus(StatId.Armor, 1), Plus(StatId.DashCooldownReduction, 0.06f));
            G("shield", EquipSlot.Offhand, 50, Parts(("gel", 3), ("herb", 2)), Plus(StatId.Armor, 3));
            G("sword", EquipSlot.Offhand, 80, Parts(("gel", 5)), Plus(StatId.PhysicalAttack, 2), Plus(StatId.AttackSpeed, 0.04f));
            G("armor_bear", EquipSlot.Body, 150, Parts(("pelt", 1), ("claw", 1)), Plus(StatId.Armor, 4), Plus(StatId.MaxHp, 30));
            G("ring", EquipSlot.Ring, 0, none,
              Plus(StatId.Strength, 1), Plus(StatId.Dexterity, 1), Plus(StatId.Constitution, 1),
              Plus(StatId.Intelligence, 1), Plus(StatId.Wisdom, 1), Plus(StatId.Charisma, 1), Plus(StatId.GoldFind, 0.05f));
            // Đầm Lầy Sương Mù (8-14)
            G("boots_toad", EquipSlot.Feet, 180, Parts(("toad_skin", 5), ("leech_tooth", 3)),
              Plus(StatId.Armor, 2), Plus(StatId.MaxHp, 20), Plus(StatId.DashCooldownReduction, 0.1f));
            G("armor_scale", EquipSlot.Body, 380, Parts(("snake_scale", 2), ("wsnake_skin", 4), ("mud_core", 2)),
              Plus(StatId.Armor, 8), Plus(StatId.MaxHp, 40), Plus(StatId.ElementalResist, 0.05f));
            G("crown_toad", EquipSlot.Head, 300, Parts(("toad_crown", 1), ("dragonfly_wing", 3), ("poison_gland", 2)),
              Plus(StatId.Armor, 3), Plus(StatId.Charisma, 2), Plus(StatId.GoldFind, 0.12f));
            // Hang Pha Lê (14-20)
            G("helm_crystal", EquipSlot.Head, 450, Parts(("crystal_shard", 10), ("bat_wing", 3), ("golem_core", 1)),
              Plus(StatId.Armor, 5), Plus(StatId.MaxEnergy, 15), Plus(StatId.CooldownReduction, 0.06f));
            G("shield_beetle", EquipSlot.Offhand, 480, Parts(("beetle_shell", 4), ("crystal_jelly", 3)),
              Plus(StatId.Armor, 7), Plus(StatId.MaxHp, 40));
            G("armor_silk", EquipSlot.Body, 800, Parts(("crystal_silk", 2), ("spider_silk", 5), ("ancient_core", 1)),
              Plus(StatId.Armor, 11), Plus(StatId.MaxHp, 60), Plus(StatId.ElementalResist, 0.1f));
            G("ring_eye", EquipSlot.Ring, 400, Parts(("eye_lens", 3), ("gem_blue", 1)),
              Plus(StatId.CritChance, 0.05f), Plus(StatId.CritDamage, 0.15f));
            G("ring_mimic", EquipSlot.Ring, 350, Parts(("mimic_tooth", 1), ("gem_red", 1)),
              Plus(StatId.Charisma, 1), Plus(StatId.GoldFind, 0.25f));
            // Thảo Nguyên Gió (20-26)
            G("helm_eagle", EquipSlot.Head, 600, Parts(("eagle_feather", 6), ("hyena_fang", 3)),
              Plus(StatId.Armor, 6), Plus(StatId.CritChance, 0.05f), Plus(StatId.MaxHp, 40));
            G("armor_bison", EquipSlot.Body, 950, Parts(("bison_hide", 5), ("bison_horn", 2), ("hyena_fang", 4)),
              Plus(StatId.Armor, 13), Plus(StatId.MaxHp, 90), Plus(StatId.ElementalResist, 0.08f));
            G("boots_wind", EquipSlot.Feet, 720, Parts(("eagle_feather", 4), ("bison_hide", 2)),
              Plus(StatId.Armor, 3), Plus(StatId.MaxHp, 30), Plus(StatId.DashCooldownReduction, 0.15f));
        }

        // ------------------------------------------------------------------ abilities
        /// <summary>
        /// The 8 prototype skills as AbilityDef data (Assets/Data/Abilities). Power is a share of
        /// Attack; the values reproduce the prototype's damage for a level-1 hero (Attack 24.5).
        /// </summary>
        static List<AbilityDef> CreateAbilities()
        {
            AbilityDef Ability(string id, string name, string icon, AbilityTags tags, float cd, float cost, string anim, float lockTime,
                               string desc, System.Action<AbilityDef> fill)
            {
                string path = $"{DataFolder}/Abilities/{id}.asset";
                EditorUtil.EnsureFolder(DataFolder + "/Abilities");
                var a = AssetDatabase.LoadAssetAtPath<AbilityDef>(path);
                if (EditorUtil.Keep(a)) return a;
                EditorUtil.Written++;
                var fresh = ScriptableObject.CreateInstance<AbilityDef>();
                if (a == null)
                {
                    a = fresh;
                    AssetDatabase.CreateAsset(a, path);
                }
                else
                {
                    EditorUtility.CopySerialized(fresh, a);
                    Object.DestroyImmediate(fresh);
                }
                a.id = id;
                a.displayName = name;
                a.icon = ArtImporter.S(icon);
                a.tags = tags;
                a.cooldown = cd;
                a.energyCost = cost;
                a.animBase = anim;
                a.lockTime = lockTime;
                a.description = desc;
                fill(a);
                EditorUtility.SetDirty(a);
                return a;
            }
            CueEffect Cue(Anchor at, string vfx = null, string sfx = null, float vol = 0.8f, float shake = 0f) =>
                new CueEffect { at = at, vfx = vfx, sfx = sfx, sfxVolume = vol, shake = shake };
            var caster = new Anchor(Anchor.From.Caster);
            var point = new Anchor(Anchor.From.Point);
            var db = Database;
            var fireball = db != null && db.fireballPrefab != null ? db.fireballPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Gameplay/Fireball.prefab");

            List<AbilityEffect> Swing(bool finisher)
            {
                var chest = new Anchor(Anchor.From.Caster, 0.45f);
                return new List<AbilityEffect>
                {
                    new CueEffect
                    {
                        delay = 0.05f, at = new Anchor(Anchor.From.Caster, 0.45f, 0.55f), vfx = finisher ? "slash_big" : "slash",
                        vfxScale = finisher ? 1.35f : 1f, rotateToDirection = true, flipOnOddCombo = true,
                        sfx = "sfx_swing", sfxVolume = 0.8f, sfxPitchVariance = 0.1f
                    },
                    new DamageEffect
                    {
                        delay = 0.05f, shape = DamageEffect.Shape.Cone, at = chest, radius = finisher ? 2.28f : 1.9f, angle = 150f,
                        hit = new HitSpec
                        {
                            power = finisher ? 1.53f : 0.9f, type = DamageType.Physical, critChance = 0.15f,
                            knockback = finisher ? 8f : 4f, poise = finisher ? 9f : 3f, hitStop = finisher ? 0.07f : 0.035f
                        },
                        onAnyHit = new List<AbilityEffect>
                        {
                            new CueEffect
                            {
                                sfx = finisher ? "sfx_hit_heavy" : "sfx_hit", sfxVolume = 0.9f, sfxPitchVariance = 0.1f,
                                shake = finisher ? 0.22f : 0.08f, impact = finisher ? 0.35f : 0f, impactDuration = 0.2f
                            }
                        }
                    }
                };
            }

            return new List<AbilityDef>
            {
                Ability("slash", "Chém Gió", "sk_slash", AbilityTags.Physical | AbilityTags.Wind | AbilityTags.Melee, 0.42f, 0f, "attack", 0.26f,
                    "Vung kiếm tạo luồng gió chém hình vòng cung. Đòn thứ 3 liên tiếp gây sát thương cực mạnh.", a =>
                    {
                        a.commitTime = 0.05f;   // the hit frame: a dash may cancel the recovery after it (plan §04)
                        a.moveWhileCasting = 0.35f;
                        a.maxRange = 12f;
                        a.effects.Add(new ComboEffect
                        {
                            window = 0.9f,
                            stages =
                            {
                                new ComboEffect.Stage { name = "Đòn 1", effects = Swing(false) },
                                new ComboEffect.Stage { name = "Đòn 2", effects = Swing(false) },
                                new ComboEffect.Stage { name = "Đòn cuối", effects = Swing(true) },
                            }
                        });
                    }),
                Ability("fireball", "Cầu Lửa", "sk_fireball", AbilityTags.Fire | AbilityTags.Projectile, 3f, 12f, "cast", 0.3f,
                    "Phóng cầu lửa nổ tung khi trúng mục tiêu, gây 1 tầng Bỏng: đốt 30% sát thương mỗi giây trong 3 giây.", a =>
                    {
                        a.commitTime = 0.15f;
                        a.castSfx = "sfx_fireball_cast";
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.55f, 0.5f), "cast_fire"));
                        a.effects.Add(new ProjectileEffect
                        {
                            prefab = fireball, spawn = new Anchor(Anchor.From.Caster, 0.55f, 0.5f), speed = 11f, explodeRadius = 1.7f,
                            hit = new HitSpec { power = 1.88f, type = DamageType.Fire, critChance = 0.12f, knockback = 3f, poise = 12f, status = new StatusHit { burn = 1 } },
                            hitVfx = "fire_explosion", hitSfx = "sfx_fireball_explode", hitShake = 0.28f
                        });
                    }),
                Ability("ice", "Mũi Băng", "sk_ice", AbilityTags.Ice | AbilityTags.Area, 6f, 15f, "cast", 0.35f,
                    "Gọi hàng gai băng trồi lên theo hướng chuột, mỗi gai gây 2 tầng Lạnh (chậm 12% mỗi tầng); đủ 4 tầng thì Đóng Băng.", a =>
                    {
                        a.commitTime = 0.2f;
                        a.castSfx = "sfx_ice_cast";
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.5f), "cast_ice"));
                        a.effects.Add(new LineEffect
                        {
                            count = 7, startOffset = 1.1f, spacing = 1.05f, interval = 0.06f, jitter = 0.18f, stopAtWalls = true,
                            startScale = 0.9f, scaleStep = 0.04f,
                            each =
                            {
                                new CueEffect { at = point, vfx = "ice_spike", sfx = "sfx_ice_shatter", sfxVolume = 0.35f, sfxPitchVariance = 0.15f, sfxAtPoint = true, sfxMinInterval = 0.02f },
                                new DamageEffect
                                {
                                    at = point, radius = 0.95f,
                                    hit = new HitSpec { power = 1.22f, type = DamageType.Ice, critChance = 0.1f, knockback = 1.5f, poise = 5f, status = new StatusHit { chill = 2 } },
                                    onAnyHit = { new CueEffect { shake = 0.05f } }
                                }
                            }
                        });
                    }),
                Ability("lightning", "Lôi Phạt", "sk_lightning", AbilityTags.Lightning | AbilityTags.Area, 16f, 28f, "cast", 0.5f,
                    "Triệu hồi bão sét tại vị trí chuột. Mỗi tia sét gây choáng ngắn.", a =>
                    {
                        a.commitTime = 0.3f;
                        a.targeting = AbilityTargeting.Point;
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.6f), "cast_lightning", "sfx_lightning_charge"));
                        a.effects.Add(new BurstEffect
                        {
                            center = new Anchor(Anchor.From.Aim), radius = 3f, count = 8, chargeTime = 0.4f, interval = 0.14f, intervalJitter = 0.3f,
                            preferTargets = 0.75f, firstAtCenter = true, areaVfx = "storm_circle", areaVfxScale = 3f / 2.5f,
                            each =
                            {
                                new CueEffect
                                {
                                    at = point, vfx = "lightning_strike", sfx = "sfx_thunder", sfxVolume = 0.7f, sfxPitchVariance = 0.12f,
                                    sfxAtPoint = true, sfxMinInterval = 0.05f, shake = 0.22f,
                                    flashColor = new Color(0.85f, 0.85f, 1f), flashStrength = 0.12f, flashDuration = 0.12f
                                },
                                new DamageEffect
                                {
                                    at = point, radius = 1.1f,
                                    hit = new HitSpec { power = 2.12f, type = DamageType.Lightning, critChance = 0.15f, knockback = 2f, poise = 8f, status = new StatusHit { stun = 0.8f } }
                                }
                            }
                        });
                    }),
                Ability("heal", "Hồi Phục", "sk_heal", AbilityTags.Holy | AbilityTags.Support, 14f, 18f, "cast", 0.35f,
                    "Hồi ngay 40 Máu và hồi thêm máu liên tục trong 4 giây.", a =>
                    {
                        a.commitTime = 0.2f;
                        a.castSfx = "sfx_heal";
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(new HealEffect { instant = 40f, perSecond = 6f, duration = 4f, auraVfx = "heal_aura" });
                        a.effects.Add(new CueEffect
                        {
                            at = caster, vfx = "heal_burst", attachToCaster = true,
                            flashColor = new Color(0.4f, 1f, 0.5f), flashStrength = 0.08f, flashDuration = 0.3f
                        });
                        a.effects.Add(new BuffEffect { buff = new BuffSpec { id = "regen", displayName = "Hồi Phục", icon = ArtImporter.S("st_regen"), duration = 4f } });
                    }),
                Ability("shield", "Khiên Thánh", "sk_shield", AbilityTags.Holy | AbilityTags.Support, 16f, 16f, "cast", 0.3f,
                    "Tạo lá chắn thánh giảm 60% sát thương nhận vào và miễn choáng trong 5 giây.", a =>
                    {
                        a.commitTime = 0.1f;
                        a.castSfx = "sfx_shield";
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(new BuffEffect
                        {
                            buff = new BuffSpec
                            {
                                id = "shield", displayName = "Khiên Thánh", icon = ArtImporter.S("st_shield"), duration = 5f,
                                damageTakenMultiplier = 0.4f, stunImmune = true, clearStun = true,
                                attachedVfx = "shield_bubble", endVfx = "shield_break"
                            }
                        });
                        a.effects.Add(Cue(caster, "shield_cast"));
                    }),
                Ability("bladestorm", "Bão Kiếm", "sk_bladestorm", AbilityTags.Physical | AbilityTags.Wind | AbilityTags.Channel, 10f, 20f, "attack", 0.2f,
                    "Kiếm ảnh xoay quanh bản thân trong 3 giây, chém mọi kẻ địch lại gần.", a =>
                    {
                        a.commitTime = 0.1f;
                        a.moveWhileCasting = 0.9f;
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(new BuffEffect { buff = new BuffSpec { id = "bladestorm", displayName = "Bão Kiếm", icon = ArtImporter.S("sk_bladestorm"), duration = 3f, speedMultiplier = 0.85f } });
                        a.effects.Add(new PulseEffect
                        {
                            duration = 3f, interval = 0.25f, at = new Anchor(Anchor.From.Caster, 0.4f), attachedVfx = "blade_storm",
                            loopSfx = "sfx_bladestorm", loopSfxVolume = 0.6f, loopSfxInterval = 0.8f,
                            each =
                            {
                                new DamageEffect
                                {
                                    at = point, radius = 2.2f,
                                    hit = new HitSpec { power = 0.45f, type = DamageType.Physical, critChance = 0.1f, knockback = 1.2f, poise = 2f },
                                    onAnyHit = { new CueEffect { sfx = "sfx_hit", sfxVolume = 0.35f, sfxPitchVariance = 0.2f, sfxMinInterval = 0.05f } }
                                }
                            }
                        });
                    }),
                Ability("dash", "Lướt", "sk_dash", AbilityTags.Movement, 2.2f, 0f, "", 0.05f,
                    "Lướt nhanh về phía trước và bất tử trong khoảnh khắc. Dùng để né vòng cảnh báo đỏ!", a =>
                    {
                        a.moveWhileCasting = 1f;
                        a.castSfx = "sfx_dash";
                        a.effects.Add(new DashEffect { distance = 4.2f, time = 0.16f, invulnerableTime = 0.28f });
                    }),
            };
        }

        /// <summary>The classes' skills (made after the VFX step, whose projectiles they use) join the database.</summary>
        public static void LinkClassAbilities(List<AbilityDef> abilities)
        {
            var db = Database;
            if (db == null) return;
            db.abilities = Merge(db.abilities, abilities);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// References to assets made by later steps (the Fireball projectile comes from the VFX
        /// step): filled in once those exist, only where still empty.
        /// </summary>
        public static void LinkLateReferences()
        {
            var db = Database;
            if (db == null) return;
            foreach (var a in db.abilities)
            {
                if (a == null) continue;
                bool changed = false;
                foreach (var e in a.effects)
                {
                    if (e is ProjectileEffect p && p.prefab == null && a.id == "fireball" && db.fireballPrefab != null)
                    {
                        p.prefab = db.fireballPrefab;
                        changed = true;
                    }
                }
                if (changed) EditorUtility.SetDirty(a);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ progression
        /// <summary>Assets/Data/Progression.asset with the default numbers of plan §05.</summary>
        static ProgressionConfig CreateProgression() => CreateConfig<ProgressionConfig>("Progression");

        /// <summary>A config asset in Assets/Data with the defaults of its class (Progression, Combat).</summary>
        static T CreateConfig<T>(string name) where T : ScriptableObject
        {
            string path = $"{DataFolder}/{name}.asset";
            var c = AssetDatabase.LoadAssetAtPath<T>(path);
            if (EditorUtil.Keep(c)) return c;
            EditorUtil.Written++;
            var fresh = ScriptableObject.CreateInstance<T>();
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

        // ------------------------------------------------------------------ zones
        /// <summary>The zones of the world (Assets/Data/Zones). Each has its own scene in Assets/Scenes/Zones.</summary>
        static List<ZoneDef> CreateZones()
        {
            ZoneDef Zone(string id, string name, string scene, int min, int max, string music, string ambience)
            {
                string path = $"{DataFolder}/Zones/{id}.asset";
                EditorUtil.EnsureFolder(DataFolder + "/Zones");
                var z = AssetDatabase.LoadAssetAtPath<ZoneDef>(path);
                if (EditorUtil.Keep(z)) return z;
                EditorUtil.Written++;
                if (z == null)
                {
                    z = ScriptableObject.CreateInstance<ZoneDef>();
                    AssetDatabase.CreateAsset(z, path);
                }
                z.id = id;
                z.displayName = name;
                z.sceneName = scene;
                z.levelMin = min;
                z.levelMax = max;
                z.music = music;
                z.ambience = ambience;
                z.defaultEntry = "spawn";
                EditorUtility.SetDirty(z);
                return z;
            }
            return new List<ZoneDef>
            {
                // the prototype map: Làng Lá Xanh, the forest and the Rừng Già Cổ Thụ arena
                Zone("rung_thi_tham", "Rừng Thì Thầm", "RungThiTham", 1, 8, "music_forest", "amb_forest"),
            };
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
            // Đầm Lầy Sương Mù: bounties that start by themselves once the forest is safe
            var swampRoad = Quest("swamp_road", "Đường Tới Đầm Lầy", QuestKind.Main, q =>
            {
                q.summary = "Trưởng Làng kể: qua Rừng Già về phía đông là Đầm Lầy Sương Mù. Ngư dân dựng một trạm nhà sàn ở bìa đầm.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Reach, "Trạm Nhà Sàn", 1, "Tới Trạm Nhà Sàn ở bìa đầm", "outpost"));
                q.xp = 150;
                q.items.Add(Reward("potion_green", 2));
            });
            var swampToads = Quest("swamp_toads", "Truy Nã: Cóc Độc Và Đỉa Bùn", QuestKind.Main, q =>
            {
                q.summary = "Ngư dân ở trạm nhà sàn treo thưởng: Cóc Độc và Đỉa Bùn không cho ai thả lưới.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "toad", 6, "Hạ Cóc Độc", "swamp"));
                q.objectives.Add(Obj(ObjectiveKind.Kill, "leech", 3, "Hạ Đỉa Bùn", "swamp"));
                q.xp = 320;
                q.gold = 40;
                q.items.Add(Reward("lotus", 3));
            });
            var swampMud = Quest("swamp_mud", "Truy Nã: Người Bùn", QuestKind.Main, q =>
            {
                q.summary = "Người Bùn lừ đừ quanh các vũng lầy sâu. Mỗi lần gục chúng lại tách ra thành Bùn Con.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "mudman", 3, "Hạ Người Bùn", "mudfield"));
                q.xp = 380;
                q.gold = 50;
                q.items.Add(Reward("potion_red", 3));
            });
            var swampHunters = Quest("swamp_hunters", "Truy Nã: Rắn Nước Và Chuồn Chuồn Kim", QuestKind.Main, q =>
            {
                q.summary = "Rắn Nước rình dưới các vũng nước, ban ngày Chuồn Chuồn Kim lượn trên mặt đầm. Ngư dân không dám ra lưới.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "watersnake", 3, "Hạ Rắn Nước", "snakepools"));
                q.objectives.Add(Obj(ObjectiveKind.Kill, "dragonfly", 4, "Hạ Chuồn Chuồn Kim (ban ngày)", "dragonflies"));
                q.xp = 360;
                q.gold = 45;
                q.items.Add(Reward("potion_blue", 2));
            });
            var swampWisps = Quest("swamp_wisps", "Ánh Lửa Ma Trơi", QuestKind.Bounty, q =>
            {
                q.summary = "Đêm xuống, Ma Trơi dụ người đi lạc vào giữa đầm rồi nổ tung. Hạ chúng trước khi chúng kịp nổ: con nào tự nổ thì không tính.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "wisp", 3, "Hạ Ma Trơi (chỉ có ban đêm)", "wisps"));
                q.xp = 420;
                q.gold = 60;
                q.items.Add(Reward("lotus", 2));
            });
            // Hang Pha Lê, north of the swamp
            var caveEnter = Quest("cave_enter", "Hang Pha Lê", QuestKind.Main, q =>
            {
                q.summary = "Ngư dân kể phía bắc đầm có một cửa hang tỏa ánh sáng xanh. Thợ mỏ bỏ đi đã lâu, nghe nói trong đó dơi, nhện và đá biết đi.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Reach, "Cửa Hang", 1, "Tới Cửa Hang Pha Lê ở phía bắc đầm", "cavemouth"));
                q.xp = 500;
                q.items.Add(Reward("potion_red", 3));
            });
            var caveBats = Quest("cave_bats", "Truy Nã: Dơi Pha Lê", QuestKind.Main, q =>
            {
                q.summary = "Bầy dơi trong Hang Dơi hút máu bất cứ ai mang đèn đi qua. Chúng sợ lửa và ánh sáng.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "bat", 6, "Hạ Dơi Pha Lê", "batcave"));
                q.xp = 560;
                q.gold = 70;
                q.items.Add(Reward("potion_red", 2));
            });
            var caveSpiders = Quest("cave_spiders", "Truy Nã: Nhện Hang", QuestKind.Main, q =>
            {
                q.summary = "Nhện Hang phun tơ trói chân người rồi mới lao tới cắn. Tổ của chúng ở sâu trong hang, phía bắc Rừng Pha Lê.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "spider", 4, "Hạ Nhện Hang", "spidernest"));
                q.xp = 620;
                q.gold = 80;
                q.items.Add(Reward("potion_green", 2));
            });
            var caveGolems = Quest("cave_golems", "Truy Nã: Golem Đá", QuestKind.Main, q =>
            {
                q.summary = "Đá trong mỏ bỏ hoang tự đứng dậy thành Golem. Kiếm chém vào chúng chỉ tóe lửa: lôi điện đánh chúng đau hơn.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "golem", 3, "Hạ Golem Đá Nhỏ", "mine"));
                q.xp = 680;
                q.gold = 90;
                q.items.Add(Reward("potion_blue", 2));
            });
            var caveBeetles = Quest("cave_beetles", "Truy Nã: Bọ Giáp Đá", QuestKind.Main, q =>
            {
                q.summary = "Trong mỏ bỏ hoang có loài bọ khoác mai đá. Đánh vào đầu nó chỉ tóe lửa: vòng ra sau lưng, hoặc dụ nó húc vào vách đá.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "beetle", 4, "Hạ Bọ Giáp Đá", "mine"));
                q.xp = 740;
                q.gold = 95;
                q.items.Add(Reward("potion_red", 2));
            });
            var caveSlimes = Quest("cave_slimes", "Truy Nã: Slime Pha Lê", QuestKind.Main, q =>
            {
                q.summary = "Slime Pha Lê trong Rừng Pha Lê hắt mọi mũi tên, quả cầu phép bay ngược về người bắn. Dùng đòn cận chiến, và tránh xa khi nó vỡ tung.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "crystalslime", 4, "Hạ Slime Pha Lê", "crystalforest"));
                q.xp = 780;
                q.gold = 100;
                q.items.Add(Reward("potion_blue", 2));
            });
            var caveEyes = Quest("cave_eyes", "Truy Nã: Mắt Hang", QuestKind.Main, q =>
            {
                q.summary = "Những con mắt khổng lồ mọc trong vách hang, bắn tia sáng dội qua vách đá. Mí đá của chúng rất cứng: đánh lúc mắt vừa bắn xong và còn mở.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "caveeye", 3, "Hạ Mắt Hang", "crystalforest"));
                q.xp = 840;
                q.gold = 110;
                q.items.Add(Reward("potion_green", 2));
            });
            var crystalGolem = Quest("slay_crystalgolem", "Golem Pha Lê Cổ", QuestKind.Main, q =>
            {
                q.summary = "Điện Pha Lê có một Golem cổ canh giữ. Mặt trước nó hắt đạn ngược lại; lõi hổ phách ở sau lưng mới là chỗ yếu. Đừng đứng sau lưng nó quá lâu.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "crystalgolem", 1, "Đánh bại Golem Pha Lê Cổ", "crystalhall"));
                q.xp = 1300;
                q.gold = 160;
                q.items.Add(Reward("potion_red", 3));
            });
            var spiderQueen = Quest("slay_queen", "Nhện Chúa Pha Lê", QuestKind.Main, q =>
            {
                q.summary = "Tận cùng Hang Pha Lê, Nhện Chúa giăng tơ giữa sáu cột pha lê. Tia sáng của nó dội qua các cột: đập vỡ cột sắp bị tia chạm vào để tia dội ngược vào nó. Xa hơn về phía tây bắc là Thảo Nguyên Gió (sắp mở).";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "spiderqueen", 1, "Đánh bại Nhện Chúa Pha Lê", "queenhall"));
                q.xp = 2000;
                q.gold = 240;
                q.items.Add(Reward("potion_red", 4));
                q.items.Add(Reward("potion_blue", 3));
                q.setFlags.Add("cave_cleared");
            });
            // Thảo Nguyên Gió, north-west through the crystal forest
            var steppeEnter = Quest("steppe_enter", "Thảo Nguyên Gió", QuestKind.Main, q =>
            {
                q.summary = "Sau Nhện Chúa, gió lùa qua một đường hầm phía tây Rừng Pha Lê. Cuối hầm là một cao nguyên cỏ vàng, gió đổi hướng từng hồi. Dân du mục dựng trại ở đó.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Reach, "Cửa Gió", 1, "Theo đường hầm phía tây Rừng Pha Lê ra Cửa Gió", "windgate"));
                q.objectives.Add(Obj(ObjectiveKind.Reach, "Trại Du Mục", 1, "Tới Trại Du Mục", "nomadcamp"));
                q.xp = 2200;
                q.gold = 200;
                q.items.Add(Reward("potion_red", 3));
            });
            var steppeHyenas = Quest("steppe_hyenas", "Truy Nã: Linh Cẩu Gió", QuestKind.Main, q =>
            {
                q.summary = "Linh Cẩu Gió đi thành bầy, vây quanh con mồi rồi thay nhau lao vào cắn và chạy ra. Thấy vạch dưới đất là con sắp lao: Lướt sang bên.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "hyena", 6, "Hạ Linh Cẩu Gió", "hyenas"));
                q.xp = 2400;
                q.gold = 220;
                q.items.Add(Reward("potion_red", 3));
            });
            var steppeEagles = Quest("steppe_eagles", "Truy Nã: Chim Ưng Đá", QuestKind.Main, q =>
            {
                q.summary = "Chim Ưng Đá lượn trên cao rồi bổ nhào xuống chỗ đã đánh dấu. Ra khỏi vòng trước khi nó rơi xuống, rồi đánh lúc nó còn đậu dưới đất.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "eagle", 4, "Hạ Chim Ưng Đá", "eagles"));
                q.xp = 2600;
                q.gold = 240;
                q.items.Add(Reward("potion_blue", 3));
            });
            var steppeBisons = Quest("steppe_bisons", "Truy Nã: Bò Rừng", QuestKind.Main, q =>
            {
                q.summary = "Bò Rừng cào đất rồi lao thẳng. Đầu sừng của nó cứng như đá: đứng trước một tảng đá rồi né, để nó húc vào đá mà choáng váng. Khe Vực chỉ qua được nhờ Cột Gió.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "bison", 4, "Hạ Bò Rừng", "bisons"));
                q.xp = 2800;
                q.gold = 260;
                q.items.Add(Reward("potion_green", 3));
                q.setFlags.Add("steppe_opened");
            });
            var caveMimic = Quest("cave_mimic", "Lời Đồn: Rương Biết Cắn", QuestKind.Bounty, q =>
            {
                q.summary = "Thợ mỏ kể có một rương kho báu trong hang tự đổi chỗ. Kẻ nào mở nó thì mất vàng. Hạ nó trước khi nó chui xuống đất lần thứ ba để lấy lại gấp đôi.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "mimic", 1, "Tìm và hạ Mimic Tham Lam", "mine"));
                q.xp = 1500;
                q.gold = 200;
                q.items.Add(Reward("gem_red", 1));
            });
            var toadKing = Quest("slay_toadking", "Cóc Tía Ao Độc", QuestKind.Main, q =>
            {
                q.summary = "Cóc Tía ngự giữa Ao Cóc Tía phía bắc đầm. Lưỡi nó kéo người vào vũng độc.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "toadking", 1, "Đánh bại Cóc Tía", "toadpond"));
                q.xp = 600;
                q.gold = 80;
                q.items.Add(Reward("potion_green", 3));
            });
            var snake = Quest("slay_snake", "Xà Mẫu Đầm Lầy", QuestKind.Main, q =>
            {
                q.summary = "Sâu trong đầm, Xà Mẫu cuộn mình giữa hồ nước có bốn gò đất. Dụ nó lao vào gò đất để nó choáng váng.";
                q.autoStart = true;
                q.objectives.Add(Obj(ObjectiveKind.Kill, "snake", 1, "Đánh bại Xà Mẫu Đầm Lầy", "snakelair"));
                q.xp = 1200;
                q.gold = 150;
                q.items.Add(Reward("potion_red", 4));
                q.items.Add(Reward("lotus", 4));
                q.setFlags.Add("swamp_saved");
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
            // one road, harder at every step (players' feedback, 25/09/2026): each region's last step
            // leads on to the next region (the forest's bear → the swamp, the snake mother → the cave)
            Link(talk, null, forest);
            Link(forest, talk, bear);
            Link(bear, forest, swampRoad);
            Link(mushrooms, talk, null);
            Link(swampRoad, bear, swampToads);
            Link(swampToads, swampRoad, swampHunters);
            Link(swampHunters, swampToads, swampMud);
            Link(swampMud, swampHunters, toadKing);
            Link(toadKing, swampMud, snake);
            Link(snake, toadKing, caveEnter);
            Link(swampWisps, swampMud, null);   // a side bounty: wisps only come out at night
            Link(caveEnter, snake, caveBats);
            Link(caveBats, caveEnter, caveSpiders);
            Link(caveSpiders, caveBats, caveGolems);
            Link(caveGolems, caveSpiders, caveBeetles);
            Link(caveBeetles, caveGolems, caveSlimes);
            Link(caveSlimes, caveBeetles, caveEyes);
            Link(caveEyes, caveSlimes, crystalGolem);
            Link(crystalGolem, caveEyes, spiderQueen);
            Link(spiderQueen, crystalGolem, steppeEnter);   // on to Thảo Nguyên Gió
            Link(steppeEnter, spiderQueen, steppeHyenas);
            Link(steppeHyenas, steppeEnter, steppeEagles);
            Link(steppeEagles, steppeHyenas, steppeBisons);
            Link(steppeBisons, steppeEagles, null);   // T62: Hắc Phong and Bò Rừng Sắt go on from here
            Link(caveMimic, caveEnter, null);   // a side bounty: the hidden boss
            // a quest made before the next region existed learns where it leads (kept assets included)
            void Lead(QuestDef q, QuestDef next, string oldText, string newText)
            {
                if (q == null || next == null) return;
                bool changed = false;
                if (!q.followUps.Contains(next))
                {
                    q.followUps.Add(next);
                    changed = true;
                }
                if (oldText != null && q.summary != null && q.summary.Contains(oldText))
                {
                    q.summary = q.summary.Replace(oldText, newText);
                    changed = true;
                }
                if (changed) EditorUtility.SetDirty(q);
            }
            Lead(spiderQueen, steppeEnter, "Xa hơn về phía tây bắc là Thảo Nguyên Gió (sắp mở).",
                 "Đường hầm phía tây Rừng Pha Lê dẫn ra Thảo Nguyên Gió.");
            // the level each step is for, in its summary
            void Rec(QuestDef q, int level)
            {
                if (written.Contains(q) && !q.summary.Contains("Cấp đề nghị")) q.summary += $" Cấp đề nghị: {level}.";
            }
            Rec(forest, 1);
            Rec(bear, 5);
            Rec(swampRoad, 7);
            Rec(swampToads, 8);
            Rec(swampHunters, 9);
            Rec(swampMud, 11);
            Rec(toadKing, 11);
            Rec(snake, 14);
            Rec(swampWisps, 11);
            Rec(caveEnter, 14);
            Rec(caveBats, 14);
            Rec(caveSpiders, 15);
            Rec(caveGolems, 16);
            Rec(caveBeetles, 17);
            Rec(steppeEnter, 20);
            Rec(steppeHyenas, 20);
            Rec(steppeEagles, 21);
            Rec(steppeBisons, 22);
            Rec(caveSlimes, 17);
            Rec(caveEyes, 18);
            Rec(crystalGolem, 18);
            Rec(spiderQueen, 20);
            Rec(caveMimic, 20);
            return new List<QuestDef>
            {
                talk, forest, bear, mushrooms, swampRoad, swampToads, swampMud, toadKing, snake, swampHunters, swampWisps,
                caveEnter, caveBats, caveSpiders, caveGolems, caveBeetles, caveSlimes, caveEyes, crystalGolem, spiderQueen, caveMimic,
                steppeEnter, steppeHyenas, steppeEagles, steppeBisons,
            };
        }

        // ------------------------------------------------------------------ database
        static void CreateDatabase(List<ItemDef> items, List<AbilityDef> abilities, ProgressionConfig progression, CombatConfig combat,
                                   List<QuestDef> quests, List<ZoneDef> zones)
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
            db.abilities = Merge(db.abilities, abilities);
            EditorUtil.Assign(ref db.progression, progression);
            EditorUtil.Assign(ref db.combat, combat);
            db.quests = Merge(db.quests, quests);
            db.zones = Merge(db.zones, zones);
            EditorUtil.Assign(ref db.startZone, zones.Count > 0 ? zones[0] : null);
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
            EditorUtil.Assign(ref db.spriteLitFX, SpriteLitFX);
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
