// Tests of the plain C++ rules (Source/RungThiTham/Rules), built and run without the engine:
//   Tools/RulesCheck/check.cmd
// The numbers are the Godot port's tests, which follow the Unity game's own (Cầu Lửa 56 on a fire
// weakness lands for 73, the XP table, every class's level-1 sheet, the wiki's armour sets).
#include "Rules/RttAbility.h"
#include "Rules/RttArmour.h"
#include "Rules/RttDamage.h"
#include "Rules/RttProcs.h"
#include "Rules/RttSheet.h"
#include "Rules/RttStatus.h"
#include "Rules/RttWeapons.h"

#include <cmath>
#include <cstdio>
#include <string>

namespace
{
	int Checks = 0;
	int Failures = 0;
	const char* Current = "";

	void Expect(bool ok, const std::string& what)
	{
		++Checks;
		if (!ok)
		{
			++Failures;
			std::printf("  FAIL %s: %s\n", Current, what.c_str());
		}
	}

	bool Near(double a, double b) { return std::fabs(a - b) < 1e-6; }

	std::string F(double v) { char b[64]; std::snprintf(b, sizeof(b), "%.3f", v); return b; }

	struct Data
	{
		Rtt::Tuning T;
		Rtt::ArmourTable Armour;
		Rtt::JsonValue Classes;
		Rtt::JsonValue Races;
		Rtt::JsonValue Abilities;
		Rtt::WeaponTable Weapons;
	};

	// ------------------------------------------------------------------ the damage formula
	void DamageFormula(const Data& d)
	{
		Current = "rounding and damage formula";
		Expect(Rtt::RoundHalfEven(52.5) == 52.0 && Rtt::RoundHalfEven(53.5) == 54.0 && Rtt::RoundHalfEven(2.4) == 2.0, "halves round to even");
		Rtt::Defender target;
		target.Side = Rtt::Team::Enemy;
		target.Resistances[static_cast<int>(Rtt::Element::Fire)] = -0.3;
		Rtt::Hit fireball;
		fireball.Amount = 56.0;
		fireball.Type = Rtt::Element::Fire;
		fireball.SourceTeam = Rtt::Team::Player;
		fireball.AttackScaled = true;
		Expect(Rtt::ResolveDamage(fireball, target, d.T, d.Armour, 1.0).Amount == 73.0, "Cầu Lửa 56 on a -30% fire weakness lands for 73");
		target.Armor = 10.0;
		Rtt::Hit atFive = fireball;
		atFive.SourceLevel = 5;
		Expect(Rtt::ResolveDamage(atFive, target, d.T, d.Armour, 1.0).Amount == 64.0, "with armour 10 against a level-5 attacker: 64");
		Rtt::Hit tiny = fireball;
		tiny.Amount = 0.2;
		Expect(Rtt::ResolveDamage(tiny, target, d.T, d.Armour, 1.0).Amount == 1.0, "every hit does at least 1");
		target.Invulnerable = true;
		const Rtt::DamageOutcome dodged = Rtt::ResolveDamage(fireball, target, d.T, d.Armour, 1.0);
		Expect(dodged.Amount == 0.0 && dodged.Evaded, "invulnerable: no damage, evaded");
		target.Invulnerable = false;
		Rtt::Hit same = fireball;
		same.SourceTeam = Rtt::Team::Enemy;
		Expect(Rtt::ResolveDamage(same, target, d.T, d.Armour, 1.0).Amount == 0.0, "a team does not hurt itself");
		Rtt::Defender neutral;
		neutral.Side = Rtt::Team::Neutral;
		Rtt::Hit enemyHit = fireball;
		enemyHit.SourceTeam = Rtt::Team::Enemy;
		Expect(Rtt::ResolveDamage(enemyHit, neutral, d.T, d.Armour, 1.0).Amount == 0.0 && Rtt::ResolveDamage(fireball, neutral, d.T, d.Armour, 1.0).Amount > 0.0,
			"neutral things only take hits from heroes");
		Rtt::Hit fall;
		fall.Amount = 41.6;
		fall.Pure = true;
		fall.SourceTeam = Rtt::Team::Enemy;
		Rtt::Defender hero;
		hero.Side = Rtt::Team::Player;
		hero.Armor = 50.0;
		hero.Resistances[0] = 0.5;
		Expect(Rtt::ResolveDamage(fall, hero, d.T, d.Armour, 1.2).Amount == 42.0, "a pure hit skips armour, resistance and the spread");
		Expect(Near(Rtt::Spread(0.0, d.T), 0.95) && Near(Rtt::Spread(1.0, d.T), 1.05) && Near(Rtt::Spread(0.5, d.T), 1.0), "the spread is ±5%");
	}

	// ------------------------------------------------------------------ levels and XP
	void Progression(const Data& d)
	{
		Current = "levels and XP";
		const int levels[] = {1, 5, 10, 20, 30, 39, 40};
		const int want[] = {50, 657, 1991, 6034, 11544, 17566, 0};
		for (int i = 0; i < 7; ++i)
		{
			const int got = Rtt::Sheet::XpToNext(levels[i], d.T);
			Expect(got == want[i], "XP to next at level " + std::to_string(levels[i]) + " = " + std::to_string(want[i]) + " (got " + std::to_string(got) + ")");
		}
		Expect(Rtt::Sheet::TotalXp(40, d.T) == 272332, "XP to reach 40 = 272332 (got " + std::to_string(Rtt::Sheet::TotalXp(40, d.T)) + ")");
		Expect(Rtt::Sheet::KillXp(2, Rtt::Rank::Normal, 1, d.T) == 16, "Slime Rêu gives 16 XP");
		Expect(Rtt::Sheet::KillXp(3, Rtt::Rank::Normal, 1, d.T) == 20, "Nấm Độc gives 20 XP");
		Expect(Rtt::Sheet::KillXp(2, Rtt::Rank::Normal, 7, d.T) == 8, "five levels above: half");
		Expect(Rtt::Sheet::KillXp(2, Rtt::Rank::Normal, 10, d.T) == 2, "eight levels above: a tenth");
		Expect(Rtt::Sheet::KillXp(6, Rtt::Rank::Boss, 6, d.T) == 1280, "Gấu Ma gives 1280 XP");
	}

