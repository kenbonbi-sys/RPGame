#include "RttBehemoth.h"

#include "AIController.h"
#include "Animation/AnimInstance.h"
#include "Animation/AnimMontage.h"
#include "Animation/AnimSequenceBase.h"
#include "Components/CapsuleComponent.h"
#include "Components/PointLightComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "PhysicsEngine/BodyInstance.h"
#include "PhysicsEngine/BodySetup.h"
#include "RttBoulder.h"
#include "RttFeed.h"
#include "RttHero.h"
#include "RttTelegraph.h"
#include "RttText.h"

DEFINE_LOG_CATEGORY_STATIC(LogRttBeast, Log, All);

namespace
{
	// the bear's reach factor (gau_ma.gd's REACH) in centimetres
	constexpr double ReachCm = Rtt::BearReach * 100.0;
	constexpr double ChargeWind = 1.1;
	constexpr double ChargeRun = 1.9;
	constexpr double ChargeStop = 0.9;
	constexpr double StrikeTime = 0.35;

	FVector Flat(const FVector& V)
	{
		return FVector(V.X, V.Y, 0.0);
	}
}

ARttBehemoth::ARttBehemoth()
{
	PrimaryActorTick.bCanEverTick = true;
	Health = CreateDefaultSubobject<URttHealth>(TEXT("Health"));
	Rays = CreateDefaultSubobject<UPointLightComponent>(TEXT("Rays"));
	Rays->SetupAttachment(GetMesh());
	Rays->SetIntensity(0.f);
	Rays->SetLightColor(FLinearColor(1.f, 0.85f, 0.45f));
	Rays->SetAttenuationRadius(900.f);
	Rays->SetCastShadows(false);

	AIControllerClass = AAIController::StaticClass();
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned;
	bUseControllerRotationYaw = false;
	UCharacterMovementComponent* Moves = GetCharacterMovement();
	Moves->bOrientRotationToMovement = true;
	// a colossus turns slowly (gau_ma.gd: 0.55 rad/s, 0.75 in a rage)
	Moves->RotationRate = FRotator(0.f, 32.f, 0.f);
	Moves->MaxWalkSpeed = 600.f;
	Moves->MaxAcceleration = 600.f;
	Moves->BrakingDecelerationWalking = 800.f;

	// the body's physics bodies (its bones) are what heroes hit and what they bump into
	USkeletalMeshComponent* Skin = GetMesh();
	Skin->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
	Skin->SetCollisionObjectType(ECC_PhysicsBody);
	Skin->SetCollisionResponseToAllChannels(ECR_Block);
	Skin->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	Skin->SetGenerateOverlapEvents(false);
	Skin->VisibilityBasedAnimTickOption = EVisibilityBasedAnimTickOption::AlwaysTickPoseAndRefreshBones;
}

