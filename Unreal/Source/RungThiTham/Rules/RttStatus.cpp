#include "Rules/RttStatus.h"

#include <cmath>

namespace Rtt
{
	bool StatusSpec::Any() const
	{
		return Burn > 0 || Chill > 0 || Charge > 0 || Poison > 0 || Stun > 0.0 || Root > 0.0 || Slow > 0.0 || Curse > 0.0 || Judgment > 0.0;
	}

	StatusSpec ReadStatus(const JsonValue& v)
	{
		StatusSpec s;
		s.Burn = v.Int("burn", 0);
		s.Chill = v.Int("chill", 0);
		s.Charge = v.Int("charge", 0);
		s.Poison = v.Int("poison", 0);
		s.Stun = v.Num("stun", 0.0);
		s.Root = v.Num("root", 0.0);
		s.Slow = v.Num("slow", 0.0);
		s.SlowDuration = v.Num("slowDuration", 0.0);
		s.Curse = v.Num("curse", 0.0);
		s.Judgment = v.Num("judgment", 0.0);
		return s;
	}

	double StatusEffects::ChillMultiplier(double now) const
	{
		return 1.0 - T->ChillSlowPerStack * LiveChill(now);
	}

	double StatusEffects::SpeedMultiplier(double now) const
	{
		if (IsFrozen(now) || IsRooted(now))
		{
			return 0.0;
		}
		return (IsSlowed(now) ? 1.0 - SlowAmount : 1.0) * ChillMultiplier(now);
	}

	double StatusEffects::DamageTakenMultiplier(double now) const
	{
		double m = 1.0;
		if (IsCursed(now))
		{
			m += T->CurseDamageTaken;
		}
		if (IsJudged(now))
		{
			m += T->JudgmentDamageTaken;
		}
		return m;
	}

	double StatusEffects::DamageDealtMultiplier(double now) const
	{
		return IsCursed(now) ? 1.0 - T->CurseDamageDealt : 1.0;
	}

	Discharge StatusEffects::Apply(const StatusSpec& s, double dealt, Team source, double now)
	{
		if (s.Burn > 0)
		{
			Burn(s.Burn, dealt * T->BurnShare, source, now);
		}
		if (s.Chill > 0)
		{
			Chill(s.Chill, now);
		}
		if (s.Poison > 0)
		{
			AddPoison(s.Poison, source, now);
		}
		if (s.Stun > 0.0)
		{
			Stun(s.Stun, now);
		}
		if (s.Root > 0.0)
		{
			Root(s.Root, now);
		}
		if (s.Slow > 0.0)
		{
			Slow(s.Slow, s.SlowDuration, now);
		}
		if (s.Curse > 0.0)
		{
			Curse(s.Curse, now);
		}
		if (s.Judgment > 0.0)
		{
			Judge(s.Judgment, now);
		}
		// last: a discharge hits others
		if (s.Charge > 0)
		{
			return Charge(s.Charge, dealt, source, now);
		}
		return Discharge();
	}

	void StatusEffects::Burn(int stacks, double dps, Team source, double now)
	{
		if (stacks <= 0 || dps <= 0.0)
		{
			return;
		}
		if (!IsBurning(now))
		{
			BurnStacks.clear();
			NextBurnTick = now + T->TickInterval;
		}
		for (int i = 0; i < stacks; ++i)
		{
			if (static_cast<int>(BurnStacks.size()) < T->BurnMaxStacks)
			{
				BurnStacks.push_back(dps);
				continue;
			}
			// full: a stronger burn replaces the weakest stack
			size_t weakest = 0;
			for (size_t j = 0; j < BurnStacks.size(); ++j)
			{
				if (BurnStacks[j] < BurnStacks[weakest])
				{
					weakest = j;
				}
			}
			if (!BurnStacks.empty() && dps > BurnStacks[weakest])
			{
				BurnStacks[weakest] = dps;
			}
		}
		BurnUntil = now + T->BurnSeconds;
		BurnSource = source;
	}

