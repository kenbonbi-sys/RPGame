// What a colossus fight is made of, without the world (the Godot port's anticipation.gd,
// colossus.gd and gau_ma.gd's choice of move): how a blow is told in beats, which move Gấu Ma picks,
// and its breakable parts. Distances are metres, times seconds.
#pragma once

#include <string>
#include <vector>

namespace Rtt
{
	// How a monster tells that a blow is coming (the animation principle of anticipation; enemies
	// get long, readable wind-ups, heroes short snappy moves):
	//   tell    a tiny counter-move first, the other way;
	//   wind    the build-up, big and easing in;
	//   hold    a beat frozen at the peak, trembling, with a glint: now;
	//   strike  fast; then a slow recovery that leaves an opening.
	// Times are seconds since the attack began.
	struct Beats
	{
		double Tell = 0.12;
		double Wind = 0.4;
		double Hold = 0.14;

		double StrikeAt() const { return Tell + Wind + Hold; }
		double HoldAt() const { return Tell + Wind; }
		// −dip through the tell, easing up to 1 by the end of the wind, 1 through the hold.
		double Windup(double t, double dip = 0.25) const;
		// A fast shiver while it holds at the peak (0 outside the hold).
		double Tremble(double t) const;
		// The strike's progress 0 → 1 over `seconds` from StrikeAt(), fast out of the gate.
		double Strike(double t, double seconds) const;
		// True on the one frame the hold begins.
		bool HoldBegins(double t, double dt) const { return t >= HoldAt() && t - dt < HoldAt(); }
	};

	// Gấu Ma's moves (gau_ma.gd).
	enum class BearMove : int { Walk, Swipe, Stomp, Charge, Pounce, Throw };

	struct BearCooldowns
	{
		double Swipe = 2.0;
		double Stomp = 4.0;
		double Throw = 3.0;
		double Pounce = 8.0;
		double Charge = 6.0;

		void Tick(double dt);
	};

	// Gấu Ma's reach scale: its moves were set when it was 1.4 bear units to the metre, it is 0.8.
	constexpr double BearScale = 0.8;
	constexpr double BearReach = BearScale / 1.4;

	// The move it starts on a hero `distance` metres away whose direction is `front` (the dot of its
	// facing and the way to the hero; 1 = straight ahead); resets that move's cooldown (×0.7 in a
	// rage). Moves this body cannot do yet are skipped.
	BearMove ChooseBearMove(double distance, double front, bool rage, BearCooldowns& cooldowns, bool canPounce = true, bool canThrow = true);

	// A part of a colossus's body. Breakable parts (Max > 0) take part damage on top of the shared
	// life and break at 0; every part says how much a hit there staggers and whether a heavy hit
	// there can interrupt.
	struct BossPart
	{
		std::string Id;
		std::string Name;
		double Max = 0.0;
		double Hp = 0.0;
		bool Broken = false;
		double Stagger = 1.0;
		bool Interrupt = false;

		bool Breakable() const { return Max > 0.0; }
	};

	// Part damage per point of core damage: a blade cuts +50%, a pierce +25%, blunt and spells 100%.
	double PartMultiplier(bool cutting, bool piercing);
	// Part damage from a hit that took `amount` off the colossus's life; true when this hit broke it.
	bool DamagePart(BossPart& part, double amount, bool cutting, bool piercing, bool dot);
	// A blow over an area hurts less toward its edge: −40% at the rim.
	double AreaFalloff(double amount, double distance, double radius);

	// Gấu Ma Rừng Già's numbers (gau_ma.gd, colossus.gd).
	struct BearStats
	{
		static constexpr int Level = 6;
		static constexpr double MaxHp = 3200.0;
		static constexpr double MaxStagger = 300.0;
		static constexpr double StaggerSeconds = 6.0;
		static constexpr double StaggerDecayDelay = 4.0;
		static constexpr double StaggerDecayPerSecond = 12.0;
		static constexpr double DenRadius = 38.0;
		static constexpr double AggroRange = 95.0;
		static constexpr double RespawnSeconds = 180.0;
		// the head (its antlers snap) and the four legs (bark shins) break; neck and trunk do not
		static std::vector<BossPart> Parts();
	};

	// A colossus on two legs and two arms (the Paragon behemoths): the same numbers as Gấu Ma, its
	// head (a heavy hit there interrupts), arms and legs breakable, the trunk not.
	struct BeastStats
	{
		enum Part : int { Head, ArmLeft, ArmRight, LegLeft, LegRight, Body };
		static std::vector<BossPart> Parts();
	};
}