// ================================================================== starting
void ARttBehemoth::BeginPlay()
{
	Super::BeginPlay();
	bCalm = FParse::Param(FCommandLine::Get(), TEXT("RttBearIdle"));
	USkeletalMesh* Asset = BodyMesh.IsNull() ? nullptr : Cast<USkeletalMesh>(BodyMesh.TryLoad());
	if (!Asset)
	{
		UE_LOG(LogRttBeast, Error, TEXT("The behemoth's body %s is missing: add the Paragon pack and run ue.cmd beast"), *BodyMesh.ToString());
	}
	USkeletalMeshComponent* Skin = GetMesh();
	if (Asset)
	{
		Skin->SetSkeletalMeshAsset(Asset);
	}
	BodyAnim = BodyAnimClass.IsNull() ? nullptr : BodyAnimClass.TryLoadClass<UAnimInstance>();
	if (BodyAnim)
	{
		Skin->SetAnimInstanceClass(BodyAnim);
	}
	// colossus-sized: the capsule and the body scale together, feet on the capsule's floor
	const float Half = CapsuleHalfHeight * BodyScale;
	GetCapsuleComponent()->SetCapsuleSize(CapsuleRadius * BodyScale, Half);
	Skin->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -Half), FRotator(0.f, -90.f, 0.f));
	Skin->SetRelativeScale3D(FVector(BodyScale));
	Home = Ground(GetActorLocation());
	HomeRotation = GetActorRotation();
	SetActorLocation(Home + FVector(0.0, 0.0, Half + 2.0));
	Rays->AttachToComponent(Skin, FAttachmentTransformRules::KeepRelativeTransform, TEXT("head"));

	auto Load = [](const FSoftObjectPath& Path) { return Path.IsNull() ? nullptr : Cast<UAnimSequenceBase>(Path.TryLoad()); };
	Swipe = Load(SwipeAnimation);
	Slam = Load(SlamAnimation);
	Throw = Load(ThrowAnimation);
	ChargeAnim = Load(ChargeAnimation);
	Roar = Load(RoarAnimation);
	StunAnim = Load(StunAnimation);
	Flinch = Load(FlinchAnimation);
	Fall = Load(DeathAnimation);
	Rock = RockMesh.IsNull() ? nullptr : Cast<UStaticMesh>(RockMesh.TryLoad());

	Parts = Rtt::BeastStats::Parts();
	Health->DisplayName = RttText::BeastName;
	Health->Level = Rtt::BearStats::Level;
	Health->Side = Rtt::Team::Enemy;
	// stone and old forest: Terra, so Shock weapons hit it hardest
	Health->Element = Rtt::DElement::Terra;
	Health->MaxHp = Rtt::BearStats::MaxHp;
	Health->Hp = Health->MaxHp;
	Health->EnablePoise(Rtt::BearStats::MaxStagger, Rtt::BearStats::StaggerSeconds, Rtt::BearStats::StaggerDecayDelay, Rtt::BearStats::StaggerDecayPerSecond);
	Health->BeforeBlow = [this](FRttBlow& Blow)
	{
		// the head staggers double
		if (!Blow.Hit.Dot && PendingPart >= 0 && PendingPart < static_cast<int32>(Parts.size()))
		{
			Blow.Poise *= Parts[PendingPart].Stagger;
		}
	};
	Health->AfterBlow = [this](const FRttBlow& Blow, float Amount)
	{
		const int32 P = Blow.Hit.Dot ? -1 : PendingPart;
		Rtt::BossPart* Part = P >= 0 && P < static_cast<int32>(Parts.size()) ? &Parts[P] : nullptr;
		if (URttFeed* Feed = URttFeed::Get(this))
		{
			const bool bBig = Blow.bCrit || Blow.bHeavy;
			if (IsStaggered())
			{
				Feed->Number(Blow.Point, Amount, ERttNumber::Staggered, !Blow.Hit.Dot);
			}
			else if (Part && Part->Breakable() && !Part->Broken && !Blow.Hit.Dot)
			{
				Feed->Number(Blow.Point, Amount * Rtt::PartMultiplier(Blow.bCutting, Blow.bPiercing), ERttNumber::Part, bBig);
			}
			else
			{
				Feed->Number(Blow.Point, Amount, bBig ? ERttNumber::Crit : ERttNumber::Core, bBig);
			}
		}
		if (bDead || !Part)
		{
			return;
		}
		if (Rtt::DamagePart(*Part, Amount, Blow.bCutting, Blow.bPiercing, Blow.Hit.Dot))
		{
			BreakPart(P);
		}
		if (Blow.bHeavy && Part->Interrupt && CanInterrupt())
		{
			Interrupt();
		}
	};
	Health->OnProc = [this](const Rtt::Proc& P, const FRttBlow&)
	{
		if (P.Element == Rtt::DElement::Frost)
		{
			SlowFactor = 1.0 - Rtt::ProcSlow;
			SlowUntil = Now() + Rtt::ProcSlowSeconds;
			return true;
		}
		if (P.Element == Rtt::DElement::Shock)
		{
			if (Mode != EMode::Dying && Mode != EMode::Dead)
			{
				Stun(Rtt::ProcParalyseSeconds);
				Word(RttText::Paralysed, FLinearColor(0.72f, 0.6f, 1.f));
			}
			return true;
		}
		return P.Element != Rtt::DElement::Blaze;
	};
	Health->OnPoiseBroken.AddLambda([this]() { OnStaggered(); });
	Health->OnDied.AddLambda([this](const FRttBlow&)
	{
		if (!bDead)
		{
			Die();
		}
	});
}

FString ARttBehemoth::GetBossName() const
{
	return RttText::BeastName;
}

double ARttBehemoth::Now() const
{
	return GetWorld() ? GetWorld()->GetTimeSeconds() : 0.0;
}

double ARttBehemoth::TimeFactor()
{
	if (SlowFactor < 1.0 && Now() >= SlowUntil)
	{
		SlowFactor = 1.0;
	}
	return SlowFactor;
}

