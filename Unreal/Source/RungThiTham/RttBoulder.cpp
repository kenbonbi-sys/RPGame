#include "RttBoulder.h"

#include "Components/StaticMeshComponent.h"
#include "Engine/CollisionProfile.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	// boulder.gd: 16 m/s² of gravity, four rocks at most
	constexpr double RockGravity = 1600.0;
	constexpr int32 MaxRocks = 4;
}

ARttBoulder::ARttBoulder()
{
	PrimaryActorTick.bCanEverTick = true;
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	Rock = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Rock"));
	RootComponent = Rock;
	Rock->SetStaticMesh(Sphere.Object);
	Rock->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

ARttBoulder* ARttBoulder::Hold(UWorld* World, UStaticMesh* Mesh, float InRadius)
{
	if (!World)
	{
		return nullptr;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	ARttBoulder* B = World->SpawnActor<ARttBoulder>(FVector::ZeroVector, FRotator(FMath::FRandRange(0.f, 360.f), FMath::FRandRange(0.f, 360.f), 0.f), Params);
	if (!B)
	{
		return nullptr;
	}
	B->Radius = InRadius;
	if (Mesh)
	{
		B->Rock->SetStaticMesh(Mesh);
	}
	// fit the mesh to the rock's size whatever it was modelled at
	const float Size = FMath::Max(1.f, static_cast<float>(B->Rock->GetStaticMesh() ? B->Rock->GetStaticMesh()->GetBounds().SphereRadius : 50.f));
	B->Rock->SetWorldScale3D(FVector(InRadius / Size));
	return B;
}

ARttBoulder* ARttBoulder::Throw(UWorld* World, UStaticMesh* Mesh, float InRadius, const FVector& From, const FVector& To, float Flight,
	TFunction<void(const FVector&)> OnLand)
{
	ARttBoulder* B = Hold(World, Mesh, InRadius);
	if (B)
	{
		B->Launch(From, To, Flight, MoveTemp(OnLand));
	}
	return B;
}

void ARttBoulder::Launch(const FVector& From, const FVector& To, float Flight, TFunction<void(const FVector&)> OnLand)
{
	SetActorLocation(From);
	Landed = MoveTemp(OnLand);
	Velocity = (To - From) / Flight;
	Velocity.Z += 0.5 * RockGravity * Flight;
	Spin = FRotator(FMath::FRandRange(-120.f, 120.f), FMath::FRandRange(-120.f, 120.f), FMath::FRandRange(-120.f, 120.f));
	bFlying = true;
}

double ARttBoulder::GroundBelow() const
{
	const FVector P = GetActorLocation();
	FHitResult Hit;
	if (GetWorld()->LineTraceSingleByObjectType(Hit, P + FVector(0.0, 0.0, 2000.0), P - FVector(0.0, 0.0, 20000.0), FCollisionObjectQueryParams(ECC_WorldStatic)))
	{
		return Hit.ImpactPoint.Z;
	}
	return 0.0;
}

void ARttBoulder::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!bFlying)
	{
		// held in a hand the thrower moves it; once it lies on the ground it lasts a while
		if (bLanded)
		{
			Life -= DeltaSeconds;
			if (Life <= 0.f)
			{
				Destroy();
			}
		}
		return;
	}
	Velocity.Z -= RockGravity * DeltaSeconds;
	SetActorLocation(GetActorLocation() + Velocity * DeltaSeconds);
	AddActorLocalRotation(Spin * DeltaSeconds);
	const double Ground = GroundBelow();
	if (Velocity.Z < 0.0 && GetActorLocation().Z - Radius * 0.6 <= Ground)
	{
		Land(Ground);
	}
}

void ARttBoulder::Land(double GroundZ)
{
	bFlying = false;
	bLanded = true;
	Velocity = FVector::ZeroVector;
	SetActorLocation(FVector(GetActorLocation().X, GetActorLocation().Y, GroundZ + Radius * 0.45));
	// it stays as a solid rock
	Rock->SetCollisionProfileName(UCollisionProfile::BlockAllDynamic_ProfileName);
	Rock->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	if (Landed)
	{
		Landed(GetActorLocation());
		Landed = nullptr;
	}
	// at most four rocks stay: the oldest crumbles
	TArray<ARttBoulder*> Lying;
	for (TActorIterator<ARttBoulder> It(GetWorld()); It; ++It)
	{
		if (It->bLanded)
		{
			Lying.Add(*It);
		}
	}
	Lying.Sort([](const ARttBoulder& A, const ARttBoulder& B) { return A.Life < B.Life; });
	for (int32 i = 0; i < Lying.Num() - MaxRocks; ++i)
	{
		Lying[i]->Destroy();
	}
}
