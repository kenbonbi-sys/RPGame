// The hero, stage U0: run, sprint, jump and Lướt (a quick dash with a short invulnerability) under a
// camera that hangs behind the shoulder. The numbers are the Godot hero's (hero.gd, controls.gd) in
// centimetres; Shift tapped is Lướt, held past DodgeTapTime it sprints. The body is a stand-in until
// the Paragon hero arrives (U1).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"

#include "RttHero.generated.h"

class UCameraComponent;
class UInputAction;
class UInputMappingContext;
class USpringArmComponent;
class UStaticMeshComponent;
struct FInputActionValue;

UCLASS()
class RUNGTHITHAM_API ARttHero : public ACharacter
{
	GENERATED_BODY()

public:
	ARttHero();

	UPROPERTY(EditAnywhere, Category = "Movement")
	float RunSpeed = 540.f;
	UPROPERTY(EditAnywhere, Category = "Movement")
	float SprintSpeed = 820.f;
	UPROPERTY(EditAnywhere, Category = "Movement")
	float DashSpeed = 1000.f;
	UPROPERTY(EditAnywhere, Category = "Movement")
	float DashInvulnerable = 0.28f;
	// Seconds Shift must be held before a tap (Lướt) turns into sprinting.
	UPROPERTY(EditAnywhere, Category = "Movement")
	float DodgeTapTime = 0.22f;

	bool IsInvulnerable() const;
	bool IsSprinting() const { return bSprinting; }
	UCameraComponent* GetCamera() const { return Camera; }

protected:
	virtual void Tick(float DeltaSeconds) override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInput) override;
	virtual void PawnClientRestart() override;

private:
	UPROPERTY(VisibleAnywhere, Category = "Camera")
	TObjectPtr<USpringArmComponent> Boom;
	UPROPERTY(VisibleAnywhere, Category = "Camera")
	TObjectPtr<UCameraComponent> Camera;
	UPROPERTY(VisibleAnywhere, Category = "Body")
	TObjectPtr<UStaticMeshComponent> Body;
	UPROPERTY(VisibleAnywhere, Category = "Body")
	TObjectPtr<UStaticMeshComponent> Head;

	UPROPERTY(Transient)
	TObjectPtr<UInputMappingContext> Controls;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> MoveAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> LookAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> JumpAction;
	UPROPERTY(Transient)
	TObjectPtr<UInputAction> DodgeAction;

	double DodgeDownAt = -1.0;
	double InvulnerableUntil = 0.0;
	bool bSprinting = false;

	void BuildControls();
	void Move(const FInputActionValue& Value);
	void Look(const FInputActionValue& Value);
	void DodgePressed();
	void DodgeReleased();
	void Dash();
	double Now() const;
};
