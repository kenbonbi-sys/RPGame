using System.Collections.Generic;

namespace RPG
{
    /// <summary>
    /// Finds a buff by id: the ones skills give (a Buff block anywhere in an ability's timeline)
    /// and Lướt Hoàn Hảo's bonus. Online, the server names a buff and every screen shows it.
    /// </summary>
    public static class Buffs
    {
        static Dictionary<string, BuffSpec> map;
        static GameDatabase builtFor;

        public static BuffSpec Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var db = GameManager.I != null ? GameManager.I.db : null;
            if (map == null || builtFor != db) Build(db);
            if (map.TryGetValue(id, out var spec)) return spec;
            if (id == PerfectDodge.BuffId) return map[id] = PerfectDodge.MakeBuff(DashIcon(db));
            return null;
        }

        static void Build(GameDatabase db)
        {
            map = new Dictionary<string, BuffSpec>();
            builtFor = db;
            if (db == null) return;
            foreach (var a in db.abilities)
                if (a != null) Walk(a.effects);
        }

        static void Walk(List<AbilityEffect> effects)
        {
            if (effects == null) return;
            foreach (var e in effects)
            {
                switch (e)
                {
                    case BuffEffect b when b.buff != null && !string.IsNullOrEmpty(b.buff.id):
                        if (!map.ContainsKey(b.buff.id)) map[b.buff.id] = b.buff;
                        break;
                    case DamageEffect d: Walk(d.onAnyHit); break;
                    case LineEffect l: Walk(l.each); break;
                    case BurstEffect bu: Walk(bu.each); break;
                    case PulseEffect p: Walk(p.each); break;
                    case ComboEffect c:
                        foreach (var s in c.stages)
                            if (s != null) Walk(s.effects);
                        break;
                }
            }
        }

        static UnityEngine.Sprite DashIcon(GameDatabase db)
        {
            if (db == null) return null;
            foreach (var a in db.abilities)
                if (a != null && a.HasTag(AbilityTags.Movement)) return a.icon;
            return null;
        }
    }
}