FVector ARttBehemoth::Ground(const FVector& P) const
{
	FHitResult Hit;
	if (GetWorld() && GetWorld()->LineTraceSingleByObjectType(Hit, P + FVector(0.0, 0.0, 3000.0), P - FVector(0.0, 0.0, 30000.0), FCollisionObjectQueryParams(ECC_WorldStatic)))
	{
		return Hit.ImpactPoint;
	}
	return FVector(P.X, P.Y, 0.0);
}

FVector ARttBehemoth::Bone(const TCHAR* Name) const
{
	const USkeletalMeshComponent* Skin = GetMesh();
	return Skin && Skin->DoesSocketExist(Name) ? Skin->GetSocketLocation(Name) : GetActorLocation();
}

void ARttBehemoth::SetMode(EMode M)
{
	Mode = M;
	ModeTime = 0.0;
	bChargeOpen = false;
	Done.Reset();
	HitThisMove.Reset();
	GetCharacterMovement()->MaxWalkSpeed = bRage ? 850.f : 600.f;
}

bool ARttBehemoth::Once(const FName& Key)
{
	if (Done.Contains(Key))
	{
		return false;
	}
	Done.Add(Key);
	return true;
}

// ================================================================== every frame
void ARttBehemoth::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const double Dt = DeltaSeconds * TimeFactor();
	ModeTime += Dt;
	CustomTimeDilation = static_cast<float>(SlowFactor);
	if (Mode == EMode::Dead)
	{
		if (bDead && Now() >= RespawnAt)
		{
			Respawn();
		}
		return;
	}
	Think(Dt);
	RunMode(Dt);
	// Dauntless's rays on its head while a heavy hit there would stop Húc
	Rays->SetIntensity(bChargeOpen ? 60000.f * (0.6f + 0.4f * FMath::Sin(static_cast<float>(Now()) * 14.f)) : 0.f);
	if (HeldRock)
	{
		HeldRock->SetActorLocation(Bone(TEXT("hand_r")));
	}
}

void ARttBehemoth::Think(double Dt)
{
	Cooldowns.Tick(Dt);
	if (bDead)
	{
		return;
	}
	const bool bWas = bEngaged;
	ARttHero* Hero = bCalm ? nullptr : NearestHero(Rtt::BearStats::AggroRange * 100.0);
	Target = Hero;
	bEngaged = Hero != nullptr;
	if (bEngaged && !bWas)
	{
		Word(RttText::Roar);
		ShakeNear(GetActorLocation(), 0.3);
		if (Roar && Mode == EMode::Idle)
		{
			PlayMove(Roar, 0.5f, Rtt::Beats{0.0, 0.6, 0.0});
		}
	}
	if (!bRage && Health->Fraction() < 0.5f)
	{
		bRage = true;
		Word(RttText::Rage, FLinearColor(1.f, 0.35f, 0.25f));
		ShakeNear(GetActorLocation(), 0.4);
	}
	if (Mode != EMode::Idle && Mode != EMode::Walk)
	{
		return;
	}
	if (!Hero)
	{
		if (!bCalm)
		{
			WanderWait -= Dt;
			if (WanderWait <= 0.0)
			{
				const double A = FMath::FRand() * 2.0 * PI;
				WanderGoal = Home + FVector(FMath::Cos(A), FMath::Sin(A), 0.0) * FMath::FRandRange(500.0, Rtt::BearStats::DenRadius * 70.0);
				WanderWait = FMath::FRandRange(4.0, 8.0);
			}
			if (FVector::Dist2D(WanderGoal, GetActorLocation()) > 400.0 && !WanderGoal.IsZero())
			{
				WalkTo(WanderGoal, 300.f);
			}
		}
		return;
	}
	const FVector To = Flat(Hero->GetFeet() - GetActorLocation());
	const double D = To.Size() / 100.0;
	const double Front = D > 0.001 ? FVector::DotProduct(GetActorForwardVector(), To.GetSafeNormal()) : 1.0;
	switch (Rtt::ChooseBearMove(D, Front, bRage, Cooldowns, false, Throw != nullptr))
	{
	case Rtt::BearMove::Swipe:
		SwipeSide = FVector::DotProduct(GetActorRightVector(), To) > 0.0 ? 1.0 : -1.0;
		SetMode(EMode::Swipe);
		Word(RttText::Punch);
		break;
	case Rtt::BearMove::Stomp:
		SetMode(EMode::Slam);
		Word(RttText::Slam);
		break;
	case Rtt::BearMove::Charge:
		SetMode(EMode::Charge);
		Word(RttText::Charge);
		break;
	case Rtt::BearMove::Throw:
		SetMode(EMode::Throw);
		Word(RttText::Throw);
		break;
	default:
		Mode = EMode::Walk;
		if (To.Size() > 11.0 * ReachCm)
		{
			WalkTo(Hero->GetFeet(), bRage ? 850.f : 600.f);
		}
		else
		{
			Face(Hero->GetFeet(), bRage ? 43.0 : 32.0, Dt);
		}
		break;
	}
}

