#include "RttForest.h"

#include "Components/HierarchicalInstancedStaticMeshComponent.h"
#include "Engine/CollisionProfile.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"

DEFINE_LOG_CATEGORY_STATIC(LogRttForest, Log, All);

ARttForest::ARttForest()
{
	PrimaryActorTick.bCanEverTick = false;
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
}

void ARttForest::BeginPlay()
{
	Super::BeginPlay();
	Scatter();
}

TArray<UStaticMesh*> ARttForest::Load(const TArray<FSoftObjectPath>& Paths)
{
	TArray<UStaticMesh*> Out;
	for (const FSoftObjectPath& P : Paths)
	{
		if (UStaticMesh* M = Cast<UStaticMesh>(P.TryLoad()))
		{
			Out.Add(M);
		}
	}
	return Out;
}

bool ARttForest::Ground(const FVector& XY, FPlace& Out) const
{
	FHitResult Hit;
	FCollisionQueryParams Params(SCENE_QUERY_STAT(RttForestGround), true, this);
	if (!GetWorld()->LineTraceSingleByObjectType(Hit, FVector(XY.X, XY.Y, 20000.0), FVector(XY.X, XY.Y, -20000.0), FCollisionObjectQueryParams(ECC_WorldStatic), Params))
	{
		return false;
	}
	Out.Point = Hit.ImpactPoint;
	Out.Normal = Hit.ImpactNormal;
	return true;
}

UHierarchicalInstancedStaticMeshComponent* ARttForest::Layer(UStaticMesh* Mesh, bool bSolid, float CullStart, float CullEnd, bool bShadow)
{
	if (UHierarchicalInstancedStaticMeshComponent** Found = Layers.Find(Mesh))
	{
		return *Found;
	}
	UHierarchicalInstancedStaticMeshComponent* L = NewObject<UHierarchicalInstancedStaticMeshComponent>(this);
	L->SetStaticMesh(Mesh);
	L->SetMobility(EComponentMobility::Static);
	if (bSolid)
	{
		// heroes and colossi bump into trunks and rocks; the camera passes through
		L->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
		L->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
	}
	else
	{
		L->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		L->bAffectDistanceFieldLighting = false;
	}
	L->SetCullDistances(static_cast<int32>(CullStart), static_cast<int32>(CullEnd));
	L->SetCastShadow(bShadow);
	L->SetupAttachment(RootComponent);
	L->RegisterComponent();
	AddInstanceComponent(L);
	Layers.Add(Mesh, L);
	return L;
}