	// ------------------------------------------------------------------ class sheets at level 1
	Rtt::Sheet MakeSheet(const Data& d, const std::string& classId, const std::string& raceId = "")
	{
		Rtt::Sheet s(d.T);
		s.Class = Rtt::ReadClass(d.Classes[classId]);
		s.Race = Rtt::ReadRace(d.Races[raceId]);
		s.WeaponId = s.Class.Weapons.empty() ? "sword" : s.Class.Weapons[0];
		s.Recalculate();
		return s;
	}

	void ClassSheets(const Data& d)
	{
		Current = "class stats at level 1";
		struct Row { const char* id; double hp, energy, physical, magic, armor; };
		// (HP, energy, physical, magic, armour) at level 1, no race, the default weapon: the Unity table
		const Row rows[] = {
			{"barbarian", 108, 54, 26.0, 26.0, 5.0}, {"bard", 94, 54, 20.5, 22.0, 3.5}, {"cleric", 104, 54, 21.0, 24.0, 7.0},
			{"fighter", 106, 54, 23.0, 23.0, 10.0}, {"monk", 94, 52, 20.0, 18.5, 4.5}, {"paladin", 96, 52, 23.0, 21.5, 9.5},
			{"ranger", 96, 52, 22.0, 20.5, 6.5}, {"rogue", 104, 54, 19.0, 19.0, 4.0}, {"wizard", 102, 54, 23.0, 23.0, 2.0},
		};
		for (const Row& r : rows)
		{
			const Rtt::Sheet s = MakeSheet(d, r.id);
			const std::string id = r.id;
			Expect(s.Class.Valid, id + " is in classes.json");
			Expect(s.HealthMax() == r.hp, id + " HP " + F(r.hp) + " (got " + F(s.HealthMax()) + ")");
			Expect(Rtt::RoundHalfEven(s.MaxEnergy) == r.energy, id + " energy " + F(r.energy) + " (got " + F(s.MaxEnergy) + ")");
			Expect(Near(s.PhysicalAttack, r.physical) && Near(s.MagicAttack, r.magic),
				id + " attack " + F(r.physical) + "/" + F(r.magic) + " (got " + F(s.PhysicalAttack) + "/" + F(s.MagicAttack) + ")");
			Expect(Near(s.Armor, r.armor), id + " armour " + F(r.armor) + " (got " + F(s.Armor) + ")");
		}
		Rtt::Sheet human = MakeSheet(d, "fighter", "human");
		Expect(human.Scores[Rtt::Str] == 16.0 && human.Scores[Rtt::Con] == 15.0, "a human gets +1 everywhere");
		human.AddXp(60);
		Expect(human.Level == 2 && human.Xp == 10 && human.StatPoints == 3, "60 XP: level 2 with 10 over and 3 points");
		Expect(human.SpendPoint(Rtt::Str) && human.Scores[Rtt::Str] == 17.0 && human.StatPoints == 2, "a point spent raises the score");
		human.RefundPoints();
		Expect(human.Scores[Rtt::Str] == 16.0 && human.StatPoints == 3, "and comes back on a refund");
		const Rtt::Sheet fighter = MakeSheet(d, "fighter");
		const double reference = d.T.WeaponAttack + d.T.AttackPerPoint * (d.T.ReferenceScore - d.T.AttackFromScore);
		Expect(Near(fighter.DamageScale(Rtt::Element::Physical), fighter.PhysicalAttack / reference), "flat hits scale by Attack over the reference Attack");
	}

	// ------------------------------------------------------------------ elements
	void ElementMatchups(const Data&)
	{
		Current = "elements: strong, weak and neutral";
		using Rtt::DElement;
		using Rtt::Element;
		Expect(Rtt::Matchup(Element::Fire, DElement::Frost) == 1.5, "Blaze hits Frost +50%");
		Expect(Rtt::Matchup(Element::Fire, DElement::Blaze) == 0.8, "Blaze hits Blaze −20%");
		Expect(Rtt::Matchup(Element::Fire, DElement::Terra) == 1.0, "Blaze hits Terra as it is");
		Expect(Rtt::Matchup(Element::Lightning, DElement::Terra) == 1.5 && Rtt::Matchup(Element::Terra, DElement::Shock) == 1.5, "Shock and Terra oppose");
		Expect(Rtt::Matchup(Element::Holy, DElement::Umbral) == 1.5 && Rtt::Matchup(Element::Dark, DElement::Radiant) == 1.5, "Radiant and Umbral oppose");
		Expect(Rtt::Matchup(Element::Physical, DElement::Frost) == 1.0 && Rtt::Matchup(Element::Poison, DElement::Terra) == 1.0, "physical and poison have no element");
		Expect(Rtt::Matchup(Element::Fire, DElement::None) == 1.0 && Rtt::Matchup(Element::Fire, DElement::Neutral) == 1.0, "a target with no element takes it as it is");
		Expect(Rtt::OfType(Element::Holy) == DElement::Radiant && Rtt::OfType(Element::Poison) == DElement::None, "combat elements map onto Dauntless's");
		Expect(Rtt::ElementFromId("umbral") == DElement::Umbral && Rtt::ElementFromId("") == DElement::None, "element ids read back");
		Expect(Rtt::ProcThreshold(100.0) == 30.0 && Near(Rtt::ProcThreshold(3200.0), 384.0), "a proc needs 12% of life, at least 30");
		Rtt::Defender slime;
		slime.Element = DElement::Terra;
		Rtt::Hit shock;
		shock.Amount = 100.0;
		shock.Type = Element::Lightning;
		shock.SourceTeam = Rtt::Team::Player;
		shock.AttackScaled = true;
		Expect(Rtt::ResolveDamage(shock, slime, Rtt::Tuning(), Rtt::ArmourTable(), 1.0).Amount == 150.0, "a Shock hit on a Terra slime: 150");
	}