void ARttBehemoth::WalkTo(const FVector& Goal, float Speed)
{
	FVector To = Flat(Goal - GetActorLocation());
	// it keeps to its den
	const FVector Next = GetActorLocation() + To.GetSafeNormal() * 100.0;
	if (FVector::Dist2D(Next, Home) > Rtt::BearStats::DenRadius * 100.0)
	{
		To = Flat(Home - GetActorLocation());
	}
	GetCharacterMovement()->MaxWalkSpeed = Speed;
	AddMovementInput(To.GetSafeNormal(), 1.f);
}

void ARttBehemoth::Face(const FVector& P, double DegreesPerSecond, double Dt)
{
	const FVector To = Flat(P - GetActorLocation());
	if (To.IsNearlyZero())
	{
		return;
	}
	const double Want = To.Rotation().Yaw;
	const double Turn = FMath::Clamp(FRotator::NormalizeAxis(Want - GetActorRotation().Yaw), -DegreesPerSecond * Dt, DegreesPerSecond * Dt);
	SetActorRotation(FRotator(0.0, GetActorRotation().Yaw + Turn, 0.0));
}

// Enemies get long, readable wind-ups and quick blows: the animation's wind-up is stretched over the
// tell, the wind and the hold, and RunMode lets the strike go at full speed.
void ARttBehemoth::PlayMove(UAnimSequenceBase* Anim, float StrikeFraction, const Rtt::Beats& B)
{
	UAnimInstance* A = GetMesh()->GetAnimInstance();
	if (!A || !Anim)
	{
		return;
	}
	const double AnimStrike = Anim->GetPlayLength() * StrikeFraction;
	const float Rate = static_cast<float>(FMath::Clamp(AnimStrike / FMath::Max(0.05, B.StrikeAt()), 0.2, 1.5));
	A->PlaySlotAnimationAsDynamicMontage(Anim, MoveSlot, 0.15f, 0.3f, Rate);
}

void ARttBehemoth::RunMode(double Dt)
{
	switch (Mode)
	{
	case EMode::Swipe: DoSwipe(Dt); break;
	case EMode::Slam: DoSlam(Dt); break;
	case EMode::Throw: DoThrow(Dt); break;
	case EMode::Charge: DoCharge(Dt); break;
	case EMode::Stunned: DoStunned(Dt); break;
	case EMode::Dying:
		if (ModeTime > 4.5)
		{
			Mode = EMode::Dead;
		}
		break;
	default: break;
	}
}

// the blow: from the strike on, the animation runs at full speed
static void Unleash(UAnimInstance* A)
{
	if (A)
	{
		if (UAnimMontage* M = A->GetCurrentActiveMontage())
		{
			A->Montage_SetPlayRate(M, 1.f);
		}
	}
}

void ARttBehemoth::DoSwipe(double Dt)
{
	const double T = ModeTime;
	const Rtt::Beats& B = SwipeBeats;
	const double Go = B.StrikeAt();
	if (Once(TEXT("start")))
	{
		PlayMove(Swipe, SwipeStrike, B);
		BlowAt = Ground(GetActorLocation() + GetActorForwardVector() * 11.0 * 0.8 * 100.0 + GetActorRightVector() * SwipeSide * 120.0);
		Telegraph(BlowAt, 8.5 * 0.8 * 100.0, Go + StrikeTime * 0.5);
	}
	if (Target.IsValid() && T < B.HoldAt())
	{
		Face(Target->GetFeet(), 35.0, Dt);
	}
	if (T >= Go && Once(TEXT("unleash")))
	{
		Unleash(GetMesh()->GetAnimInstance());
		ShakeNear(BlowAt, 0.15);
	}
	if (T >= Go && T < Go + StrikeTime)
	{
		for (ARttHero* H : Heroes())
		{
			if (!HitThisMove.Contains(H) && FVector::Dist2D(H->GetFeet(), BlowAt) < 8.5 * 0.8 * 100.0)
			{
				HitThisMove.Add(H);
				HurtHero(H, 22.0, BlowAt, 12.0, 6.0, RttText::Punch);
			}
		}
	}
	if (T >= Go + StrikeTime + 0.9)
	{
		SetMode(EMode::Idle);
	}
}

