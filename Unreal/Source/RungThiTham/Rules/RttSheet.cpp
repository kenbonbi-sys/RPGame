#include "Rules/RttSheet.h"

#include <cmath>

namespace Rtt
{
	namespace
	{
		const WeaponKind Kinds[] = {
			{"sword", 20.0, "slash", false, false, false},
			{"axe", 23.0, "cleave", false, false, false},
			{"mace", 21.0, "smash", false, false, false},
			{"spear", 20.0, "thrust", false, false, false},
			{"rapier", 19.0, "thrust", true, false, false},
			{"scimitar", 18.0, "slash", true, false, false},
			{"dagger", 16.0, "stab", true, false, false},
			{"fist", 17.0, "flurry", true, false, false},
			{"quarterstaff", 19.0, "smash", true, false, false},
			{"bow", 19.0, "arrow", false, true, false},
			{"staff", 20.0, "", false, false, true},
			{"wand", 19.0, "", false, false, true},
			{"orb", 20.0, "", false, false, true},
			{"tome", 20.0, "", false, false, true},
			{"lute", 19.0, "", false, false, true},
		};

		double Max(double a, double b) { return a > b ? a : b; }
		double Min(double a, double b) { return a < b ? a : b; }
		double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }
	}

	const char* ScoreBonusKey(int s)
	{
		// Sức Mạnh, Trí Tuệ, Khéo Léo, Thể Chất, Thông Thái, Sức Hút (UTF-8)
		static const char* Keys[ScoreCount] = {"S\xe1\xbb\xa9\x63 M\xe1\xba\xa1nh", "Tr\xc3\xad Tu\xe1\xbb\x87",
			"Kh\xc3\xa9o L\xc3\xa9o", "Th\xe1\xbb\x83 Ch\xe1\xba\xa5t", "Th\xc3\xb4ng Th\xc3\xa1i", "S\xe1\xbb\xa9\x63 H\xc3\xbat"};
		return s >= 0 && s < ScoreCount ? Keys[s] : "";
	}

	const WeaponKind& WeaponKindOf(const std::string& id)
	{
		for (const WeaponKind& k : Kinds)
		{
			if (id == k.Id)
			{
				return k;
			}
		}
		return Kinds[0];
	}

	ClassData ReadClass(const JsonValue& v)
	{
		ClassData c;
		if (!v.IsObject())
		{
			return c;
		}
		c.Valid = true;
		c.Id = v.Str("id");
		c.HitDie = v.Int("hitDie", 8);
		const JsonValue& order = v["arrayOrder"];
		for (int k = 0; k < ScoreCount; ++k)
		{
			c.ArrayOrder[k] = order[static_cast<size_t>(k)].Int(k);
		}
		const JsonValue& saves = v["savingThrows"];
		for (size_t k = 0; k < saves.Size(); ++k)
		{
			c.SavingThrows.push_back(saves[k].Int());
		}
		c.Casting = v.Int("casting", 0);
		c.Armor = static_cast<ArmorKind>(v.Int("armor", 0));
		const JsonValue& weapons = v["weapons"];
		for (size_t k = 0; k < weapons.Size(); ++k)
		{
			c.Weapons.push_back(weapons[k].Str());
		}
		return c;
	}

	RaceData ReadRace(const JsonValue& v)
	{
		RaceData r;
		if (!v.IsObject())
		{
			return r;
		}
		r.Valid = true;
		r.Id = v.Str("id");
		const JsonValue& bonus = v["bonus"];
		for (int k = 0; k < ScoreCount; ++k)
		{
			r.Bonus[k] = bonus[static_cast<size_t>(k)].Num(0.0);
		}
		r.HpPerLevel = v.Num("hpPerLevel", 0.0);
		r.SavageCrit = v.Num("savageCrit", 0.0);
		r.SpeedMultiplier = v.Num("speedMultiplier", 1.0);
		const JsonValue& resist = v["resist"];
		static const char* Keys[ResistanceSlots] = {"physical", "fire", "ice", "lightning", "poison", "holy", "dark"};
		for (int k = 0; k < ResistanceSlots; ++k)
		{
			r.Resist[k] = resist.Num(Keys[k], 0.0);
		}
		r.Draconic = v.Int("draconic", 0) == 1;
		r.ControlShorter = v.Num("controlShorter", 0.0);
		r.PoisonShorter = v.Num("poisonShorter", 0.0);
		r.XpBonus = v.Num("xpBonus", 0.0);
		return r;
	}

	double Sheet::Bonus(const std::string& stat) const
	{
		const auto it = Bonuses.find(stat);
		return it == Bonuses.end() ? 0.0 : it->second;
	}

	double Sheet::BaseScore(int s) const
	{
		double v = 0.0;
		if (!Class.Valid)
		{
			v = Classless[s];
		}
		else
		{
			int at = -1;
			for (int k = 0; k < ScoreCount; ++k)
			{
				if (Class.ArrayOrder[k] == s)
				{
					at = k;
				}
			}
			v = at >= 0 ? StandardArray[at] : 10.0;
		}
		return v + Race.Bonus[s];
	}

	double Sheet::HitDieAverage() const
	{
		return Class.Valid ? Class.HitDie / 2.0 + 0.5 : 4.5;
	}

	double Sheet::CastingScore() const
	{
		if (!Class.Valid)
		{
			return Max(Scores[Int], Scores[Str]);
		}
		return Scores[Class.Casting >= 0 && Class.Casting < ScoreCount ? Class.Casting : 0];
	}

	double Sheet::AttackScore() const
	{
		const WeaponKind& w = WeaponKindOf(WeaponId);
		if (w.Focus) return CastingScore();
		if (w.Ranged) return Scores[Dex];
		if (w.Finesse) return Max(Scores[Str], Scores[Dex]);
		return Scores[Str];
	}

	double Sheet::WeaponAttack() const
	{
		const int up = WeaponUpgrade < 0 ? 0 : (WeaponUpgrade > MaxUpgrade ? MaxUpgrade : WeaponUpgrade);
		return WeaponKindOf(WeaponId).Attack * (1.0 + AttackPerUpgrade * up);
	}

	double Sheet::ClassBaseArmor() const
	{
		if (!Class.Valid)
		{
			return 2.0;
		}
		switch (Class.Armor)
		{
		case ArmorKind::Light: return 2.0;
		case ArmorKind::Medium: return 5.0;
		case ArmorKind::Heavy: return 8.0;
		case ArmorKind::Unarmored:
		{
			const double x = Class.Id == "monk" ? Scores[Wis] : Scores[Con];
			return 1.0 + 0.5 * Max(0.0, x - 10.0);
		}
		default: return 0.0;
		}
	}

	bool Sheet::HasSave(int s) const
	{
		for (int v : Class.SavingThrows)
		{
			if (v == s)
			{
				return true;
			}
		}
		return false;
	}

	void Sheet::Recalculate()
	{
		const Tuning& t = *T;
		for (int s = 0; s < ScoreCount; ++s)
		{
			Scores[s] = BaseScore(s) + Allocated[s] + Bonus(ScoreBonusKey(s));
		}
		const double str = Scores[Str];
		const double dex = Scores[Dex];
		const double con = Scores[Con];
		const double intl = Scores[Int];
		const double wis = Scores[Wis];
		const double cha = Scores[Cha];
		const double wAtk = WeaponAttack();
		MaxHp = t.HpBase + t.HpPerHitDie * HitDieAverage() * Level + t.HpPerConstitution * (con - 10.0) + Race.HpPerLevel * Level + Bonus("MaxHp");
		MaxEnergy = t.EnergyBase + t.EnergyPerLevel * Level + t.EnergyPerCastingPoint * (CastingScore() - 10.0) + Bonus("MaxEnergy");
		PhysicalAttack = wAtk + t.AttackPerPoint * (AttackScore() - t.AttackFromScore) + Bonus("PhysicalAttack");
		MagicAttack = wAtk + t.AttackPerPoint * (CastingScore() - t.AttackFromScore) + Bonus("MagicAttack");
		CritChance = t.CritPerDexterity * (dex - 10.0) + Bonus("CritChance");
		CritDamage = t.CritMultiplier + Race.SavageCrit + Bonus("CritDamage");
		AttackSpeed = t.AttackSpeedPerDexterity * (dex - 10.0) + Bonus("AttackSpeed");
		DashCooldownReduction = t.DashCooldownPerDexterity * (dex - 10.0) + Bonus("DashCooldownReduction");
		Armor = Max(0.0, ClassBaseArmor() + t.ArmorPerConstitution * (con - 10.0)) + Bonus("Armor");
		ElementalResist = Max(0.0, t.ResistPerWisdom * (wis - 10.0)) + Bonus("ElementalResist");
		PoiseMultiplier = 1.0 + t.PoisePerStrength * (str - 10.0) + Bonus("PoiseDamage");
		DamageDealt = 1.0 + Bonus("DamageDealt");
		CooldownReduction = Clamp(t.CooldownPerIntelligence * (intl - 10.0), 0.0, t.CooldownReductionMax) + Bonus("CooldownReduction");
		HealingPower = Max(0.5, 1.0 + t.HealingPerWisdom * (wis - 10.0)) + Bonus("HealingPower");
		GoldFind = Max(0.0, t.GoldPerCharisma * (cha - 10.0)) + Bonus("GoldFind");
		SpeedMultiplier = Max(0.5, Race.SpeedMultiplier);

		for (int k = 0; k < ResistanceSlots; ++k)
		{
			Resistances[k] = Race.Resist[k];
		}
		if (Race.Draconic)
		{
			// the ancestor's colour: red, gold -> fire; blue, bronze -> lightning; white, silver -> ice;
			// green, black -> poison
			static const Element Draconic[8] = {Element::Fire, Element::Fire, Element::Lightning, Element::Lightning, Element::Ice, Element::Ice,
				Element::Poison, Element::Poison};
			const int skin = SkinIndex < 0 ? 0 : (SkinIndex > 7 ? 7 : SkinIndex);
			Resistances[static_cast<int>(Draconic[skin])] += 0.5;
		}
		ControlShorter = Min(0.6, Race.ControlShorter + (HasSave(Wis) ? 0.2 : 0.0));
		PoisonShorter = Min(0.75, Race.PoisonShorter + (HasSave(Con) ? 0.25 : 0.0));
	}

	double Sheet::AttackFor(Element type) const
	{
		return type == Element::Physical ? PhysicalAttack : MagicAttack;
	}

	double Sheet::DamageScale(Element type) const
	{
		const Tuning& t = *T;
		const double reference = t.WeaponAttack + t.AttackPerPoint * (t.ReferenceScore - t.AttackFromScore);
		return AttackFor(type) / reference;
	}

	double Sheet::CooldownMultiplier(bool isBasic, bool isMovement) const
	{
		if (isMovement)
		{
			return Max(0.5, 1.0 - DashCooldownReduction);
		}
		if (isBasic)
		{
			return 1.0 / Max(0.5, 1.0 + AttackSpeed);
		}
		return 1.0 - CooldownReduction;
	}

	bool Sheet::SpendPoint(int s)
	{
		if (StatPoints <= 0 || s < 0 || s >= ScoreCount)
		{
			return false;
		}
		--StatPoints;
		++Allocated[s];
		Recalculate();
		return true;
	}

	void Sheet::RefundPoints()
	{
		for (int s = 0; s < ScoreCount; ++s)
		{
			StatPoints += Allocated[s];
			Allocated[s] = 0;
		}
		Recalculate();
	}

	bool Sheet::AddXp(int amount)
	{
		const Tuning& t = *T;
		if (amount <= 0 || Level >= t.MaxLevel)
		{
			return false;
		}
		Xp += amount;
		bool grew = false;
		while (Level < t.MaxLevel && Xp >= XpToNext(Level, t))
		{
			Xp -= XpToNext(Level, t);
			++Level;
			StatPoints += t.StatPointsPerLevel;
			TalentPoints += t.TalentPointsPerLevel;
			SkillPoints += t.SkillPointsPerLevel;
			grew = true;
		}
		if (Level >= t.MaxLevel)
		{
			Xp = 0;
		}
		if (grew)
		{
			Recalculate();
		}
		return grew;
	}

	int Sheet::XpToNext(int level, const Tuning& t)
	{
		if (level >= t.MaxLevel)
		{
			return 0;
		}
		return static_cast<int>(RoundHalfEven(t.XpBase * std::pow(Max(1.0, static_cast<double>(level)), t.XpExponent)));
	}

	int Sheet::TotalXp(int level, const Tuning& t)
	{
		int total = 0;
		for (int l = 1; l < level; ++l)
		{
			total += XpToNext(l, t);
		}
		return total;
	}

	int Sheet::KillXp(int enemyLevel, Rank rank, int heroLevel, const Tuning& t)
	{
		const int r = static_cast<int>(rank);
		double v = (t.KillXpBase + t.KillXpPerLevel * enemyLevel) * t.RankMultiplier[r < 0 ? 0 : (r > 3 ? 3 : r)];
		const int gap = heroLevel - enemyLevel;
		if (gap >= t.VeryLowGap)
		{
			v *= t.VeryLowGapFactor;
		}
		else if (gap >= t.LowGap)
		{
			v *= t.LowGapFactor;
		}
		const int xp = static_cast<int>(RoundHalfEven(v));
		return xp < 1 ? 1 : xp;
	}
}
