#include "Rules/RttTuning.h"

namespace Rtt
{
	void Tuning::Load(const JsonValue& p, const JsonValue& c)
	{
		MaxLevel = p.Int("maxLevel", MaxLevel);
		XpBase = p.Num("xpBase", XpBase);
		XpExponent = p.Num("xpExponent", XpExponent);
		StatPointsPerLevel = p.Int("statPointsPerLevel", StatPointsPerLevel);
		TalentPointsPerLevel = p.Int("talentPointsPerLevel", TalentPointsPerLevel);
		SkillPointsPerLevel = p.Int("skillPointsPerLevel", SkillPointsPerLevel);
		ReferenceScore = p.Num("referenceScore", ReferenceScore);
		HpBase = p.Num("hpBase", HpBase);
		HpPerHitDie = p.Num("hpPerHitDie", HpPerHitDie);
		HpPerConstitution = p.Num("hpPerConstitution", HpPerConstitution);
		EnergyBase = p.Num("energyBase", EnergyBase);
		EnergyPerLevel = p.Num("energyPerLevel", EnergyPerLevel);
		EnergyPerCastingPoint = p.Num("energyPerCastingPoint", EnergyPerCastingPoint);
		WeaponAttack = p.Num("weaponAttack", WeaponAttack);
		AttackPerPoint = p.Num("attackPerPoint", AttackPerPoint);
		AttackFromScore = p.Num("attackFromScore", AttackFromScore);
		CritPerDexterity = p.Num("critPerDexterity", CritPerDexterity);
		CritMax = p.Num("critMax", CritMax);
		CritMultiplier = p.Num("critMultiplier", CritMultiplier);
		ArmorPerConstitution = p.Num("armorPerConstitution", ArmorPerConstitution);
		ResistPerWisdom = p.Num("resistPerWisdom", ResistPerWisdom);
		ResistMin = p.Num("resistMin", ResistMin);
		ResistMax = p.Num("resistMax", ResistMax);
		ArmorConstant = p.Num("armorConstant", ArmorConstant);
		ArmorPerAttackerLevel = p.Num("armorPerAttackerLevel", ArmorPerAttackerLevel);
		AttackSpeedPerDexterity = p.Num("attackSpeedPerDexterity", AttackSpeedPerDexterity);
		DashCooldownPerDexterity = p.Num("dashCooldownPerDexterity", DashCooldownPerDexterity);
		PoisePerStrength = p.Num("poisePerStrength", PoisePerStrength);
		CooldownPerIntelligence = p.Num("cooldownPerIntelligence", CooldownPerIntelligence);
		CooldownReductionMax = p.Num("cooldownReductionMax", CooldownReductionMax);
		HealingPerWisdom = p.Num("healingPerWisdom", HealingPerWisdom);
		GoldPerCharisma = p.Num("goldPerCharisma", GoldPerCharisma);
		KillXpBase = p.Num("killXpBase", KillXpBase);
		KillXpPerLevel = p.Num("killXpPerLevel", KillXpPerLevel);
		const JsonValue& ranks = p["rankMultiplier"];
		for (size_t r = 0; r < 4 && r < ranks.Size(); ++r)
		{
			RankMultiplier[r] = ranks[r].Num(RankMultiplier[r]);
		}
		LowGap = p.Int("lowGap", LowGap);
		LowGapFactor = p.Num("lowGapFactor", LowGapFactor);
		VeryLowGap = p.Int("veryLowGap", VeryLowGap);
		VeryLowGapFactor = p.Num("veryLowGapFactor", VeryLowGapFactor);
		DamageSpread = p.Num("damageSpread", DamageSpread);

		CurseDamageTaken = c.Num("curseDamageTaken", CurseDamageTaken);
		CurseDamageDealt = c.Num("curseDamageDealt", CurseDamageDealt);
		JudgmentDamageTaken = c.Num("judgmentDamageTaken", JudgmentDamageTaken);
		PerfectWindow = c.Num("perfectWindow", PerfectWindow);
		PerfectDamageMultiplier = c.Num("perfectDamageMultiplier", PerfectDamageMultiplier);
	}
}
