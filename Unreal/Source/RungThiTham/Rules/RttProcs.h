// Element procs (the Godot port's Elements.on_hit / proc): weapon blows of an element fill that
// element's meter on what they hit; at the threshold (12% of its life, at least 30) the element
// procs, then rests for 6 s. A target never procs its own element; skills, ticks and the killing
// blow do not fill meters.
//   Blaze   burns it (3 stacks of threshold × 0.12 per second)
//   Frost   slows it (45% for 5 s; a colossus slows everything it does)
//   Shock   paralyses it (a 2 s stun; a colossus 2.5 s of stagger)
//   Terra   drops three healing orbs for the heroes (12% of life each)
//   Radiant marks it: a charge or an attack detonates the mark (threshold × 0.25) and interrupts it
//   Umbral  grows a cyst (a colossus) or a delayed blast (a small monster) worth the threshold
#pragma once

#include "Rules/RttStatus.h"
#include "Rules/RttElements.h"

namespace Rtt
{
	constexpr int ProcBurnStacks = 3;
	constexpr double ProcBurnShare = 0.12;
	constexpr double ProcSlow = 0.45;
	constexpr double ProcSlowSeconds = 5.0;
	constexpr double ProcStunSeconds = 2.0;
	constexpr double ProcParalyseSeconds = 2.5;
	constexpr int ProcHealOrbs = 3;
	constexpr double ProcHealOrbShare = 0.12;
	constexpr double ProcMarkShare = 0.25;

	struct Proc
	{
		DElement Element = DElement::None;
		// the meter it took; what the proc's damage scales from
		double Threshold = 0.0;
	};

	// One target's element meters.
	class ProcMeter
	{
	public:
		// After a weapon blow of `type` took `amount` off a target of element `own` with `maxHp`
		// (call only for weapon blows that landed and did not kill, never for ticks): fills the
		// meter; returns the element that procs now, if any.
		Proc Fill(Element type, double amount, double maxHp, DElement own, double now);
		double Meter(DElement e) const { return Meters[static_cast<int>(e)]; }
		bool Resting(DElement e, double now) const { return now < ReadyAt[static_cast<int>(e)]; }
		void Reset();

	private:
		double Meters[DElementCount] = {};
		double ReadyAt[DElementCount] = {};
	};

	// A proc on a small monster: Blaze, Frost and Shock land on its statuses (true). Terra,
	// Radiant and Umbral make things in the world (orbs, a mark, a blast): the game's (false).
	bool ApplyProc(const Proc& proc, Team source, StatusEffects& status, double now);
}
