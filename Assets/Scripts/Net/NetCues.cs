using System.Collections.Generic;
using UnityEngine;

namespace RPG
{
    /// <summary>
    /// What the world's rules show and play: effects, sounds, shakes, floating words, ground
    /// warnings, a boss's announcements. The code that decides (offline, a host, a server) calls
    /// these instead of VFX / AudioManager / CameraRig directly: they play on this screen when
    /// there is one, and a server sends them to every player too (online phase 3). Screen
    /// effects (shake, flash, impact, flat sounds, log lines) only reach heroes near where they
    /// happen. Presentation that a screen makes up on its own (footsteps, a hero's own
    /// predicted skill) keeps calling VFX and AudioManager directly.
    /// </summary>
    public static class NetCues
    {
        public enum Kind : byte
        {
            Vfx = 1,
            VfxOn = 2,
            Sound = 3,
            FlatSound = 4,
            WorldText = 5,
            Shake = 6,
            Flash = 7,
            Impact = 8,
            Telegraph = 9,
            TelegraphCancel = 10,
            Announce = 11,
            Projectile = 12,
            Arc = 13,
            Log = 14,
            Banner = 15,
            Boss = 16,
            Beam = 17
        }

        /// <summary>How far from a screen effect a hero still feels it.</summary>
        public const float NearRadius = 16f;
        /// <summary>How far a boss's roar or a log line about a fight reaches.</summary>
        public const float FarRadius = 30f;

        // ------------------------------------------------------------------ what the rules call
        /// <summary>A one-shot effect at a point.</summary>
        public static void Vfx(string id, Vector3 pos, float angle = 0f, float scale = 1f)
        {
            if (string.IsNullOrEmpty(id)) return;
            Send(new CueMsg { kind = (byte)Kind.Vfx, id = id, pos = pos, a = angle, b = scale, c = pos.z });
        }

        /// <summary>An effect that follows a character (level up, quest done). <paramref name="up"/> lifts it.</summary>
        public static void VfxOn(string id, Component on, float scale = 1f, float up = 0f)
        {
            if (string.IsNullOrEmpty(id) || on == null) return;
            // this screen follows the character itself (offline it has no network id to look up)
            if (GameSession.HasScreen) VFX.Spawn(id, on.transform.position + Vector3.up * up, Quaternion.identity, scale, on.transform);
            if (GameSession.Serving)
                NetWorld.SendCue(new CueMsg { kind = (byte)Kind.VfxOn, id = id, pos = on.transform.position, target = NetWorld.IdOf(on), b = scale, d = up });
        }

        /// <summary>A sound heard from a point (quieter away from the camera), or flat when <paramref name="at"/> is null.</summary>
        public static void Sound(string id, float volume = 1f, float pitchVariance = 0.06f, Vector3? at = null, float minInterval = 0.03f)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (at.HasValue) Send(new CueMsg { kind = (byte)Kind.Sound, id = id, pos = at.Value, a = volume, b = pitchVariance, c = minInterval });
            else Send(new CueMsg { kind = (byte)Kind.FlatSound, id = id, a = volume, b = pitchVariance, c = minInterval, d = float.MaxValue });
        }

        /// <summary>A sound played flat for every hero within <paramref name="radius"/> of <paramref name="near"/> (a boss's roar).</summary>
        public static void FlatSound(string id, Vector2 near, float volume = 1f, float pitchVariance = 0.06f, float radius = FarRadius)
        {
            if (string.IsNullOrEmpty(id)) return;
            Send(new CueMsg { kind = (byte)Kind.FlatSound, id = id, pos = near, a = volume, b = pitchVariance, c = 0.03f, d = radius });
        }

        /// <summary>A floating word ("Choáng!", "Hoàn Hảo!").</summary>
        public static void WorldText(string text, Vector3 pos, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            Send(new CueMsg { kind = (byte)Kind.WorldText, text = text, pos = pos, c = pos.z, color = color });
        }

        public static void Shake(float amount, Vector2 at, float radius = NearRadius) =>
            Send(new CueMsg { kind = (byte)Kind.Shake, pos = at, a = amount, d = radius });

        public static void Flash(Color c, float strength, float duration, Vector2 at, float radius = NearRadius) =>
            Send(new CueMsg { kind = (byte)Kind.Flash, pos = at, color = c, a = strength, b = duration, d = radius });

        public static void Impact(float strength, float duration, Vector2 at, float radius = NearRadius) =>
            Send(new CueMsg { kind = (byte)Kind.Impact, pos = at, a = strength, b = duration, d = radius });

        /// <summary>A log line for the heroes near <paramref name="near"/> (a boss's rage, a poise break).</summary>
        public static void Log(string text, Color color, Vector2 near, float radius = FarRadius) =>
            Send(new CueMsg { kind = (byte)Kind.Log, text = text, color = color, pos = near, d = radius });

