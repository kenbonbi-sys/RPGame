// The hero's fight (the Godot port's hero.gd, player_skills.gd and ability_context.gd): Chém Gió on
// the left button, three swings of the weapon whose numbers come from abilities.json; the heavy
// move on the right button, held to gather power (weapons.json); being thrown by a blow; Lướt
// Hoàn Hảo; falling and getting back up. Hits land on the closest spot of a colossus's body.
#include "RttHero.h"

#include "Animation/AnimInstance.h"
#include "Animation/AnimMontage.h"
#include "Animation/AnimSequenceBase.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "EngineUtils.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Kismet/GameplayStatics.h"
#include "RttFeed.h"
#include "RttBoss.h"
#include "RttRules.h"
#include "RttText.h"

namespace
{
	// Chém Gió's damage block for one stage of its combo (abilities.json: slash > Combo > stages).
	const Rtt::JsonValue& SlashStage(const Rtt::Ability& Slash, int32 Stage)
	{
		const Rtt::JsonValue& Effects = Slash.Effects()[size_t(0)]["stages"][static_cast<size_t>(Stage)]["effects"];
		for (size_t i = 0; i < Effects.Size(); ++i)
		{
			if (Effects[i].Str("$type") == "DamageEffect")
			{
				return Effects[i];
			}
		}
		return Rtt::JsonValue::NullValue();
	}
}

void ARttHero::SetUpCombat()
{
	const FRttRuleSet* R = URttHealth::Rules(this);
	const std::string Item = TCHAR_TO_UTF8(*WeaponItem);
	Family = &R->Weapons.FamilyOf(Item);
	WeaponType = R->Weapons.DamageType(Item);
	Sheet = MakeUnique<Rtt::Sheet>(R->Tuning);
	Sheet->Class = Rtt::ReadClass(R->Classes[std::string(TCHAR_TO_UTF8(*ClassId))]);
	Sheet->Race = Rtt::ReadRace(R->Races[std::string(TCHAR_TO_UTF8(*RaceId))]);
	Sheet->WeaponId = Family->Stat.empty() ? "sword" : Family->Stat;
	Sheet->Recalculate();
	Perfect = MakeUnique<Rtt::PerfectDodge>(R->Tuning);

	Health->Side = Rtt::Team::Player;
	Health->Level = Sheet->Level;
	Health->MaxHp = static_cast<float>(Sheet->HealthMax());
	Health->Hp = Health->MaxHp;
	Health->Armor = Sheet->Armor;
	Health->ElementalResist = Sheet->ElementalResist;
	for (int32 i = 0; i < Rtt::ResistanceSlots; ++i)
	{
		Health->Resistances[i] = Sheet->Resistances[i];
	}
	Health->PoiseMultiplier = Sheet->PoiseMultiplier;
	Health->OnDamaged.AddUObject(this, &ARttHero::OnHurt);
	Health->OnEvaded.AddUObject(this, &ARttHero::OnEvaded);
	Health->OnDied.AddUObject(this, &ARttHero::OnFall);
}

FVector ARttHero::GetFeet() const
{
	return GetActorLocation() - FVector(0.f, 0.f, GetCapsuleComponent()->GetScaledCapsuleHalfHeight());
}

float ARttHero::RespawnIn() const
{
	return IsDown() ? FMath::Max(0.f, static_cast<float>(RespawnAt - Now())) : 0.f;
}

float ARttHero::HeavyCharge() const
{
	if (!bHeavyCharging || !Family)
	{
		return -1.f;
	}
	return FMath::Clamp(static_cast<float>((Now() - HeavyStart) / FMath::Max(0.1, Family->Heavy.Charge)), 0.f, 1.f);
}

FString ARttHero::HeavyName() const
{
	return Family ? FString(UTF8_TO_TCHAR(Family->Heavy.Name.c_str())) : FString();
}