	void StatusEffects::Chill(int stacks, double now)
	{
		const int have = LiveChill(now) + stacks;
		ChillUntil = now + T->ChillSeconds;
		if (have < T->ChillMaxStacks)
		{
			ChillStacks = have;
			return;
		}
		ChillStacks = 0;
		ChillUntil = 0.0;
		Freeze(now);
	}

	void StatusEffects::Freeze(double now)
	{
		if (Bar)
		{
			Bar->Add(T->HeavyFreezePoise, now);
		}
		if (StunImmune)
		{
			return;
		}
		const double seconds = CrowdControl(IsHeavy() ? T->HeavyFreezeSeconds : T->FreezeSeconds, now);
		if (seconds <= 0.0)
		{
			return;
		}
		FreezeUntil = std::fmax(FreezeUntil, now + seconds);
		if (IsHeavy())
		{
			CcImmuneUntil = std::fmax(CcImmuneUntil, FreezeUntil + T->HeavyCrowdControlImmunity);
		}
		Words.push_back(StatusWord::Frozen);
	}

	Discharge StatusEffects::Charge(int stacks, double dealt, Team source, double now)
	{
		const int have = LiveCharge(now) + stacks;
		ChargeUntil = now + T->ChargeSeconds;
		if (have < T->ChargeMaxStacks)
		{
			ChargeStacks = have;
			return Discharge();
		}
		ChargeStacks = 0;
		Words.push_back(StatusWord::Discharged);
		Discharge d;
		d.Happened = true;
		d.Amount = dealt * T->DischargeShare;
		d.Source = source;
		d.Radius = T->DischargeRadius;
		d.Targets = T->DischargeTargets;
		return d;
	}

	void StatusEffects::AddPoison(int stacks, Team source, double now)
	{
		if (!IsPoisoned(now))
		{
			PoisonStacks = 0;
			NextPoisonTick = now + T->TickInterval;
			Words.push_back(StatusWord::Poisoned);
		}
		PoisonStacks = PoisonStacks + stacks < T->PoisonMaxStacks ? PoisonStacks + stacks : T->PoisonMaxStacks;
		PoisonUntil = now + T->PoisonSeconds * (1.0 - PoisonShorter);
		PoisonSource = source;
	}

	void StatusEffects::Stun(double seconds, double now)
	{
		if (StunImmune || seconds <= 0.0)
		{
			return;
		}
		seconds = CrowdControl(seconds * StunResist * (1.0 - ControlShorter), now);
		if (seconds > 0.0)
		{
			ApplyStun(seconds, now);
		}
	}

	void StatusEffects::ForceStun(double seconds, double now)
	{
		ApplyStun(seconds, now);
	}

	void StatusEffects::ApplyStun(double seconds, double now)
	{
		const bool fresh = now >= StunUntil;
		StunUntil = std::fmax(StunUntil, now + seconds);
		if (IsHeavy())
		{
			CcImmuneUntil = std::fmax(CcImmuneUntil, StunUntil + T->HeavyCrowdControlImmunity);
		}
		if (fresh)
		{
			Words.push_back(StatusWord::Stunned);
		}
	}

	void StatusEffects::ClearStun()
	{
		StunUntil = 0.0;
		FreezeUntil = 0.0;
	}

	void StatusEffects::Root(double seconds, double now)
	{
		if (StunImmune || seconds <= 0.0)
		{
			return;
		}
		seconds = CrowdControl(seconds * (1.0 - ControlShorter), now);
		if (seconds <= 0.0)
		{
			return;
		}
		const bool fresh = !IsRooted(now);
		RootUntil = std::fmax(RootUntil, now + seconds);
		if (fresh)
		{
			Words.push_back(StatusWord::Rooted);
		}
	}

	void StatusEffects::Slow(double amount, double seconds, double now)
	{
		const double a = amount < 0.0 ? 0.0 : (amount > 1.0 ? 1.0 : amount);
		SlowAmount = std::fmax(IsSlowed(now) ? SlowAmount : 0.0, a);
		SlowUntil = std::fmax(SlowUntil, now + (seconds > 0.0 ? seconds : T->SlowSeconds));
	}

