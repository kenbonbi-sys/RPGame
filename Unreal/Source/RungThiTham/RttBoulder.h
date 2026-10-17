// Tảng Đá Lớn (the Godot port's boulder.gd): a rock a colossus tears from the ground and hurls. It
// flies on an arc under its own gravity, hurts where it lands (the thrower's area blow), then stays
// as a solid rock for a while; at most four stay on the field.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"

#include "RttBoulder.generated.h"

class UStaticMesh;
class UStaticMeshComponent;

UCLASS()
class RUNGTHITHAM_API ARttBoulder : public AActor
{
	GENERATED_BODY()

public:
	ARttBoulder();

	// Hurled from `From` to land on `To` after `Flight` seconds; `OnLand` is the thrower's blow.
	static ARttBoulder* Throw(UWorld* World, UStaticMesh* Mesh, float Radius, const FVector& From, const FVector& To, float Flight,
		TFunction<void(const FVector&)> OnLand);
	// a rock in the thrower's hand before it flies (held, no collision)
	static ARttBoulder* Hold(UWorld* World, UStaticMesh* Mesh, float Radius);
	void Launch(const FVector& From, const FVector& To, float Flight, TFunction<void(const FVector&)> OnLand);

	virtual void Tick(float DeltaSeconds) override;

private:
	UPROPERTY(VisibleAnywhere)
	TObjectPtr<UStaticMeshComponent> Rock;

	FVector Velocity = FVector::ZeroVector;
	FRotator Spin = FRotator::ZeroRotator;
	float Radius = 180.f;
	float Life = 75.f;
	bool bFlying = false;
	bool bLanded = false;
	TFunction<void(const FVector&)> Landed;

	void Land(double GroundZ);
	double GroundBelow() const;
};