	// ------------------------------------------------------------------ armour
	void ArmourSets(const Data& d)
	{
		Current = "armour: the wiki's sets";
		const Rtt::ArmourTable& a = d.Armour;
		Expect(a.Sets.size() == 35, "35 sets as on the wiki (" + std::to_string(a.Sets.size()) + ")");
		Expect(a.Sets.size() >= 2 && a.Sets[0].Id == "krolachi" && a.Sets[1].Id == "recruit", "the neutral starter sets come first");
		bool tiers = true;
		int last = 0;
		int perElement[Rtt::DElementCount] = {};
		for (const Rtt::ArmourSet& s : a.Sets)
		{
			const bool known = s.Element != Rtt::DElement::None;
			const bool tier = s.Resist == 3 || s.Resist == 5 || s.Resist == 10 || s.Resist == 15 || s.Resist == 20 || s.Resist == 25;
			if (!known || !tier || s.Resist < last)
			{
				tiers = false;
			}
			last = s.Resist;
			++perElement[static_cast<int>(s.Element)];
		}
		Expect(tiers, "every set: a known element, a wiki tier, in tier order");
		for (int e = static_cast<int>(Rtt::DElement::Blaze); e < Rtt::DElementCount; ++e)
		{
			Expect(perElement[e] >= 4, std::string("several sets of ") + Rtt::Info(static_cast<Rtt::DElement>(e)).Id);
		}
		Expect(!a.HasPiece("recruit", Rtt::Slot::Head), "Recruit has no head piece");
		const Rtt::ArmourPiece aegis = a.Piece("charrogg", Rtt::Slot::Torso);
		Expect(aegis.Valid && aegis.Name == "Volcanic Aegis" && aegis.Element == Rtt::DElement::Blaze && aegis.Resist == 10, "Volcanic Aegis: Blaze 10");
		const Rtt::ArmourPiece apex = a.Piece("torgadoro", Rtt::Slot::Head);
		Expect(apex.Name == "Torgadoro's Apex" && apex.Resist == 25, "Torgadoro's Apex: 25");
		const Rtt::ArmourPiece guile = a.Piece("thrax", Rtt::Slot::Legs);
		Expect(guile.Name == "Thrax's Guile" && guile.Element == Rtt::DElement::Umbral, "Thrax's Guile: Umbral");
		bool carried[Rtt::DElementCount] = {};
		int kinds = 0;
		for (const std::string& id : a.Wardrobe)
		{
			const Rtt::ArmourSet* s = a.Find(id);
			Expect(s != nullptr, "the wardrobe's " + id + " is a set");
			if (s && !carried[static_cast<int>(s->Element)])
			{
				carried[static_cast<int>(s->Element)] = true;
				++kinds;
			}
		}
		Expect(kinds == 7, "the wardrobe has the starter set and one set per element");
		Expect(a.Find(a.Start) != nullptr, "the hero starts in a set that exists");
	}

	void ArmourMaths(const Data& d)
	{
		Current = "armour: resist and weak maths";
		using Rtt::DElement;
		const Rtt::ArmourTable& a = d.Armour;
		const Rtt::Worn charrogg = a.FullSet("charrogg");
		const Rtt::ResistPoints pts = a.Points(charrogg);
		Expect(pts[static_cast<int>(DElement::Blaze)] == 40 && pts[static_cast<int>(DElement::Frost)] == -40, "four Blaze 10 pieces: +40 Blaze, −40 Frost");
		Expect(Near(a.Multiplier(pts, DElement::Blaze), 0.8), "Blaze hits −20%");
		Expect(Near(a.Multiplier(pts, DElement::Frost), 1.2), "Frost hits +20% (the opposing element)");
		Expect(Near(a.Multiplier(pts, DElement::Shock), 1.0) && Near(a.Multiplier(pts, DElement::None), 1.0), "other and plain hits unchanged");
		const Rtt::ResistPoints none = a.Points(a.FullSet("krolachi"));
		bool allZero = true;
		for (int v : none)
		{
			allZero = allZero && v == 0;
		}
		Expect(allZero, "neutral armour resists nothing");
		for (int e = static_cast<int>(DElement::Blaze); e < Rtt::DElementCount; ++e)
		{
			const DElement el = static_cast<DElement>(e);
			const Rtt::ArmourSet* set = nullptr;
			for (const Rtt::ArmourSet& s : a.Sets)
			{
				if (s.Element == el)
				{
					set = &s;
					break;
				}
			}
			if (!set)
			{
				Expect(false, std::string("a set of ") + Rtt::Info(el).Id);
				continue;
			}
			const double r = set->Resist * 4.0;
			const Rtt::ResistPoints p = a.Points(a.FullSet(set->Id));
			Expect(Near(a.Multiplier(p, el), 1.0 - r * 0.005) && Near(a.Multiplier(p, Rtt::Info(el).Opposite), 1.0 + r * 0.005),
				set->Id + " armour resists its element and is weak to the opposing one");
		}
		Rtt::Worn mixed{"charrogg", "pangar", "", ""};
		const Rtt::ResistPoints cancel = a.Points(mixed);
		Expect(cancel[static_cast<int>(DElement::Blaze)] == 0 && cancel[static_cast<int>(DElement::Frost)] == 0, "a Blaze and a Frost piece cancel out");
		Rtt::Worn terra{"gnasher", "gnasher", "shrike", ""};
		const Rtt::ResistPoints tp = a.Points(terra);
		Expect(tp[static_cast<int>(DElement::Terra)] == 10 && tp[static_cast<int>(DElement::Shock)] == -10, "two Terra pieces and a Shock one: Terra +10, Shock −10");
		const Rtt::ResistPoints top = a.Points(a.FullSet("torgadoro"));
		Expect(Near(a.Multiplier(top, DElement::Blaze), 0.5) && Near(a.Multiplier(top, DElement::Frost), 1.5), "a full tier-25 set: half from its element, half again from the opposing one");
		Rtt::ResistPoints huge{};
		huge[static_cast<int>(DElement::Blaze)] = 300;
		Rtt::ResistPoints neg{};
		neg[static_cast<int>(DElement::Blaze)] = -300;
		Expect(Near(a.Multiplier(huge, DElement::Blaze), 0.5) && Near(a.Multiplier(neg, DElement::Blaze), 1.5), "never past ±50%");
		Expect(a.WholeSet(charrogg) == "charrogg" && a.WholeSet(mixed).empty(), "a whole set is named, mixed pieces are not");
		Expect(a.MainElement(terra) == DElement::Terra, "mixed pieces lean to the most worn element");
	}