	void StatusEffects::Curse(double seconds, double now)
	{
		const bool fresh = !IsCursed(now);
		CurseUntil = std::fmax(CurseUntil, now + seconds * (1.0 - ControlShorter));
		if (fresh)
		{
			Words.push_back(StatusWord::Cursed);
		}
	}

	void StatusEffects::Judge(double seconds, double now)
	{
		const bool fresh = !IsJudged(now);
		JudgmentUntil = std::fmax(JudgmentUntil, now + seconds);
		if (fresh)
		{
			Words.push_back(StatusWord::Judged);
		}
	}

	void StatusEffects::Cleanse()
	{
		BurnStacks.clear();
		BurnUntil = 0.0;
		ChillStacks = 0;
		ChillUntil = 0.0;
		FreezeUntil = 0.0;
		ChargeStacks = 0;
		ChargeUntil = 0.0;
		PoisonStacks = 0;
		PoisonUntil = 0.0;
		StunUntil = 0.0;
		RootUntil = 0.0;
		SlowUntil = 0.0;
		SlowAmount = 0.0;
		CurseUntil = 0.0;
		JudgmentUntil = 0.0;
	}

	double StatusEffects::CrowdControl(double seconds, double now)
	{
		if (seconds <= 0.0 || CrowdControlImmune(now))
		{
			return 0.0;
		}
		CcRepeats = now < CcRepeatUntil ? CcRepeats + 1 : 0;
		CcRepeatUntil = now + T->CrowdControlRepeatWindow;
		return seconds * std::pow(T->CrowdControlRepeatFactor, CcRepeats);
	}

	std::vector<DotTick> StatusEffects::Tick(double now, double maxHp, bool dead)
	{
		std::vector<DotTick> ticks;
		if (dead)
		{
			if (!BurnStacks.empty() || PoisonStacks > 0 || now < StunUntil)
			{
				Cleanse();
			}
			return ticks;
		}
		const double tick = T->TickInterval;
		while (!BurnStacks.empty() && NextBurnTick <= now && NextBurnTick <= BurnUntil + 0.001)
		{
			double dps = 0.0;
			for (double s : BurnStacks)
			{
				dps += s;
			}
			ticks.push_back({dps * tick, BurnSource, Element::Fire});
			NextBurnTick += tick;
		}
		if (!BurnStacks.empty() && now >= BurnUntil)
		{
			BurnStacks.clear();
		}
		while (PoisonStacks > 0 && NextPoisonTick <= now && NextPoisonTick <= PoisonUntil + 0.001)
		{
			ticks.push_back({maxHp * T->PoisonMaxHpPerSecond * PoisonStacks * tick, PoisonSource, Element::Poison});
			NextPoisonTick += tick;
		}
		if (PoisonStacks > 0 && now >= PoisonUntil)
		{
			PoisonStacks = 0;
		}
		const bool frozen = IsFrozen(now);
		if (WasFrozen && !frozen)
		{
			Words.push_back(StatusWord::Thawed);
		}
		WasFrozen = frozen;
		return ticks;
	}

	bool Poise::Add(double poise, double now, bool ownerDead)
	{
		if (poise <= 0.0 || IsBroken(now) || ownerDead)
		{
			return false;
		}
		Current += poise;
		LastHit = now;
		if (Current < Threshold)
		{
			return false;
		}
		++Breaks;
		Current = 0.0;
		Threshold = MaxPoise * (1.0 + ThresholdGrowth * Breaks);
		BrokenUntil = now + BreakStun;
		if (Status)
		{
			Status->ForceStun(BreakStun, now);
		}
		return true;
	}

	void Poise::Tick(double dt, double now)
	{
		if (!IsBroken(now) && Current > 0.0 && now - LastHit > DecayDelay)
		{
			Current = std::fmax(0.0, Current - DecayPerSecond * dt);
		}
	}

	void Poise::ForceBreak(double seconds, double now, bool ownerDead)
	{
		if (ownerDead)
		{
			return;
		}
		Current = 0.0;
		BrokenUntil = std::fmax(BrokenUntil, now + seconds);
	}

	void Poise::Reset()
	{
		BrokenUntil = 0.0;
		Current = 0.0;
		Breaks = 0;
		Threshold = MaxPoise;
		LastHit = -100.0;
	}
}
