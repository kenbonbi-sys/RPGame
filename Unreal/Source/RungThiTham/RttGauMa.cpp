#include "RttGauMa.h"

#include "Components/CapsuleComponent.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/CollisionProfile.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "RttFeed.h"
#include "RttHero.h"
#include "RttTelegraph.h"
#include "RttText.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "UObject/ConstructorHelpers.h"

using RttG::FBasis;
using RttG::FXf;

DEFINE_LOG_CATEGORY_STATIC(LogRttFight, Log, All);

namespace
{
	// Every length of the bear is in "bear units"; S turns them into metres (about 10 m to the crown).
	constexpr double S = Rtt::BearScale;
	constexpr double Reach = Rtt::BearReach;
	constexpr double StandHeight = 7.4 * S;
	constexpr double Stride = 2.8 * S;
	constexpr double StepTime = 0.85;
	constexpr double Lift = 1.5 * S;
	constexpr double ChargeWind = 1.1;
	constexpr double ChargeRun = 1.9;
	constexpr double ChargeStop = 0.9;
	constexpr double SwipeTime = 0.35;
	constexpr double SlamTime = 0.25;
	const FVector NeckBase = FVector(0.0, 1.4, -8.6) * S;
	const FVector HeadOffset = FVector(0.0, 1.4, -4.6) * S;
	const FVector Right(1.0, 0.0, 0.0);
	const FVector Up(0.0, 1.0, 0.0);
	const FVector GForward(0.0, 0.0, -1.0);

	FXf At(const FVector& O)
	{
		return {FBasis(), O};
	}

	double Lerp(double A, double B, double T)
	{
		return A + (B - A) * T;
	}

	double Flat(const FVector& A, const FVector& B)
	{
		return FVector2D(A.X - B.X, A.Z - B.Z).Size();
	}

	// gau_ma.gd's solve_knee: two-bone IK, the knee bent toward `Pole`.
	FVector SolveKnee(const FVector& Hip, const FVector& Foot, double L1, double L2, const FVector& Pole)
	{
		const FVector Axis = Foot - Hip;
		const double Dist = Axis.Size();
		const FVector Dir = Dist > 0.001 ? Axis / Dist : FVector(0.0, -1.0, 0.0);
		const double D = FMath::Clamp(Dist, FMath::Abs(L1 - L2) + 0.01, L1 + L2 - 0.001);
		const double A = (L1 * L1 - L2 * L2 + D * D) / (2.0 * D);
		const double H = FMath::Sqrt(FMath::Max(0.0, L1 * L1 - A * A));
		FVector Perp = Pole - Dir * FVector::DotProduct(Pole, Dir);
		if (Perp.Size() < 0.001)
		{
			Perp = FVector::CrossProduct(Dir, Right);
		}
		return Hip + Dir * A + Perp.GetSafeNormal() * H;
	}

	// A limb segment from `Top` to `Bottom`, its Z leaning toward `Back`.
	FXf Segment(const FVector& Top, const FVector& Bottom, const FVector& Back)
	{
		const FVector Y = (Top - Bottom).GetSafeNormal();
		FVector Z = Back - Y * FVector::DotProduct(Back, Y);
		Z = Z.Size() > 0.01 ? Z.GetSafeNormal() : FVector::CrossProduct(Y, Right).GetSafeNormal();
		const FVector X = FVector::CrossProduct(Y, Z).GetSafeNormal();
		return {{X, Y, Z}, (Top + Bottom) * 0.5};
	}
}