	void ArmourHits(const Data& d)
	{
		Current = "armour: hits on a hero in armour";
		using Rtt::DElement;
		Rtt::Defender hero;
		hero.Side = Rtt::Team::Player;
		hero.HasArmour = true;
		hero.ArmourPoints = d.Armour.Points(d.Armour.FullSet("charrogg"));
		Rtt::Hit fire;
		fire.Amount = 100.0;
		fire.Type = Rtt::Element::Fire;
		fire.SourceTeam = Rtt::Team::Enemy;
		fire.SourceLevel = 1;
		Expect(Rtt::ResolveDamage(fire, hero, d.T, d.Armour, 1.0).Amount == 80.0, "a Blaze hit on a hero in Charrogg: 80");
		Rtt::Hit ice = fire;
		ice.Type = Rtt::Element::Ice;
		Expect(Rtt::ResolveDamage(ice, hero, d.T, d.Armour, 1.0).Amount == 120.0, "a Frost hit on them: 120");
		Rtt::Hit lunge;
		lunge.Amount = 40.0;
		lunge.SourceTeam = Rtt::Team::Enemy;
		lunge.SourceLevel = 1;
		lunge.AttackerElement = DElement::Terra;
		Rtt::Defender plain;
		plain.Side = Rtt::Team::Player;
		plain.HasArmour = true;
		plain.ArmourPoints = d.Armour.Points(d.Armour.FullSet("krolachi"));
		Rtt::Defender gnasher = plain;
		gnasher.ArmourPoints = d.Armour.Points(d.Armour.FullSet("gnasher"));
		Rtt::Defender shrike = plain;
		shrike.ArmourPoints = d.Armour.Points(d.Armour.FullSet("shrike"));
		const double p = Rtt::ResolveDamage(lunge, plain, d.T, d.Armour, 1.0).Amount;
		const double r = Rtt::ResolveDamage(lunge, gnasher, d.T, d.Armour, 1.0).Amount;
		const double w = Rtt::ResolveDamage(lunge, shrike, d.T, d.Armour, 1.0).Amount;
		Expect(p == 40.0 && r == 32.0 && w == 48.0, "a Terra slime's plain lunge counts as Terra: Krolachi " + F(p) + ", Gnasher " + F(r) + ", Shrike " + F(w));
		Rtt::Hit fall = fire;
		fall.Pure = true;
		Expect(Rtt::ResolveDamage(fall, hero, d.T, d.Armour, 1.0).Amount == 100.0, "a pure hit skips armour");
		Expect(Rtt::HitElement(Rtt::Element::Physical, DElement::Neutral) == DElement::None, "a neutral attacker's plain blow has no element");
	}

	// ------------------------------------------------------------------ JSON
	void Json(const Data& d)
	{
		Current = "data files";
		std::string err;
		const Rtt::JsonValue v = Rtt::ParseJson("{\"a\": [1, 2.5, -3e2], \"b\": \"T\\u00e1\\u00ba\", \"c\": true, \"d\": null, \"e\": {}}", &err);
		Expect(err.empty() && v["a"].Size() == 3 && v["a"][2].Num() == -300.0 && v["c"].Bool() && v["d"].IsNull() && v["e"].IsObject(), "JSON values parse (" + err + ")");
		Expect(v["b"].Str() == "T\xc3\xa1\xc2\xba", "\\u escapes become UTF-8");
		Rtt::ParseJson("{\"a\": [1, }", &err);
		Expect(!err.empty(), "a broken file reports where (" + err + ")");
		Expect(d.Classes.IsObject() && d.Classes["fighter"].Str("displayName").size() > 0, "classes.json loads, Vietnamese names and all");
		Expect(d.Races.IsObject() && d.Races["human"].IsObject(), "races.json loads");
		Expect(d.T.MaxLevel == 40 && Near(d.T.XpExponent, 1.6) && d.T.RankMultiplier[3] == 40.0, "progression.json fills the tuning");
		Expect(d.T.BurnMaxStacks == 3 && Near(d.T.ChillSlowPerStack, 0.12) && Near(d.T.PerfectCounterPoise, 25.0), "combat.json fills the status tuning");
	}

	// ------------------------------------------------------------------ statuses and poise
	// Deals burn and poison ticks to a plain target, as the game does (Dot, already scaled).
	double DealTicks(const std::vector<Rtt::DotTick>& ticks, const Data& d)
	{
		double total = 0.0;
		Rtt::Defender target;
		target.Side = Rtt::Team::Enemy;
		for (const Rtt::DotTick& t : ticks)
		{
			Rtt::Hit h;
			h.Amount = t.Amount;
			h.Type = t.Type;
			h.SourceTeam = t.Source;
			h.AttackScaled = true;
			h.Dot = true;
			total += Rtt::ResolveDamage(h, target, d.T, d.Armour, 1.0).Amount;
		}
		return total;
	}

