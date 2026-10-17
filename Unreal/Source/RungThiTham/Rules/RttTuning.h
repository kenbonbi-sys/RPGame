// Every tuning number of the combat and progression rules, from Content/Data/progression.json and
// combat.json (the Unity game's ProgressionConfig and CombatConfig). The defaults are the Unity
// values, used when a key is missing.
#pragma once

#include "Rules/RttJson.h"

namespace Rtt
{
	struct Tuning
	{
		// progression
		int MaxLevel = 40;
		double XpBase = 50.0;
		double XpExponent = 1.6;
		int StatPointsPerLevel = 3;
		int TalentPointsPerLevel = 1;
		int SkillPointsPerLevel = 1;
		double ReferenceScore = 16.0;
		double HpBase = 56.0;
		double HpPerHitDie = 1.8;
		double HpPerConstitution = 10.0;
		double EnergyBase = 45.0;
		double EnergyPerLevel = 1.5;
		double EnergyPerCastingPoint = 1.5;
		double WeaponAttack = 20.0;
		double AttackPerPoint = 1.5;
		double AttackFromScore = 13.0;
		double CritPerDexterity = 0.0025;
		double CritMax = 0.6;
		double CritMultiplier = 1.8;
		double ArmorPerConstitution = 0.5;
		double ResistPerWisdom = 0.003;
		double ResistMin = -0.5;
		double ResistMax = 0.75;
		double ArmorConstant = 50.0;
		double ArmorPerAttackerLevel = 5.0;
		double AttackSpeedPerDexterity = 0.005;
		double DashCooldownPerDexterity = 0.005;
		double PoisePerStrength = 0.01;
		double CooldownPerIntelligence = 0.004;
		double CooldownReductionMax = 0.3;
		double HealingPerWisdom = 0.02;
		double GoldPerCharisma = 0.02;
		double KillXpBase = 8.0;
		double KillXpPerLevel = 4.0;
		double RankMultiplier[4] = {1.0, 4.0, 15.0, 40.0};
		int LowGap = 5;
		double LowGapFactor = 0.5;
		int VeryLowGap = 8;
		double VeryLowGapFactor = 0.1;
		double DamageSpread = 0.05;

		// combat
		double CurseDamageTaken = 0.15;
		double CurseDamageDealt = 0.2;
		double JudgmentDamageTaken = 0.2;
		double PerfectWindow = 0.15;
		double PerfectDamageMultiplier = 1.3;

		// Reads both files' objects (either may be null: its defaults stay).
		void Load(const JsonValue& progression, const JsonValue& combat);
	};
}
