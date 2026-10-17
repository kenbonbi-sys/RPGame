// Life, armour, statuses and the stagger bar of anything that can be hurt (the Godot port's
// health.gd, status_effects.gd and poise.gd on one component), all decided by the plain C++
// rules in Rules/: a blow goes through ResolveDamage, lands its statuses, fills the Poise bar of
// a heavy owner and the element meters of a weapon blow. Owners hook in before and after a blow
// (a colossus finds the part it landed on and counts part damage).
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "Rules/RttDamage.h"
#include "Rules/RttProcs.h"
#include "Rules/RttStatus.h"

#include "RttHealth.generated.h"

struct FRttRuleSet;

// One blow as it travels: the rules' Hit, plus where it lands and how it throws.
struct FRttBlow
{
	Rtt::Hit Hit;
	Rtt::StatusSpec Status;
	double Poise = 0.0;
	bool bFromWeapon = false;
	bool bCutting = false;
	bool bPiercing = false;
	bool bHeavy = false;
	bool bCrit = false;
	// throws a hero to the ground
	bool bKnockdown = false;
	// a touch, not a blow (no perfect dodge off it)
	bool bContact = false;
	FVector Point = FVector::ZeroVector;
	FVector Direction = FVector::ForwardVector;
	// metres per second sideways and up
	float Knockback = 0.f;
	float Lift = 0.f;
	TWeakObjectPtr<AActor> Source;
	FString SkillName;
};

DECLARE_MULTICAST_DELEGATE_TwoParams(FRttOnDamaged, const FRttBlow&, float);
DECLARE_MULTICAST_DELEGATE_OneParam(FRttOnBlow, const FRttBlow&);

UCLASS(ClassGroup = (Rtt))
class RUNGTHITHAM_API URttHealth : public UActorComponent
{
	GENERATED_BODY()

public:
	URttHealth();

	FString DisplayName;
	int32 Level = 1;
	Rtt::Team Side = Rtt::Team::Enemy;
	// the Dauntless element of a monster (matchups, its own element never procs on it)
	Rtt::DElement Element = Rtt::DElement::None;
	double Armor = 0.0;
	double ElementalResist = 0.0;
	double Resistances[Rtt::ResistanceSlots] = {};
	double DamageTakenMultiplier = 1.0;
	// the armour a hero wears
	bool bHasArmour = false;
	Rtt::ResistPoints ArmourPoints{};
	// the poise multiplier of the owner's own hits (a hero's Sức Mạnh)
	double PoiseMultiplier = 1.0;

	float MaxHp = 100.f;
	float Hp = 100.f;
	bool bInvulnerable = false;
	bool bDead = false;
	Rtt::ProcMeter Procs;

	// Before a blow is weighed (a colossus finds the part and scales its stagger).
	TFunction<void(FRttBlow&)> BeforeBlow;
	// After it landed for `amount` (part damage, an interrupt).
	TFunction<void(const FRttBlow&, float)> AfterBlow;
	// An element proc: return true when the owner handled it (a colossus slows or is paralysed);
	// otherwise Blaze, Frost and Shock land on its statuses.
	TFunction<bool(const Rtt::Proc&, const FRttBlow&)> OnProc;

	FRttOnDamaged OnDamaged;
	FRttOnBlow OnDied;
	// a hostile blow met it while invulnerable (Lướt Hoàn Hảo listens)
	FRttOnBlow OnEvaded;
	FSimpleMulticastDelegate OnPoiseBroken;

	// Gives the owner a stagger bar (bosses).
	void EnablePoise(double MaxPoise, double BreakSeconds, double DecayDelay, double DecayPerSecond);
	Rtt::Poise* Poise() const { return PoiseBar.Get(); }
	Rtt::StatusEffects& Status();
	double Now() const;
	float Fraction() const { return MaxHp > 0.f ? Hp / MaxHp : 0.f; }

	// Takes a blow; the life it took (0 when it missed, was evaded or the target is dead).
	float TakeBlow(FRttBlow Blow);
	void Heal(float Amount);
	void Revive(float NewMax = -1.f);

	static const FRttRuleSet* Rules(const UObject* WorldContext);

	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

private:
	TUniquePtr<Rtt::StatusEffects> StatusFx;
	TUniquePtr<Rtt::Poise> PoiseBar;
};
