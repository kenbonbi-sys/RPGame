#include "RttTelegraph.h"

#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "UObject/ConstructorHelpers.h"

ARttTelegraph::ARttTelegraph()
{
	PrimaryActorTick.bCanEverTick = true;
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Disc(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	static ConstructorHelpers::FObjectFinder<UMaterialInterface> Look(TEXT("/Game/Materials/M_Telegraph.M_Telegraph"));
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	auto MakeDisc = [&](const TCHAR* Name)
	{
		UStaticMeshComponent* C = CreateDefaultSubobject<UStaticMeshComponent>(Name);
		C->SetupAttachment(RootComponent);
		C->SetStaticMesh(Disc.Object);
		C->SetMaterial(0, Look.Object);
		C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		C->SetCastShadow(false);
		return C;
	};
	Area = MakeDisc(TEXT("Area"));
	Fill = MakeDisc(TEXT("Fill"));
}

ARttTelegraph* ARttTelegraph::Spawn(UWorld* World, const FVector& Center, float InRadius, float InSeconds)
{
	if (!World)
	{
		return nullptr;
	}
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	ARttTelegraph* T = World->SpawnActor<ARttTelegraph>(Center + FVector(0.f, 0.f, 3.f), FRotator::ZeroRotator, Params);
	if (!T)
	{
		return nullptr;
	}
	T->Radius = InRadius;
	T->Seconds = FMath::Max(0.05f, InSeconds);
	// the Cylinder is 100 cm across and 100 cm tall: a thin disc on the ground
	T->Area->SetRelativeScale3D(FVector(InRadius / 50.f, InRadius / 50.f, 0.02f));
	T->Fill->SetRelativeScale3D(FVector(0.01f, 0.01f, 0.025f));
	T->AreaLook = T->Area->CreateDynamicMaterialInstance(0);
	T->FillLook = T->Fill->CreateDynamicMaterialInstance(0);
	if (T->AreaLook)
	{
		T->AreaLook->SetScalarParameterValue(TEXT("Opacity"), 0.16f);
	}
	return T;
}

void ARttTelegraph::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Age += DeltaSeconds;
	const float K = FMath::Clamp(Age / Seconds, 0.f, 1.f);
	const float Grow = FMath::Max(0.01f, Radius / 50.f * K);
	Fill->SetRelativeScale3D(FVector(Grow, Grow, 0.025f));
	if (FillLook)
	{
		FillLook->SetScalarParameterValue(TEXT("Opacity"), 0.2f + 0.3f * K);
	}
	if (Age >= Seconds + 0.12f)
	{
		Destroy();
	}
}
