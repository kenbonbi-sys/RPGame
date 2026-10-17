#include "Rules/RttAbility.h"

namespace Rtt
{
	namespace
	{
		const JsonValue& Field(const Ability& a, const char* key)
		{
			return a.Data ? (*a.Data)[key] : JsonValue::NullValue();
		}

		double Number(const Ability& a, const char* key, double fallback)
		{
			return a.Data ? a.Data->Num(key, fallback) : fallback;
		}

		double Clamp01(double v, double lo, double hi)
		{
			return v < lo ? lo : (v > hi ? hi : v);
		}
	}

	std::string Ability::DisplayName() const
	{
		return Data ? Data->Str("displayName", Id) : Id;
	}

	int Ability::Tags() const
	{
		return Data ? Data->Int("tags", 0) : 0;
	}

	double Ability::Cooldown() const
	{
		return Number(*this, "cooldown", 1.0);
	}

	double Ability::CooldownAt(int level) const
	{
		const int l = level < 1 ? 1 : (level > 5 ? 5 : level);
		const double m = 1.0 - Number(*this, "cooldownPerLevel", 0.04) * (l - 1);
		return Cooldown() * (m > 0.2 ? m : 0.2);
	}

	double Ability::EnergyCost() const
	{
		return Number(*this, "energyCost", 0.0);
	}

	Targeting Ability::GetTargeting() const
	{
		const int t = Data ? Data->Int("targeting", 0) : 0;
		return t == 1 ? Targeting::Point : (t == 2 ? Targeting::Self : Targeting::Direction);
	}

	double Ability::MaxRange() const
	{
		return Number(*this, "maxRange", 12.0) * AbilitySpace;
	}

	double Ability::LockTime() const
	{
		return Number(*this, "lockTime", 0.3);
	}

	double Ability::CommitTime() const
	{
		if (HasTag(TagUltimate))
		{
			return LockTime();
		}
		return Clamp01(Number(*this, "commitTime", 0.0), 0.0, LockTime());
	}

	std::string Ability::AnimBase() const
	{
		const JsonValue& a = Field(*this, "animBase");
		if (!Data || !Data->Has("animBase"))
		{
			return "attack";
		}
		return a.Str();
	}

	double Ability::MoveWhileCasting() const
	{
		return Number(*this, "moveWhileCasting", 0.25);
	}

	double Ability::PowerScale(int level) const
	{
		return 1.0 + Number(*this, "powerPerLevel", 0.12) * ((level > 1 ? level : 1) - 1);
	}

	const JsonValue& Ability::Effects() const
	{
		return Field(*this, "effects");
	}

	Ability FindAbility(const JsonValue& abilities, const std::string& id)
	{
		Ability a;
		a.Id = id;
		if (!id.empty() && abilities.Has(id))
		{
			a.Data = &abilities[id];
		}
		return a;
	}

	SkillHit MakeHit(const Cast& cast, const Caster& caster, const JsonValue& hit)
	{
		SkillHit out;
		double power = hit.Num("power", 1.0);
		out.Status = ReadStatus(hit["status"]);
		if (cast.Slot == 0 && caster.ImbuePower > 0.0)
		{
			power *= 1.0 + caster.ImbuePower;
			out.Status.Charge += caster.ImbueCharge;
		}
		const int typeIndex = hit.Int("type", 0);
		Element type = typeIndex >= 0 && typeIndex < ElementCount ? static_cast<Element>(typeIndex) : Element::Physical;
		const bool weaponHit = cast.Slot == 0 && caster.Weapon != nullptr;
		if (weaponHit && type == Element::Physical)
		{
			type = caster.WeaponType;
		}
		const double attack = caster.Stats ? caster.Stats->AttackFor(type) : 0.0;
		Hit& d = out.Damage;
		d.Amount = power * cast.Skill.PowerScale(cast.Level) * attack * caster.DealtMultiplier * cast.DamageMultiplier;
		d.Type = type;
		d.SourceTeam = Team::Player;
		d.SourceLevel = caster.Stats ? caster.Stats->Level : 1;
		d.AttackScaled = true;
		out.SkillName = cast.Skill.DisplayName();
		out.Poise = hit.Num("poise", 0.0) + cast.CounterPoise;
		out.HitStop = hit.Num("hitStop", 0.0);
		out.Knockback = hit.Num("knockback", 0.0);
		out.CritChance = hit.Num("critChance", 0.0);
		if (weaponHit)
		{
			out.FromWeapon = true;
			out.Poise *= caster.Weapon->Stagger;
			out.Cutting = caster.Weapon->Cut;
			out.Piercing = caster.Weapon->Pierce;
		}
		return out;
	}

