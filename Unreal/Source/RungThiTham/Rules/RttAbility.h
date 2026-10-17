// Skills (the Unity AbilityDef assets in Content/Data/abilities.json), as the Godot port plays
// them: what a skill costs and how long it holds the hero (Ability), what one of its hits deals
// (MakeHit, ability_context.gd's make_damage), the combo counter, the skill bar's cooldowns, global
// cooldown and input buffer (SkillBar, player_skills.gd), and Lướt Hoàn Hảo's bonus.
//
// The effect blocks themselves (Damage, Projectile, Dash, Chain, Beam, ...) need the world and
// are the engine's; the numbers they share with the rules are here (ChainShare, HealAmount).
#pragma once

#include "Rules/RttDamage.h"
#include "Rules/RttSheet.h"
#include "Rules/RttStatus.h"
#include "Rules/RttWeapons.h"

#include <map>
#include <string>

namespace Rtt
{
	enum AbilityTag : int
	{
		TagPhysical = 1 << 0,
		TagFire = 1 << 1,
		TagIce = 1 << 2,
		TagLightning = 1 << 3,
		TagWind = 1 << 4,
		TagDark = 1 << 5,
		TagHoly = 1 << 6,
		TagPoison = 1 << 7,
		TagMelee = 1 << 10,
		TagProjectile = 1 << 11,
		TagArea = 1 << 12,
		TagChannel = 1 << 13,
		TagSummon = 1 << 14,
		TagMovement = 1 << 15,
		TagSupport = 1 << 16,
		TagUltimate = 1 << 17,
	};

	enum class Targeting : int { Direction, Point, Self };

	// Distances in the data are the 2D game's; the 3D world stretches them a little.
	constexpr double AbilitySpace = 1.25;

	// A view of one skill in abilities.json (which must outlive it). Empty when not found.
	class Ability
	{
	public:
		std::string Id;
		const JsonValue* Data = nullptr;

		bool Valid() const { return Data != nullptr; }
		std::string DisplayName() const;
		int Tags() const;
		bool HasTag(int tag) const { return (Tags() & tag) != 0; }
		double Cooldown() const;
		// −4% a skill level (cooldownPerLevel), never below a fifth.
		double CooldownAt(int level) const;
		double EnergyCost() const;
		Targeting GetTargeting() const;
		// metres, in the 3D world
		double MaxRange() const;
		// how long the pose holds the hero
		double LockTime() const;
		// the part of the pose a Movement skill cannot cut short (all of it for an ultimate)
		double CommitTime() const;
		std::string AnimBase() const;
		double MoveWhileCasting() const;
		// +12% a skill level (powerPerLevel)
		double PowerScale(int level) const;
		const JsonValue& Effects() const;
	};

	Ability FindAbility(const JsonValue& abilities, const std::string& id);

	// Who casts and with what: the numbers a hit needs from the hero.
	struct Caster
	{
		const Sheet* Stats = nullptr;
		// the hero's damage-dealt multiplier (its sheet × buffs)
		double DealtMultiplier = 1.0;
		// Lôi Ấn and similar buffs on the basic attack
		double ImbuePower = 0.0;
		int ImbueCharge = 0;
		// the weapon in hand (its basic attack's hits are weapon blows); null for none
		const WeaponFamily* Weapon = nullptr;
		Element WeaponType = Element::Physical;
	};

	// One cast: the skill, its level, the bar slot (0 = the basic attack) and a damage bonus
	// (Lướt Hoàn Hảo's 1.3).
	struct Cast
	{
		Ability Skill;
		int Level = 1;
		int Slot = 0;
		double DamageMultiplier = 1.0;
		// added to the first hit's poise (Lướt Hoàn Hảo's counter)
		double CounterPoise = 0.0;
	};

	// What one hit of a skill deals.
	struct SkillHit
	{
		Hit Damage;
		StatusSpec Status;
		double Poise = 0.0;
		double HitStop = 0.0;
		double Knockback = 0.0;
		double CritChance = 0.0;
		bool FromWeapon = false;
		// on a colossus: cut +50% part damage, pierce in between
		bool Cutting = false;
		bool Piercing = false;
		std::string SkillName;
	};

	// A hit worth `hit.power` × Attack of its element × the skill level × the hero's damage bonuses
	// (ability_context.gd's make_damage). On the basic attack a physical hit takes the weapon's
	// element and its blows are weapon blows (they stagger by the family and fill element meters).
	SkillHit MakeHit(const Cast& cast, const Caster& caster, const JsonValue& hit);