void ARttBehemoth::DoSlam(double Dt)
{
	const double T = ModeTime;
	const Rtt::Beats& B = SlamBeats;
	const double Go = B.StrikeAt();
	if (Once(TEXT("start")))
	{
		PlayMove(Slam, SlamStrike, B);
		BlowAt = Ground(GetActorLocation() + GetActorForwardVector() * 11.5 * 0.8 * 100.0);
		Telegraph(BlowAt, 11.0 * 0.8 * 100.0, Go);
	}
	if (T >= Go && Once(TEXT("impact")))
	{
		Unleash(GetMesh()->GetAnimInstance());
		HitArea(BlowAt, 11.0 * 0.8 * 100.0, 28.0, 10.0, 6.0, RttText::Slam);
		ShakeNear(BlowAt, 0.85);
	}
	if (T >= Go + 1.25)
	{
		SetMode(EMode::Idle);
	}
}

void ARttBehemoth::DoThrow(double Dt)
{
	const double T = ModeTime;
	const Rtt::Beats& B = ThrowBeats;
	const double Go = B.StrikeAt();
	if (Once(TEXT("start")))
	{
		PlayMove(Throw, ThrowRelease, B);
	}
	if (Target.IsValid() && T < Go)
	{
		Face(Target->GetFeet(), 30.0, Dt);
	}
	// it tears the rock up as the wind-up begins
	if (T >= B.Tell && Once(TEXT("grab")))
	{
		HeldRock = ARttBoulder::Hold(GetWorld(), Rock, 150.f * BodyScale / 3.5f);
	}
	if (T >= Go && Once(TEXT("release")))
	{
		Unleash(GetMesh()->GetAnimInstance());
		if (HeldRock)
		{
			ARttHero* Hero = Target.Get();
			FVector Aim = Hero ? Hero->GetFeet() + Flat(Hero->GetVelocity()) * 0.5 : GetActorLocation() + GetActorForwardVector() * 2500.0;
			Aim = Ground(Aim);
			const FVector From = HeldRock->GetActorLocation();
			const float Flight = static_cast<float>(FMath::Clamp(FVector::Dist(From, Aim) / 2200.0, 1.0, 1.8));
			HeldRock->Launch(From, Aim, Flight, [this](const FVector& Landed)
			{
				HitArea(Landed, 550.0, 24.0, 9.0, 5.0, RttText::Throw);
				ShakeNear(Landed, 0.6);
			});
			Telegraph(Aim, 5.5 * ReachCm, Flight);
			HeldRock = nullptr;
		}
	}
	if (T >= Go + 1.15)
	{
		SetMode(EMode::Idle);
	}
}