	void Statuses(const Data& d)
	{
		Current = "burn, chill, freeze, poise, crowd control";
		using Rtt::Team;
		Rtt::StatusEffects st(d.T);
		st.Burn(1, 30.0, Team::Player, 0.0);
		const double burnt = DealTicks(st.Tick(3.2, 1000.0, false), d);
		Expect(burnt == 90.0, "one Bỏng stack of 30/s burns 90 over 3 s (" + F(burnt) + ")");
		Expect(!st.IsBurning(3.2), "and goes out");
		st.Chill(2, 4.0);
		Expect(st.LiveChill(4.0) == 2 && Near(st.SpeedMultiplier(4.0), 0.76), "two Lạnh stacks: 24% slower");
		st.Chill(2, 4.0);
		Expect(st.IsFrozen(4.0) && st.SpeedMultiplier(4.0) == 0.0 && st.IsStunned(4.0), "the fourth stack freezes");
		Expect(st.IsFrozen(5.4) && !st.IsFrozen(5.6), "for 1.5 s");

		Rtt::StatusEffects cc(d.T);
		cc.Stun(2.0, 10.0);
		const double first = cc.StunRemaining(10.0);
		cc.ClearStun();
		cc.Stun(2.0, 10.0);
		const double second = cc.StunRemaining(10.0);
		cc.ClearStun();
		cc.Stun(2.0, 10.0);
		const double third = cc.StunRemaining(10.0);
		Expect(Near(first, 2.0) && Near(second, 1.2) && Near(third, 0.72), "a second stun within 6 s lasts 60%, a third 36% (" + F(first) + ", " + F(second) + ", " + F(third) + ")");
		cc.ClearStun();
		cc.Stun(2.0, 17.0);
		Expect(Near(cc.StunRemaining(17.0), 2.0), "after 6 s quiet it is whole again");
		cc.StunImmune = true;
		cc.ClearStun();
		cc.Stun(2.0, 30.0);
		cc.Root(2.0, 30.0);
		Expect(!cc.IsStunned(30.0) && !cc.IsRooted(30.0), "a hero immune to crowd control shrugs off Choáng and Trói");

		Rtt::StatusEffects po(d.T);
		po.AddPoison(2, Team::Player, 0.0);
		const double poisoned = DealTicks(po.Tick(6.2, 1000.0, false), d);
		Expect(poisoned == 180.0, "two Độc stacks take 1.5% of 1000 each per second for 6 s: 180 (" + F(poisoned) + ")");
		po.AddPoison(9, Team::Enemy, 10.0);
		Expect(po.LivePoison(10.0) == 5, "at most five Độc stacks");

		Rtt::StatusEffects ch(d.T);
		const Rtt::Discharge a = ch.Charge(1, 50.0, Team::Player, 0.0);
		const Rtt::Discharge b = ch.Charge(1, 50.0, Team::Player, 1.0);
		const Rtt::Discharge c = ch.Charge(1, 50.0, Team::Player, 2.0);
		Expect(!a.Happened && !b.Happened && c.Happened && Near(c.Amount, 40.0) && c.Targets == 3 && Near(c.Radius, 4.0),
			"the third Tích Điện discharges 80% of the hit onto three others within 4 m");
		Expect(ch.LiveCharge(2.0) == 0, "and the stacks are spent");

		Rtt::StatusEffects cu(d.T);
		cu.Curse(5.0, 0.0);
		Expect(Near(cu.DamageTakenMultiplier(1.0), 1.15) && Near(cu.DamageDealtMultiplier(1.0), 0.8), "Nguyền: takes 15% more, deals 20% less");
		cu.Judge(5.0, 0.0);
		Expect(Near(cu.DamageTakenMultiplier(1.0), 1.35), "with Phán Xét on top: 35% more");
		cu.Slow(0.35, 1.8, 0.0);
		Expect(Near(cu.SpeedMultiplier(1.0), 0.65) && Near(cu.SpeedMultiplier(2.0), 1.0), "a spore's Làm Chậm: 35% for 1.8 s");

		Rtt::StatusEffects fb(d.T);
		Rtt::StatusSpec burn;
		burn.Burn = 1;
		fb.Apply(burn, 56.0, Team::Player, 0.0);
		const double fbBurnt = DealTicks(fb.Tick(3.0, 1000.0, false), d);
		Expect(fbBurnt == 48.0, "Cầu Lửa's 56 leaves a burn of 30%/s: ticks of 8.4 land as 8, 48 in all (" + F(fbBurnt) + ")");
		fb.Burn(1, 10.0, Team::Player, 4.0);
		const std::vector<Rtt::DotTick> late = fb.Tick(100.0, 1000.0, true);
		Expect(late.empty() && !fb.IsBurning(4.1), "the dead stop burning");

		// a boss: poise breaks into a 3 s stun and +50% damage taken
		Rtt::StatusEffects bst(d.T);
		Rtt::Poise bp(300.0);
		Rtt::Link(bst, bp);
		Expect(!bp.Add(299.0, 0.0), "299 of 300: not yet");
		Expect(bp.Add(5.0, 0.0) && bp.IsBroken(0.0) && bst.IsStunned(0.0) && Near(bp.DamageTakenMultiplier(0.0), 1.5), "300: Vỡ Trấn Áp, stunned, x1.5");
		Expect(Near(bp.Threshold, 375.0), "the next break needs 375");
		bst.Stun(1.0, 0.0);
		Expect(bst.StunRemaining(0.0) > 2.5 && Near(bst.StunRemaining(0.0), 3.0), "a broken boss is immune to more crowd control");
		Expect(!bp.Add(500.0, 1.0), "a broken bar fills no more");
		Expect(!bp.IsBroken(3.1) && Near(bp.DamageTakenMultiplier(3.1), 1.0), "back up after 3 s");
		bp.Add(100.0, 10.0);
		bp.Tick(1.0, 11.0);
		Expect(Near(bp.Current, 100.0), "the bar holds for 2.5 s");
		bp.Tick(1.0, 13.0);
		Expect(Near(bp.Current, 60.0), "then drains 40 a second");

		Rtt::StatusEffects heavy(d.T);
		Rtt::Poise hp(300.0);
		Rtt::Link(heavy, hp);
		heavy.Chill(4, 100.0);
		Expect(heavy.IsFrozen(100.0) && !heavy.IsFrozen(100.7) && Near(hp.Current, 60.0), "a boss freezes for only 0.6 s and takes 60 poise");
		heavy.Stun(1.0, 101.0);
		Expect(!heavy.IsStunned(101.0), "then shrugs off crowd control for 4 s");
		bool sawFrozen = false;
		for (Rtt::StatusWord w : heavy.Words)
		{
			sawFrozen = sawFrozen || w == Rtt::StatusWord::Frozen;
		}
		Expect(sawFrozen, "and says Đóng Băng!");
	}

	// ------------------------------------------------------------------ element procs
	void Procs(const Data& d)
	{
		Current = "elements: the procs on a small monster";
		using Rtt::DElement;
		using Rtt::Element;
		Rtt::ProcMeter m;
		Rtt::StatusEffects st(d.T);
		const auto hit = [&](Element type, double amount, double now)
		{
			const Rtt::Proc p = m.Fill(type, amount, 1000.0, DElement::None, now);
			Rtt::ApplyProc(p, Rtt::Team::Player, st, now);
			return p;
		};
		Expect(hit(Element::Fire, 100.0, 0.0).Element == DElement::None && !st.IsBurning(0.0), "below the threshold (12% of 1000) nothing happens");
		const Rtt::Proc blaze = hit(Element::Fire, 30.0, 0.0);
		Expect(blaze.Element == DElement::Blaze && Near(blaze.Threshold, 120.0) && st.IsBurning(0.0), "Blaze at the threshold: it burns");
		const double burnt = DealTicks(st.Tick(3.0, 1000.0, false), d);
		Expect(burnt == 132.0, "three stacks of 12% of the threshold a second: ticks of 21.6 land as 22, 132 in 3 s (" + F(burnt) + ")");
		hit(Element::Ice, 130.0, 0.0);
		Expect(Near(st.SpeedMultiplier(0.0), 0.55), "Frost: slowed 45% (" + F(st.SpeedMultiplier(0.0)) + ")");
		hit(Element::Lightning, 130.0, 0.0);
		Expect(st.IsStunned(0.0) && Near(st.StunRemaining(0.0), 2.0), "Shock: paralysed 2 s");
		const Rtt::Proc terra = hit(Element::Terra, 130.0, 0.0);
		Expect(terra.Element == DElement::Terra && !Rtt::ApplyProc(terra, Rtt::Team::Player, st, 0.0), "Terra procs, and its healing orbs are the world's");
		Expect(hit(Element::Holy, 130.0, 0.0).Element == DElement::Radiant, "Radiant procs (a mark)");
		Expect(hit(Element::Dark, 130.0, 0.0).Element == DElement::Umbral, "Umbral procs (a blast)");
		Expect(hit(Element::Fire, 130.0, 1.0).Element == DElement::None && m.Resting(DElement::Blaze, 1.0), "an element cannot proc again for 6 s");
		Expect(hit(Element::Fire, 130.0, 6.5).Element == DElement::Blaze, "then it can");
		Expect(hit(Element::Physical, 500.0, 7.0).Element == DElement::None && hit(Element::Poison, 500.0, 7.0).Element == DElement::None, "plain and poison blows have no element to proc");
		Rtt::ProcMeter own;
		Expect(own.Fill(Element::Fire, 500.0, 1000.0, DElement::Blaze, 0.0).Element == DElement::None, "an element never procs on its own kind");
		Rtt::ProcMeter small;
		Expect(small.Fill(Element::Fire, 30.0, 100.0, DElement::None, 0.0).Element == DElement::Blaze, "small monsters need at least 30");
	}

