#include "RttHealth.h"

#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "GameFramework/Actor.h"
#include "RttRules.h"

namespace
{
	// rules for a world with no game instance (editor tests): the Unity defaults
	const FRttRuleSet& Fallback()
	{
		static FRttRuleSet Rules;
		return Rules;
	}
}

URttHealth::URttHealth()
{
	PrimaryComponentTick.bCanEverTick = true;
}

const FRttRuleSet* URttHealth::Rules(const UObject* WorldContext)
{
	const UWorld* World = WorldContext ? WorldContext->GetWorld() : nullptr;
	const UGameInstance* Game = World ? World->GetGameInstance() : nullptr;
	const URttRules* Loaded = Game ? Game->GetSubsystem<URttRules>() : nullptr;
	return Loaded && Loaded->IsLoaded() ? &Loaded->Get() : &Fallback();
}

Rtt::StatusEffects& URttHealth::Status()
{
	if (!StatusFx)
	{
		StatusFx = MakeUnique<Rtt::StatusEffects>(Rules(this)->Tuning);
		if (PoiseBar)
		{
			Rtt::Link(*StatusFx, *PoiseBar);
		}
	}
	return *StatusFx;
}

double URttHealth::Now() const
{
	return GetWorld() ? GetWorld()->GetTimeSeconds() : 0.0;
}

void URttHealth::EnablePoise(double MaxPoise, double BreakSeconds, double DecayDelay, double DecayPerSecond)
{
	PoiseBar = MakeUnique<Rtt::Poise>(MaxPoise);
	PoiseBar->BreakStun = BreakSeconds;
	PoiseBar->DecayDelay = DecayDelay;
	PoiseBar->DecayPerSecond = DecayPerSecond;
	Rtt::Link(Status(), *PoiseBar);
}

float URttHealth::TakeBlow(FRttBlow Blow)
{
	if (bDead || !Rtt::CanHurt(Blow.Hit.SourceTeam, Side))
	{
		return 0.f;
	}
	if (bInvulnerable)
	{
		OnEvaded.Broadcast(Blow);
		return 0.f;
	}
	if (BeforeBlow)
	{
		BeforeBlow(Blow);
	}
	const FRttRuleSet& R = *Rules(this);
	const double T = Now();
	// a curse on the attacker weakens what it deals
	if (const AActor* Source = Blow.Source.Get())
	{
		if (URttHealth* Theirs = Source->FindComponentByClass<URttHealth>())
		{
			Blow.Hit.AttackerDealt *= Theirs->Status().DamageDealtMultiplier(T);
		}
	}
	Rtt::Defender D;
	D.Side = Side;
	D.Level = Level;
	D.Armor = Armor;
	D.ElementalResist = ElementalResist;
	for (int32 i = 0; i < Rtt::ResistanceSlots; ++i)
	{
		D.Resistances[i] = Resistances[i];
	}
	D.DamageTakenMultiplier = DamageTakenMultiplier * (PoiseBar ? PoiseBar->DamageTakenMultiplier(T) : 1.0);
	D.StatusDamageTakenMultiplier = Status().DamageTakenMultiplier(T);
	D.Element = Element;
	D.HasArmour = bHasArmour;
	D.ArmourPoints = ArmourPoints;
	const Rtt::DamageOutcome Out = Rtt::ResolveDamage(Blow.Hit, D, R.Tuning, R.Armour, Rtt::Spread(FMath::FRand(), R.Tuning));
	const float Amount = static_cast<float>(Out.Amount);
	if (Amount <= 0.f)
	{
		return 0.f;
	}
	Hp = FMath::Max(0.f, Hp - Amount);
	// statuses never land on the killing blow; they scale from the attacker-side damage
	if (Hp > 0.f && Blow.Status.Any())
	{
		Status().Apply(Blow.Status, Blow.Hit.Amount * Blow.Hit.AttackerDealt, Blow.Hit.SourceTeam, T);
	}
	// heroes' blows fill a heavy owner's stagger bar
	if (PoiseBar && Blow.Hit.SourceTeam == Rtt::Team::Player && Blow.Poise > 0.0 && Hp > 0.f)
	{
		double Multiplier = 1.0;
		if (const AActor* Source = Blow.Source.Get())
		{
			if (const URttHealth* Theirs = Source->FindComponentByClass<URttHealth>())
			{
				Multiplier = Theirs->PoiseMultiplier;
			}
		}
		if (PoiseBar->Add(Blow.Poise * Multiplier, T))
		{
			OnPoiseBroken.Broadcast();
		}
	}
	OnDamaged.Broadcast(Blow, Amount);
	if (AfterBlow)
	{
		AfterBlow(Blow, Amount);
	}
	if (Hp <= 0.f)
	{
		bDead = true;
		OnDied.Broadcast(Blow);
		return Amount;
	}
	if (Blow.bFromWeapon && !Blow.Hit.Dot)
	{
		const Rtt::Proc P = Procs.Fill(Blow.Hit.Type, Amount, MaxHp, Element, T);
		if (P.Element != Rtt::DElement::None && !(OnProc && OnProc(P, Blow)))
		{
			Rtt::ApplyProc(P, Blow.Hit.SourceTeam, Status(), T);
		}
	}
	return Amount;
}

void URttHealth::Heal(float Amount)
{
	if (!bDead && Amount > 0.f)
	{
		Hp = FMath::Min(MaxHp, Hp + Amount);
	}
}

void URttHealth::Revive(float NewMax)
{
	if (NewMax > 0.f)
	{
		MaxHp = NewMax;
	}
	Hp = MaxHp;
	bDead = false;
	bInvulnerable = false;
	Procs.Reset();
	Status().Cleanse();
	if (PoiseBar)
	{
		PoiseBar->Reset();
	}
}

void URttHealth::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	const double T = Now();
	for (const Rtt::DotTick& Tick : Status().Tick(T, MaxHp, bDead))
	{
		if (bDead)
		{
			break;
		}
		FRttBlow Blow;
		Blow.Hit.Amount = Tick.Amount;
		Blow.Hit.Type = Tick.Type;
		Blow.Hit.SourceTeam = Tick.Source;
		Blow.Hit.AttackScaled = true;
		Blow.Hit.Dot = true;
		Blow.Point = GetOwner() ? GetOwner()->GetActorLocation() : FVector::ZeroVector;
		TakeBlow(Blow);
	}
	if (PoiseBar)
	{
		PoiseBar->Tick(DeltaTime, T);
	}
}