// Dauntless's charge: head down with rays glowing on it (the window), then it runs at where the
// hero stood. Whoever its front meets is thrown; a heavy hit on its head interrupts it.
void ARttBehemoth::DoCharge(double Dt)
{
	const double T = ModeTime;
	const Rtt::Beats& B = ChargeBeats;
	if (Once(TEXT("start")))
	{
		ARttHero* Hero = Target.Get();
		const FVector From = GetActorLocation();
		const FVector Aim = Hero ? Hero->GetFeet() : From + GetActorForwardVector() * 2000.0;
		FVector To = Flat(Aim - From);
		const FVector Dir = To.Size() > 50.0 ? To.GetSafeNormal() : GetActorForwardVector();
		FVector End = From + Dir * FMath::Clamp(To.Size() + 800.0, 1200.0, 3200.0);
		if (FVector::Dist2D(End, Home) > Rtt::BearStats::DenRadius * 100.0)
		{
			End = Home + Flat(End - Home).GetSafeNormal() * Rtt::BearStats::DenRadius * 100.0;
		}
		ChargeTo = Ground(End);
		bChargeOpen = true;
		PlayMove(ChargeAnim ? ChargeAnim.Get() : Roar.Get(), 0.6f, B);
		for (int32 i = 0; i < 4; ++i)
		{
			Telegraph(Ground(FMath::Lerp(From, ChargeTo, (i + 1) / 4.0)), 3.2 * 0.8 * 100.0, ChargeWind + ChargeRun * (i + 1) / 4.0);
		}
	}
	if (T < ChargeWind)
	{
		Face(ChargeTo, 150.0, Dt);
		return;
	}
	if (T < ChargeWind + ChargeRun)
	{
		const double K = (T - ChargeWind) / ChargeRun;
		bChargeOpen = K < 0.9;
		if (Once(TEXT("run")))
		{
			Unleash(GetMesh()->GetAnimInstance());
			GetCharacterMovement()->MaxWalkSpeed = static_cast<float>(FMath::Max(900.0, FVector::Dist2D(GetActorLocation(), ChargeTo) / ChargeRun));
			GetCharacterMovement()->MaxAcceleration = 6000.f;
		}
		if (FVector::Dist2D(GetActorLocation(), ChargeTo) > 150.0)
		{
			AddMovementInput(Flat(ChargeTo - GetActorLocation()).GetSafeNormal(), 1.f);
		}
		// whoever its front meets is thrown, once
		const FVector Front = GetActorLocation() + GetActorForwardVector() * GetCapsuleComponent()->GetScaledCapsuleRadius();
		for (ARttHero* H : Heroes())
		{
			if (!HitThisMove.Contains(H) && FVector::Dist2D(H->GetActorLocation(), Front) < GetCapsuleComponent()->GetScaledCapsuleRadius() + 150.0)
			{
				HitThisMove.Add(H);
				HurtHero(H, 34.0, GetActorLocation(), 15.0, 7.0, RttText::Charge);
				ShakeNear(H->GetActorLocation(), 0.6);
			}
		}
		return;
	}
	bChargeOpen = false;
	GetCharacterMovement()->MaxAcceleration = 600.f;
	if (T >= ChargeWind + ChargeRun + ChargeStop)
	{
		SetMode(EMode::Idle);
	}
}

void ARttBehemoth::DoStunned(double Dt)
{
	if (ModeTime >= StunFor + (bStunDown ? 1.0 : 0.0))
	{
		if (UAnimInstance* A = GetMesh()->GetAnimInstance())
		{
			A->Montage_Stop(0.3f);
		}
		SetMode(EMode::Idle);
	}
}

// ================================================================== reacting
bool ARttBehemoth::IsStaggered() const
{
	return Health && Health->Poise() && Health->Poise()->IsBroken(Now());
}

float ARttBehemoth::StaggerFraction() const
{
	return Health && Health->Poise() ? static_cast<float>(Health->Poise()->Fraction(Now())) : 0.f;
}

float ARttBehemoth::RespawnIn() const
{
	return bDead ? FMath::Max(0.f, static_cast<float>(RespawnAt - Now())) : 0.f;
}

void ARttBehemoth::Stun(double Seconds, bool bStaggered, bool bDown)
{
	if (bDead)
	{
		return;
	}
	if (bStaggered && Health->Poise())
	{
		Health->Poise()->ForceBreak(Seconds, Now());
	}
	if (HeldRock)
	{
		HeldRock->Destroy();
		HeldRock = nullptr;
	}
	StunFor = Seconds;
	bStunDown = bDown;
	SetMode(EMode::Stunned);
	UAnimInstance* A = GetMesh()->GetAnimInstance();
	UAnimSequenceBase* Anim = bDown ? StunAnim.Get() : Flinch.Get();
	if (A && Anim)
	{
		// knocked down it stays down: the animation loops for as long as the stun lasts
		const int32 Loops = bDown ? FMath::Max(1, FMath::CeilToInt(Seconds / FMath::Max(0.2f, Anim->GetPlayLength()))) : 1;
		A->PlaySlotAnimationAsDynamicMontage(Anim, MoveSlot, 0.2f, 0.3f, 1.f, Loops);
	}
}

void ARttBehemoth::OnStaggered()
{
	if (bDead || Mode == EMode::Dying || Mode == EMode::Dead)
	{
		return;
	}
	Stun(FMath::Max(Health->Poise()->BrokenRemaining(Now()), 1.0), false);
	Word(RttText::Stunned, FLinearColor(1.f, 0.6f, 0.2f));
	ShakeNear(GetActorLocation(), 0.5);
}

