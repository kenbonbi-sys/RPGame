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

		TickInterval = c.Num("tickInterval", TickInterval);
		BurnShare = c.Num("burnShare", BurnShare);
		BurnMaxStacks = c.Int("burnMaxStacks", BurnMaxStacks);
		BurnSeconds = c.Num("burnSeconds", BurnSeconds);
		ChillSlowPerStack = c.Num("chillSlowPerStack", ChillSlowPerStack);
		ChillMaxStacks = c.Int("chillMaxStacks", ChillMaxStacks);
		ChillSeconds = c.Num("chillSeconds", ChillSeconds);
		FreezeSeconds = c.Num("freezeSeconds", FreezeSeconds);
		HeavyFreezeSeconds = c.Num("heavyFreezeSeconds", HeavyFreezeSeconds);
		HeavyFreezePoise = c.Num("heavyFreezePoise", HeavyFreezePoise);
		ChargeMaxStacks = c.Int("chargeMaxStacks", ChargeMaxStacks);
		ChargeSeconds = c.Num("chargeSeconds", ChargeSeconds);
		DischargeShare = c.Num("dischargeShare", DischargeShare);
		DischargeTargets = c.Int("dischargeTargets", DischargeTargets);
		DischargeRadius = c.Num("dischargeRadius", DischargeRadius);
		PoisonMaxHpPerSecond = c.Num("poisonMaxHpPerSecond", PoisonMaxHpPerSecond);
		PoisonMaxStacks = c.Int("poisonMaxStacks", PoisonMaxStacks);
		PoisonSeconds = c.Num("poisonSeconds", PoisonSeconds);
		SlowSeconds = c.Num("slowSeconds", SlowSeconds);
		CurseDamageTaken = c.Num("curseDamageTaken", CurseDamageTaken);
		CurseDamageDealt = c.Num("curseDamageDealt", CurseDamageDealt);
		JudgmentDamageTaken = c.Num("judgmentDamageTaken", JudgmentDamageTaken);
		CrowdControlRepeatWindow = c.Num("crowdControlRepeatWindow", CrowdControlRepeatWindow);
		CrowdControlRepeatFactor = c.Num("crowdControlRepeatFactor", CrowdControlRepeatFactor);
		HeavyCrowdControlImmunity = c.Num("heavyCrowdControlImmunity", HeavyCrowdControlImmunity);
		WallSlamSpeed = c.Num("wallSlamSpeed", WallSlamSpeed);
		WallSlamStun = c.Num("wallSlamStun", WallSlamStun);
		HitStopLight = c.Num("hitStopLight", HitStopLight);
		HitStopHeavy = c.Num("hitStopHeavy", HitStopHeavy);
		HitStopBreak = c.Num("hitStopBreak", HitStopBreak);
		PerfectWindow = c.Num("perfectWindow", PerfectWindow);
		PerfectTimeScale = c.Num("perfectTimeScale", PerfectTimeScale);
		PerfectSlowSeconds = c.Num("perfectSlowSeconds", PerfectSlowSeconds);
		PerfectSlowEase = c.Num("perfectSlowEase", PerfectSlowEase);
		PerfectEnergy = c.Num("perfectEnergy", PerfectEnergy);
		PerfectDamageMultiplier = c.Num("perfectDamageMultiplier", PerfectDamageMultiplier);
		PerfectBonusSeconds = c.Num("perfectBonusSeconds", PerfectBonusSeconds);
		PerfectCounterPoise = c.Num("perfectCounterPoise", PerfectCounterPoise);
	}
}