	int NextCombo(ComboState& state, double window, double now)
	{
		state.Count = now - state.Last <= window ? state.Count + 1 : 0;
		state.Last = now;
		return state.Count;
	}

	double ChainShare(int jump, double falloff)
	{
		const double share = 1.0 - falloff * jump;
		return share > 0.1 ? share : 0.1;
	}

	double HealAmount(double instant, double percentOfMaxHp, double maxHp, double healingPower)
	{
		return (instant + percentOfMaxHp * maxHp) * healingPower;
	}

	void PerfectDodge::StartDash(double now)
	{
		PerfectUntil = now + T->PerfectWindow;
		Used = false;
	}

	bool PerfectDodge::Evaded(bool contact, bool dot, double now)
	{
		if (Used || contact || dot || now > PerfectUntil)
		{
			return false;
		}
		Used = true;
		BonusUntil = now + T->PerfectBonusSeconds;
		return true;
	}

	double PerfectDodge::TakeBonus(const Ability& skill, double now)
	{
		if (skill.HasTag(TagMovement) || now >= BonusUntil)
		{
			return 1.0;
		}
		BonusUntil = 0.0;
		CounterSkill = skill.DisplayName();
		CounterUntil = now + T->PerfectBonusSeconds;
		return T->PerfectDamageMultiplier;
	}

	double PerfectDodge::CounterPoise(const std::string& skillName, double now)
	{
		if (!CounterSkill.empty() && skillName == CounterSkill && now < CounterUntil)
		{
			CounterSkill.clear();
			return T->PerfectCounterPoise;
		}
		return 0.0;
	}

	void SkillBar::ApplyKit(const JsonValue& abilities, const JsonValue& classData, const std::string& weaponKind,
		const std::map<int, std::string>& learned)
	{
		for (int i = 0; i < SlotCount; ++i)
		{
			Slots[i] = Ability();
		}
		const JsonValue& kit = classData["kit"];
		for (int i = 0; i < 7; ++i)
		{
			const JsonValue& id = kit[static_cast<size_t>(i)];
			if (id.kind == JsonValue::Kind::String && !id.Str().empty())
			{
				Slots[i] = FindAbility(abilities, id.Str());
			}
		}
		const WeaponKind& w = WeaponKindOf(weaponKind);
		if (!w.Focus && w.Basic[0] != '\0')
		{
			Slots[0] = FindAbility(abilities, w.Basic);
		}
		for (const auto& entry : learned)
		{
			const Ability a = FindAbility(abilities, entry.second);
			if (a.Valid() && entry.first >= 1 && entry.first <= 6)
			{
				Slots[entry.first] = a;
			}
		}
		Slots[DashSlot] = FindAbility(abilities, "dash");
		if (!Slots[0].Valid())
		{
			Slots[0] = FindAbility(abilities, "slash");
		}
		GcdUntil = 0.0;
		CommitUntil = 0.0;
		BufferedSlot = -1;
	}

	double SkillBar::CooldownLeft(int slot, double now) const
	{
		const Ability& a = Slots[slot];
		if (!a.Valid())
		{
			return 0.0;
		}
		const auto it = ReadyAt.find(a.Id);
		const double left = it == ReadyAt.end() ? 0.0 : it->second - now;
		return left > 0.0 ? left : 0.0;
	}

