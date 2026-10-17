// A hero's character sheet (the Unity game's PlayerStats + ProgressionConfig, as the Godot port has
// them): level, XP, the six D&D ability scores from class (the standard array in the class's order)
// and race, and everything derived from them: life, energy, Attack, crit, armour, resistances,
// speeds. Classes and races come from Content/Data/classes.json and races.json.
#pragma once

#include "Rules/RttCombat.h"

#include <map>
#include <string>
#include <vector>

namespace Rtt
{
	enum Score : int { Str, Int, Dex, Con, Wis, Cha };
	constexpr int ScoreCount = 6;
	enum class ArmorKind : int { None, Light, Medium, Heavy, Unarmored };
	enum class Rank : int { Normal, Elite, Miniboss, Boss };

	// WeaponKinds.cs: Attack, how the weapon strikes (which score feeds it), its basic attack.
	struct WeaponKind
	{
		const char* Id;
		double Attack;
		const char* Basic;
		bool Finesse;
		bool Ranged;
		bool Focus;
	};
	const WeaponKind& WeaponKindOf(const std::string& id);

	struct ClassData
	{
		bool Valid = false;
		std::string Id;
		int HitDie = 8;
		int ArrayOrder[ScoreCount] = {0, 1, 2, 3, 4, 5};
		std::vector<int> SavingThrows;
		int Casting = 0;
		ArmorKind Armor = ArmorKind::None;
		std::vector<std::string> Weapons;
	};
	ClassData ReadClass(const JsonValue& v);

	struct RaceData
	{
		bool Valid = false;
		std::string Id;
		double Bonus[ScoreCount] = {};
		double HpPerLevel = 0.0;
		double SavageCrit = 0.0;
		double SpeedMultiplier = 1.0;
		double Resist[ResistanceSlots] = {};
		bool Draconic = false;
		double ControlShorter = 0.0;
		double PoisonShorter = 0.0;
		double XpBonus = 0.0;
	};
	RaceData ReadRace(const JsonValue& v);

	class Sheet
	{
	public:
		static constexpr int StandardArray[ScoreCount] = {15, 14, 13, 12, 10, 8};
		static constexpr int Classless[ScoreCount] = {16, 16, 11, 14, 10, 10};
		static constexpr double AttackPerUpgrade = 0.08;
		static constexpr int MaxUpgrade = 10;

		const Tuning* T = nullptr;
		ClassData Class;
		RaceData Race;
		std::string WeaponId = "sword";
		int WeaponUpgrade = 0;
		int SkinIndex = 0;

		int Level = 1;
		int Xp = 0;
		int StatPoints = 0;
		int TalentPoints = 0;
		int SkillPoints = 0;
		int Allocated[ScoreCount] = {};
		// flat bonuses from gear and buffs: {stat name: value}
		std::map<std::string, double> Bonuses;

		// derived (Recalculate fills these)
		double Scores[ScoreCount] = {10, 10, 10, 10, 10, 10};
		double MaxHp = 100.0;
		double MaxEnergy = 50.0;
		double PhysicalAttack = 24.5;
		double MagicAttack = 24.5;
		double CritChance = 0.0;
		double CritDamage = 1.8;
		double AttackSpeed = 0.0;
		double DashCooldownReduction = 0.0;
		double Armor = 0.0;
		double ElementalResist = 0.0;
		double PoiseMultiplier = 1.0;
		double DamageDealt = 1.0;
		double CooldownReduction = 0.0;
		double HealingPower = 1.0;
		double GoldFind = 0.0;
		double SpeedMultiplier = 1.0;
		// pushed into the hero's Health
		double Resistances[ResistanceSlots] = {};
		double ControlShorter = 0.0;
		double PoisonShorter = 0.0;

		explicit Sheet(const Tuning& tuning) : T(&tuning) {}

		double BaseScore(int s) const;
		double HitDieAverage() const;
		double CastingScore() const;
		double AttackScore() const;
		double WeaponAttack() const;
		double ClassBaseArmor() const;
		bool HasSave(int s) const;
		void Recalculate();

		// The life the hero's Health gets (rounded half to even).
		double HealthMax() const { return RoundHalfEven(MaxHp); }
		double AttackFor(Element type) const;
		// Old flat-number hits are scaled by Attack / Attack(16, 20).
		double DamageScale(Element type) const;
		double CooldownMultiplier(bool isBasic, bool isMovement) const;

		bool SpendPoint(int s);
		void RefundPoints();
		// Gives XP and levels up as far as it goes; true when it levelled.
		bool AddXp(int amount);

		static int XpToNext(int level, const Tuning& t);
		static int TotalXp(int level, const Tuning& t);
		// XP for a monster of `enemyLevel` and `rank`, less when it is far below the hero.
		static int KillXp(int enemyLevel, Rank rank, int heroLevel, const Tuning& t);

	private:
		double Bonus(const std::string& stat) const;
	};

	// The bonus key of a score (the Vietnamese score names, as gear and buffs name them).
	const char* ScoreBonusKey(int s);
}