void ARttForest::Scatter()
{
	const TArray<UStaticMesh*> TreeSet = Load(TreeMeshes);
	const TArray<UStaticMesh*> BushSet = Load(BushMeshes);
	const TArray<UStaticMesh*> RockSet = Load(RockMeshes);
	const TArray<UStaticMesh*> GrassSet = Load(GrassMeshes);
	if (TreeSet.Num() + BushSet.Num() + RockSet.Num() + GrassSet.Num() == 0)
	{
		UE_LOG(LogRttForest, Warning, TEXT("No forest meshes named in DefaultGame.ini: add the environment pack and run ue.cmd forest"));
		return;
	}
	FRandomStream R(Seed);
	const FVector Centre = GetActorLocation();
	TMap<UStaticMesh*, TArray<FTransform>> Placed;
	auto Pick = [&R](const TArray<UStaticMesh*>& Set) { return Set[R.RandRange(0, Set.Num() - 1)]; };
	// a point in a ring around the clearing, as many per square metre near and far
	auto InRing = [&R, &Centre](double Inner, double Outer)
	{
		const double A = R.FRand() * 2.0 * PI;
		const double D = FMath::Sqrt(FMath::Lerp(Inner * Inner, Outer * Outer, static_cast<double>(R.FRand())));
		return Centre + FVector(FMath::Cos(A) * D, FMath::Sin(A) * D, 0.0);
	};

	// trees: a wall at the clearing's edge, thinning up the hills, never closer than 5.5 m apart
	TArray<FVector> Trunks;
	int32 Tries = 0;
	while (TreeSet.Num() > 0 && Trunks.Num() < Trees && Tries++ < Trees * 20)
	{
		const FVector P = InRing(ClearingRadius * 0.95, OuterRadius);
		const double D = FVector::Dist2D(P, Centre);
		const double Keep = D < ClearingRadius + 3500.0 ? 1.0 : 0.45;
		if (R.FRand() > Keep)
		{
			continue;
		}
		bool bCrowded = false;
		for (const FVector& T : Trunks)
		{
			if (FVector::DistSquared2D(T, P) < 550.0 * 550.0)
			{
				bCrowded = true;
				break;
			}
		}
		FPlace G;
		if (bCrowded || !Ground(P, G) || G.Normal.Z < 0.82)
		{
			continue;
		}
		Trunks.Add(G.Point);
		const float S = TreeScale * R.FRandRange(0.8f, 1.35f);
		const FRotator Lean(R.FRandRange(-3.f, 3.f), R.FRandRange(0.f, 360.f), R.FRandRange(-3.f, 3.f));
		Placed.FindOrAdd(Pick(TreeSet)).Add(FTransform(Lean, G.Point - FVector(0.0, 0.0, 30.0), FVector(S)));
	}

	// bushes: at the feet of trees, and along the clearing's edge
	for (int32 i = 0; BushSet.Num() > 0 && i < Bushes; ++i)
	{
		FVector P;
		if (Trunks.Num() > 0 && R.FRand() < 0.6f)
		{
			const FVector T = Trunks[R.RandRange(0, Trunks.Num() - 1)];
			const double A = R.FRand() * 2.0 * PI;
			P = T + FVector(FMath::Cos(A), FMath::Sin(A), 0.0) * R.FRandRange(180.f, 520.f);
		}
		else
		{
			P = InRing(ClearingRadius * 0.85, ClearingRadius * 1.25);
		}
		FPlace G;
		if (!Ground(P, G) || G.Normal.Z < 0.7)
		{
			continue;
		}
		const float S = BushScale * R.FRandRange(0.7f, 1.4f);
		Placed.FindOrAdd(Pick(BushSet)).Add(FTransform(FRotator(0.f, R.FRandRange(0.f, 360.f), 0.f), G.Point, FVector(S)));
	}

	// rocks: clusters up the hills, a few at the clearing's edge, sunk a little and leaning with the ground
	for (int32 c = 0; RockSet.Num() > 0 && c < RockClusters; ++c)
	{
		const FVector Heart = c < 8 ? InRing(ClearingRadius * 0.9, ClearingRadius * 1.15) : InRing(ClearingRadius * 1.1, OuterRadius);
		const int32 N = R.RandRange(2, 5);
		for (int32 k = 0; k < N; ++k)
		{
			const double A = R.FRand() * 2.0 * PI;
			const FVector P = Heart + FVector(FMath::Cos(A), FMath::Sin(A), 0.0) * R.FRandRange(0.f, 450.f);
			FPlace G;
			if (!Ground(P, G))
			{
				continue;
			}
			const float S = RockScale * (k == 0 ? R.FRandRange(1.4f, 2.6f) : R.FRandRange(0.5f, 1.3f));
			const FQuat Tilt = FQuat::FindBetweenNormals(FVector::UpVector, G.Normal);
			const FQuat Spin(FVector::UpVector, R.FRandRange(0.f, 2.f * PI));
			Placed.FindOrAdd(Pick(RockSet)).Add(FTransform(Tilt * Spin, G.Point - G.Normal * 40.0 * S, FVector(S)));
		}
	}

	// grass: everywhere, worn thin where the colossus walks
	for (int32 i = 0; GrassSet.Num() > 0 && i < Grass; ++i)
	{
		const FVector P = InRing(0.0, OuterRadius * 0.85);
		if (FVector::Dist2D(P, DenCentre) < 2200.0 && R.FRand() < 0.75f)
		{
			continue;
		}
		FPlace G;
		if (!Ground(P, G) || G.Normal.Z < 0.75)
		{
			continue;
		}
		const float S = GrassScale * R.FRandRange(0.7f, 1.3f);
		Placed.FindOrAdd(Pick(GrassSet)).Add(FTransform(FRotator(0.f, R.FRandRange(0.f, 360.f), 0.f), G.Point, FVector(S)));
	}

	int32 Count = 0;
	for (TPair<UStaticMesh*, TArray<FTransform>>& Kind : Placed)
	{
		const bool bTree = TreeSet.Contains(Kind.Key);
		const bool bRock = RockSet.Contains(Kind.Key);
		const bool bGrass = GrassSet.Contains(Kind.Key);
		UHierarchicalInstancedStaticMeshComponent* L = Layer(Kind.Key, bTree || bRock,
			bGrass ? 3500.f : (bTree ? 0.f : 9000.f), bGrass ? 6500.f : (bTree ? 0.f : 14000.f), !bGrass);
		L->AddInstances(Kind.Value, false, true);
		Count += Kind.Value.Num();
	}
	UE_LOG(LogRttForest, Log, TEXT("Forest: %d trees, %d instances in %d layers"), Trunks.Num(), Count, Layers.Num());
}