void ARttBehemoth::Interrupt()
{
	Word(RttText::Interrupted, FLinearColor(1.f, 0.85f, 0.45f));
	ShakeNear(GetActorLocation(), 0.7);
	Stun(7.0);
}

void ARttBehemoth::BreakPart(int32 P)
{
	Word(FString(RttText::BrokenPrefix) + UTF8_TO_TCHAR(Parts[P].Name.c_str()) + TEXT("!"), FLinearColor(1.f, 0.84f, 0.4f));
	ShakeNear(GetActorLocation(), 0.5);
	if (Mode == EMode::Idle || Mode == EMode::Walk || Mode == EMode::Swipe || Mode == EMode::Slam || Mode == EMode::Charge)
	{
		Stun(1.8, false, false);
	}
}

void ARttBehemoth::Die()
{
	bDead = true;
	bEngaged = false;
	SetMode(EMode::Dying);
	Word(RttText::BearDown, FLinearColor(1.f, 0.86f, 0.55f));
	RespawnAt = Now() + Rtt::BearStats::RespawnSeconds;
	GetCharacterMovement()->DisableMovement();
	if (HeldRock)
	{
		HeldRock->Destroy();
		HeldRock = nullptr;
	}
	if (Fall)
	{
		GetMesh()->PlayAnimation(Fall, false);
	}
}

void ARttBehemoth::Respawn()
{
	Parts = Rtt::BeastStats::Parts();
	Health->Revive(Rtt::BearStats::MaxHp);
	SetActorLocationAndRotation(Home + FVector(0.0, 0.0, GetCapsuleComponent()->GetScaledCapsuleHalfHeight() + 2.0), HomeRotation);
	GetCharacterMovement()->SetMovementMode(MOVE_Walking);
	if (BodyAnim)
	{
		GetMesh()->SetAnimationMode(EAnimationMode::AnimationBlueprint);
		GetMesh()->SetAnimInstanceClass(BodyAnim);
	}
	bDead = false;
	bRage = false;
	SetMode(EMode::Idle);
}

void ARttBehemoth::Word(const FString& Text, const FLinearColor& Color)
{
	UE_LOG(LogRttBeast, Log, TEXT("Beast: %s"), *Text);
	if (URttFeed* Feed = URttFeed::Get(this))
	{
		Feed->Word(Text, Color);
	}
}

// ================================================================== heroes' blows
int32 ARttBehemoth::PartOfBone(const FName& BoneName) const
{
	const FString B = BoneName.ToString().ToLower();
	if (B.Contains(TEXT("head")) || B.Contains(TEXT("neck")))
	{
		return Rtt::BeastStats::Head;
	}
	const bool bLeft = B.EndsWith(TEXT("_l"));
	static const TCHAR* Arm[] = {TEXT("clavicle"), TEXT("upperarm"), TEXT("lowerarm"), TEXT("hand"), TEXT("thumb"), TEXT("index"), TEXT("middle"),
		TEXT("ring"), TEXT("pinky"), TEXT("weapon")};
	for (const TCHAR* W : Arm)
	{
		if (B.Contains(W))
		{
			return bLeft ? Rtt::BeastStats::ArmLeft : Rtt::BeastStats::ArmRight;
		}
	}
	static const TCHAR* Leg[] = {TEXT("thigh"), TEXT("calf"), TEXT("foot"), TEXT("ball"), TEXT("toe")};
	for (const TCHAR* W : Leg)
	{
		if (B.Contains(W))
		{
			return bLeft ? Rtt::BeastStats::LegLeft : Rtt::BeastStats::LegRight;
		}
	}
	return Rtt::BeastStats::Body;
}

FVector ARttBehemoth::ClosestPoint(const FVector& Point, int32& OutPart) const
{
	OutPart = Rtt::BeastStats::Body;
	FVector Best = GetActorLocation();
	float BestD = TNumericLimits<float>::Max();
	for (const FBodyInstance* Body : GetMesh()->Bodies)
	{
		if (!Body || !Body->IsValidBodyInstance() || !Body->BodySetup.IsValid())
		{
			continue;
		}
		FVector On;
		const float D = Body->GetDistanceToBody(Point, On);
		if (D < 0.f || D >= BestD)
		{
			continue;
		}
		BestD = D;
		Best = D == 0.f ? Point : On;
		OutPart = PartOfBone(Body->BodySetup->BoneName);
	}
	if (BestD == TNumericLimits<float>::Max())
	{
		// no physics bodies: the capsule
		const UCapsuleComponent* C = GetCapsuleComponent();
		FVector Axis = GetActorLocation();
		const float Half = C->GetScaledCapsuleHalfHeight() - C->GetScaledCapsuleRadius();
		Axis.Z = FMath::Clamp(Point.Z, Axis.Z - Half, Axis.Z + Half);
		Best = Axis + (Point - Axis).GetSafeNormal() * C->GetScaledCapsuleRadius();
	}
	return Best;
}