void ARttHero::Shake(float Amount)
{
	Trauma = FMath::Clamp(Trauma + Amount, 0.f, 1.f);
}

bool ARttHero::CanAct() const
{
	return !IsDown() && Now() >= KnockedUntil;
}

// ================================================================== Chém Gió
void ARttHero::AttackPressed()
{
	if (!CanAct() || bHeavyCharging || HeavyHitAt >= 0.0)
	{
		return;
	}
	const double T = Now();
	if (T < NextSwingAt)
	{
		// a press a little early is remembered (player_skills.gd's input buffer)
		bSwingQueued = NextSwingAt - T <= 0.4;
		return;
	}
	Swing();
}

void ARttHero::Swing()
{
	const FRttRuleSet* R = URttHealth::Rules(this);
	const Rtt::Ability Slash = Rtt::FindAbility(R->Abilities, "slash");
	const double T = Now();
	SwingStage = Rtt::NextCombo(Combo, Slash.Effects()[size_t(0)].Num("window", 0.9), T) % 3;
	SwingBonus = Perfect ? Perfect->TakeBonus(Slash, T) : 1.0;
	FaceFoe();
	// the blow lands a little before the swing's SaveAttack mark, where the next swing may start
	double Save = 0.5;
	if (UAnimMontage* M = Swings.IsValidIndex(SwingStage) ? Swings[SwingStage].Get() : nullptr)
	{
		Save = M->GetPlayLength() * 0.45;
		for (const FAnimNotifyEvent& N : M->Notifies)
		{
			if (N.NotifyName == FName(TEXT("SaveAttack")))
			{
				Save = N.GetTriggerTime();
				break;
			}
		}
		if (UAnimInstance* A = GetMesh()->GetAnimInstance())
		{
			A->Montage_Play(M, SwingRate);
		}
	}
	const double Scaled = Save / SwingRate;
	SwingHitAt = T + FMath::Max(0.12, Scaled - 0.1);
	const double Cooldown = Slash.CooldownAt(1) * (Sheet ? Sheet->CooldownMultiplier(true, false) : 1.0);
	NextSwingAt = T + FMath::Max(Cooldown, Scaled);
}

void ARttHero::LandSwing()
{
	const FRttRuleSet* R = URttHealth::Rules(this);
	const Rtt::Ability Slash = Rtt::FindAbility(R->Abilities, "slash");
	const Rtt::JsonValue& Block = SlashStage(Slash, SwingStage);
	if (!Sheet || Block.IsNull())
	{
		return;
	}
	Rtt::Cast C;
	C.Skill = Slash;
	C.Slot = 0;
	C.DamageMultiplier = SwingBonus;
	C.CounterPoise = Perfect ? Perfect->CounterPoise(Slash.DisplayName(), Now()) : 0.0;
	Rtt::Caster K;
	K.Stats = Sheet.Get();
	K.DealtMultiplier = Sheet->DamageDealt;
	K.Weapon = Family;
	K.WeaponType = WeaponType;
	const Rtt::SkillHit H = Rtt::MakeHit(C, K, Block["hit"]);
	FRttBlow B;
	B.Hit = H.Damage;
	B.Status = H.Status;
	B.Poise = H.Poise;
	B.bFromWeapon = H.FromWeapon;
	B.bCutting = H.Cutting;
	B.bPiercing = H.Piercing;
	B.Knockback = static_cast<float>(H.Knockback);
	B.SkillName = UTF8_TO_TCHAR(H.SkillName.c_str());
	if (FMath::FRand() < H.CritChance + Sheet->CritChance)
	{
		B.Hit.Amount *= Sheet->CritDamage;
		B.bCrit = true;
	}
	const float Reach = static_cast<float>(Block.Num("radius", 1.9) * Rtt::AbilitySpace * 100.0) + BladeReach;
	Strike(B, Reach, static_cast<float>(Block.Num("angle", 150.0)), static_cast<float>(H.HitStop));
}