	// ------------------------------------------------------------------ weapons
	void Weapons(const Data& d)
	{
		Current = "weapons: the seven families";
		const Rtt::WeaponTable& w = d.Weapons;
		Expect(w.Families.size() == 7 && w.Armory.size() == 7 && w.Start.size() == 7, "seven families, seven weapons in hand");
		struct Row { const char* item; const char* family; const char* basic; bool dual; };
		const Row rows[] = {
			{"w_kiem_hong_anh", "sword", "slash", false}, {"w_riu_hoa_nguc", "axe", "cleave", false},
			{"w_bua_bang_phong", "hammer", "smash", false}, {"w_song_liem_loi_dieu", "chain_blades", "stab", true},
			{"w_thuong_moc_lam", "war_pike", "thrust", false}, {"w_gang_thanh_quang", "aether_strikers", "flurry", true},
			{"w_sung_hu_anh", "repeaters", "arrow", true},
		};
		for (const Row& r : rows)
		{
			const std::string item = r.item;
			Expect(w.IsArmory(item) && w.Item(item).Family == r.family, item + " is a " + r.family);
			Expect(std::string(w.KindOf(item).Basic) == r.basic, item + ": its basic attack is " + r.basic);
			Expect(w.FamilyOf(item).Dual == r.dual, item + (r.dual ? ": one in each hand" : ": one in hand"));
			Expect(Rtt::FindAbility(d.Abilities, r.basic).Valid(), std::string("the basic ") + r.basic + " is in abilities.json");
		}
		Expect(w.Item("w_song_liem_loi_dieu").Name == "Song Liêm Lôi Điểu" && w.DamageType("w_song_liem_loi_dieu") == Rtt::Element::Lightning, "Song Liêm Lôi Điểu deals Shock");
		Expect(w.DamageType("w_kiem_hong_anh") == Rtt::Element::Physical && w.DamageType("w_sung_hu_anh") == Rtt::Element::Dark, "a neutral blade is physical, Súng Hư Ảnh umbral");
		Expect(w.Next("w_song_liem_loi_dieu") == "w_thuong_moc_lam" && w.Next("w_sung_hu_anh") == "w_kiem_hong_anh", "V takes the next weapon, round again");
		Expect(w.Item("nothing").Id == "w_kiem_hong_anh", "an unknown weapon is the starting sword");
		Expect(!w.IsArmory("npc_sung_janek") && w.Item("npc_sung_janek").Element == Rtt::DElement::Frost, "Janek Zai's frost repeater is a prop, never the hero's");
		const Rtt::WeaponFamily& sword = w.FamilyOf("w_kiem_hong_anh");
		Expect(sword.Heavy.Name == "Trảm" && Near(sword.Heavy.Charge, 0.9) && sword.Heavy.Shape == Rtt::HeavyShape::Cone, "the sword's heavy move is Trảm, full in 0.9 s");
		const Rtt::HeavyBlow full = Rtt::HeavyBlowAt(sword.Heavy, 23.0, 1.0, 1.0);
		const Rtt::HeavyBlow none = Rtt::HeavyBlowAt(sword.Heavy, 23.0, 1.0, 0.0);
		Expect(Near(full.Amount, 23.0 * 3.2) && Near(full.Poise, 80.0) && Near(full.Knockback, 10.0) && Near(full.HitStop, 0.15), "a full Trảm: 3.2x Attack, 80 poise");
		Expect(Near(none.Amount, 23.0 * 1.4) && Near(none.Poise, 30.0), "a quick one: 1.4x, 30 poise");
		const Rtt::WeaponFamily& chain = w.FamilyOf("w_song_liem_loi_dieu");
		Expect(chain.Heavy.Shape == Rtt::HeavyShape::Spin && chain.Heavy.Hits == 3 && Near(chain.Stagger, 0.8) && chain.Cut, "Lốc Liêm spins three times; chain blades cut and stagger less");
		const Rtt::WeaponFamily& pike = w.FamilyOf("w_thuong_moc_lam");
		Expect(pike.Heavy.Shape == Rtt::HeavyShape::Lunge && Near(pike.Heavy.Lunge, 3.2) && pike.Pierce && !pike.Cut, "Xuyên Phá lunges 3.2 m; the pike pierces");
		Expect(Near(w.FamilyOf("w_bua_bang_phong").Stagger, 1.6) && w.FamilyOf("w_bua_bang_phong").Heavy.Shape == Rtt::HeavyShape::Slam, "the hammer staggers most and slams");
	}