        public static void Banner(BannerKind kind, string title, string subtitle, Color color, Vector2 near, float radius = FarRadius) =>
            Send(new CueMsg { kind = (byte)Kind.Banner, flag = (byte)kind, id = title, text = subtitle, color = color, pos = near, d = radius });

        /// <summary>"Kỹ năng: …" over a character's head.</summary>
        public static void Announce(Health who, string text)
        {
            if (who == null) return;
            if (GameSession.HasScreen) GameEvents.RaiseSkillAnnounced(who, text);
            if (GameSession.Serving)
                NetWorld.SendCue(new CueMsg { kind = (byte)Kind.Announce, target = NetWorld.IdOf(who), text = text, pos = who.transform.position });
        }

        /// <summary>A ground warning (circle) of <paramref name="source"/>'s attack; the local one is returned (null on a server without a screen).</summary>
        public static Telegraph Circle(Component source, Vector2 pos, float radius, float duration, Color? color = null) =>
            SendTelegraph(source, false, pos, Vector2.zero, radius, duration, color);

        /// <summary>A cone ground warning.</summary>
        public static Telegraph Cone(Component source, Vector2 pos, Vector2 dir, float radius, float duration, Color? color = null) =>
            SendTelegraph(source, true, pos, dir, radius, duration, color);

        /// <summary>Ends every warning of <paramref name="source"/> early (the boss got stunned).</summary>
        public static void CancelTelegraphs(Component source) =>
            Send(new CueMsg { kind = (byte)Kind.TelegraphCancel, target = NetWorld.IdOf(source) });

        /// <summary>
        /// A projectile the others only watch (an enemy's spore, seen by the players' screens): it
        /// flies and bursts there, the hits are the server's. <paramref name="prefabKey"/>: "spore", "venom", "web", "fireball".
        /// </summary>
        public static void Projectile(string prefabKey, Vector2 start, Vector2 dir, float speed, float lifetime, Team team,
                                      string hitVfx, string hitSfx, float shake, float explodeRadius)
        {
            if (!GameSession.Serving) return;   // the machine that fired it already has the real one
            NetWorld.SendCue(new CueMsg
            {
                kind = (byte)Kind.Projectile, id = prefabKey, pos = start, pos2 = dir, a = speed, b = lifetime, c = shake, d = explodeRadius,
                text = hitVfx + "|" + hitSfx, flag = (byte)team
            });
        }

        /// <summary>
        /// A lobbed rock (or <paramref name="prefabKey"/> "venom": a poison glob) others only watch
        /// (its landing is sent on its own).
        /// </summary>
        public static void Arc(Vector2 from, Vector2 to, float time, string prefabKey = null)
        {
            if (!GameSession.Serving) return;
            NetWorld.SendCue(new CueMsg { kind = (byte)Kind.Arc, id = prefabKey, pos = from, pos2 = to, a = time });
        }

        /// <summary>
        /// A beam of light from <paramref name="from"/> to <paramref name="to"/> (Mắt Hang, the spider
        /// queen's Tia Pha Lê): every screen near it draws it for <paramref name="duration"/> seconds.
        /// </summary>
        public static void Beam(Vector2 from, Vector2 to, float width, float duration, Color color) =>
            Send(new CueMsg { kind = (byte)Kind.Beam, pos = from, pos2 = to, a = width, b = duration, color = color });

        /// <summary>A boss's moment (intro, rage, fall, reset): screens near it run the boss's own presentation.</summary>
        public static void Boss(BossBase boss, BossBase.Moment moment)
        {
            if (boss == null) return;
            // this screen presents the boss itself: offline it has no network id to look up
            if (GameSession.HasScreen) boss.Present(moment);
            if (GameSession.Serving)
                NetWorld.SendCue(new CueMsg { kind = (byte)Kind.Boss, target = NetWorld.IdOf(boss), flag = (byte)moment, pos = boss.transform.position });
        }

        // ------------------------------------------------------------------ plumbing
        static void Send(CueMsg m)
        {
            if (GameSession.HasScreen) Play(m, false);
            if (GameSession.Serving) NetWorld.SendCue(m);
        }

        static Telegraph SendTelegraph(Component source, bool cone, Vector2 pos, Vector2 dir, float radius, float duration, Color? color)
        {
            var m = new CueMsg
            {
                kind = (byte)Kind.Telegraph, target = NetWorld.IdOf(source), flag = (byte)(cone ? 1 : 0), pos = pos, pos2 = dir,
                a = radius, b = duration, color = color ?? Palette.Telegraph
            };
            if (GameSession.Serving) NetWorld.SendCue(m);
            return GameSession.HasScreen ? Play(m, false) : null;
        }