	// A Combo block: presses within `window` seconds go on to the next stage, a later one starts
	// again at 0. Returns the stage count (the stage is count % stages).
	struct ComboState
	{
		int Count = -1;
		double Last = -100.0;
	};
	int NextCombo(ComboState& state, double window, double now);

	// A Chain block's share of the hit on its n-th jump (0 = the first target).
	double ChainShare(int jump, double falloff);
	// A Heal block's instant heal on a target with `maxHp`.
	double HealAmount(double instant, double percentOfMaxHp, double maxHp, double healingPower);

	// Lướt Hoàn Hảo: a hostile blow passing through a dash within PerfectWindow of its start gives
	// +PerfectEnergy, and the next non-Movement skill within PerfectBonusSeconds hits ×1.3, its
	// first hit adding PerfectCounterPoise to a boss's bar.
	class PerfectDodge
	{
	public:
		explicit PerfectDodge(const Tuning& tuning) : T(&tuning) {}
		const Tuning* T;

		// A dash with perfectDodge starts.
		void StartDash(double now);
		// A hostile hit was evaded during the dash (not a contact touch, not a tick): true when it
		// counts as perfect (once per dash); the caller adds PerfectEnergy and slows time.
		bool Evaded(bool contact, bool dot, double now);
		bool BonusReady(double now) const { return now < BonusUntil; }
		// The damage multiplier for `skill` (1 for Movement skills or with no bonus); spends it.
		double TakeBonus(const Ability& skill, double now);
		// The counter's poise, once, for the first hit of the skill cast with the bonus.
		double CounterPoise(const std::string& skillName, double now);

	private:
		double PerfectUntil = 0.0;
		bool Used = true;
		double BonusUntil = 0.0;
		std::string CounterSkill;
		double CounterUntil = 0.0;
	};

	// The hero's skill bar: slot 0 the basic attack (the weapon's, or the class's), 1–5 the class
	// kit (or spells from the Sách Chiêu), 6 the ultimate, 7 Lướt. Cooldowns belong to the skill,
	// not the slot; a 0.08 s global cooldown; a press up to 0.15 s early is remembered and fires
	// the moment the skill is ready. A skill waits for the previous pose, except Movement skills
	// once its commit time has passed.
	class SkillBar
	{
	public:
		static constexpr int SlotCount = 8;
		static constexpr int DashSlot = 7;
		static constexpr double GlobalCooldown = 0.08;
		static constexpr double InputBuffer = 0.15;

		// The hero as the bar sees it this frame.
		struct Hero
		{
			const Sheet* Stats = nullptr;
			double Energy = 0.0;
			bool Dead = false;
			bool Stunned = false;
			bool Rooted = false;
			// on the ground, in the air or rolling (not climbing, not in a heavy move)
			bool CanCast = true;
			// what is left of the current skill pose
			double ActionRemaining = 0.0;
			double ChillMultiplier = 1.0;
			double AttackSpeedBonus = 0.0;
		};

		enum class Result : int { Cast, Buffered, Busy, Cooldown, NoEnergy, Rooted, Empty };

		Ability Slots[SlotCount];
		int Levels[SlotCount] = {1, 1, 1, 1, 1, 1, 1, 1};
		// skill id -> when it is ready again / its full cooldown last time
		std::map<std::string, double> ReadyAt;
		std::map<std::string, double> CooldownOf;

		// Fills the bar from the class's kit, the weapon kind's basic attack and spells learned
		// for slots 1–6 (ApplyKit).
		void ApplyKit(const JsonValue& abilities, const JsonValue& classData, const std::string& weaponKind,
			const std::map<int, std::string>& learned = {});

		double CooldownLeft(int slot, double now) const;
		double CooldownFraction(int slot, double now) const;
		double PoseWait(int slot, const Hero& hero, double now) const;
		double ReadyIn(int slot, const Hero& hero, double now) const;

		// A key press: casts now, or remembers it if it is ready within InputBuffer.
		Result Request(int slot, Hero& hero, double now);
		// Every frame: fires a remembered press once it is ready; the slot cast, or -1.
		int Update(Hero& hero, double now);
		// Casts if it can: spends the energy (from hero.Energy) and starts the cooldowns.
		Result TryCast(int slot, Hero& hero, double now);
		int Buffered() const { return BufferedSlot; }

	private:
		double GcdUntil = 0.0;
		double CommitUntil = 0.0;
		int BufferedSlot = -1;
		double BufferUntil = 0.0;

		void Commit(int slot, const Hero& hero, double now);
	};
}