	// ------------------------------------------------------------------ abilities
	void Abilities(const Data& d)
	{
		Current = "abilities: data, hits, combos";
		size_t count = 0;
		bool ultimateCommits = true;
		int ultimates = 0;
		for (size_t i = 0; i < d.Abilities.keys.size(); ++i)
		{
			const Rtt::Ability a = Rtt::FindAbility(d.Abilities, d.Abilities.keys[i]);
			if (a.Valid() && d.Abilities.keys[i][0] != '_')
			{
				++count;
				if (a.HasTag(Rtt::TagUltimate))
				{
					++ultimates;
					ultimateCommits = ultimateCommits && Near(a.CommitTime(), a.LockTime());
				}
			}
		}
		Expect(count == 92, "92 skills (" + std::to_string(count) + ")");
		Expect(ultimates > 0 && ultimateCommits, "an ultimate commits for its whole pose");
		const Rtt::Ability fireball = Rtt::FindAbility(d.Abilities, "fireball");
		Expect(fireball.Valid() && fireball.DisplayName() == "Cầu Lửa", "Cầu Lửa is there");
		Expect(fireball.HasTag(Rtt::TagFire) && fireball.HasTag(Rtt::TagProjectile) && !fireball.HasTag(Rtt::TagMovement), "a fire projectile");
		Expect(Near(fireball.Cooldown(), 3.0) && Near(fireball.CooldownAt(3), 2.76) && Near(fireball.CooldownAt(9), 2.52), "3 s, 4% less a skill level up to level 5");
		Expect(Near(fireball.EnergyCost(), 12.0) && Near(fireball.CommitTime(), 0.15) && Near(fireball.MaxRange(), 15.0), "12 energy, 0.15 s committed, 15 m");
		Expect(Near(fireball.PowerScale(3), 1.24), "+12% power a skill level");
		const Rtt::Ability dash = Rtt::FindAbility(d.Abilities, "dash");
		Expect(dash.HasTag(Rtt::TagMovement) && dash.AnimBase().empty() && Near(dash.CommitTime(), 0.0), "Lướt moves, has no cast pose and commits nothing");
		Expect(!Rtt::FindAbility(d.Abilities, "nope").Valid(), "an unknown skill is empty");

		Rtt::Sheet wizard(d.T);
		wizard.Class = Rtt::ReadClass(d.Classes["wizard"]);
		wizard.WeaponId = wizard.Class.Weapons.empty() ? "staff" : wizard.Class.Weapons[0];
		wizard.Recalculate();
		Rtt::Caster mage;
		mage.Stats = &wizard;
		Rtt::Cast cast;
		cast.Skill = fireball;
		cast.Slot = 1;
		const Rtt::JsonValue& fireHit = fireball.Effects()[1]["hit"];
		Rtt::SkillHit h = Rtt::MakeHit(cast, mage, fireHit);
		Expect(Near(h.Damage.Amount, 1.88 * 23.0) && h.Damage.Type == Rtt::Element::Fire && h.Damage.AttackScaled, "a level-1 wizard's Cầu Lửa: 1.88 x 23 Attack (" + F(h.Damage.Amount) + ")");
		Expect(h.Status.Burn == 1 && Near(h.Poise, 12.0) && !h.FromWeapon && h.SkillName == "Cầu Lửa", "it burns, staggers 12, is no weapon blow");
		cast.Level = 3;
		cast.DamageMultiplier = 1.3;
		Expect(Near(Rtt::MakeHit(cast, mage, fireHit).Damage.Amount, 1.88 * 23.0 * 1.24 * 1.3), "at skill level 3 after a Lướt Hoàn Hảo: x1.24 x1.3");

		Rtt::Sheet fighter(d.T);
		fighter.Class = Rtt::ReadClass(d.Classes["fighter"]);
		fighter.WeaponId = "dagger";
		fighter.Recalculate();
		Rtt::Caster blades;
		blades.Stats = &fighter;
		blades.Weapon = &d.Weapons.FamilyOf("w_song_liem_loi_dieu");
		blades.WeaponType = d.Weapons.DamageType("w_song_liem_loi_dieu");
		Rtt::Cast basic;
		basic.Skill = Rtt::FindAbility(d.Abilities, "slash");
		basic.Slot = 0;
		Rtt::JsonValue swing = Rtt::ParseJson("{\"power\": 0.9, \"type\": 0, \"poise\": 3, \"knockback\": 4, \"critChance\": 0.15}");
		const Rtt::SkillHit blow = Rtt::MakeHit(basic, blades, swing);
		Expect(blow.Damage.Type == Rtt::Element::Lightning && Near(blow.Damage.Amount, 0.9 * fighter.AttackFor(Rtt::Element::Lightning)),
			"a basic swing of Song Liêm Lôi Điểu is a Shock blow");
		Expect(blow.FromWeapon && blow.Cutting && !blow.Piercing && Near(blow.Poise, 3.0 * 0.8), "a weapon blow: it cuts, staggers by the family");
		basic.Slot = 2;
		Expect(!Rtt::MakeHit(basic, blades, swing).FromWeapon, "the same hit from a skill slot is not a weapon blow");
		basic.Slot = 0;
		blades.ImbuePower = 0.5;
		blades.ImbueCharge = 1;
		const Rtt::SkillHit imbued = Rtt::MakeHit(basic, blades, swing);
		Expect(Near(imbued.Damage.Amount, blow.Damage.Amount * 1.5) && imbued.Status.Charge == 1, "Lôi Ấn: the basic attack hits 50% harder and charges");

		Rtt::ComboState combo;
		const int c0 = Rtt::NextCombo(combo, 0.9, 0.0);
		const int c1 = Rtt::NextCombo(combo, 0.9, 0.5);
		const int c2 = Rtt::NextCombo(combo, 0.9, 1.3);
		const int c3 = Rtt::NextCombo(combo, 0.9, 3.0);
		Expect(c0 == 0 && c1 == 1 && c2 == 2 && c3 == 0, "presses within 0.9 s chain the combo, a late one starts again");
		Expect(Near(Rtt::ChainShare(0, 0.15), 1.0) && Near(Rtt::ChainShare(3, 0.15), 0.55) && Near(Rtt::ChainShare(10, 0.15), 0.1), "a chain loses 15% a jump, never below 10%");
		Expect(Near(Rtt::HealAmount(20.0, 0.1, 200.0, 1.5), 60.0), "a heal: (20 + 10% of 200) x healing power 1.5");
	}