// ================================================================== the heavy move (Trảm)
void ARttHero::HeavyPressed()
{
	if (!CanAct() || bHeavyCharging || HeavyHitAt >= 0.0 || GetCharacterMovement()->IsFalling() || !Family)
	{
		return;
	}
	bHeavyCharging = true;
	bHeavyFull = false;
	HeavyStart = Now();
	SwingHitAt = -1.0;
	bSwingQueued = false;
	FaceFoe();
	if (UAnimInstance* A = GetMesh()->GetAnimInstance())
	{
		if (HeavySwing)
		{
			A->Montage_Play(HeavySwing, 1.f);
		}
	}
}

void ARttHero::HeavyReleased()
{
	if (!bHeavyCharging)
	{
		return;
	}
	bHeavyCharging = false;
	const double T = Now();
	HeavyChargeAtRelease = FMath::Clamp((T - HeavyStart) / FMath::Max(0.1, Family->Heavy.Charge), 0.0, 1.0);
	if (UAnimInstance* A = GetMesh()->GetAnimInstance())
	{
		if (HeavySwing)
		{
			A->Montage_SetPlayRate(HeavySwing, 1.6f);
		}
	}
	HeavyHitAt = T + 0.18;
	NextSwingAt = T + 0.6;
}

void ARttHero::LandHeavy()
{
	if (!Sheet || !Family)
	{
		return;
	}
	const Rtt::HeavyMove& Move = Family->Heavy;
	const double C = HeavyChargeAtRelease;
	const Rtt::HeavyBlow HB = Rtt::HeavyBlowAt(Move, Sheet->AttackFor(WeaponType), Sheet->DamageDealt, C);
	FRttBlow B;
	B.Hit.Amount = HB.Amount;
	B.Hit.Type = WeaponType;
	B.Hit.SourceTeam = Rtt::Team::Player;
	B.Hit.SourceLevel = Sheet->Level;
	B.Hit.AttackScaled = true;
	B.Poise = HB.Poise;
	B.bHeavy = true;
	B.bFromWeapon = true;
	B.bCutting = Family->Cut;
	B.bPiercing = Family->Pierce;
	B.Knockback = static_cast<float>(HB.Knockback);
	B.SkillName = HeavyName();
	if (FMath::FRand() < 0.1 + Sheet->CritChance)
	{
		B.Hit.Amount *= Sheet->CritDamage;
		B.bCrit = true;
	}
	if (Strike(B, static_cast<float>(Move.Reach * 100.0) + BladeReach, static_cast<float>(Move.Angle), static_cast<float>(HB.HitStop)) > 0)
	{
		Shake(0.15f + 0.25f * static_cast<float>(C));
	}
}

// ================================================================== landing blows
void ARttHero::FaceFoe()
{
	FVector Dir = GetLastMovementInputVector();
	Dir.Z = 0.0;
	if (Dir.IsNearlyZero())
	{
		// no stick: turn to the colossus if one is close
		const FVector Center = GetActorLocation();
		double Best = 1200.0;
		for (const TPair<AActor*, IRttBoss*>& Each : IRttBoss::All(this))
		{
			if (Each.Value->IsDefeated())
			{
				continue;
			}
			int32 Part = 0;
			FVector To = Each.Value->ClosestPoint(Center, Part) - Center;
			To.Z = 0.0;
			if (To.Size() < Best)
			{
				Best = To.Size();
				Dir = To;
			}
		}
	}
	if (!Dir.IsNearlyZero())
	{
		SetActorRotation(FRotator(0.0, Dir.Rotation().Yaw, 0.0));
	}
}