	double SkillBar::CooldownFraction(int slot, double now) const
	{
		const Ability& a = Slots[slot];
		if (!a.Valid())
		{
			return 0.0;
		}
		const auto it = CooldownOf.find(a.Id);
		const double full = it == CooldownOf.end() ? 0.0 : it->second;
		return full > 0.0 ? CooldownLeft(slot, now) / full : 0.0;
	}

	double SkillBar::PoseWait(int slot, const Hero& hero, double now) const
	{
		double w = CommitUntil - now;
		const Ability& a = Slots[slot];
		if (a.Valid() && !a.HasTag(TagMovement) && hero.ActionRemaining > w)
		{
			w = hero.ActionRemaining;
		}
		return w > 0.0 ? w : 0.0;
	}

	double SkillBar::ReadyIn(int slot, const Hero& hero, double now) const
	{
		double w = CooldownLeft(slot, now);
		if (GcdUntil - now > w)
		{
			w = GcdUntil - now;
		}
		const double pose = PoseWait(slot, hero, now);
		return pose > w ? pose : w;
	}

	SkillBar::Result SkillBar::Request(int slot, Hero& hero, double now)
	{
		if (slot < 0 || slot >= SlotCount || !Slots[slot].Valid())
		{
			return Result::Empty;
		}
		const double wait = ReadyIn(slot, hero, now);
		if (wait > 0.0 && wait <= InputBuffer && hero.Energy >= Slots[slot].EnergyCost())
		{
			BufferedSlot = slot;
			BufferUntil = now + wait + 0.1;
			return Result::Buffered;
		}
		BufferedSlot = -1;
		return TryCast(slot, hero, now);
	}

	int SkillBar::Update(Hero& hero, double now)
	{
		if (BufferedSlot < 0)
		{
			return -1;
		}
		if (hero.Dead || hero.Stunned || now > BufferUntil)
		{
			BufferedSlot = -1;
			return -1;
		}
		if (ReadyIn(BufferedSlot, hero, now) > 0.0)
		{
			return -1;
		}
		const int slot = BufferedSlot;
		BufferedSlot = -1;
		return TryCast(slot, hero, now) == Result::Cast ? slot : -1;
	}

	SkillBar::Result SkillBar::TryCast(int slot, Hero& hero, double now)
	{
		if (slot < 0 || slot >= SlotCount || !Slots[slot].Valid())
		{
			return Result::Empty;
		}
		const Ability& a = Slots[slot];
		if (hero.Dead || hero.Stunned || !hero.CanCast || now < GcdUntil || PoseWait(slot, hero, now) > 0.0)
		{
			return Result::Busy;
		}
		if (CooldownLeft(slot, now) > 0.0)
		{
			return Result::Cooldown;
		}
		if (hero.Energy < a.EnergyCost())
		{
			return Result::NoEnergy;
		}
		if (a.HasTag(TagMovement) && hero.Rooted)
		{
			return Result::Rooted;
		}
		Commit(slot, hero, now);
		hero.Energy -= a.EnergyCost();
		return Result::Cast;
	}

	void SkillBar::Commit(int slot, const Hero& hero, double now)
	{
		const Ability& a = Slots[slot];
		double cd = a.CooldownAt(Levels[slot]) * (hero.Stats ? hero.Stats->CooldownMultiplier(slot == 0, a.HasTag(TagMovement)) : 1.0);
		if (slot == 0)
		{
			cd /= hero.ChillMultiplier > 0.1 ? hero.ChillMultiplier : 0.1;
			const double speed = 1.0 + hero.AttackSpeedBonus;
			cd /= speed > 0.5 ? speed : 0.5;
		}
		ReadyAt[a.Id] = now + cd;
		CooldownOf[a.Id] = cd;
		GcdUntil = now + GlobalCooldown;
		CommitUntil = now + a.CommitTime();
	}
}
