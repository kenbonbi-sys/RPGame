// The forest around a colossus's clearing (Hang Gấu): trees standing thick at the clearing's edge
// and thinning up the hills, bushes at their feet, rocks in clusters, grass everywhere but worn thin
// in the den. Everything is scattered on the ground by a fixed seed when play begins, as
// instanced meshes (one per mesh kind) that fade out with distance. The meshes come from an
// environment pack named in Config/DefaultGame.ini by Tools/Unreal/ue.cmd forest.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"

#include "RttForest.generated.h"

class UHierarchicalInstancedStaticMeshComponent;
class UStaticMesh;

UCLASS(Config = Game)
class RUNGTHITHAM_API ARttForest : public AActor
{
	GENERATED_BODY()

public:
	ARttForest();

	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	TArray<FSoftObjectPath> TreeMeshes;
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	TArray<FSoftObjectPath> BushMeshes;
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	TArray<FSoftObjectPath> RockMeshes;
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	TArray<FSoftObjectPath> GrassMeshes;
	// how big each kind is drawn (the pack's own size times this, give or take)
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	float TreeScale = 1.f;
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	float BushScale = 1.f;
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	float RockScale = 1.f;
	UPROPERTY(Config, EditAnywhere, Category = "Forest")
	float GrassScale = 1.f;

	// the clearing (cm, around the actor's place: the map puts it at the clearing's middle)
	UPROPERTY(EditAnywhere, Category = "Forest")
	float ClearingRadius = 7000.f;
	UPROPERTY(EditAnywhere, Category = "Forest")
	float OuterRadius = 24000.f;
	// where the colossus keeps (grass is worn thin there)
	UPROPERTY(EditAnywhere, Category = "Forest")
	FVector DenCentre = FVector(3500.0, 0.0, 0.0);
	UPROPERTY(EditAnywhere, Category = "Forest")
	int32 Trees = 650;
	UPROPERTY(EditAnywhere, Category = "Forest")
	int32 Bushes = 900;
	UPROPERTY(EditAnywhere, Category = "Forest")
	int32 RockClusters = 55;
	UPROPERTY(EditAnywhere, Category = "Forest")
	int32 Grass = 30000;
	UPROPERTY(EditAnywhere, Category = "Forest")
	int32 Seed = 2617;

protected:
	virtual void BeginPlay() override;

private:
	struct FPlace
	{
		FVector Point;
		FVector Normal;
	};

	TMap<UStaticMesh*, UHierarchicalInstancedStaticMeshComponent*> Layers;

	bool Ground(const FVector& XY, FPlace& Out) const;
	UHierarchicalInstancedStaticMeshComponent* Layer(UStaticMesh* Mesh, bool bSolid, float CullStart, float CullEnd, bool bShadow);
	static TArray<UStaticMesh*> Load(const TArray<FSoftObjectPath>& Paths);
	void Scatter();
};
