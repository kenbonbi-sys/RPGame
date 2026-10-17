// Tests of the plain C++ rules (Source/RungThiTham/Rules), built and run without the engine:
//   Tools/RulesCheck/check.cmd
// The numbers are the Godot port's tests, which follow the Unity game's own (Cầu Lửa 56 on a fire
// weakness lands for 73, the XP table, every class's level-1 sheet, the wiki's armour sets).
#include "Rules/RttArmour.h"
#include "Rules/RttDamage.h"
#include "Rules/RttSheet.h"

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
	if (progression.IsNull() || d.Classes.IsNull())
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
	std::printf("%d checks, %d failed\n", Checks, Failures);
	return Failures == 0 ? 0 : 1;
}
