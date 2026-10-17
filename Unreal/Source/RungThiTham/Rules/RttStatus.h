// Statuses and the stagger bar (the Unity game's StatusEffects.cs and Poise.cs, as the Godot port
// has them in status_effects.gd and poise.gd), with the numbers of Content/Data/combat.json.
//
// StatusEffects: Bỏng (burn stacks ticking fire), Lạnh (chill stacks; the fourth freezes), Tích
// Điện (charge stacks; the third discharges onto others), Độc (poison ticking a share of max life),
// Choáng, Trói, Làm Chậm, Nguyền and Phán Xét. Crowd control again within 6 s is shorter (×0.6 per
// repeat). "Heavy" owners are those with a Poise bar (bosses): a freeze adds poise and is shorter,
// and after a stun or freeze they shrug off crowd control for a while.
//
// Poise (Thanh Trấn Áp): heroes' hits fill it; full, it breaks: 3 s of Choáng and +50% damage
// taken, and the next break needs 25% more. Untouched for 2.5 s it drains at 40 per second.
//
// Time is the caller's game time in seconds (`now`), so the same rules run in the game, on a
// server and in Tools/RulesCheck. Ticks come back as values the game deals through ResolveDamage.
#pragma once

#include "Rules/RttTuning.h"
#include "Rules/RttCombat.h"

#include <vector>

namespace Rtt
{
	// What a hit puts on its target (an ability hit's "status" block).
	struct StatusSpec
	{
		int Burn = 0;
		int Chill = 0;
		int Charge = 0;
		int Poison = 0;
		double Stun = 0.0;
		double Root = 0.0;
		double Slow = 0.0;
		double SlowDuration = 0.0;
		double Curse = 0.0;
		double Judgment = 0.0;

		bool Any() const;
	};
	StatusSpec ReadStatus(const JsonValue& v);

	// Words the game shows over the owner (and plays effects for); StatusEffects collects them in Words.
	enum class StatusWord : int { Frozen, Thawed, Stunned, Rooted, Cursed, Judged, Discharged, Poisoned };

	// A burn or poison tick: the game deals it to the owner as a Dot, AttackScaled hit.
	struct DotTick
	{
		double Amount = 0.0;
		Team Source = Team::Player;
		Element Type = Element::Fire;
	};

	// Phóng Điện: lightning that jumps from the owner to the nearest others (up to Targets within
	// Radius metres, of a side Source can hurt), each taking Amount as a Lightning hit.
	struct Discharge
	{
		bool Happened = false;
		double Amount = 0.0;
		Team Source = Team::Player;
		double Radius = 0.0;
		int Targets = 0;
	};

	class Poise;

	class StatusEffects
	{
	public:
		explicit StatusEffects(const Tuning& tuning) : T(&tuning) {}

		const Tuning* T;
		// Set on heavy owners (bosses); Link() ties the two together.
		Poise* Bar = nullptr;
		// Hero buffs: no Choáng, Trói or freezing.
		bool StunImmune = false;
		// Multiplies Choáng only (bosses 0.5).
		double StunResist = 1.0;
		// 0..1: shortens Choáng, Trói and Nguyền (races, saving throws) / Độc.
		double ControlShorter = 0.0;
		double PoisonShorter = 0.0;
		// Filled as things happen; the game reads and clears it.
		std::vector<StatusWord> Words;

		bool IsHeavy() const { return Bar != nullptr; }
		bool IsFrozen(double now) const { return now < FreezeUntil; }
		bool IsStunned(double now) const { return now < StunUntil || IsFrozen(now); }
		bool IsRooted(double now) const { return now < RootUntil; }
		bool IsSlowed(double now) const { return now < SlowUntil; }
		bool IsCursed(double now) const { return now < CurseUntil; }
		bool IsJudged(double now) const { return now < JudgmentUntil; }
		bool IsBurning(double now) const { return !BurnStacks.empty() && now < BurnUntil; }
		bool IsPoisoned(double now) const { return PoisonStacks > 0 && now < PoisonUntil; }
		int LiveChill(double now) const { return now < ChillUntil ? ChillStacks : 0; }
		int LiveCharge(double now) const { return now < ChargeUntil ? ChargeStacks : 0; }
		int LivePoison(double now) const { return IsPoisoned(now) ? PoisonStacks : 0; }
		double StunRemaining(double now) const { return StunUntil > now ? StunUntil - now : 0.0; }
		bool CrowdControlImmune(double now) const { return IsHeavy() && now < CcImmuneUntil; }