	void SkillBars(const Data& d)
	{
		Current = "the skill bar";
		Rtt::Sheet fighter(d.T);
		fighter.Class = Rtt::ReadClass(d.Classes["fighter"]);
		fighter.WeaponId = "sword";
		fighter.Recalculate();
		Rtt::SkillBar bar;
		bar.ApplyKit(d.Abilities, d.Classes["fighter"], "sword");
		Expect(bar.Slots[0].Id == "slash" && bar.Slots[1].Id == "charge" && bar.Slots[6].Id == "bladestorm" && bar.Slots[7].Id == "dash",
			"a fighter with a sword: Chém Gió, the class kit, Lướt");
		Rtt::SkillBar staff;
		staff.ApplyKit(d.Abilities, d.Classes["wizard"], "staff");
		Expect(staff.Slots[0].Id == "missile" && staff.Slots[1].Id == "fireball", "a wizard with a staff: the class's cantrip on the left button");
		std::map<int, std::string> learned{{3, "fireball"}};
		Rtt::SkillBar learnt;
		learnt.ApplyKit(d.Abilities, d.Classes["fighter"], "axe", learned);
		Expect(learnt.Slots[0].Id == "cleave" && learnt.Slots[3].Id == "fireball", "an axe's Bổ basic, and a spell from the Sách Chiêu on slot 3");

		Rtt::SkillBar::Hero hero;
		hero.Stats = &fighter;
		hero.Energy = 100.0;
		using R = Rtt::SkillBar::Result;
		Expect(bar.TryCast(1, hero, 0.0) == R::Cast && Near(hero.Energy, 90.0), "Xung Phong: cast, 10 energy");
		const double chargeCd = 7.0 * fighter.CooldownMultiplier(false, true);
		Expect(Near(bar.CooldownLeft(1, 0.0), chargeCd) && Near(bar.CooldownFraction(1, 0.0), 1.0), "its cooldown runs");
		Expect(bar.TryCast(2, hero, 0.05) == R::Busy, "the global cooldown and the pose hold the next skill");
		Expect(bar.TryCast(2, hero, 0.16) == R::Cast, "free once both have passed");
		Expect(bar.TryCast(1, hero, 1.0) == R::Cooldown, "a skill on cooldown does not cast");
		hero.Energy = 5.0;
		Expect(bar.TryCast(6, hero, 2.0) == R::NoEnergy, "Bão Kiếm wants 20 energy");
		hero.Rooted = true;
		Expect(bar.TryCast(7, hero, 2.0) == R::Rooted, "no Lướt while bound by Trói");
		hero.Rooted = false;
		hero.ActionRemaining = 0.2;
		Expect(bar.TryCast(7, hero, 2.0) == R::Cast, "Lướt cuts a pose short once its commit time has passed");
		hero.ActionRemaining = 0.0;
		hero.Stunned = true;
		Expect(bar.TryCast(0, hero, 3.0) == R::Busy, "nothing while stunned");
		hero.Stunned = false;

		Rtt::SkillBar basic;
		basic.ApplyKit(d.Abilities, d.Classes["fighter"], "sword");
		const double swing = 0.42 * fighter.CooldownMultiplier(true, false);
		Expect(basic.TryCast(0, hero, 0.0) == R::Cast && Near(basic.CooldownLeft(0, 0.0), swing), "Chém Gió: 0.42 s by the hero's attack speed");
		Expect(basic.Request(0, hero, swing - 0.1) == R::Buffered && basic.Buffered() == 0, "pressed 0.1 s early: remembered");
		Expect(basic.Update(hero, swing - 0.05) == -1, "not yet");
		Expect(basic.Update(hero, swing + 0.001) == 0, "fires the moment it is ready");
		Expect(basic.Request(0, hero, swing + 0.1) == R::Cooldown, "pressed well before it is ready: not remembered");
		Rtt::SkillBar cold;
		cold.ApplyKit(d.Abilities, d.Classes["fighter"], "sword");
		hero.ChillMultiplier = 0.76;
		cold.TryCast(0, hero, 0.0);
		Expect(Near(cold.CooldownLeft(0, 0.0), swing / 0.76), "two Lạnh stacks slow the basic attack");
	}

	void Perfect(const Data& d)
	{
		Current = "Lướt Hoàn Hảo";
		Rtt::PerfectDodge p(d.T);
		const Rtt::Ability fireball = Rtt::FindAbility(d.Abilities, "fireball");
		const Rtt::Ability dash = Rtt::FindAbility(d.Abilities, "dash");
		p.StartDash(0.0);
		Expect(!p.Evaded(true, false, 0.05) && !p.Evaded(false, true, 0.05), "a touch or a tick is no perfect dodge");
		Expect(p.Evaded(false, false, 0.1), "a blow within 0.15 s of the dash: perfect");
		Expect(!p.Evaded(false, false, 0.12), "once a dash");
		Expect(p.TakeBonus(dash, 0.5) == 1.0, "a Movement skill does not spend the bonus");
		Expect(Near(p.TakeBonus(fireball, 0.5), 1.3) && p.TakeBonus(fireball, 0.6) == 1.0, "the next skill hits 30% harder, only once");
		Expect(Near(p.CounterPoise("Cầu Lửa", 0.7), 25.0) && p.CounterPoise("Cầu Lửa", 0.8) == 0.0, "its first hit adds 25 to a boss's bar");
		Rtt::PerfectDodge late(d.T);
		late.StartDash(0.0);
		Expect(!late.Evaded(false, false, 0.2), "a blow after the window is an ordinary dodge");
		Rtt::PerfectDodge slow(d.T);
		slow.StartDash(0.0);
		slow.Evaded(false, false, 0.0);
		Expect(slow.TakeBonus(fireball, 1.6) == 1.0, "the bonus lasts 1.5 s");
	}
}

int main(int argc, char** argv)
{
	std::string dir = argc > 1 ? argv[1] : "Unreal/Content/Data";
	if (!dir.empty() && dir.back() != '/' && dir.back() != '\\')
	{
		dir += '/';
	}
	Data d;
	std::string err;
	const Rtt::JsonValue progression = Rtt::LoadJsonFile(dir + "progression.json", &err);
	const Rtt::JsonValue combat = Rtt::LoadJsonFile(dir + "combat.json", &err);
	d.T.Load(progression, combat);
	d.Armour.Load(Rtt::LoadJsonFile(dir + "armour_sets.json", &err));
	d.Classes = Rtt::LoadJsonFile(dir + "classes.json", &err);
	d.Races = Rtt::LoadJsonFile(dir + "races.json", &err);
	d.Abilities = Rtt::LoadJsonFile(dir + "abilities.json", &err);
	d.Weapons.Load(Rtt::LoadJsonFile(dir + "weapons.json", &err));
	if (progression.IsNull() || d.Classes.IsNull() || d.Abilities.IsNull() || d.Weapons.Families.empty())
	{
		std::printf("cannot read the data in %s: %s\n", dir.c_str(), err.c_str());
		return 2;
	}
	Json(d);
	DamageFormula(d);
	Progression(d);
	ClassSheets(d);
	ElementMatchups(d);
	ArmourSets(d);
	ArmourMaths(d);
	ArmourHits(d);
	Statuses(d);
	Procs(d);
	Weapons(d);
	Abilities(d);
	SkillBars(d);
	Perfect(d);
	std::printf("%d checks, %d failed\n", Checks, Failures);
	return Failures == 0 ? 0 : 1;
}