        /// <summary>The hero on this screen is within <paramref name="radius"/> of <paramref name="at"/> (always true offline).</summary>
        static bool Near(Vector2 at, float radius)
        {
            if (!GameSession.Online || radius >= float.MaxValue) return true;
            var me = Players.Local;
            return me != null && ((Vector2)me.transform.position - at).sqrMagnitude <= radius * radius;
        }

        // warnings on this screen, by who made them (so a stun can cancel them)
        static readonly Dictionary<int, List<Telegraph>> Warnings = new Dictionary<int, List<Telegraph>>();

        /// <summary>
        /// Shows a cue on this screen. <paramref name="remote"/>: it came from the server (a
        /// ground warning then ends as early as it arrived late, so it closes when the hit lands).
        /// </summary>
        public static Telegraph Play(CueMsg m, bool remote)
        {
            switch ((Kind)m.kind)
            {
                case Kind.Vfx:
                    VFX.Spawn(m.id, new Vector3(m.pos.x, m.pos.y, m.c), Quaternion.Euler(0, 0, m.a), m.b);
                    break;
                case Kind.VfxOn:
                {
                    var on = NetWorld.Find(m.target);
                    if (on != null) VFX.Spawn(m.id, on.transform.position + Vector3.up * m.d, Quaternion.identity, m.b, on.transform);
                    else VFX.Spawn(m.id, (Vector3)m.pos + Vector3.up * m.d, Quaternion.identity, m.b);
                    break;
                }
                case Kind.Sound:
                    AudioManager.Play(m.id, m.a, m.b, m.pos, m.c);
                    break;
                case Kind.FlatSound:
                    if (Near(m.pos, m.d)) AudioManager.Play(m.id, m.a, m.b, null, m.c);
                    break;
                case Kind.WorldText:
                    GameEvents.RaiseWorldText(m.text, new Vector3(m.pos.x, m.pos.y, m.c), m.color);
                    break;
                case Kind.Shake:
                    if (Near(m.pos, m.d)) CameraRig.Shake(m.a);
                    break;
                case Kind.Flash:
                    if (Near(m.pos, m.d)) ScreenFX.Flash(m.color, m.a, m.b);
                    break;
                case Kind.Impact:
                    if (Near(m.pos, m.d)) ScreenFX.Impact(m.a, m.b);
                    break;
                case Kind.Log:
                    if (Near(m.pos, m.d)) GameEvents.RaiseLog(m.text, m.color);
                    break;
                case Kind.Banner:
                    if (Near(m.pos, m.d)) GameEvents.RaiseBanner((BannerKind)m.flag, m.id, m.text, m.color);
                    break;
                case Kind.Announce:
                {
                    var who = NetWorld.Find(m.target);
                    var h = who != null ? who.GetComponent<Health>() : null;
                    if (h != null) GameEvents.RaiseSkillAnnounced(h, m.text);
                    break;
                }
                case Kind.Telegraph:
                {
                    float duration = m.b;
                    if (remote) duration = Mathf.Max(0.1f, duration - NetWorld.OneWayDelay);
                    var t = m.flag == 1
                        ? global::RPG.Telegraph.Cone(m.pos, m.pos2, m.a, duration, m.color)
                        : global::RPG.Telegraph.Circle(m.pos, m.a, duration, m.color);
                    if (t != null && m.target != 0)
                    {
                        if (!Warnings.TryGetValue(m.target, out var list)) Warnings[m.target] = list = new List<Telegraph>();
                        list.RemoveAll(x => x == null || !x.gameObject.activeInHierarchy);
                        list.Add(t);
                    }
                    return t;
                }
                case Kind.TelegraphCancel:
                    if (Warnings.TryGetValue(m.target, out var warnings))
                    {
                        foreach (var t in warnings)
                            if (t != null && t.gameObject.activeInHierarchy) t.Cancel();
                        warnings.Clear();
                    }
                    break;
                case Kind.Projectile:
                    NetWorld.ShowProjectile(m);
                    break;
                case Kind.Arc:
                    NetWorld.ShowArc(m);
                    break;
                case Kind.Beam:
                    BeamFX.Show(m.pos, m.pos2, m.a, m.b, m.color);
                    break;
                case Kind.Boss:
                {
                    var who = NetWorld.Find(m.target);
                    var boss = who != null ? who.GetComponent<BossBase>() : null;
                    if (boss != null) boss.Present((BossBase.Moment)m.flag);
                    break;
                }
            }
            return null;
        }

        /// <summary>Forgets the warnings of a finished session.</summary>
        public static void Reset() => Warnings.Clear();
    }
}