		double ChillMultiplier(double now) const;
		// Movement speed multiplier (0 while frozen or rooted).
		double SpeedMultiplier(double now) const;
		double AttackSpeedMultiplier(double now) const { return ChillMultiplier(now); }
		double DamageTakenMultiplier(double now) const;
		double DamageDealtMultiplier(double now) const;

		// What a landed hit does (never on a killing blow); `dealt` is the attacker-side damage
		// Bỏng and Phóng Điện scale from. A discharge is last and is the caller's to spread.
		Discharge Apply(const StatusSpec& s, double dealt, Team source, double now);

		void Burn(int stacks, double dps, Team source, double now);
		void Chill(int stacks, double now);
		void Freeze(double now);
		Discharge Charge(int stacks, double dealt, Team source, double now);
		void AddPoison(int stacks, Team source, double now);
		void Stun(double seconds, double now);
		// Poise breaks and wall rams: no resistance, immunity or shortening.
		void ForceStun(double seconds, double now);
		void ClearStun();
		void Root(double seconds, double now);
		void Slow(double amount, double seconds, double now);
		void Curse(double seconds, double now);
		void Judge(double seconds, double now);
		// Rammed into a wall hard enough (WallSlamSpeed): a short stun.
		void WallSlam(double now) { Stun(T->WallSlamStun, now); }
		void Cleanse();

		// The burn and poison ticks due by `now`, in order (the game stops dealing them once the
		// owner dies). A dead owner is cleansed instead.
		std::vector<DotTick> Tick(double now, double maxHp, bool dead);

	private:
		std::vector<double> BurnStacks; // damage per second of each stack
		double BurnUntil = 0.0;
		double NextBurnTick = 0.0;
		Team BurnSource = Team::Player;
		int ChillStacks = 0;
		double ChillUntil = 0.0;
		double FreezeUntil = 0.0;
		int ChargeStacks = 0;
		double ChargeUntil = 0.0;
		int PoisonStacks = 0;
		double PoisonUntil = 0.0;
		double NextPoisonTick = 0.0;
		Team PoisonSource = Team::Player;
		double StunUntil = 0.0;
		double RootUntil = 0.0;
		double SlowAmount = 0.0;
		double SlowUntil = 0.0;
		double CurseUntil = 0.0;
		double JudgmentUntil = 0.0;
		int CcRepeats = 0;
		double CcRepeatUntil = 0.0;
		double CcImmuneUntil = 0.0;
		bool WasFrozen = false;

		void ApplyStun(double seconds, double now);
		// The length a crowd control lasts after immunity and repeats (0 = none).
		double CrowdControl(double seconds, double now);
	};

	class Poise
	{
	public:
		double MaxPoise = 300.0;
		double DecayDelay = 2.5;
		double DecayPerSecond = 40.0;
		double BreakStun = 3.0;
		double BrokenDamageBonus = 0.5;
		double ThresholdGrowth = 0.25;

		double Current = 0.0;
		double Threshold = 300.0;
		int Breaks = 0;
		StatusEffects* Status = nullptr;

		explicit Poise(double maxPoise = 300.0) : MaxPoise(maxPoise), Threshold(maxPoise) {}

		bool IsBroken(double now) const { return now < BrokenUntil; }
		double Fraction(double now) const { return IsBroken(now) ? 1.0 : Current / Threshold; }
		double BrokenRemaining(double now) const { return BrokenUntil > now ? BrokenUntil - now : 0.0; }
		// ×1.5 while broken, on top of the owner's own damage-taken multiplier.
		double DamageTakenMultiplier(double now) const { return IsBroken(now) ? 1.0 + BrokenDamageBonus : 1.0; }

		// A hero's hit worth `poise` (× the hero's poise multiplier); true when it broke the bar.
		bool Add(double poise, double now, bool ownerDead = false);
		// Drains the bar once it has been left alone for DecayDelay.
		void Tick(double dt, double now);
		// Knocked down from outside the bar (an interrupt): the broken window and its bonus,
		// without filling or raising the bar.
		void ForceBreak(double seconds, double now, bool ownerDead = false);
		void Reset();

	private:
		double BrokenUntil = 0.0;
		double LastHit = -100.0;
	};

	// Ties a heavy owner's statuses and stagger bar together.
	inline void Link(StatusEffects& status, Poise& poise)
	{
		status.Bar = &poise;
		poise.Status = &status;
	}
}