ARttGauMa::ARttGauMa()
{
	PrimaryActorTick.bCanEverTick = true;
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Health = CreateDefaultSubobject<URttHealth>(TEXT("Health"));

	static ConstructorHelpers::FObjectFinder<UStaticMesh> TorsoMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/Torso.Torso"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> TorsoGlowMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/TorsoGlow.TorsoGlow"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> NeckMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/Neck.Neck"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> HeadMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/Head.Head"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> HeadGlowMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/HeadGlow.HeadGlow"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> AntlersMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/Antlers.Antlers"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> UpperFront(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/UpperFront.UpperFront"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> LowerFront(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/LowerFront.LowerFront"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> GreaveFront(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/GreaveFront.GreaveFront"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> UpperHind(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/UpperHind.UpperHind"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> LowerHind(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/LowerHind.LowerHind"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> GreaveHind(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/GreaveHind.GreaveHind"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> PawMesh(TEXT("/Game/Creatures/GauMa/gau_ma/StaticMeshes/Paw.Paw"));

	Bodies.SetNum(3 + 3 * 4);
	// the trunk: a long capsule, the shoulder hump, bark plates on the flanks and the chest
	AddLook(0, TorsoMesh.Object, TEXT("Torso"));
	AddLook(0, TorsoGlowMesh.Object, TEXT("TorsoGlow"));
	AddShape(0, EShape::Capsule, {RttG::AxisAngle(Right, PI * 0.5), FVector(0.0, 0.5, -2.0) * S}, 3.7 * S, 16.0 * S, FVector::OneVector, 2, true);
	AddShape(0, EShape::Sphere, At(FVector(0.0, 2.2, -5.5) * S), 3.4 * S, 0.0, FVector::OneVector, 2, true);
	for (double Side : {-1.0, 1.0})
	{
		AddShape(0, EShape::Box, {RttG::AxisAngle(GForward, Side * 0.2), FVector(Side * 3.45, -0.3, -3.2) * S}, 0.0, 0.0, FVector(0.8, 3.6, 9.5) * S, 2, false);
	}
	AddShape(0, EShape::Box, {RttG::AxisAngle(Right, 0.28), FVector(0.0, -0.6, -9.7) * S}, 0.0, 0.0, FVector(5.8, 4.4, 1.0) * S, 2, false);
	// the neck
	AddLook(1, NeckMesh.Object, TEXT("Neck"));
	const FVector Nd = FVector(0.0, 1.0, -3.4) * S;
	AddShape(1, EShape::Capsule, {RttG::AxisBasis(Nd), Nd * 0.5}, 2.4 * S, 5.6 * S, FVector::OneVector, 1, true);
	// the head: the skull, the bone mask, the muzzle
	AddLook(2, HeadMesh.Object, TEXT("Head"));
	AddLook(2, HeadGlowMesh.Object, TEXT("HeadGlow"));
	Antlers = AddLook(2, AntlersMesh.Object, TEXT("Antlers"));
	AddShape(2, EShape::Sphere, FXf(), 2.7 * S, 0.0, FVector::OneVector, 0, true);
	AddShape(2, EShape::Box, At(FVector(0.0, 0.1, -2.3) * S), 0.0, 0.0, FVector(3.6, 2.8, 1.0) * S, 0, false);
	AddShape(2, EShape::Capsule, {RttG::AxisAngle(Right, PI * 0.5), FVector(0.0, -1.1, -3.0) * S}, 0.95 * S, 3.4 * S, FVector::OneVector, 0, false);

	AddLeg(TEXT("FL"), 3, FVector(-2.6, -1.4, -7.0), 4.1, 4.2, 1.5, 1.2, 1.0, 0);
	AddLeg(TEXT("FR"), 4, FVector(2.6, -1.4, -7.0), 4.1, 4.2, 1.5, 1.2, 1.0, 1);
	AddLeg(TEXT("HL"), 5, FVector(-2.8, -0.9, 3.6), 4.4, 4.0, 1.75, 1.25, -1.0, 1);
	AddLeg(TEXT("HR"), 6, FVector(2.8, -0.9, 3.6), 4.4, 4.0, 1.75, 1.25, -1.0, 0);
	for (const FLeg& Leg : Legs)
	{
		const bool bFront = Leg.Bend > 0.0;
		AddLook(Leg.Upper, (bFront ? UpperFront : UpperHind).Object, *(Leg.Name + TEXT("_Upper")));
		AddLook(Leg.Upper + 1, (bFront ? LowerFront : LowerHind).Object, *(Leg.Name + TEXT("_Lower")));
		Greaves.Add(AddLook(Leg.Upper + 1, (bFront ? GreaveFront : GreaveHind).Object, *(Leg.Name + TEXT("_Greave"))));
		AddLook(Leg.Upper + 2, PawMesh.Object, *(Leg.Name + TEXT("_Paw")));
	}
}

UStaticMeshComponent* ARttGauMa::AddLook(int32 Body, UStaticMesh* Mesh, const TCHAR* Name)
{
	UStaticMeshComponent* C = CreateDefaultSubobject<UStaticMeshComponent>(Name);
	C->SetupAttachment(RootComponent);
	C->SetStaticMesh(Mesh);
	C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	C->SetCanEverAffectNavigation(false);
	Looks.Add({Body, C});
	return C;
}

void ARttGauMa::AddShape(int32 Body, EShape Kind, const FXf& Local, double Radius, double InHeight, const FVector& Size, int32 Part, bool bBlock)
{
	FShape Sh;
	Sh.Body = Body;
	Sh.Kind = Kind;
	Sh.Local = Local;
	Sh.Radius = Radius;
	Sh.Height = InHeight;
	Sh.Size = Size;
	Sh.Part = Part;
	if (bBlock && Kind != EShape::Box)
	{
		const FName Name(*FString::Printf(TEXT("Block%d"), Shapes.Num()));
		UPrimitiveComponent* P = nullptr;
		if (Kind == EShape::Capsule)
		{
			UCapsuleComponent* C = CreateDefaultSubobject<UCapsuleComponent>(Name);
			C->InitCapsuleSize(Radius * 100.0, FMath::Max(Radius, InHeight * 0.5) * 100.0);
			P = C;
		}
		else
		{
			USphereComponent* C = CreateDefaultSubobject<USphereComponent>(Name);
			C->InitSphereRadius(Radius * 100.0);
			P = C;
		}
		P->SetupAttachment(RootComponent);
		// blocks heroes; the camera passes through, as in Shadow of the Colossus (blocking it slams
		// the camera into the bear's face whenever its head comes between)
		P->SetCollisionProfileName(UCollisionProfile::BlockAllDynamic_ProfileName);
		P->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
		P->SetCanEverAffectNavigation(false);
		P->SetHiddenInGame(true);
		Sh.Blocker = P;
	}
	Shapes.Add(Sh);
}

void ARttGauMa::AddLeg(const TCHAR* Name, int32 Part, const FVector& Hip, double L1, double L2, double R1, double R2, double Bend, int32 Group)
{
	FLeg Leg;
	Leg.Name = Name;
	Leg.Part = Part;
	Leg.Hip = Hip * S;
	Leg.L1 = L1 * S;
	Leg.L2 = L2 * S;
	Leg.R1 = R1 * S;
	Leg.R2 = R2 * S;
	Leg.Bend = Bend;
	Leg.Group = Group;
	Leg.Upper = 3 + 3 * Legs.Num();
	AddShape(Leg.Upper, EShape::Capsule, FXf(), Leg.R1, Leg.L1 + Leg.R1, FVector::OneVector, Part, true);
	AddShape(Leg.Upper + 1, EShape::Capsule, FXf(), Leg.R2, Leg.L2 + Leg.R2 * 0.6, FVector::OneVector, Part, true);
	// the bark shin plate on the front of the lower leg
	AddShape(Leg.Upper + 1, EShape::Box, At(FVector(0.0, -0.2 * S, -Leg.R2 * 0.95)), 0.0, 0.0, FVector(Leg.R2 * 1.7, Leg.L2 * 0.7, 0.55 * S), Part, false);
	AddShape(Leg.Upper + 2, EShape::Box, At(FVector(0.0, -0.1, -0.5) * S), 0.0, 0.0, FVector(2.5, 1.1, 3.1) * S, Part, false);
	Legs.Add(Leg);
}

// ================================================================== starting
void ARttGauMa::BeginPlay()
{
	Super::BeginPlay();
	RootPos = RttG::ToG(GetActorLocation());
	Yaw = -FMath::DegreesToRadians(GetActorRotation().Yaw);
	HomePos = RootPos;
	HomeYaw = Yaw;
	Parts = Rtt::BearStats::Parts();

	Health->DisplayName = RttText::BearName;
	Health->Level = Rtt::BearStats::Level;
	Health->Side = Rtt::Team::Enemy;
	// the old forest: Terra, so Shock weapons hit it hardest
	Health->Element = Rtt::DElement::Terra;
	Health->MaxHp = Rtt::BearStats::MaxHp;
	Health->Hp = Health->MaxHp;
	Health->EnablePoise(Rtt::BearStats::MaxStagger, Rtt::BearStats::StaggerSeconds, Rtt::BearStats::StaggerDecayDelay, Rtt::BearStats::StaggerDecayPerSecond);
	Health->BeforeBlow = [this](FRttBlow& Blow)
	{
		// the head staggers double, the neck half again
		if (!Blow.Hit.Dot && Parts.size() > static_cast<size_t>(FMath::Max(0, PendingPart)) && PendingPart >= 0)
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
		if (!Blow.Hit.Dot)
		{
			FlashUntil = Now() + 0.08;
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
			// Frost: everything it does runs slower for a while
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
		// Blaze burns it (its statuses); the others make things in the world the MVP does not have yet
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

	// one skin for the whole hide (it flashes when struck), glowing marks and eyes
	for (const FLook& L : Looks)
	{
		const FString Name = L.Mesh->GetName();
		if (Name.Contains(TEXT("Glow")))
		{
			UMaterialInstanceDynamic* M = L.Mesh->CreateDynamicMaterialInstance(0);
			if (Name.StartsWith(TEXT("Head")))
			{
				Eyes = M;
			}
			else
			{
				Marks = M;
			}
			continue;
		}
		if (!Skin)
		{
			Skin = L.Mesh->CreateDynamicMaterialInstance(0);
		}
		else
		{
			L.Mesh->SetMaterial(0, Skin);
		}
	}
	if (Marks)
	{
		Marks->SetVectorParameterValue(TEXT("Color"), FLinearColor(0.45f, 1.f, 0.85f));
		Marks->SetScalarParameterValue(TEXT("Strength"), 1.8f);
	}
	// screenshots: -RttBearIdle keeps it calm where it stands; -RttPlainSkin shows the bare painted
	// sculpt (M_VertexColour) instead of the detailed creature skin, to compare
	bCalm = FParse::Param(FCommandLine::Get(), TEXT("RttBearIdle"));
	if (FParse::Param(FCommandLine::Get(), TEXT("RttPlainSkin")))
	{
		if (UMaterialInterface* Plain = LoadObject<UMaterialInterface>(nullptr, TEXT("/Game/Materials/M_VertexColour.M_VertexColour")))
		{
			Skin = UMaterialInstanceDynamic::Create(Plain, this);
			for (const FLook& L : Looks)
			{
				if (!L.Mesh->GetName().Contains(TEXT("Glow")))
				{
					L.Mesh->SetMaterial(0, Skin);
				}
			}
		}
	}

	Height = StandHeight;
	GoalHeight = StandHeight;
	PitchPivotZ = 3.6 * S;
	SnapToGround();
	PlantFeet();
	UpdateBody(0.0);
	PlaceLegs();
	PlaceComponents();
}

double ARttGauMa::Now() const
{
	return GetWorld() ? GetWorld()->GetTimeSeconds() : 0.0;
}

double ARttGauMa::TimeFactor()
{
	if (SlowFactor < 1.0 && Now() >= SlowUntil)
	{
		SlowFactor = 1.0;
	}
	return SlowFactor;
}

FXf ARttGauMa::Root() const
{
	return {RttG::RotY(Yaw), RootPos};
}

double ARttGauMa::GroundAt(const FVector& G) const
{
	const FVector U = RttG::ToU(G);
	FHitResult Hit;
	FCollisionObjectQueryParams Statics(ECC_WorldStatic);
	if (GetWorld() && GetWorld()->LineTraceSingleByObjectType(Hit, FVector(U.X, U.Y, U.Z + 6000.0), FVector(U.X, U.Y, U.Z - 20000.0), Statics))
	{
		return Hit.ImpactPoint.Z / 100.0;
	}
	return 0.0;
}

void ARttGauMa::SnapToGround()
{
	RootPos.Y = GroundAt(RootPos);
}

void ARttGauMa::PlantFeet()
{
	const FXf R = Root();
	for (FLeg& Leg : Legs)
	{
		FVector P = R * FVector(Leg.Hip.X * 1.08, 0.0, Leg.Hip.Z);
		P.Y = GroundAt(P) + 0.6 * S;
		Leg.Foot = P;
		Leg.bStepping = false;
		Leg.bOverride = false;
	}
}

void ARttGauMa::SetMode(EMode M)
{
	Mode = M;
	ModeTime = 0.0;
	PoseRate = 0.0;
	bChargeOpen = false;
	StepScale = 1.0;
	Done.Reset();
	HitThisMove.Reset();
	for (FLeg& Leg : Legs)
	{
		Leg.bOverride = false;
		Leg.OverrideRate = 10.0;
	}
	bHeadFree = true;
	GoalHeight = StandHeight;
	GoalPitch = 0.0;
	GoalRoll = 0.0;
	PitchPivotZ = 3.6 * S;
}

bool ARttGauMa::Once(const FName& Key)
{
	if (Done.Contains(Key))
	{
		return false;
	}
	Done.Add(Key);
	return true;
}

// ================================================================== every frame
void ARttGauMa::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const double Dt = DeltaSeconds * TimeFactor();
	ModeTime += Dt;
	if (Mode != EMode::Dead)
	{
		Think(Dt);
		RunMode(Dt);
	}
	else if (bDead && Now() >= RespawnAt)
	{
		Respawn();
	}
	EasePose(Dt);
	UpdateFeet(Dt);
	UpdateBody(Dt);
	PlaceLegs();
	SetActorLocationAndRotation(RttG::ToU(RootPos), FRotator(0.0, -FMath::RadiansToDegrees(Yaw), 0.0));
	PlaceComponents();
	UpdateCues(Dt);
	ShakeAmount = FMath::Max(0.0, ShakeAmount - Dt * 1.5);
}

void ARttGauMa::UpdateCues(double Dt)
{
	if (Skin)
	{
		Skin->SetScalarParameterValue(TEXT("Hurt"), Now() < FlashUntil ? 0.35f : 0.f);
	}
	if (!Eyes)
	{
		return;
	}
	const float Pulse = 0.5f + 0.5f * FMath::Sin(static_cast<float>(Now()) * 14.f);
	FLinearColor Colour = bRage ? FLinearColor(1.f, 0.25f, 0.15f) : FLinearColor(0.6f, 1.f, 0.8f);
	float Strength = 4.f;
	if (bChargeOpen)
	{
		// Dauntless's rays on the head: a heavy hit there now interrupts Húc
		Colour = FLinearColor(1.f, 0.85f, 0.45f);
		Strength = 10.f + 14.f * Pulse;
	}
	else if (Mode == EMode::Stunned)
	{
		Strength = 1.f;
	}
	else if (Mode == EMode::Dying || Mode == EMode::Dead)
	{
		Strength = FMath::Max(0.f, 4.f - static_cast<float>(ModeTime));
	}
	Eyes->SetVectorParameterValue(TEXT("Color"), Colour);
	Eyes->SetScalarParameterValue(TEXT("Strength"), Strength);
}

// ================================================================== deciding what to do
void ARttGauMa::Think(double Dt)
{
	Cooldowns.Tick(Dt);
	if (bDead)
	{
		return;
	}
	const bool bWas = bEngaged;
	ARttHero* Hero = bCalm ? nullptr : NearestHero(Rtt::BearStats::AggroRange);
	Target = Hero;
	bEngaged = Hero != nullptr;
	if (bEngaged && !bWas)
	{
		Word(RttText::Roar);
		ShakeNear(RootPos, 0.3);
	}
	if (!bRage && Health->Fraction() < 0.5f)
	{
		bRage = true;
		Word(RttText::Rage, FLinearColor(1.f, 0.35f, 0.25f));
		ShakeNear(RootPos, 0.4);
	}
	if (Mode != EMode::Idle && Mode != EMode::Walk)
	{
		return;
	}
	if (!Hero)
	{
		if (!bCalm)
		{
			Wander(Dt);
		}
		return;
	}
	FVector To = HeroFeet(Hero) - RootPos;
	To.Y = 0.0;
	const double D = To.Size();
	const FXf R = Root();
	const FVector Forward = -R.B.Z;
	const double Front = D > 0.1 ? FVector::DotProduct(Forward, To / D) : 1.0;
	switch (Rtt::ChooseBearMove(D, Front, bRage, Cooldowns, false, false))
	{
	case Rtt::BearMove::Swipe:
		SwipeSide = FVector::DotProduct(R.B.X, To) > 0.0 ? 1.0 : -1.0;
		SetMode(EMode::Swipe);
		Word(RttText::Swipe);
		break;
	case Rtt::BearMove::Stomp:
		SetMode(EMode::Stomp);
		Word(RttText::Stomp);
		break;
	case Rtt::BearMove::Charge:
		SetMode(EMode::Charge);
		Word(RttText::Charge);
		break;
	default:
		Mode = EMode::Walk;
		WalkTo(HeroFeet(Hero), bRage ? 3.4 : 2.4, 11.0 * Reach, Dt);
		break;
	}
}

void ARttGauMa::Wander(double Dt)
{
	WanderWait -= Dt;
	if (WanderWait > 0.0)
	{
		WalkTo(RootPos - Root().B.Z, 0.0, 99.0, Dt);
		return;
	}
	if (WanderGoal.IsZero() || Flat(WanderGoal, RootPos) < 4.0)
	{
		const double A = FMath::FRand() * 2.0 * PI;
		WanderGoal = HomePos + FVector(FMath::Cos(A), 0.0, FMath::Sin(A)) * FMath::FRandRange(5.0, Rtt::BearStats::DenRadius * 0.7);
		WanderWait = FMath::FRandRange(2.0, 5.0);
		return;
	}
	WalkTo(WanderGoal, 1.6, 2.0, Dt);
}

void ARttGauMa::WalkTo(const FVector& Goal, double WalkSpeed, double StopAt, double Dt)
{
	FVector To = Goal - RootPos;
	To.Y = 0.0;
	const double WantYaw = FMath::Atan2(-To.X, -To.Z);
	const double Diff = RttG::AngleDifference(Yaw, WantYaw);
	const double Turn = (bRage ? 0.75 : 0.55) * Dt;
	Yaw += FMath::Clamp(Diff, -Turn, Turn);
	const double Want = To.Size() > StopAt && FMath::Abs(Diff) < 1.1 ? WalkSpeed : 0.0;
	Speed = RttG::MoveToward(Speed, Want, 1.6 * Dt);
	MoveForward(Speed * Dt);
}

void ARttGauMa::MoveForward(double Dist)
{
	FVector P = RootPos - Root().B.Z * Dist;
	FVector2D Off(P.X - HomePos.X, P.Z - HomePos.Z);
	if (Off.Size() > Rtt::BearStats::DenRadius)
	{
		Off = Off.GetSafeNormal() * Rtt::BearStats::DenRadius;
		P.X = HomePos.X + Off.X;
		P.Z = HomePos.Z + Off.Y;
	}
	RootPos = P;
	SnapToGround();
	Phase += Dist * 2.2 / S;
}

void ARttGauMa::Face(const FVector& P, double Rate, double Dt)
{
	const FVector To = P - RootPos;
	const double WantYaw = FMath::Atan2(-To.X, -To.Z);
	Yaw += FMath::Clamp(RttG::AngleDifference(Yaw, WantYaw), -Rate * Dt, Rate * Dt);
}

// ================================================================== moves
void ARttGauMa::RunMode(double Dt)
{
	switch (Mode)
	{
	case EMode::Swipe: Swipe(Dt); break;
	case EMode::Stomp: Stomp(Dt); break;
	case EMode::Charge: Charge(Dt); break;
	case EMode::Stunned: Stunned(Dt); break;
	case EMode::Dying: Dying(Dt); break;
	default: break;
	}
	if (Mode != EMode::Walk && Mode != EMode::Idle)
	{
		Speed = RttG::MoveToward(Speed, 0.0, 3.0 * Dt);
	}
}

void ARttGauMa::Swipe(double Dt)
{
	const double T = ModeTime;
	const Rtt::Beats& B = SwipeBeats;
	FLeg& Leg = SwipeSide > 0.0 ? Legs[1] : Legs[0];
	const FXf R = Root();
	const double Go = B.StrikeAt();
	if (Once(TEXT("start")))
	{
		Telegraph(R * (FVector(SwipeSide * 1.5, 0.0, -11.0) * S), 8.5 * S, Go + SwipeTime * 0.5);
	}
	ARttHero* Hero = Target.Get();
	if (Hero && T < B.HoldAt())
	{
		Face(HeroFeet(Hero), 0.6, Dt);
	}
	if (T < B.Tell)
	{
		// the counter-move: it shifts its weight onto the paw it will swing, dipping a little
		PoseRate = 9.0;
		GoalRoll = SwipeSide * 0.06;
		GoalHeight = StandHeight - 0.35 * S;
	}
	else if (T < Go)
	{
		PoseRate = 0.0;
		GoalHeight = StandHeight;
		GoalRoll = -SwipeSide * 0.09;
		Leg.bOverride = true;
		const FVector Shiver = FVector(0.0, B.Tremble(T) * 0.12, 0.0) * S;
		Leg.OverridePos = BodyXf * (FVector(SwipeSide * 4.4, -0.3, -6.3) * S + Shiver);
		Leg.OverrideRate = T < B.HoldAt() ? 6.0 : 14.0;
	}
	else if (T < Go + SwipeTime)
	{
		Leg.bOverride = true;
		const double K = B.Strike(T, SwipeTime);
		FVector P = R * (FVector(Lerp(SwipeSide * 7.0, -SwipeSide * 3.5, K), 0.0, Lerp(-9.0, -12.0, K)) * S);
		P.Y = GroundAt(P) + 0.9 * S;
		Leg.OverridePos = P;
		Leg.OverrideRate = 30.0;
		GoalRoll = SwipeSide * 0.05;
		for (ARttHero* H : Heroes())
		{
			if (!HitThisMove.Contains(H) && FVector::Dist(HeroFeet(H), Leg.Foot) < 3.4 * S)
			{
				HitThisMove.Add(H);
				HurtHero(H, 22.0, Leg.Foot, 12.0, 6.0, RttText::Swipe);
			}
		}
		if (Once(TEXT("whoosh")))
		{
			ShakeNear(Leg.Foot, 0.15);
		}
	}
	else if (T < Go + SwipeTime + 0.8)
	{
		Leg.bOverride = false;
		GoalRoll = 0.0;
	}
	else
	{
		SetMode(EMode::Idle);
	}
}

void ARttGauMa::Stomp(double Dt)
{
	const double T = ModeTime;
	const Rtt::Beats& B = StompBeats;
	const FXf R = Root();
	const double Impact = B.StrikeAt() + SlamTime;
	if (Once(TEXT("start")))
	{
		StompPoint = R * (FVector(0.0, 0.0, -11.5) * S);
		StompPoint.Y = GroundAt(StompPoint);
		Telegraph(StompPoint, 11.0 * S, Impact);
	}
	PitchPivotZ = 3.6 * S;
	FLeg* Front[2] = {&Legs[0], &Legs[1]};
	if (T < B.Tell)
	{
		// the counter-move: it drops its head and sinks before it rears up
		PoseRate = 8.0;
		GoalPitch = -0.1;
		GoalHeight = StandHeight - 0.8 * S;
		GoalHead = FVector2D(0.0, -0.35);
		bHeadFree = false;
	}
	else if (T < B.StrikeAt())
	{
		PoseRate = 0.0;
		GoalPitch = 0.46 + B.Tremble(T) * 0.02;
		GoalHeight = StandHeight + 0.5 * S;
		for (FLeg* Leg : Front)
		{
			Leg->bOverride = true;
			Leg->OverrideRate = 5.0;
			Leg->OverridePos = BodyXf * FVector(Leg->Hip.X, Leg->Hip.Y - 5.2 * S, Leg->Hip.Z - 2.0 * S);
		}
		GoalHead = FVector2D(0.0, 0.25);
		bHeadFree = false;
	}
	else if (T < Impact)
	{
		PoseRate = 14.0;
		GoalPitch = -0.08;
		for (FLeg* Leg : Front)
		{
			FVector P = StompPoint + R.B.X * FMath::Sign(Leg->Hip.X) * 2.4 * S;
			P.Y = GroundAt(P) + 0.6 * S;
			Leg->OverridePos = P;
			Leg->OverrideRate = 26.0;
		}
	}
	else if (T < Impact + 1.25)
	{
		PoseRate = 0.0;
		if (Once(TEXT("impact")))
		{
			HitArea(StompPoint, 11.0 * S, 28.0, 10.0, 6.0, RttText::Stomp);
			ShakeNear(StompPoint, 0.85);
			ShakeAmount = FMath::Max(ShakeAmount, 0.8);
		}
		GoalPitch = 0.0;
		bHeadFree = true;
		if (T > Impact + 0.45)
		{
			for (FLeg* Leg : Front)
			{
				Leg->bOverride = false;
			}
		}
	}
	else
	{
		SetMode(EMode::Idle);
	}
}

// Dauntless's charge: head down, rays glowing on it (the window), then it runs at where the hero
// stood. Whoever its head or front paws meet is thrown; a heavy hit on its head interrupts it.
void ARttGauMa::Charge(double Dt)
{
	const double T = ModeTime;
	if (Once(TEXT("start")))
	{
		ARttHero* Hero = Target.Get();
		const FVector Aim = Hero ? HeroFeet(Hero) : RootPos - Root().B.Z * 20.0;
		FVector To = Aim - RootPos;
		To.Y = 0.0;
		const FVector Dir = To.Size() > 0.5 ? To.GetSafeNormal() : -Root().B.Z;
		ChargeFrom = RootPos;
		FVector End = RootPos + Dir * FMath::Clamp(To.Size() + 8.0, 12.0, 32.0);
		FVector2D Off(End.X - HomePos.X, End.Z - HomePos.Z);
		if (Off.Size() > Rtt::BearStats::DenRadius)
		{
			Off = Off.GetSafeNormal() * Rtt::BearStats::DenRadius;
			End = FVector(HomePos.X + Off.X, End.Y, HomePos.Z + Off.Y);
		}
		ChargeTo = End;
		HitThisMove.Reset();
		bChargeOpen = true;
		for (int32 i = 0; i < 4; ++i)
		{
			FVector P = FMath::Lerp(ChargeFrom, ChargeTo, (i + 1) / 4.0);
			P.Y = GroundAt(P);
			Telegraph(P, 3.2 * S, ChargeWind + ChargeRun * (i + 1) / 4.0);
		}
	}
	const Rtt::Beats& B = ChargeBeats;
	if (T < B.Tell)
	{
		// the counter-move: the head comes up first (a snort), then goes down to ram
		Face(ChargeTo, 2.6, Dt);
		PoseRate = 8.0;
		GoalPitch = 0.08;
		GoalHead = FVector2D(0.0, 0.35);
		bHeadFree = false;
	}
	else if (T < ChargeWind)
	{
		PoseRate = T < B.HoldAt() ? 0.0 : 6.0;
		Face(ChargeTo, 2.6, Dt);
		GoalPitch = -0.32 + B.Tremble(T) * 0.015;
		GoalHeight = StandHeight - 1.05 * S;
		GoalHead = FVector2D(0.0, -0.62);
		bHeadFree = false;
	}
	else if (T < ChargeWind + ChargeRun)
	{
		const double K = (T - ChargeWind) / ChargeRun;
		FVector P = FMath::Lerp(ChargeFrom, ChargeTo, K);
		P.Y = GroundAt(P);
		Speed = FVector::Dist(P, RootPos) / FMath::Max(Dt, 0.001);
		RootPos = P;
		StepScale = 0.38;
		GoalPitch = -0.32;
		GoalHeight = StandHeight - 1.2 * S;
		GoalHead = FVector2D(0.0, -0.7);
		bHeadFree = false;
		bChargeOpen = K < 0.9;
		Ram();
	}
	else if (T < ChargeWind + ChargeRun + ChargeStop)
	{
		bChargeOpen = false;
		StepScale = 1.0;
		GoalPitch = 0.0;
		GoalHeight = StandHeight;
		bHeadFree = true;
	}
	else
	{
		SetMode(EMode::Idle);
	}
}

// Heroes the charging head or front paws touch are thrown (once per charge).
void ARttGauMa::Ram()
{
	for (ARttHero* H : Heroes())
	{
		if (HitThisMove.Contains(H))
		{
			continue;
		}
		const FVector Chest = HeroFeet(H) + Up;
		for (const FShape& Sh : Shapes)
		{
			// the head, and the lower legs and paws of the front legs
			const bool bFront = Sh.Body == 2 || Sh.Body == 4 || Sh.Body == 5 || Sh.Body == 7 || Sh.Body == 8;
			if (!bFront)
			{
				continue;
			}
			bool bInside = false;
			if (FVector::Dist(ClosestOn(Sh, ShapeXf(Sh), Chest, bInside), Chest) < 0.9 || bInside)
			{
				HitThisMove.Add(H);
				HurtHero(H, 34.0, RootPos, 15.0, 7.0, RttText::Charge);
				ShakeNear(Chest, 0.6);
				break;
			}
		}
	}
}

void ARttGauMa::Stunned(double Dt)
{
	const double T = ModeTime;
	PitchPivotZ = 3.6 * S;
	if (!bStunDown)
	{
		// a flinch (a part broke): it rears and shakes its head
		GoalPitch = 0.16 * FMath::Sin(FMath::Clamp(T / StunFor, 0.0, 1.0) * PI);
		GoalHead = FVector2D(FMath::Sin(T * 9.0) * 0.35, 0.25);
		bHeadFree = false;
		if (T >= StunFor)
		{
			SetMode(EMode::Idle);
		}
		return;
	}
	if (T < StunFor)
	{
		GoalPitch = -0.3;
		GoalHeight = StandHeight - 1.4 * S;
		GoalHead = FVector2D(0.0, -0.75);
		bHeadFree = false;
		const FXf R = Root();
		for (FLeg& Leg : Legs)
		{
			if (Leg.Bend > 0.0)
			{
				Leg.bOverride = true;
				Leg.OverrideRate = 4.0;
				FVector P = R * FVector(Leg.Hip.X * 1.6, 0.0, Leg.Hip.Z - 2.5 * S);
				P.Y = GroundAt(P) + 0.6 * S;
				Leg.OverridePos = P;
			}
		}
	}
	else if (T < StunFor + 1.5)
	{
		for (FLeg& Leg : Legs)
		{
			Leg.bOverride = false;
		}
		GoalPitch = 0.0;
		GoalHeight = StandHeight;
		bHeadFree = true;
	}
	else
	{
		SetMode(EMode::Idle);
	}
}

void ARttGauMa::Dying(double Dt)
{
	GoalHeight = 3.0 * S;
	GoalRoll = 1.15;
	GoalPitch = -0.12;
	GoalHead = FVector2D(0.3, -0.5);
	bHeadFree = false;
	if (ModeTime > 4.5)
	{
		Mode = EMode::Dead;
	}
}

// ================================================================== reacting
bool ARttGauMa::IsStaggered() const
{
	return Health && Health->Poise() && Health->Poise()->IsBroken(Now());
}

float ARttGauMa::StaggerFraction() const
{
	return Health && Health->Poise() ? static_cast<float>(Health->Poise()->Fraction(Now())) : 0.f;
}

float ARttGauMa::RespawnIn() const
{
	return bDead ? FMath::Max(0.f, static_cast<float>(RespawnAt - Now())) : 0.f;
}

bool ARttGauMa::CanInterrupt() const
{
	return Mode == EMode::Charge && bChargeOpen;
}

// Knocked down for `Seconds` with its head on the ground; `bStaggered` opens the staggered-damage
// window (+50%). A short flinch (`bDown` false) only rears.
void ARttGauMa::Stun(double Seconds, bool bStaggered, bool bDown)
{
	if (bDead)
	{
		return;
	}
	if (bStaggered && Health->Poise())
	{
		Health->Poise()->ForceBreak(Seconds, Now());
	}
	StunFor = Seconds;
	bStunDown = bDown;
	SetMode(EMode::Stunned);
}

void ARttGauMa::OnStaggered()
{
	if (bDead || Mode == EMode::Dying || Mode == EMode::Dead)
	{
		return;
	}
	Stun(FMath::Max(Health->Poise()->BrokenRemaining(Now()), 1.0), false);
	Word(RttText::Stunned, FLinearColor(1.f, 0.6f, 0.2f));
	ShakeNear(RootPos, 0.5);
}

void ARttGauMa::Interrupt()
{
	Word(RttText::Interrupted, FLinearColor(1.f, 0.85f, 0.45f));
	ShakeNear(RootPos, 0.7);
	Stun(7.0);
}

void ARttGauMa::BreakPart(int32 P)
{
	const Rtt::BossPart& Part = Parts[P];
	Word(FString(RttText::BrokenPrefix) + UTF8_TO_TCHAR(Part.Name.c_str()) + TEXT("!"), FLinearColor(1.f, 0.84f, 0.4f));
	ShakeNear(RootPos, 0.5);
	// the broken armour falls away: the antlers off the head, the bark shin off a leg
	if (Part.Id == "head" && Antlers)
	{
		Antlers->SetVisibility(false);
	}
	for (int32 i = 0; i < Legs.Num(); ++i)
	{
		if (Legs[i].Part == P && Greaves.IsValidIndex(i))
		{
			Greaves[i]->SetVisibility(false);
		}
	}
	if (Mode == EMode::Idle || Mode == EMode::Walk || Mode == EMode::Swipe || Mode == EMode::Stomp || Mode == EMode::Charge)
	{
		Stun(1.8, false, false);
	}
}

void ARttGauMa::Die()
{
	bDead = true;
	bEngaged = false;
	SetMode(EMode::Dying);
	Word(RttText::BearDown, FLinearColor(1.f, 0.86f, 0.55f));
	RespawnAt = Now() + Rtt::BearStats::RespawnSeconds;
}

void ARttGauMa::Respawn()
{
	Parts = Rtt::BearStats::Parts();
	Health->Revive(Rtt::BearStats::MaxHp);
	RootPos = HomePos;
	Yaw = HomeYaw;
	bDead = false;
	bRage = false;
	ShakeAmount = 0.0;
	if (Antlers)
	{
		Antlers->SetVisibility(true);
	}
	for (UStaticMeshComponent* G : Greaves)
	{
		G->SetVisibility(true);
	}
	SetMode(EMode::Idle);
	Height = StandHeight;
	Pitch = 0.0;
	Roll = 0.0;
	SnapToGround();
	PlantFeet();
}

void ARttGauMa::Word(const FString& Text, const FLinearColor& Color)
{
	UE_LOG(LogRttFight, Log, TEXT("Gau Ma: %s"), *Text);
	if (URttFeed* Feed = URttFeed::Get(this))
	{
		Feed->Word(Text, Color);
	}
}

// ================================================================== posing
void ARttGauMa::EasePose(double Dt)
{
	double K = 1.0 - FMath::Exp(-(PoseRate > 0.0 ? PoseRate : (Mode == EMode::Stomp ? 5.0 : 2.6)) * Dt);
	if (Mode == EMode::Dying || Mode == EMode::Dead)
	{
		K = 1.0 - FMath::Exp(-0.9 * Dt);
	}
	Height = Lerp(Height, GoalHeight, K);
	Pitch = Lerp(Pitch, GoalPitch, K);
	Roll = Lerp(Roll, GoalRoll, K);
	FVector2D Want = GoalHead;
	ARttHero* Hero = Target.Get();
	if (bHeadFree && Hero)
	{
		const FVector Hp = Bodies[2].O;
		const FVector Local = BodyXf.B.Transposed() * (HeroFeet(Hero) + Up - Hp);
		Want = FVector2D(FMath::Clamp(FMath::Atan2(-Local.X, -Local.Z), -0.75, 0.75),
			FMath::Clamp(FMath::Atan2(Local.Y, FVector2D(Local.X, Local.Z).Size()), -0.55, 0.3));
	}
	else if (bHeadFree)
	{
		Want = FVector2D(FMath::Sin(Now() * 0.25) * 0.3, -0.05);
	}
	HeadLook = FMath::Lerp(HeadLook, Want, 1.0 - FMath::Exp(-2.2 * Dt));
}

void ARttGauMa::UpdateFeet(double Dt)
{
	const FXf R = Root();
	const FVector Vel = -R.B.Z * Speed;
	const double Step = StepTime * (bRage ? 0.72 : 1.0) * StepScale;
	for (FLeg& Leg : Legs)
	{
		if (Leg.bOverride)
		{
			Leg.bStepping = false;
			Leg.Foot = FMath::Lerp(Leg.Foot, Leg.OverridePos, 1.0 - FMath::Exp(-Leg.OverrideRate * Dt));
			continue;
		}
		FVector Ideal = R * FVector(Leg.Hip.X * 1.08, 0.0, Leg.Hip.Z);
		Ideal += Vel * Step * 0.6;
		Ideal.Y = GroundAt(Ideal) + 0.6 * S;
		if (Leg.bStepping)
		{
			Leg.T += Dt / Step;
			const double K = FMath::Clamp(Leg.T, 0.0, 1.0);
			const double E = K * K * (3.0 - 2.0 * K);
			Leg.Foot = FMath::Lerp(Leg.From, Leg.To, E) + Up * FMath::Sin(K * PI) * Lift;
			if (Leg.T >= 1.0)
			{
				Leg.bStepping = false;
				Leg.Foot = Leg.To;
				Footfall(Leg);
			}
			continue;
		}
		const double Off = Flat(Ideal, Leg.Foot);
		const bool bLifted = Leg.Foot.Y - Ideal.Y > 0.4 * S;
		if ((Off > Stride * 0.5 || bLifted) && GroupCanStep(Leg.Group))
		{
			Leg.bStepping = true;
			Leg.T = 0.0;
			Leg.From = Leg.Foot;
			Leg.To = Ideal;
		}
	}
}

bool ARttGauMa::GroupCanStep(int32 Group) const
{
	for (const FLeg& L : Legs)
	{
		if (L.bStepping && L.Group != Group)
		{
			return false;
		}
	}
	return true;
}

void ARttGauMa::Footfall(const FLeg& Leg)
{
	ShakeNear(Leg.Foot, 0.22);
	for (ARttHero* H : Heroes())
	{
		const FVector F = HeroFeet(H);
		if (Flat(F, Leg.Foot) < 2.3 * S && FMath::Abs(F.Y - Leg.Foot.Y) < 2.5 * S)
		{
			HurtHero(H, 18.0, Leg.Foot, 8.0, 3.0, RttText::Step);
		}
	}
}

void ARttGauMa::UpdateBody(double Dt)
{
	const FXf R = Root();
	double G = 0.0;
	for (const FLeg& Leg : Legs)
	{
		G += Leg.bOverride ? GroundAt(Leg.Foot) : Leg.Foot.Y - 0.6 * S;
	}
	G = Legs.Num() > 0 ? G / Legs.Num() : R.O.Y;
	const double Bob = FMath::Sin(Phase) * 0.2 * S * FMath::Clamp(Speed / 2.4, 0.0, 1.0);
	FVector Pos(0.0, G - R.O.Y + Height + Bob, 0.0);
	double P = Pitch;
	double Rl = Roll;
	double YawJ = 0.0;
	if (ShakeAmount > 0.01 && Mode != EMode::Dying)
	{
		const double T = Now();
		P += FMath::Sin(T * 7.3) * 0.08 * ShakeAmount;
		Rl += FMath::Sin(T * 9.1) * 0.14 * ShakeAmount;
		YawJ = FMath::Sin(T * 5.2) * 0.1 * ShakeAmount;
		Pos.Y += FMath::Sin(T * 11.0) * 0.45 * S * ShakeAmount;
	}
	const FVector Pivot(0.0, 0.0, PitchPivotZ);
	const FXf Local = At(Pos + Pivot) * FXf{RttG::Euler(P, YawJ, Rl), FVector::ZeroVector} * At(-Pivot);
	BodyXf = R * Local;
	Bodies[0] = BodyXf;
	HeadXf = BodyXf * FXf{RttG::Euler(HeadLook.Y, HeadLook.X, 0.0), NeckBase};
	Bodies[1] = HeadXf;
	Bodies[2] = HeadXf * At(HeadOffset);
}

void ARttGauMa::PlaceLegs()
{
	const FXf R = Root();
	const FVector Back = BodyXf.B.Z.GetSafeNormal();
	const FVector Side = BodyXf.B.X.GetSafeNormal();
	for (const FLeg& Leg : Legs)
	{
		const FVector Hip = BodyXf * Leg.Hip;
		const FVector Pole = Back * Leg.Bend + Side * FMath::Sign(Leg.Hip.X) * 0.3;
		const FVector Knee = SolveKnee(Hip, Leg.Foot, Leg.L1, Leg.L2, Pole);
		Bodies[Leg.Upper] = Segment(Hip, Knee, Back);
		Bodies[Leg.Upper + 1] = Segment(Knee, Leg.Foot, Back);
		Bodies[Leg.Upper + 2] = FXf{R.B.Orthonormalized(), Leg.Foot};
	}
}

void ARttGauMa::PlaceComponents()
{
	for (const FLook& L : Looks)
	{
		L.Mesh->SetWorldTransform(RttG::XfToU(Bodies[L.Body], S));
	}
	for (const FShape& Sh : Shapes)
	{
		if (Sh.Blocker)
		{
			Sh.Blocker->SetWorldTransform(RttG::XfToU(ShapeXf(Sh)));
		}
	}
}

// ================================================================== heroes
TArray<ARttHero*> ARttGauMa::Heroes() const
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

ARttHero* ARttGauMa::NearestHero(double MaxDistance) const
{
	ARttHero* Best = nullptr;
	double BestD = MaxDistance;
	for (ARttHero* H : Heroes())
	{
		const double D = FVector::Dist(HeroFeet(H), RootPos);
		if (D < BestD)
		{
			BestD = D;
			Best = H;
		}
	}
	return Best;
}

FVector ARttGauMa::HeroFeet(const ARttHero* Hero) const
{
	return RttG::ToG(Hero->GetFeet());
}

int32 ARttGauMa::HitArea(const FVector& Center, double Radius, double Amount, double Knock, double InLift, const FString& Skill)
{
	int32 N = 0;
	for (ARttHero* H : Heroes())
	{
		const FVector F = HeroFeet(H);
		const double D = Flat(F, Center);
		if (D <= Radius && FMath::Abs(F.Y - Center.Y) < 6.0)
		{
			HurtHero(H, Rtt::AreaFalloff(Amount, D, Radius), Center, Knock, InLift, Skill);
			++N;
		}
	}
	return N;
}

// A blow that throws a hero to the ground (armour still counts against the colossus's level).
float ARttGauMa::HurtHero(ARttHero* Hero, double Amount, const FVector& From, double Knock, double InLift, const FString& Skill)
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
	Blow.Lift = static_cast<float>(InLift);
	Blow.Point = Hero->GetActorLocation();
	FVector Away = RttG::DirToU(HeroFeet(Hero) - From);
	Away.Z = 0.0;
	Blow.Direction = Away.GetSafeNormal();
	Blow.Source = this;
	Blow.SkillName = Skill;
	const float Taken = Theirs->TakeBlow(Blow);
	UE_LOG(LogRttFight, Log, TEXT("%s hits the hero: %.0f (life %.0f / %.0f)%s"), *Skill, Taken, Theirs->Hp, Theirs->MaxHp,
		Theirs->bInvulnerable ? TEXT(", dodged") : TEXT(""));
	return Taken;
}

void ARttGauMa::ShakeNear(const FVector& P, double Amount) const
{
	for (ARttHero* H : Heroes())
	{
		const double D = FVector::Dist(HeroFeet(H), P);
		H->Shake(static_cast<float>(Amount * FMath::Clamp(1.0 - D / 70.0, 0.15, 1.0)));
	}
}

void ARttGauMa::Telegraph(const FVector& G, double Radius, double Seconds) const
{
	ARttTelegraph::Spawn(GetWorld(), RttG::ToU(G), static_cast<float>(Radius * 100.0), static_cast<float>(Seconds));
}

FVector ARttGauMa::LookPoint() const
{
	return RttG::ToU(Bodies.Num() > 2 ? Bodies[2].O : RootPos + FVector(0.0, 8.0, 0.0));
}

// ================================================================== shapes and blows
FXf ARttGauMa::ShapeXf(const FShape& Sh) const
{
	return Bodies[Sh.Body] * Sh.Local;
}

FVector ARttGauMa::ClosestOn(const FShape& Sh, const FXf& Xf, const FVector& P, bool& bInside)
{
	const FVector L = Xf.Inverse() * P;
	FVector Q = FVector::ZeroVector;
	bInside = false;
	switch (Sh.Kind)
	{
	case EShape::Sphere:
		bInside = L.Size() < Sh.Radius;
		Q = L.Size() > 0.0001 ? L.GetSafeNormal() * Sh.Radius : FVector(0.0, Sh.Radius, 0.0);
		break;
	case EShape::Capsule:
	{
		const double Half = FMath::Max(0.0, Sh.Height * 0.5 - Sh.Radius);
		const FVector C(0.0, FMath::Clamp(L.Y, -Half, Half), 0.0);
		const FVector Dv = L - C;
		bInside = Dv.Size() < Sh.Radius;
		Q = C + (Dv.Size() > 0.0001 ? Dv.GetSafeNormal() * Sh.Radius : FVector(Sh.Radius, 0.0, 0.0));
		break;
	}
	case EShape::Box:
	{
		const FVector H = Sh.Size * 0.5;
		bInside = FMath::Abs(L.X) < H.X && FMath::Abs(L.Y) < H.Y && FMath::Abs(L.Z) < H.Z;
		Q = FVector(FMath::Clamp(L.X, -H.X, H.X), FMath::Clamp(L.Y, -H.Y, H.Y), FMath::Clamp(L.Z, -H.Z, H.Z));
		break;
	}
	}
	return Xf * Q;
}

FVector ARttGauMa::ClosestPoint(const FVector& Point, int32& OutPart) const
{
	const FVector P = RttG::ToG(Point);
	FVector Best = Bodies.Num() > 2 ? Bodies[2].O : RootPos;
	double BestD = TNumericLimits<double>::Max();
	OutPart = 2;
	for (const FShape& Sh : Shapes)
	{
		bool bInside = false;
		const FVector Q = ClosestOn(Sh, ShapeXf(Sh), P, bInside);
		const double D = bInside ? 0.0 : FVector::DistSquared(Q, P);
		if (D < BestD)
		{
			BestD = D;
			Best = Q;
			OutPart = Sh.Part;
		}
	}
	return RttG::ToU(Best);
}

float ARttGauMa::TakeHeroBlow(FRttBlow Blow, int32 Part)
{
	PendingPart = Part;
	const float Amount = Health->TakeBlow(Blow);
	PendingPart = -1;
	UE_LOG(LogRttFight, Log, TEXT("%s on %s: %.0f (life %.0f / %.0f, stagger %.2f%s)"), *Blow.SkillName,
		Parts.size() > static_cast<size_t>(FMath::Max(0, Part)) ? UTF8_TO_TCHAR(Parts[Part].Id.c_str()) : TEXT("?"), Amount,
		Health->Hp, Health->MaxHp, StaggerFraction(), IsStaggered() ? TEXT(", staggered") : TEXT(""));
	return Amount;
}