float ARttBehemoth::TakeHeroBlow(FRttBlow Blow, int32 Part)
{
	PendingPart = Part;
	const float Amount = Health->TakeBlow(Blow);
	PendingPart = -1;
	UE_LOG(LogRttBeast, Log, TEXT("%s on %s: %.0f (life %.0f / %.0f, stagger %.2f%s)"), *Blow.SkillName,
		Part >= 0 && Part < static_cast<int32>(Parts.size()) ? UTF8_TO_TCHAR(Parts[Part].Id.c_str()) : TEXT("?"), Amount,
		Health->Hp, Health->MaxHp, StaggerFraction(), IsStaggered() ? TEXT(", staggered") : TEXT(""));
	return Amount;
}

// ================================================================== heroes
TArray<ARttHero*> ARttBehemoth::Heroes() const
{
	TArray<ARttHero*> Out;
	for (TActorIterator<ARttHero> It(GetWorld()); It; ++It)
	{
		if (!It->IsDown())
		{
			Out.Add(*It);
		}
	}
	return Out;
}

ARttHero* ARttBehemoth::NearestHero(double MaxDistance) const
{
	ARttHero* Best = nullptr;
	double BestD = MaxDistance;
	for (ARttHero* H : Heroes())
	{
		const double D = FVector::Dist(H->GetFeet(), GetActorLocation());
		if (D < BestD)
		{
			BestD = D;
			Best = H;
		}
	}
	return Best;
}

int32 ARttBehemoth::HitArea(const FVector& Center, double Radius, double Amount, double Knock, double Lift, const FString& Skill)
{
	int32 N = 0;
	for (ARttHero* H : Heroes())
	{
		const double D = FVector::Dist2D(H->GetFeet(), Center);
		if (D <= Radius && FMath::Abs(H->GetFeet().Z - Center.Z) < 600.0)
		{
			HurtHero(H, Rtt::AreaFalloff(Amount, D, Radius), Center, Knock, Lift, Skill);
			++N;
		}
	}
	return N;
}

float ARttBehemoth::HurtHero(ARttHero* Hero, double Amount, const FVector& From, double Knock, double Lift, const FString& Skill)
{
	URttHealth* Theirs = Hero ? Hero->GetHealth() : nullptr;
	if (!Theirs)
	{
		return 0.f;
	}
	FRttBlow Blow;
	Blow.Hit.Amount = Amount;
	Blow.Hit.Type = Rtt::Element::Physical;
	Blow.Hit.SourceTeam = Rtt::Team::Enemy;
	Blow.Hit.SourceLevel = Rtt::BearStats::Level;
	Blow.Hit.AttackerElement = Rtt::DElement::Terra;
	Blow.bKnockdown = true;
	Blow.Knockback = static_cast<float>(Knock);
	Blow.Lift = static_cast<float>(Lift);
	Blow.Point = Hero->GetActorLocation();
	Blow.Direction = Flat(Hero->GetFeet() - From).GetSafeNormal();
	Blow.Source = this;
	Blow.SkillName = Skill;
	const float Taken = Theirs->TakeBlow(Blow);
	UE_LOG(LogRttBeast, Log, TEXT("%s hits the hero: %.0f (life %.0f / %.0f)"), *Skill, Taken, Theirs->Hp, Theirs->MaxHp);
	return Taken;
}

void ARttBehemoth::ShakeNear(const FVector& P, double Amount) const
{
	for (ARttHero* H : Heroes())
	{
		const double D = FVector::Dist(H->GetFeet(), P) / 100.0;
		H->Shake(static_cast<float>(Amount * FMath::Clamp(1.0 - D / 70.0, 0.15, 1.0)));
	}
}

void ARttBehemoth::Telegraph(const FVector& At, double Radius, double Seconds) const
{
	ARttTelegraph::Spawn(GetWorld(), At, static_cast<float>(Radius), static_cast<float>(Seconds));
}