int32 ARttHero::Strike(const FRttBlow& Blow, float ReachCm, float AngleDegrees, float HitStopSeconds)
{
	const FVector Center = GetActorLocation();
	const FVector Facing = GetActorForwardVector();
	int32 Hits = 0;
	for (const TPair<AActor*, IRttBoss*>& Each : IRttBoss::All(this))
	{
		if (Each.Value->IsDefeated())
		{
			continue;
		}
		int32 Part = 0;
		const FVector P = Each.Value->ClosestPoint(Center, Part);
		FVector To = P - Center;
		const double Rise = To.Z;
		To.Z = 0.0;
		if (To.Size() > ReachCm || Rise > 350.0 || Rise < -250.0)
		{
			continue;
		}
		const double Cos = FVector::DotProduct(Facing, To.GetSafeNormal());
		if (To.Size() > 80.0 && AngleDegrees < 359.0 && Cos < FMath::Cos(FMath::DegreesToRadians(AngleDegrees * 0.5)))
		{
			continue;
		}
		FRttBlow B = Blow;
		B.Point = P;
		B.Direction = To.GetSafeNormal();
		B.Source = this;
		if (B.Hit.SourceLevel <= 0 && Sheet)
		{
			B.Hit.SourceLevel = Sheet->Level;
		}
		if (Each.Value->TakeHeroBlow(B, Part) > 0.f)
		{
			++Hits;
		}
	}
	if (Hits > 0)
	{
		HitStop(FMath::Max(HitStopSeconds, 0.03f));
		Shake(0.08f);
	}
	return Hits;
}

void ARttHero::UpdateCombat()
{
	const double T = Now();
	Health->bInvulnerable = IsInvulnerable();
	if (IsDown())
	{
		if (T >= RespawnAt)
		{
			Rise();
		}
		return;
	}
	if (bHeavyCharging && Family)
	{
		const double Held = T - HeavyStart;
		// the weapon rises, then holds at the top while the power gathers
		if (Held > 0.3 && HeavySwing)
		{
			if (UAnimInstance* A = GetMesh()->GetAnimInstance())
			{
				A->Montage_SetPlayRate(HeavySwing, 0.03f);
			}
		}
		if (Held >= Family->Heavy.Charge && !bHeavyFull)
		{
			bHeavyFull = true;
			Shake(0.12f);
		}
		// held half a second past full: it goes by itself
		if (Held > Family->Heavy.Charge + 0.5)
		{
			HeavyReleased();
		}
	}
	if (SwingHitAt >= 0.0 && T >= SwingHitAt)
	{
		SwingHitAt = -1.0;
		LandSwing();
	}
	if (HeavyHitAt >= 0.0 && T >= HeavyHitAt)
	{
		HeavyHitAt = -1.0;
		LandHeavy();
	}
	if (bSwingQueued && T >= NextSwingAt && CanAct())
	{
		bSwingQueued = false;
		Swing();
	}
	// slow while swinging (moveWhileCasting) or gathering power
	float Speed = bSprinting ? SprintSpeed : RunSpeed;
	if (bHeavyCharging)
	{
		Speed = 120.f;
	}
	else if (T < NextSwingAt)
	{
		Speed = RunSpeed * 0.35f;
	}
	GetCharacterMovement()->MaxWalkSpeed = Speed;
}

// ================================================================== being hit
void ARttHero::OnHurt(const FRttBlow& Blow, float Amount)
{
	if (URttFeed* Feed = URttFeed::Get(this))
	{
		Feed->Number(GetActorLocation() + FVector(0.f, 0.f, 110.f), Amount, ERttNumber::Hero);
	}
	Shake(0.25f);
	if (!Blow.bKnockdown || IsDown())
	{
		return;
	}
	// thrown: whatever it was doing stops
	SwingHitAt = -1.0;
	HeavyHitAt = -1.0;
	bHeavyCharging = false;
	bSwingQueued = false;
	LaunchCharacter(Blow.Direction * Blow.Knockback * 60.0 + FVector(0.0, 0.0, Blow.Lift * 60.0), true, true);
	KnockedUntil = Now() + 0.55;
	UAnimInstance* A = GetMesh()->GetAnimInstance();
	UAnimSequenceBase* React = FVector::DotProduct(GetActorForwardVector(), -Blow.Direction) > 0.0 ? HitFront.Get() : HitBack.Get();
	if (A && React)
	{
		A->Montage_Stop(0.05f);
		A->PlaySlotAnimationAsDynamicMontage(React, TEXT("UpperBody"), 0.05f, 0.2f);
	}
}

