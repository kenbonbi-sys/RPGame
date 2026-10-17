// The ground warning before a monster's blow (the Godot port's Fx.telegraph): a faint disc as wide
// as the blow and a brighter one growing inside it, full the moment the blow lands, then gone.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"

#include "RttTelegraph.generated.h"

class UMaterialInstanceDynamic;
class UStaticMeshComponent;

UCLASS()
class RUNGTHITHAM_API ARttTelegraph : public AActor
{
	GENERATED_BODY()

public:
	ARttTelegraph();

	// A warning at `Center` (on the ground), `Radius` cm, landing in `Seconds`.
	static ARttTelegraph* Spawn(UWorld* World, const FVector& Center, float Radius, float Seconds);

	virtual void Tick(float DeltaSeconds) override;

private:
	UPROPERTY(VisibleAnywhere)
	TObjectPtr<UStaticMeshComponent> Area;
	UPROPERTY(VisibleAnywhere)
	TObjectPtr<UStaticMeshComponent> Fill;
	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> AreaLook;
	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> FillLook;

	float Radius = 100.f;
	float Seconds = 1.f;
	float Age = 0.f;
};
