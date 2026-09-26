using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// The skills of the twelve classes (ClassDef.kit) and the basic attacks of the weapon kinds
    /// (WeaponKinds): made from the same blocks as the prototype's skills — swings, bolts, bursts,
    /// storms, pulses, buffs, heals and dashes — after the D&D spells and features they are named
    /// for (Rage, Thunderwave, Sacred Flame, Hunter's Mark, Eldritch Blast, Magic Missile…).
    /// Runs after the VFX step, whose projectiles it points at. Authoring mode: existing assets stay.
    /// </summary>
    public static class ClassAbilityFactory
    {
        const string Folder = AssetFactory.DataFolder + "/Abilities";
        const string Gameplay = "Assets/Prefabs/Gameplay";

        static readonly Anchor Caster = new Anchor(Anchor.From.Caster);
        static readonly Anchor Chest = new Anchor(Anchor.From.Caster, 0.45f);
        static readonly Anchor Hand = new Anchor(Anchor.From.Caster, 0.55f, 0.5f);
        static readonly Anchor Point = new Anchor(Anchor.From.Point);
        static readonly Anchor Aim = new Anchor(Anchor.From.Aim);
        static readonly Anchor Origin = new Anchor(Anchor.From.Origin);

        /// <summary>Skills drawn with an older icon (the bear's stomp and roar).</summary>
        static string IconOf(string id) => id == "slam" ? "sk_stomp" : id == "warcry" ? "sk_roar" : "sk_" + id;

        static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Gameplay}/{name}.prefab");

        static AbilityDef Ability(string id, string name, AbilityTags tags, float cd, float cost, string anim, float lockTime, string desc,
                                  System.Action<AbilityDef> fill)
        {
            string path = $"{Folder}/{id}.asset";
            EditorUtil.EnsureFolder(Folder);
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
            a.icon = ArtImporter.S(IconOf(id));
            a.tags = tags;
            a.cooldown = cd;
            a.energyCost = cost;
            a.animBase = anim;
            a.lockTime = lockTime;
            a.description = desc;
            a.commitTime = Mathf.Min(0.15f, lockTime);
            fill(a);
            EditorUtility.SetDirty(a);
            return a;
        }

        // ------------------------------------------------------------------ blocks
        static CueEffect Cue(Anchor at, string vfx, string sfx = null, float vol = 0.8f, float shake = 0f, float scale = 1f) =>
            new CueEffect { at = at, vfx = vfx, vfxScale = scale, sfx = sfx, sfxVolume = vol, shake = shake, sfxAtPoint = at.from != Anchor.From.Caster };

        static HitSpec Hit(float power, DamageType type, float crit = 0.1f, float knock = 3f, float poise = 4f, StatusHit status = default) =>
            new HitSpec { power = power, type = type, critChance = crit, knockback = knock, poise = poise, status = status };

        /// <summary>A weapon's swing in front of the hero (the prototype's Chém Gió, reshaped).</summary>
        static List<AbilityEffect> Swing(float radius, float angle, HitSpec hit, string vfx, float vfxScale, string sfx, float shake, float hitStop)
        {
            hit.hitStop = hitStop;
            return new List<AbilityEffect>
            {
                new CueEffect
                {
                    delay = 0.05f, at = new Anchor(Anchor.From.Caster, 0.45f, 0.55f), vfx = vfx, vfxScale = vfxScale, rotateToDirection = true,
                    flipOnOddCombo = true, sfx = "sfx_swing", sfxVolume = 0.8f, sfxPitchVariance = 0.1f
                },
                new DamageEffect
                {
                    delay = 0.05f, shape = DamageEffect.Shape.Cone, at = Chest, radius = radius, angle = angle, hit = hit,
                    onAnyHit = new List<AbilityEffect> { new CueEffect { sfx = sfx, sfxVolume = 0.9f, sfxPitchVariance = 0.1f, shake = shake } }
                }
            };
        }

        static ComboEffect Combo(List<AbilityEffect> a, List<AbilityEffect> b, List<AbilityEffect> finisher, float window = 0.9f) => new ComboEffect
        {
            window = window,
            stages =
            {
                new ComboEffect.Stage { name = "Đòn 1", effects = a },
                new ComboEffect.Stage { name = "Đòn 2", effects = b },
                new ComboEffect.Stage { name = "Đòn cuối", effects = finisher },
            }
        };

        static ProjectileEffect Bolt(string prefab, float speed, HitSpec hit, string hitVfx, string hitSfx = "sfx_hit", float explode = 0f, bool pierce = false,
                                     float angle = 0f, float delay = 0f) => new ProjectileEffect
        {
            delay = delay, prefab = Prefab(prefab), spawn = Hand, speed = speed, hit = hit, hitVfx = hitVfx, hitSfx = hitSfx,
            explodeRadius = explode, pierce = pierce, angle = angle, hitShake = 0.08f
        };

        static DamageEffect Circle(Anchor at, float radius, HitSpec hit, float delay = 0f, string sfx = null, float shake = 0f)
        {
            var d = new DamageEffect { delay = delay, shape = DamageEffect.Shape.Circle, at = at, radius = radius, hit = hit };
            if (sfx != null || shake > 0f) d.onAnyHit.Add(new CueEffect { sfx = sfx, shake = shake });
            return d;
        }

        static DamageEffect Cone(float radius, float angle, HitSpec hit, float delay = 0f, string sfx = "sfx_hit", float shake = 0.1f)
        {
            var d = new DamageEffect { delay = delay, shape = DamageEffect.Shape.Cone, at = Chest, radius = radius, angle = angle, hit = hit };
            d.onAnyHit.Add(new CueEffect { sfx = sfx, shake = shake });
            return d;
        }

        static BurstEffect Storm(float radius, int count, float interval, float charge, string areaVfx, float areaScale, params AbilityEffect[] each)
        {
            var b = new BurstEffect
            {
                center = Aim, radius = radius, count = count, chargeTime = charge, interval = interval, intervalJitter = 0.3f,
                preferTargets = 0.7f, firstAtCenter = true, areaVfx = areaVfx, areaVfxScale = areaScale
            };
            b.each.AddRange(each);
            return b;
        }

        static BuffEffect Buff(string id, string name, float duration, System.Action<BuffSpec> fill, float allies = 0f)
        {
            var spec = new BuffSpec { id = id, displayName = name, icon = ArtImporter.S("sk_" + id), duration = duration };
            fill(spec);
            return new BuffEffect { buff = spec, allies = allies };
        }

        static void Dash(AbilityDef a, float distance, float time, bool away = false, string start = "dash_burst", string end = "step_dust")
        {
            a.moveWhileCasting = 1f;
            a.effects.Add(new DashEffect
            {
                distance = distance, time = time, invulnerableTime = time + 0.1f, preferMoveDirection = false, away = away,
                startVfx = start, endVfx = end, perfectDodge = false
            });
        }

        static StatusHit Status(float stun = 0f, float root = 0f, float slow = 0f, float slowFor = 0f, int burn = 0, int chill = 0, int poison = 0,
                                int charge = 0, float curse = 0f, float judgment = 0f) =>
            new StatusHit { stun = stun, root = root, slow = slow, slowDuration = slowFor, burn = burn, chill = chill, poison = poison, charge = charge, curse = curse, judgment = judgment };

        // ------------------------------------------------------------------ the skills
        public static List<AbilityDef> CreateAll()
        {
            var P = DamageType.Physical;
            var F = DamageType.Fire;
            var L = DamageType.Lightning;
            var Po = DamageType.Poison;
            var Ho = DamageType.Holy;
            var Dk = DamageType.Dark;
            var I = DamageType.Ice;
            var melee = AbilityTags.Physical | AbilityTags.Melee;
            var list = new List<AbilityDef>
            {
                // ============================================== weapons' basic attacks (Q)
                Ability("cleave", "Bổ Rìu", melee, 0.62f, 0f, "attack", 0.34f,
                    "Bổ rìu rộng nửa vòng tròn, hất lùi kẻ địch. Đòn thứ 3 bổ cực mạnh.", a =>
                    {
                        a.moveWhileCasting = 0.3f;
                        a.maxRange = 12f;
                        a.effects.Add(Combo(
                            Swing(2.2f, 170f, Hit(1.1f, P, 0.12f, 5f, 5f), "slash", 1.15f, "sfx_hit", 0.1f, 0.04f),
                            Swing(2.2f, 170f, Hit(1.1f, P, 0.12f, 5f, 5f), "slash", 1.15f, "sfx_hit", 0.1f, 0.04f),
                            Swing(2.5f, 180f, Hit(1.9f, P, 0.18f, 9f, 12f), "slash_big", 1.45f, "sfx_hit_heavy", 0.25f, 0.08f)));
                    }),
                Ability("smash", "Nện", melee, 0.5f, 0f, "attack", 0.3f,
                    "Nện mạnh về phía trước. Đòn thứ 3 làm choáng.", a =>
                    {
                        a.moveWhileCasting = 0.3f;
                        a.maxRange = 12f;
                        a.effects.Add(Combo(
                            Swing(1.9f, 130f, Hit(1.0f, P, 0.1f, 4f, 6f), "slash", 1f, "sfx_hit", 0.1f, 0.04f),
                            Swing(1.9f, 130f, Hit(1.0f, P, 0.1f, 4f, 6f), "slash", 1f, "sfx_hit", 0.1f, 0.04f),
                            Swing(2.1f, 140f, Hit(1.7f, P, 0.15f, 6f, 14f, Status(stun: 0.5f)), "slash_big", 1.3f, "sfx_hit_heavy", 0.22f, 0.07f)));
                    }),
                Ability("thrust", "Đâm Thẳng", melee, 0.45f, 0f, "attack", 0.26f,
                    "Đâm thẳng xa và hẹp, xuyên qua mọi kẻ địch trên đường. Đòn thứ 3 đâm mạnh hơn.", a =>
                    {
                        a.moveWhileCasting = 0.35f;
                        a.maxRange = 12f;
                        a.effects.Add(Combo(
                            Swing(2.8f, 45f, Hit(0.95f, P, 0.15f, 3f, 3f), "slash", 0.9f, "sfx_hit", 0.08f, 0.03f),
                            Swing(2.8f, 45f, Hit(0.95f, P, 0.15f, 3f, 3f), "slash", 0.9f, "sfx_hit", 0.08f, 0.03f),
                            Swing(3.1f, 50f, Hit(1.6f, P, 0.25f, 6f, 8f), "slash_big", 1.1f, "sfx_hit_heavy", 0.18f, 0.06f)));
                    }),
                Ability("stab", "Đâm Nhanh", melee, 0.28f, 0f, "attack", 0.18f,
                    "Đâm dao thật nhanh, dễ chí mạng. Đòn thứ 3 đâm hai nhát.", a =>
                    {
                        a.moveWhileCasting = 0.5f;
                        a.maxRange = 12f;
                        var finisher = Swing(1.7f, 100f, Hit(0.75f, P, 0.4f, 2f, 3f), "slash", 0.85f, "sfx_hit", 0.08f, 0.03f);
                        finisher.Add(new DamageEffect { delay = 0.12f, shape = DamageEffect.Shape.Cone, at = Chest, radius = 1.7f, angle = 100f, hit = Hit(0.75f, P, 0.4f, 3f, 3f) });
                        a.effects.Add(Combo(
                            Swing(1.6f, 90f, Hit(0.62f, P, 0.3f, 1.5f, 2f), "slash", 0.75f, "sfx_hit", 0.05f, 0.02f),
                            Swing(1.6f, 90f, Hit(0.62f, P, 0.3f, 1.5f, 2f), "slash", 0.75f, "sfx_hit", 0.05f, 0.02f),
                            finisher, 0.7f));
                    }),
                Ability("flurry", "Liên Quyền", melee, 0.3f, 0f, "attack", 0.2f,
                    "Hai cú đấm liền tay; đòn thứ 3 là cú đá hất bay.", a =>
                    {
                        a.moveWhileCasting = 0.5f;
                        a.maxRange = 12f;
                        a.effects.Add(Combo(
                            Swing(1.5f, 100f, Hit(0.55f, P, 0.15f, 1.5f, 3f), "hit_spark", 1f, "sfx_hit", 0.05f, 0.02f),
                            Swing(1.5f, 100f, Hit(0.55f, P, 0.15f, 1.5f, 3f), "hit_spark", 1f, "sfx_hit", 0.05f, 0.02f),
                            Swing(1.8f, 110f, Hit(1.3f, P, 0.2f, 8f, 10f), "slash_big", 1f, "sfx_hit_heavy", 0.15f, 0.06f), 0.7f));
                    }),
                Ability("arrow", "Bắn Tên", AbilityTags.Physical | AbilityTags.Projectile, 0.5f, 0f, "attack", 0.25f,
                    "Bắn một mũi tên nhanh và xa.", a =>
                    {
                        a.maxRange = 13f;
                        a.castSfx = "sfx_swing";
                        a.effects.Add(Bolt("Arrow", 17f, Hit(0.95f, P, 0.15f, 2f, 3f), "hit_spark"));
                    }),

                // ============================================== spellcasters' cantrips (Q with a focus)
                Ability("notes", "Nốt Nhạc Xung Kích", AbilityTags.Lightning | AbilityTags.Projectile, 0.55f, 0f, "cast", 0.25f,
                    "Gảy một nốt nhạc bay tới kẻ địch, vang như sấm nhỏ.", a =>
                    {
                        a.maxRange = 11f;
                        a.effects.Add(Bolt("NoteBolt", 12f, Hit(0.9f, L, 0.1f, 2f, 3f), "note_hit"));
                    }),
                Ability("sacredflame", "Ngọn Lửa Thánh", AbilityTags.Holy | AbilityTags.Area, 0.8f, 0f, "cast", 0.28f,
                    "Ngọn lửa thánh bùng lên dưới chân kẻ địch tại vị trí chuột.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 9f;
                        a.effects.Add(Cue(Point, "holy_hit", "sfx_shield", 0.5f, 0.05f));
                        a.effects.Add(Circle(Point, 1.1f, Hit(1.05f, Ho, 0.1f, 2f, 4f), 0.05f));
                    }),
                Ability("thornwhip", "Roi Gai", AbilityTags.Poison | AbilityTags.Melee, 0.6f, 0f, "attack", 0.28f,
                    "Quất roi gai xa, kéo kẻ địch lại gần.", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.45f, 0.6f), "slash", "sfx_swing", 0.7f));
                        a.effects.Add(Cone(3.2f, 35f, Hit(0.95f, Po, 0.1f, -4f, 3f)));
                    }),
                Ability("firebolt", "Tia Lửa", AbilityTags.Fire | AbilityTags.Projectile, 0.55f, 0f, "cast", 0.25f,
                    "Bắn một tia lửa nhỏ.", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Bolt("FireBolt", 14f, Hit(0.95f, F, 0.12f, 2f, 3f), "hit_fire"));
                    }),
                Ability("eldritch", "Tia Hắc Ám", AbilityTags.Dark | AbilityTags.Projectile, 0.6f, 0f, "cast", 0.25f,
                    "Tia năng lượng hắc ám từ khế ước, đẩy lùi kẻ trúng đòn.", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Bolt("DarkBolt", 15f, Hit(1.0f, Dk, 0.1f, 4.5f, 4f), "dark_hit"));
                    }),
                Ability("missile", "Tên Ma Thuật", AbilityTags.Lightning | AbilityTags.Projectile, 0.6f, 0f, "cast", 0.28f,
                    "Ba mũi tên ma thuật bay ra liền nhau.", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Bolt("ArcaneBolt", 14f, Hit(0.38f, L, 0.1f, 1f, 2f), "arcane_hit", "sfx_hit", 0f, false, -8f));
                        a.effects.Add(Bolt("ArcaneBolt", 14f, Hit(0.38f, L, 0.1f, 1f, 2f), "arcane_hit", "sfx_hit", 0f, false, 0f, 0.06f));
                        a.effects.Add(Bolt("ArcaneBolt", 14f, Hit(0.38f, L, 0.1f, 1f, 2f), "arcane_hit", "sfx_hit", 0f, false, 8f, 0.12f));
                    }),

                // ============================================== Cuồng Chiến Binh
                Ability("rage", "Cuồng Nộ", AbilityTags.Physical | AbilityTags.Support, 22f, 10f, "cast", 0.3f,
                    "Nổi cơn thịnh nộ trong 8 giây: gây thêm 30% sát thương, nhận ít hơn 25%, không bị choáng.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_enrage";
                        a.effects.Add(Buff("rage", "Cuồng Nộ", 8f, b =>
                        {
                            b.damageDealtMultiplier = 1.3f;
                            b.damageTakenMultiplier = 0.75f;
                            b.stunImmune = true;
                            b.clearStun = true;
                            b.attachedVfx = "enrage_aura";
                        }));
                        a.effects.Add(Cue(Caster, "boss_roar", null, 0.8f, 0.15f, 0.6f));
                    }),
                Ability("slam", "Nện Đất", AbilityTags.Physical | AbilityTags.Area, 8f, 14f, "attack", 0.4f,
                    "Nện xuống đất, gây sát thương xung quanh và làm choáng.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(Cue(Caster, "stomp_shockwave", "sfx_boss_stomp", 0.9f, 0.3f, 0.9f));
                        a.effects.Add(Circle(Caster, 2.6f, Hit(1.6f, P, 0.1f, 6f, 12f, Status(stun: 0.8f)), 0.05f));
                    }),
                Ability("axethrow", "Ném Rìu", AbilityTags.Physical | AbilityTags.Projectile, 5f, 10f, "attack", 0.32f,
                    "Ném rìu xoay tròn xuyên qua mọi kẻ địch trên đường.", a =>
                    {
                        a.maxRange = 11f;
                        a.castSfx = "sfx_rock_throw";
                        a.effects.Add(Bolt("ThrownAxe", 13f, Hit(1.7f, P, 0.15f, 6f, 10f), "hit_spark", "sfx_hit_heavy", 0f, true));
                    }),
                Ability("warcry", "Tiếng Gầm Chiến", AbilityTags.Physical | AbilityTags.Area, 14f, 12f, "cast", 0.35f,
                    "Gầm vang làm kẻ địch xung quanh khiếp sợ: Nguyền (gây ít sát thương hơn) và chậm 30% trong 4 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(Cue(Caster, "boss_roar", "sfx_boss_roar", 0.6f, 0.2f, 0.7f));
                        a.effects.Add(Circle(Caster, 4.5f, Hit(0.3f, P, 0f, 2f, 3f, Status(curse: 4f, slow: 0.3f, slowFor: 4f)), 0.1f));
                    }),
                Ability("endure", "Bất Khuất", AbilityTags.Physical | AbilityTags.Support, 18f, 10f, "cast", 0.25f,
                    "Nghiến răng chịu đòn: hồi 12% máu, nhận ít hơn 50% và không bị choáng trong 4 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(new HealEffect { instantPercentOfMaxHp = 0.12f });
                        a.effects.Add(Buff("endure", "Bất Khuất", 4f, b =>
                        {
                            b.damageTakenMultiplier = 0.5f;
                            b.stunImmune = true;
                            b.clearStun = true;
                            b.attachedVfx = "enrage_aura";
                        }));
                    }),

                // ============================================== Thi Sĩ
                Ability("soundwave", "Sóng Âm", AbilityTags.Lightning | AbilityTags.Area, 5f, 10f, "cast", 0.3f,
                    "Một hợp âm vang như sấm phía trước, hất bay kẻ địch.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.4f, 1f), "stomp_shockwave", "sfx_thunder", 0.5f, 0.15f, 0.8f));
                        a.effects.Add(Cone(4f, 90f, Hit(1.3f, L, 0.1f, 9f, 8f), 0.05f));
                    }),
                Ability("mockery", "Lời Chế Nhạo", AbilityTags.Dark | AbilityTags.Projectile, 4f, 8f, "cast", 0.25f,
                    "Lời chế nhạo cay độc làm kẻ trúng đòn bị Nguyền 4 giây: gây ít sát thương hơn.", a =>
                    {
                        a.maxRange = 11f;
                        a.effects.Add(Bolt("NoteBolt", 13f, Hit(1.1f, Dk, 0.1f, 2f, 3f, Status(curse: 4f)), "note_hit"));
                    }),
                Ability("anthem", "Khúc Ca Hùng Tráng", AbilityTags.Lightning | AbilityTags.Support, 20f, 20f, "cast", 0.4f,
                    "Khúc ca cổ vũ bản thân và đồng đội quanh 7 ô: gây thêm 20% sát thương, đi nhanh hơn 10% trong 8 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_quest";
                        a.effects.Add(Buff("anthem", "Khúc Ca Hùng Tráng", 8f, b =>
                        {
                            b.damageDealtMultiplier = 1.2f;
                            b.speedMultiplier = 1.1f;
                            b.attachedVfx = "holy_aura";
                        }, 7f));
                    }),
                Ability("song", "Khúc Hát Chữa Lành", AbilityTags.Holy | AbilityTags.Support, 14f, 18f, "cast", 0.35f,
                    "Hồi ngay 30 máu và thêm 5 máu mỗi giây trong 4 giây cho bản thân và đồng đội quanh 7 ô.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_heal";
                        a.effects.Add(new HealEffect { instant = 30f, perSecond = 5f, duration = 4f, auraVfx = "heal_aura", allies = 7f });
                        a.effects.Add(Cue(Caster, "heal_burst"));
                    }),
                Ability("misty", "Bước Sương", AbilityTags.Movement, 7f, 8f, "", 0.05f,
                    "Tan vào sương mù rồi hiện ra cách đó 5 ô, về phía chuột.", a =>
                    {
                        a.castSfx = "sfx_wisp";
                        Dash(a, 5f, 0.06f, false, "wisp_blink", "wisp_blink");
                    }),
                Ability("hypnotic", "Mê Hoặc", AbilityTags.Lightning | AbilityTags.Area | AbilityTags.Ultimate, 22f, 25f, "cast", 0.5f,
                    "Giai điệu mê hoặc làm mọi kẻ địch phía trước đứng sững 2.2 giây.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.4f, 2f), "crystal_burst", "sfx_wisp_burst", 0.6f, 0.1f, 1.6f));
                        a.effects.Add(Cone(5f, 100f, Hit(0.5f, L, 0f, 0f, 4f, Status(stun: 2.2f)), 0.1f));
                    }),

                // ============================================== Tu Sĩ
                Ability("guidingbolt", "Tia Dẫn Lối", AbilityTags.Holy | AbilityTags.Projectile, 4f, 12f, "cast", 0.3f,
                    "Tia sáng thần thánh đánh dấu kẻ địch: nó chịu thêm sát thương (Phán Xét) trong 5 giây.", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Bolt("HolyBolt", 14f, Hit(1.9f, Ho, 0.12f, 3f, 6f, Status(judgment: 5f)), "holy_hit", "sfx_hit"));
                    }),
                Ability("spiritguard", "Tinh Linh Hộ Vệ", AbilityTags.Holy | AbilityTags.Area, 16f, 18f, "cast", 0.35f,
                    "Tinh linh thánh xoay quanh bản thân 5 giây, đốt và làm chậm kẻ địch lại gần.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(new PulseEffect
                        {
                            duration = 5f, interval = 0.5f, at = Caster, attachedVfx = "holy_aura",
                            each = { Circle(Caster, 2.6f, Hit(0.35f, Ho, 0.05f, 0.5f, 1f, Status(slow: 0.25f, slowFor: 1f))) }
                        });
                    }),
                Ability("lightpillar", "Cột Sáng", AbilityTags.Holy | AbilityTags.Area, 12f, 22f, "cast", 0.45f,
                    "Gọi năm cột sáng giáng xuống quanh vị trí chuột.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Storm(2.5f, 5, 0.16f, 0.35f, "holy_aura", 1.3f,
                            Cue(Point, "holy_strike", "sfx_thunder", 0.4f, 0.12f),
                            Circle(Point, 1.2f, Hit(1.6f, Ho, 0.12f, 2f, 8f))));
                    }),
                Ability("radiance", "Hào Quang Hồi Sinh", AbilityTags.Holy | AbilityTags.Area | AbilityTags.Ultimate, 26f, 30f, "cast", 0.55f,
                    "Bùng nổ ánh sáng: hồi 60 máu cho bản thân và đồng đội quanh 7 ô, đánh bật kẻ địch quanh 4 ô.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_heal";
                        a.effects.Add(new HealEffect { instant = 60f, allies = 7f });
                        a.effects.Add(Cue(Caster, "holy_strike", "sfx_levelup", 0.6f, 0.25f, 1.4f));
                        a.effects.Add(Circle(Caster, 4f, Hit(2.2f, Ho, 0.15f, 8f, 14f), 0.1f));
                    }),

                // ============================================== Tế Sư Rừng Xanh
                Ability("entangle", "Rễ Trói", AbilityTags.Poison | AbilityTags.Area, 10f, 14f, "cast", 0.35f,
                    "Rễ cây trồi lên tại vị trí chuột, trói chân kẻ địch 2 giây và gây Độc.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Cue(Point, "nature_burst", "sfx_web", 0.7f, 0.08f, 1.3f));
                        a.effects.Add(Circle(Point, 2.4f, Hit(0.6f, Po, 0.05f, 0f, 3f, Status(root: 2.2f, poison: 1)), 0.05f));
                    }),
                Ability("thunderwave", "Sóng Sấm", AbilityTags.Lightning | AbilityTags.Area, 6f, 12f, "cast", 0.3f,
                    "Sóng sấm phía trước hất văng kẻ địch.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.4f, 1f), "stomp_shockwave", "sfx_thunder", 0.6f, 0.2f, 0.9f));
                        a.effects.Add(Cone(3.5f, 110f, Hit(1.4f, L, 0.1f, 10f, 10f), 0.05f));
                    }),
                Ability("barkskin", "Da Vỏ Cây", AbilityTags.Poison | AbilityTags.Support, 20f, 12f, "cast", 0.3f,
                    "Da cứng như vỏ cây trong 8 giây: nhận ít hơn 40% sát thương.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(Buff("barkskin", "Da Vỏ Cây", 8f, b =>
                        {
                            b.damageTakenMultiplier = 0.6f;
                            b.attachedVfx = "shield_bubble";
                        }));
                        a.effects.Add(Cue(Caster, "nature_burst"));
                    }),
                Ability("thornstorm", "Bão Gai", AbilityTags.Poison | AbilityTags.Area | AbilityTags.Ultimate, 18f, 28f, "cast", 0.5f,
                    "Mưa gai nhọn trút xuống quanh vị trí chuột, gây Độc.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Storm(3.5f, 12, 0.1f, 0.4f, "holy_aura", 1.8f,
                            Cue(Point, "nature_burst", "sfx_web", 0.4f, 0.05f, 0.8f),
                            Circle(Point, 1f, Hit(0.9f, Po, 0.08f, 1f, 3f, Status(poison: 1)))));
                    }),

                // ============================================== Chiến Binh
                Ability("charge", "Xung Phong", AbilityTags.Physical | AbilityTags.Movement, 7f, 10f, "attack", 0.3f,
                    "Lao tới 5 ô về phía chuột, húc bay và làm choáng kẻ địch ở chỗ dừng.", a =>
                    {
                        a.castSfx = "sfx_dash";
                        Dash(a, 5f, 0.2f);
                        a.effects.Add(Cue(Caster, "stomp_shockwave", "sfx_hit_heavy", 0.8f, 0.2f, 0.6f));
                        a.effects[a.effects.Count - 1].delay = 0.2f;
                        a.effects.Add(Circle(Caster, 1.8f, Hit(1.3f, P, 0.1f, 8f, 12f, Status(stun: 0.4f)), 0.2f));
                    }),
                Ability("whirl", "Chém Xoáy", AbilityTags.Physical | AbilityTags.Area, 5f, 10f, "attack", 0.35f,
                    "Xoay người chém một vòng quanh mình.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(Cue(Caster, "stomp_shockwave", "sfx_bladestorm", 0.7f, 0.12f, 0.7f));
                        a.effects.Add(Circle(Chest, 2.4f, Hit(1.5f, P, 0.15f, 5f, 8f), 0.05f, "sfx_hit", 0.1f));
                    }),
                Ability("secondwind", "Hồi Sức", AbilityTags.Physical | AbilityTags.Support, 22f, 5f, "cast", 0.25f,
                    "Lấy lại hơi sức: hồi ngay 30% máu.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_heal";
                        a.effects.Add(new HealEffect { instantPercentOfMaxHp = 0.3f });
                        a.effects.Add(Cue(Caster, "heal_burst"));
                    }),
                Ability("parry", "Thế Thủ", AbilityTags.Physical | AbilityTags.Support, 12f, 8f, "cast", 0.2f,
                    "Thủ thế 3 giây: nhận ít hơn 65% sát thương, không bị choáng.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(Buff("parry", "Thế Thủ", 3f, b =>
                        {
                            b.damageTakenMultiplier = 0.35f;
                            b.stunImmune = true;
                            b.speedMultiplier = 0.7f;
                            b.attachedVfx = "shield_bubble";
                        }));
                    }),
                Ability("surge", "Bùng Nổ Hành Động", AbilityTags.Physical | AbilityTags.Support, 25f, 10f, "cast", 0.25f,
                    "Dồn hết sức trong 6 giây: đánh thường nhanh hơn 60%, gây thêm 10% sát thương, đi nhanh hơn.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_enrage";
                        a.effects.Add(Buff("surge", "Bùng Nổ Hành Động", 6f, b =>
                        {
                            b.attackSpeedBonus = 0.6f;
                            b.damageDealtMultiplier = 1.1f;
                            b.speedMultiplier = 1.15f;
                            b.attachedVfx = "enrage_aura";
                        }));
                    }),

                // ============================================== Võ Tăng
                Ability("flyingkick", "Cước Phi", AbilityTags.Physical | AbilityTags.Movement, 5f, 8f, "attack", 0.28f,
                    "Phi thân 4.5 ô đá bay kẻ địch ở chỗ đáp.", a =>
                    {
                        a.castSfx = "sfx_dash";
                        Dash(a, 4.5f, 0.18f);
                        a.effects.Add(Circle(Caster, 1.6f, Hit(1.4f, P, 0.15f, 9f, 10f), 0.18f, "sfx_hit_heavy", 0.15f));
                    }),
                Ability("stunstrike", "Chưởng Choáng", AbilityTags.Physical | AbilityTags.Melee, 7f, 10f, "attack", 0.3f,
                    "Chưởng vào huyệt, kẻ địch phía trước bị choáng 1.2 giây.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.45f, 0.8f), "hit_spark", "sfx_hit_heavy", 0.8f, 0.1f, 1.4f));
                        a.effects.Add(Cone(1.9f, 90f, Hit(1.4f, P, 0.1f, 3f, 12f, Status(stun: 1.2f)), 0.05f));
                    }),
                Ability("flurryblows", "Liên Hoàn Cước", AbilityTags.Physical | AbilityTags.Channel, 9f, 14f, "attack", 0.3f,
                    "Tung liên hoàn cước quanh mình trong 1.2 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.moveWhileCasting = 0.8f;
                        a.effects.Add(new PulseEffect
                        {
                            duration = 1.2f, interval = 0.15f, at = Chest, loopSfx = "sfx_swing", loopSfxVolume = 0.5f, loopSfxInterval = 0.15f,
                            each = { Cue(Chest, "hit_spark", null, 0.5f, 0.03f), Circle(Chest, 1.8f, Hit(0.35f, P, 0.1f, 1.5f, 3f)) }
                        });
                    }),
                Ability("patience", "Tĩnh Tâm", AbilityTags.Physical | AbilityTags.Support, 16f, 10f, "cast", 0.3f,
                    "Tĩnh tâm 4 giây: nhận ít hơn 50% sát thương và hồi 6 máu mỗi giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_heal";
                        a.effects.Add(Buff("patience", "Tĩnh Tâm", 4f, b =>
                        {
                            b.damageTakenMultiplier = 0.5f;
                            b.attachedVfx = "holy_aura";
                        }));
                        a.effects.Add(new HealEffect { perSecond = 6f, duration = 4f });
                    }),
                Ability("windstep", "Bước Gió", AbilityTags.Physical | AbilityTags.Support, 12f, 6f, "cast", 0.15f,
                    "Chân nhẹ như gió: đi nhanh hơn 40% trong 5 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_dash";
                        a.effects.Add(Buff("windstep", "Bước Gió", 5f, b =>
                        {
                            b.speedMultiplier = 1.4f;
                            b.attachedVfx = "shield_bubble";
                        }));
                    }),
                Ability("palm", "Chưởng Rung Động", AbilityTags.Physical | AbilityTags.Melee | AbilityTags.Ultimate, 20f, 25f, "attack", 0.55f,
                    "Dồn khí vào một chưởng: sát thương cực lớn và hất bay kẻ địch phía trước.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.45f, 0.9f), "stomp_shockwave", "sfx_boss_stomp", 0.9f, 0.3f, 0.8f));
                        a.effects[a.effects.Count - 1].delay = 0.3f;
                        a.effects.Add(Cone(2.2f, 60f, Hit(4f, P, 0.2f, 12f, 30f), 0.3f, "sfx_hit_heavy", 0.3f));
                    }),

                // ============================================== Hiệp Sĩ Thánh
                Ability("smite", "Trừng Phạt Thánh", AbilityTags.Holy | AbilityTags.Melee, 6f, 14f, "attack", 0.35f,
                    "Chém một nhát mang sức mạnh thần thánh; kẻ trúng đòn bị Phán Xét 3 giây.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.45f, 0.8f), "holy_strike", "sfx_hit_heavy", 0.8f, 0.2f, 0.7f));
                        a.effects.Add(Cone(2.2f, 140f, Hit(2.2f, Ho, 0.15f, 6f, 12f, Status(judgment: 3f)), 0.05f));
                    }),
                Ability("layonhands", "Đặt Tay", AbilityTags.Holy | AbilityTags.Support, 24f, 16f, "cast", 0.35f,
                    "Đặt tay chữa lành: hồi 35% máu cho bản thân và đồng đội quanh 4 ô.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_heal";
                        a.effects.Add(new HealEffect { instantPercentOfMaxHp = 0.35f, allies = 4f });
                        a.effects.Add(Cue(Caster, "heal_burst"));
                    }),
                Ability("auraprotect", "Hào Quang Hộ Vệ", AbilityTags.Holy | AbilityTags.Support, 22f, 16f, "cast", 0.35f,
                    "Hào quang che chở bản thân và đồng đội quanh 7 ô: nhận ít hơn 20% sát thương trong 10 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(Buff("auraprotect", "Hào Quang Hộ Vệ", 10f, b =>
                        {
                            b.damageTakenMultiplier = 0.8f;
                            b.attachedVfx = "holy_aura";
                        }, 7f));
                    }),
                Ability("holycharge", "Xung Phong Thánh", AbilityTags.Holy | AbilityTags.Movement, 8f, 12f, "attack", 0.3f,
                    "Lao tới 5 ô trong ánh sáng thánh, giáng đòn ở chỗ dừng.", a =>
                    {
                        a.castSfx = "sfx_dash";
                        Dash(a, 5f, 0.2f);
                        a.effects.Add(Cue(Caster, "holy_strike", "sfx_hit_heavy", 0.8f, 0.2f, 0.6f));
                        a.effects[a.effects.Count - 1].delay = 0.2f;
                        a.effects.Add(Circle(Caster, 2f, Hit(1.5f, Ho, 0.1f, 7f, 12f, Status(stun: 0.3f)), 0.2f));
                    }),
                Ability("heavenfall", "Phán Quyết Trời Cao", AbilityTags.Holy | AbilityTags.Area | AbilityTags.Ultimate, 24f, 30f, "cast", 0.55f,
                    "Bảy cột sáng phán quyết giáng xuống quanh vị trí chuột, làm choáng.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Storm(3.5f, 7, 0.14f, 0.45f, "holy_aura", 2f,
                            Cue(Point, "holy_strike", "sfx_thunder", 0.5f, 0.18f),
                            Circle(Point, 1.3f, Hit(1.8f, Ho, 0.12f, 3f, 8f, Status(stun: 0.5f)))));
                    }),

                // ============================================== Du Hiệp
                Ability("volley", "Mưa Tên", AbilityTags.Physical | AbilityTags.Area, 9f, 16f, "cast", 0.4f,
                    "Bắn mưa tên xuống quanh vị trí chuột.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 12f;
                        a.effects.Add(Storm(3f, 10, 0.08f, 0.3f, null, 1f,
                            Cue(Point, "hit_spark", "sfx_hit", 0.35f, 0.03f),
                            Circle(Point, 0.9f, Hit(0.8f, P, 0.12f, 1.5f, 2f))));
                    }),
                Ability("piercing", "Tên Xuyên Tâm", AbilityTags.Physical | AbilityTags.Projectile, 5f, 10f, "attack", 0.35f,
                    "Mũi tên cực mạnh xuyên qua mọi kẻ địch trên đường.", a =>
                    {
                        a.maxRange = 14f;
                        a.castSfx = "sfx_swing";
                        a.effects.Add(Bolt("Arrow", 22f, Hit(1.8f, P, 0.25f, 5f, 8f), "hit_spark", "sfx_hit_heavy", 0f, true));
                    }),
                Ability("trap", "Bẫy Gai", AbilityTags.Physical | AbilityTags.Area, 12f, 12f, "cast", 0.3f,
                    "Ném bẫy gai tới vị trí chuột: trói chân kẻ địch 2.5 giây và gây Độc.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 9f;
                        a.effects.Add(Cue(Point, "nature_burst", "sfx_web", 0.7f, 0.05f));
                        a.effects.Add(Circle(Point, 1.8f, Hit(0.5f, P, 0.05f, 0f, 2f, Status(root: 2.5f, poison: 1)), 0.05f));
                    }),
                Ability("huntermark", "Dấu Thợ Săn", AbilityTags.Physical | AbilityTags.Projectile, 10f, 8f, "attack", 0.25f,
                    "Mũi tên đánh dấu con mồi: nó chịu thêm sát thương (Phán Xét) trong 6 giây.", a =>
                    {
                        a.maxRange = 14f;
                        a.effects.Add(Bolt("Arrow", 18f, Hit(0.6f, P, 0.1f, 1f, 2f, Status(judgment: 6f)), "holy_hit"));
                    }),
                Ability("disengage", "Nhảy Lùi", AbilityTags.Physical | AbilityTags.Movement, 8f, 6f, "", 0.1f,
                    "Nhảy lùi 4.5 ô khỏi hướng chuột, làm chậm kẻ địch vừa ở cạnh.", a =>
                    {
                        a.effects.Add(Circle(Caster, 2f, Hit(0.4f, P, 0f, 3f, 2f, Status(slow: 0.4f, slowFor: 3f))));
                        Dash(a, 4.5f, 0.18f, true);
                    }),
                Ability("multishot", "Tên Bão", AbilityTags.Physical | AbilityTags.Projectile | AbilityTags.Ultimate, 14f, 22f, "attack", 0.45f,
                    "Bắn một lúc bảy mũi tên xòe hình quạt.", a =>
                    {
                        a.maxRange = 12f;
                        a.castSfx = "sfx_swing";
                        for (int i = -3; i <= 3; i++)
                            a.effects.Add(Bolt("Arrow", 17f, Hit(1.1f, P, 0.15f, 3f, 3f), "hit_spark", "sfx_hit", 0f, false, i * 10f));
                    }),

                // ============================================== Đạo Tặc
                Ability("throwknife", "Ném Dao", AbilityTags.Physical | AbilityTags.Projectile, 3f, 6f, "attack", 0.22f,
                    "Ném dao thật nhanh, rất dễ chí mạng.", a =>
                    {
                        a.maxRange = 11f;
                        a.castSfx = "sfx_swing";
                        a.effects.Add(Bolt("ThrownKnife", 18f, Hit(1.3f, P, 0.35f, 2f, 3f), "hit_spark"));
                    }),
                Ability("deathstrike", "Nhát Chí Mạng", AbilityTags.Physical | AbilityTags.Melee, 8f, 14f, "attack", 0.35f,
                    "Đâm vào chỗ hiểm: nhát này luôn chí mạng.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.45f, 0.6f), "slash_big", "sfx_crit", 0.9f, 0.15f));
                        a.effects.Add(Cone(1.8f, 80f, Hit(2.4f, P, 1f, 4f, 10f), 0.05f, "sfx_hit_heavy", 0.2f));
                    }),
                Ability("smokebomb", "Bom Khói", AbilityTags.Physical | AbilityTags.Area, 16f, 12f, "cast", 0.25f,
                    "Ném bom khói: kẻ địch quanh 3 ô bị choáng 1 giây, còn mình chạy nhanh hơn 30% trong 3 giây.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.effects.Add(Cue(Caster, "smoke_cloud", "sfx_shroom_puff", 0.9f, 0.1f));
                        a.effects.Add(Circle(Caster, 3f, Hit(0.3f, P, 0f, 0f, 4f, Status(stun: 1f, slow: 0.3f, slowFor: 3f)), 0.05f));
                        a.effects.Add(Buff("smokebomb", "Bom Khói", 3f, b => b.speedMultiplier = 1.3f));
                    }),
                Ability("evasion", "Né Tránh", AbilityTags.Physical | AbilityTags.Support, 12f, 6f, "cast", 0.15f,
                    "Né tránh mọi đòn trong 3 giây: nhận ít hơn 70% sát thương.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_dash";
                        a.effects.Add(Buff("evasion", "Né Tránh", 3f, b =>
                        {
                            b.damageTakenMultiplier = 0.3f;
                            b.attachedVfx = "shield_bubble";
                        }));
                    }),
                Ability("shadowstep", "Bước Bóng", AbilityTags.Dark | AbilityTags.Movement, 8f, 8f, "", 0.05f,
                    "Hòa vào bóng tối và hiện ra cách đó 6 ô, về phía chuột.", a =>
                    {
                        a.castSfx = "sfx_dash";
                        Dash(a, 6f, 0.05f, false, "dark_hit", "dark_hit");
                    }),
                Ability("bladefan", "Vũ Điệu Lưỡi Dao", AbilityTags.Physical | AbilityTags.Projectile | AbilityTags.Ultimate, 16f, 22f, "attack", 0.4f,
                    "Tung mười lưỡi dao ra mọi hướng.", a =>
                    {
                        a.maxRange = 9f;
                        a.castSfx = "sfx_bladestorm";
                        for (int i = 0; i < 10; i++)
                            a.effects.Add(Bolt("ThrownKnife", 16f, Hit(0.9f, P, 0.3f, 3f, 3f), "hit_spark", "sfx_hit", 0f, false, i * 36f));
                    }),

                // ============================================== Thuật Sĩ
                Ability("chaosbolt", "Tia Hỗn Loạn", AbilityTags.Fire | AbilityTags.Projectile, 5f, 12f, "cast", 0.3f,
                    "Tia ma lực hỗn loạn: vừa Bỏng, vừa Lạnh, vừa Tích Điện.", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Bolt("ChaosBolt", 13f, Hit(1.7f, F, 0.15f, 3f, 6f, Status(burn: 1, chill: 1, charge: 1)), "crystal_burst", "sfx_fireball_explode", 1.2f));
                    }),
                Ability("arcaneshield", "Khiên Phép", AbilityTags.Lightning | AbilityTags.Support, 14f, 10f, "cast", 0.2f,
                    "Tấm khiên ma lực trong 4 giây: nhận ít hơn 50% sát thương.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(Buff("arcaneshield", "Khiên Phép", 4f, b =>
                        {
                            b.damageTakenMultiplier = 0.5f;
                            b.attachedVfx = "shield_bubble";
                            b.endVfx = "shield_break";
                        }));
                    }),
                Ability("meteor", "Mưa Thiên Thạch", AbilityTags.Fire | AbilityTags.Area | AbilityTags.Ultimate, 22f, 32f, "cast", 0.6f,
                    "Sáu thiên thạch lửa rơi xuống quanh vị trí chuột, gây Bỏng.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 11f;
                        a.effects.Add(Storm(3.5f, 6, 0.25f, 0.8f, "storm_circle", 1.6f,
                            Cue(Point, "fire_explosion", "sfx_fireball_explode", 0.7f, 0.3f),
                            Circle(Point, 1.6f, Hit(2.4f, F, 0.15f, 6f, 12f, Status(burn: 1)))));
                    }),

                // ============================================== Khế Ước Sư
                Ability("hex", "Lời Nguyền Hex", AbilityTags.Dark | AbilityTags.Projectile, 6f, 10f, "cast", 0.28f,
                    "Nguyền rủa kẻ địch 6 giây: nó gây ít sát thương hơn (Nguyền) và chịu thêm sát thương (Phán Xét).", a =>
                    {
                        a.maxRange = 12f;
                        a.effects.Add(Bolt("DarkBolt", 14f, Hit(0.8f, Dk, 0.1f, 1f, 3f, Status(curse: 6f, judgment: 6f)), "dark_hit"));
                    }),
                Ability("tentacles", "Xúc Tu Hắc Ám", AbilityTags.Dark | AbilityTags.Area, 8f, 14f, "cast", 0.35f,
                    "Xúc tu bóng tối trồi lên tại vị trí chuột, trói chân và làm chậm.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Cue(Point, "dark_strike", "sfx_web", 0.7f, 0.1f, 0.8f));
                        a.effects.Add(Circle(Point, 2.5f, Hit(1.2f, Dk, 0.1f, 0f, 4f, Status(root: 1.5f, slow: 0.35f, slowFor: 3f)), 0.05f));
                    }),
                Ability("drain", "Hút Sinh Lực", AbilityTags.Dark | AbilityTags.Melee, 7f, 12f, "cast", 0.3f,
                    "Rút sinh lực kẻ địch phía trước và hồi 20 máu cho mình.", a =>
                    {
                        a.effects.Add(Cue(new Anchor(Anchor.From.Caster, 0.45f, 0.8f), "dark_hit", "sfx_wisp", 0.7f, 0.08f, 1.4f));
                        a.effects.Add(Cone(2.2f, 70f, Hit(1.6f, Dk, 0.1f, 1f, 4f), 0.05f));
                        a.effects.Add(new HealEffect { delay = 0.1f, instant = 20f });
                    }),
                Ability("darkarmor", "Giáp Hắc Ám", AbilityTags.Dark | AbilityTags.Support, 16f, 10f, "cast", 0.25f,
                    "Giáp bóng tối trong 6 giây: nhận ít hơn 40% sát thương.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_shield";
                        a.effects.Add(Buff("darkarmor", "Giáp Hắc Ám", 6f, b =>
                        {
                            b.damageTakenMultiplier = 0.6f;
                            b.attachedVfx = "dark_aura";
                        }));
                    }),
                Ability("voidstorm", "Bão Hư Không", AbilityTags.Dark | AbilityTags.Area | AbilityTags.Ultimate, 22f, 30f, "cast", 0.55f,
                    "Chín tia hư không giáng xuống quanh vị trí chuột, gây Nguyền.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Storm(3.5f, 9, 0.12f, 0.4f, "dark_aura", 2f,
                            Cue(Point, "dark_strike", "sfx_thunder", 0.4f, 0.15f),
                            Circle(Point, 1.2f, Hit(1.7f, Dk, 0.12f, 2f, 6f, Status(curse: 3f)))));
                    }),

                // ============================================== Pháp Sư
                Ability("blackhole", "Hố Đen", AbilityTags.Dark | AbilityTags.Area | AbilityTags.Ultimate, 24f, 32f, "cast", 0.55f,
                    "Mở hố đen tại vị trí chuột trong 3 giây: hút kẻ địch vào giữa và nghiền chúng.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.effects.Add(Cue(Point, "dark_strike", "sfx_wisp_burst", 0.7f, 0.2f));
                        a.effects.Add(new PulseEffect
                        {
                            duration = 3f, interval = 0.3f, at = Point, attachedVfx = "dark_aura",
                            each = { Circle(Point, 3f, Hit(0.5f, Dk, 0.05f, -4f, 2f)) }
                        });
                    }),

                // ============================================== Sách Chiêu (T63): learned from Bí Kíp, see Spellbook
                // ---------------------------------------------- Băng
                Ability("frostarrows", "Băng Tiễn", AbilityTags.Ice | AbilityTags.Projectile, 4f, 10f, "cast", 0.35f,
                    "Ba mũi tên băng liên tiếp về phía chuột, mỗi mũi gây 1 tầng Lạnh.", a =>
                    {
                        a.maxRange = 11f;
                        a.castSfx = "sfx_ice_cast";
                        for (int i = 0; i < 3; i++)
                            a.effects.Add(Bolt("FrostArrow", 15f, Hit(0.6f, I, 0.1f, 1.5f, 3f, Status(chill: 1)), "hit_ice", "sfx_ice_shatter", 0f, false, 0f, i * 0.12f));
                    }),
                Ability("frostarmor", "Giáp Sương", AbilityTags.Ice | AbilityTags.Support, 18f, 16f, "cast", 0.25f,
                    "Lớp giáp sương 6 giây: nhận ít hơn 40% sát thương, kẻ đánh ngươi từ gần bị 1 tầng Lạnh.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_ice_cast";
                        a.effects.Add(Cue(Caster, "cast_ice"));
                        a.effects.Add(Buff("frostarmor", "Giáp Sương", 6f, b =>
                        {
                            b.damageTakenMultiplier = 0.6f;
                            b.chillAttackers = 1;
                            b.attachedVfx = "frost_aura";
                            b.endVfx = "hit_ice";
                        }));
                    }),
                Ability("iceprison", "Ngục Băng", AbilityTags.Ice | AbilityTags.Area, 15f, 22f, "cast", 0.4f,
                    "Sau 0.6 giây, cột băng trồi lên quanh vị trí chuột (2.5 ô): sát thương lớn và Đóng Băng mọi kẻ bên trong.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 9f;
                        a.castSfx = "sfx_ice_cast";
                        a.effects.Add(Cue(Point, "cast_ice", null, 0.8f, 0f, 2.2f));
                        a.effects.Add(new CueEffect { delay = 0.6f, at = Point, vfx = "ice_prison", sfx = "sfx_ice_shatter", sfxVolume = 1f, sfxAtPoint = true, shake = 0.3f });
                        a.effects.Add(Circle(Point, 2.5f, Hit(1.8f, I, 0.1f, 2f, 20f, Status(chill: 4)), 0.6f));
                    }),
                Ability("blizzard", "Bão Tuyết", AbilityTags.Ice | AbilityTags.Area, 20f, 26f, "cast", 0.45f,
                    "Bão tuyết 5 giây tại vị trí chuột (4 ô): sát thương liên tục, mỗi nhịp thêm 1 tầng Lạnh.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.castSfx = "sfx_ice_cast";
                        a.effects.Add(new PulseEffect
                        {
                            duration = 5f, interval = 0.5f, at = Point, attachedVfx = "blizzard", loopSfx = "sfx_gust", loopSfxVolume = 0.35f, loopSfxInterval = 1.1f,
                            each = { Circle(Point, 4f, Hit(0.3f, I, 0.05f, 0f, 1f, Status(chill: 1))) }
                        });
                    }),
                Ability("iceage", "Kỷ Băng Hà", AbilityTags.Ice | AbilityTags.Area | AbilityTags.Ultimate, 26f, 34f, "cast", 0.6f,
                    "Mặt đất 7 ô quanh ngươi đóng băng: sát thương rất lớn và Đóng Băng mọi kẻ địch (boss ngắn hơn).", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_ice_cast";
                        a.effects.Add(Cue(Caster, "cast_ice", null, 0.8f, 0f, 1.6f));
                        a.effects.Add(new CueEffect
                        {
                            delay = 0.25f, at = Caster, vfx = "ice_age", sfx = "sfx_ice_shatter", sfxVolume = 1f, shake = 0.55f,
                            flashColor = new Color(0.7f, 0.9f, 1f), flashStrength = 0.25f, flashDuration = 0.3f, impact = 0.7f
                        });
                        a.effects.Add(Circle(Caster, 7f, Hit(3.2f, I, 0.15f, 5f, 40f, Status(chill: 4)), 0.25f));
                    }),
                // ---------------------------------------------- Lôi
                Ability("chainlightning", "Xích Lôi", AbilityTags.Lightning | AbilityTags.Projectile, 4f, 12f, "cast", 0.3f,
                    "Tia sét nhảy qua 4 kẻ địch, lần nhảy sau yếu hơn 15%; mỗi kẻ trúng thêm 1 Tích Điện.", a =>
                    {
                        a.maxRange = 9f;
                        a.castSfx = "sfx_lightning_charge";
                        a.effects.Add(Cue(Hand, "cast_lightning"));
                        a.effects.Add(new ChainEffect { range = 9f, jumpRange = 4.5f, jumps = 4, falloff = 0.15f, hit = Hit(1.15f, L, 0.12f, 1f, 4f, Status(charge: 1)) });
                    }),
                Ability("thunderstep", "Thiểm Bộ", AbilityTags.Lightning | AbilityTags.Movement, 8f, 10f, "", 0.05f,
                    "Hóa thành tia sét, hiện ra cách đó 6 ô về phía chuột. Chỗ cũ để lại một quả cầu điện nổ sau 0.5 giây.", a =>
                    {
                        a.castSfx = "sfx_thunder";
                        a.effects.Add(Cue(Origin, "thunder_step"));
                        Dash(a, 6f, 0.05f, false, "hit_lightning", "hit_lightning");
                        a.effects.Add(new CueEffect { delay = 0.5f, at = Origin, vfx = "lightning_burst", vfxScale = 0.8f, sfx = "sfx_thunder", sfxVolume = 0.6f, sfxAtPoint = true });
                        a.effects.Add(Circle(Origin, 2f, Hit(1.2f, L, 0.1f, 3f, 6f, Status(charge: 1)), 0.5f));
                    }),
                Ability("lightningbrand", "Lôi Ấn", AbilityTags.Lightning | AbilityTags.Support, 18f, 14f, "cast", 0.25f,
                    "Vũ khí nhiễm điện 8 giây: đòn Q mạnh hơn 35% và thêm 1 Tích Điện (đủ 3 thì phóng điện sang kẻ gần).", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_lightning_charge";
                        a.effects.Add(Cue(Caster, "cast_lightning"));
                        a.effects.Add(Buff("lightningbrand", "Lôi Ấn", 8f, b =>
                        {
                            b.imbuePower = 0.35f;
                            b.imbueCharge = 1;
                            b.attachedVfx = "lightning_imbue";
                        }));
                    }),
                Ability("balllightning", "Lôi Cầu", AbilityTags.Lightning | AbilityTags.Projectile, 10f, 18f, "cast", 0.35f,
                    "Quả cầu điện bay chậm về phía chuột, giật mọi kẻ trong 2 ô, rồi nổ ở cuối đường bay hoặc khi chạm đá.", a =>
                    {
                        a.maxRange = 9f;
                        a.castSfx = "sfx_lightning_charge";
                        a.effects.Add(Cue(Hand, "cast_lightning"));
                        a.effects.Add(new OrbEffect
                        {
                            speed = 3.2f, duration = 2.6f, radius = 2f, interval = 0.3f, tick = Hit(0.4f, L, 0f, 0.5f, 1f),
                            burstRadius = 2.6f, burst = Hit(2.5f, L, 0.15f, 5f, 15f, Status(charge: 1))
                        });
                    }),
                Ability("stormfield", "Điện Trường", AbilityTags.Lightning | AbilityTags.Area, 16f, 20f, "cast", 0.35f,
                    "Vòng điện 3 ô quanh chỗ ngươi đứng trong 5 giây: giật và làm chậm kẻ địch; ngươi chạy nhanh hơn 15%.", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_lightning_charge";
                        a.effects.Add(new PulseEffect
                        {
                            duration = 5f, interval = 0.5f, at = Origin, attachedVfx = "storm_field", loopSfx = "sfx_beam", loopSfxVolume = 0.3f, loopSfxInterval = 1.2f,
                            each = { Circle(Point, 3f, Hit(0.3f, L, 0.05f, 0f, 2f, Status(slow: 0.2f, slowFor: 0.8f))) }
                        });
                        a.effects.Add(Buff("stormfield", "Điện Trường", 5f, b => b.speedMultiplier = 1.15f));
                    }),
                Ability("thunderstorm", "Cửu Thiên Lôi", AbilityTags.Lightning | AbilityTags.Area | AbilityTags.Ultimate, 24f, 32f, "cast", 0.55f,
                    "Mây giông phủ vị trí chuột (4 ô): chín tia sét trời liên tiếp, mỗi tia gây choáng ngắn và 1 Tích Điện, ưu tiên đánh vào kẻ địch.", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 10f;
                        a.castSfx = "sfx_lightning_charge";
                        a.effects.Add(Cue(Caster, "cast_lightning", null, 0.8f, 0f, 1.4f));
                        a.effects.Add(new BurstEffect
                        {
                            center = Aim, radius = 4f, count = 9, chargeTime = 0.5f, interval = 0.16f, intervalJitter = 0.25f,
                            preferTargets = 0.85f, firstAtCenter = true, areaVfx = "storm_circle", areaVfxScale = 4f / 2.5f,
                            each =
                            {
                                new CueEffect
                                {
                                    at = Point, vfx = "lightning_strike", sfx = "sfx_thunder", sfxVolume = 0.75f, sfxPitchVariance = 0.12f,
                                    sfxAtPoint = true, sfxMinInterval = 0.05f, shake = 0.25f,
                                    flashColor = new Color(0.85f, 0.85f, 1f), flashStrength = 0.12f, flashDuration = 0.12f
                                },
                                Circle(Point, 1.2f, Hit(1.9f, L, 0.15f, 2f, 10f, Status(stun: 0.8f, charge: 1)))
                            }
                        });
                    }),
                // ---------------------------------------------- Ám
                Ability("shadowknives", "Ám Tiễn", AbilityTags.Dark | AbilityTags.Projectile, 3f, 8f, "attack", 0.25f,
                    "Phi ba dao bóng tối hình quạt về phía chuột, dễ chí mạng.", a =>
                    {
                        a.maxRange = 10f;
                        a.castSfx = "sfx_swing";
                        foreach (float angle in new[] { -12f, 0f, 12f })
                            a.effects.Add(Bolt("ShadowKnife", 16f, Hit(0.75f, Dk, 0.25f, 2f, 3f), "dark_hit", "sfx_hit", 0f, false, angle));
                    }),
                Ability("curse", "Lời Nguyền", AbilityTags.Dark | AbilityTags.Area, 14f, 14f, "cast", 0.35f,
                    "Ấn nguyền tại vị trí chuột: mọi kẻ địch trong 3 ô bị Nguyền 8 giây (gây ít, chịu nhiều sát thương hơn).", a =>
                    {
                        a.targeting = AbilityTargeting.Point;
                        a.maxRange = 9f;
                        a.castSfx = "sfx_wisp";
                        a.effects.Add(Cue(Point, "curse_sigil", "sfx_wisp_burst", 0.8f, 0.1f));
                        a.effects.Add(Circle(Point, 3f, Hit(0.6f, Dk, 0.05f, 0f, 3f, Status(curse: 8f)), 0.1f));
                    }),
                Ability("shadowclone", "Phân Thân", AbilityTags.Dark | AbilityTags.Summon, 20f, 18f, "cast", 0.3f,
                    "Một bóng của ngươi bước ra đứng cạnh 8 giây, chém kẻ địch gần nó; hết giờ thì nổ thành khói đen.", a =>
                    {
                        a.targeting = AbilityTargeting.Direction;
                        a.maxRange = 3f;
                        a.castSfx = "sfx_wisp";
                        a.effects.Add(new CloneEffect
                        {
                            duration = 8f, interval = 0.8f, reach = 2.4f, hit = Hit(0.65f, Dk, 0.2f, 2f, 4f),
                            burstRadius = 2.2f, burst = Hit(1.2f, Dk, 0.1f, 4f, 8f)
                        });
                    }),
                Ability("soulsiphon", "Hút Hồn", AbilityTags.Dark | AbilityTags.Channel, 12f, 16f, "cast", 1.2f,
                    "Tia hút hồn 2 giây vào kẻ địch gần chuột nhất; hồi cho ngươi 30% sát thương gây ra.", a =>
                    {
                        a.maxRange = 7f;
                        a.moveWhileCasting = 0.35f;
                        a.castSfx = "sfx_wisp";
                        a.effects.Add(new BeamEffect { duration = 2f, interval = 0.2f, range = 7f, hit = Hit(0.3f, Dk, 0.05f, 0f, 1f), lifesteal = 0.3f });
                    }),
                Ability("eclipse", "Nhật Thực", AbilityTags.Dark | AbilityTags.Area | AbilityTags.Ultimate, 26f, 34f, "cast", 0.5f,
                    "Mặt trời đen che trên đầu ngươi 6 giây: chí mạng +50%, sát thương +15%. Hết giờ, bóng tối nổ tung quanh ngươi (5 ô).", a =>
                    {
                        a.targeting = AbilityTargeting.Self;
                        a.castSfx = "sfx_wisp_burst";
                        a.effects.Add(new CueEffect { at = Caster, vfx = "dark_strike", vfxScale = 1.4f, shake = 0.3f, flashColor = new Color(0.1f, 0f, 0.15f), flashStrength = 0.45f, flashDuration = 0.6f });
                        a.effects.Add(Buff("eclipse", "Nhật Thực", 6f, b =>
                        {
                            b.critBonus = 0.5f;
                            b.damageDealtMultiplier = 1.15f;
                            b.attachedVfx = "eclipse";
                        }));
                        a.effects.Add(new CueEffect { delay = 6f, at = Caster, vfx = "dark_strike", vfxScale = 2.4f, sfx = "sfx_wisp_burst", sfxVolume = 1f, shake = 0.5f, impact = 0.6f });
                        a.effects.Add(Circle(Caster, 5f, Hit(3.5f, Dk, 0.2f, 6f, 20f), 6f));
                    }),
            };
            return list;
        }
    }
}