void ARttHero::OnEvaded(const FRttBlow& Blow)
{
	if (!Perfect || !Perfect->Evaded(Blow.bContact, Blow.Hit.Dot, Now()))
	{
		return;
	}
	// Lướt Hoàn Hảo: a moment of slow motion, and the next skill hits harder
	if (URttFeed* Feed = URttFeed::Get(this))
	{
		Feed->Word(RttText::Perfect, FLinearColor(1.f, 0.85f, 0.4f), 1.2f);
	}
	const FRttRuleSet* R = URttHealth::Rules(this);
	SlowMoUntil = RealNow() + R->Tuning.PerfectSlowSeconds;
}

void ARttHero::OnFall(const FRttBlow& Blow)
{
	if (URttFeed* Feed = URttFeed::Get(this))
	{
		Feed->Word(RttText::YouFell, FLinearColor(1.f, 0.3f, 0.25f), 3.f);
	}
	RespawnAt = Now() + 5.0;
	SwingHitAt = -1.0;
	HeavyHitAt = -1.0;
	bHeavyCharging = false;
	bSwingQueued = false;
	GetCharacterMovement()->DisableMovement();
	if (Fall)
	{
		GetMesh()->PlayAnimation(Fall, false);
	}
}

void ARttHero::Rise()
{
	SetActorTransform(StartXf, false, nullptr, ETeleportType::TeleportPhysics);
	GetCharacterMovement()->SetMovementMode(MOVE_Walking);
	Health->Revive();
	if (BodyAnimClass)
	{
		GetMesh()->SetAnimationMode(EAnimationMode::AnimationBlueprint);
		GetMesh()->SetAnimInstanceClass(BodyAnimClass);
	}
	KnockedUntil = 0.0;
	bCameraPlaced = false;
}

// ================================================================== the demo (screenshots)
void ARttHero::DemoFight()
{
	const FVector Center = GetActorLocation();
	for (const TPair<AActor*, IRttBoss*>& Each : IRttBoss::All(this))
	{
		if (Each.Value->IsDefeated())
		{
			continue;
		}
		int32 Part = 0;
		FVector To = Each.Value->ClosestPoint(Center, Part) - Center;
		To.Z = 0.0;
		if (To.Size() > 220.0)
		{
			AddMovementInput(To.GetSafeNormal(), 1.f);
			return;
		}
		const double T = Now();
		if (T >= DemoHeavyAt && !bHeavyCharging)
		{
			HeavyPressed();
		}
		else if (bHeavyCharging && T - HeavyStart >= (Family ? Family->Heavy.Charge : 1.0))
		{
			HeavyReleased();
			DemoHeavyAt = T + 6.0;
		}
		else if (!bHeavyCharging)
		{
			AttackPressed();
		}
		return;
	}
}

// ================================================================== time
void ARttHero::HitStop(float Seconds)
{
	HitStopUntil = FMath::Max(HitStopUntil, RealNow() + Seconds);
}

void ARttHero::UpdateTime()
{
	const double T = RealNow();
	const FRttRuleSet* R = URttHealth::Rules(this);
	const float Want = T < HitStopUntil ? 0.05f : (T < SlowMoUntil ? static_cast<float>(R->Tuning.PerfectTimeScale) : 1.f);
	if (!FMath::IsNearlyEqual(UGameplayStatics::GetGlobalTimeDilation(this), Want))
	{
		UGameplayStatics::SetGlobalTimeDilation(this, Want);
	}
}
