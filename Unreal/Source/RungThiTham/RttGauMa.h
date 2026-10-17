// Gấu Ma Rừng Già, the forest's colossus (the Godot port's gau_ma.gd and colossus.gd): a bear the
// size of a hill walking on four procedurally stepping legs, its body built from the Godot sculpt
// (one mesh per part, placed every frame by the same maths). The MVP fight:
//   Vồ        a paw swipe in front of it
//   Dậm Đất   it rears and slams both front paws down (an area)
//   Húc       head down, rays on it, then it rams; a heavy hit on its head in that window
//             interrupts it and knocks it down
//   Giẫm      its feet hurt whoever they come down on
//   Cuồng Nộ  below half its life: faster, quicker to strike again
// Its life is one pool every hit lowers (Health); the head and the legs are breakable parts; a
// stagger bar (Poise) fills from heroes' hits and knocks it down; a Shock proc paralyses it, a
// Frost proc slows everything it does. Climbing, weak points, the rock throw and the pounce are
// the Godot port's still to bring over.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "RttBoss.h"
#include "RttGodotSpace.h"
#include "RttHealth.h"
#include "Rules/RttColossus.h"

#include <vector>

#include "RttGauMa.generated.h"

class ARttHero;
class UMaterialInstanceDynamic;
class UPrimitiveComponent;
class UStaticMesh;
class UStaticMeshComponent;

UCLASS()
class RUNGTHITHAM_API ARttGauMa : public AActor, public IRttBoss
{
	GENERATED_BODY()

public:
	ARttGauMa();

	UPROPERTY(VisibleAnywhere, Category = "Colossus")
	TObjectPtr<URttHealth> Health;

	// IRttBoss: what the HUD shows and where heroes' blows land
	virtual URttHealth* GetBossHealth() const override { return Health; }
	virtual FString GetBossName() const override;
	virtual bool IsEngaged() const override { return bEngaged; }
	virtual bool IsDefeated() const override { return bDead; }
	virtual bool IsRaging() const override { return bRage; }
	virtual bool IsStaggered() const override;
	virtual float StaggerFraction() const override;
	virtual float RespawnIn() const override;
	const std::vector<Rtt::BossPart>& GetParts() const { return Parts; }
	// the head, in Unreal space
	FVector LookPoint() const;

	// The closest spot on its body to `Point` and the part it is on (Unreal space).
	virtual FVector ClosestPoint(const FVector& Point, int32& OutPart) const override;
	// A hero's blow landing at Blow.Point on `Part` (from ClosestPoint).
	virtual float TakeHeroBlow(FRttBlow Blow, int32 Part) override;
	// A heavy hit on the head now would interrupt Húc.
	bool CanInterrupt() const;

protected:
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

private:
	enum class EMode : uint8 { Idle, Walk, Swipe, Stomp, Charge, Stunned, Dying, Dead };

	struct FLeg
	{
		FString Name;
		int32 Part = 0;
		FVector Hip = FVector::ZeroVector;
		double L1 = 4.0, L2 = 4.0, R1 = 1.4, R2 = 1.2, Bend = 1.0;
		int32 Group = 0;
		FVector Foot = FVector::ZeroVector;
		bool bStepping = false;
		FVector From = FVector::ZeroVector;
		FVector To = FVector::ZeroVector;
		double T = 0.0;
		bool bOverride = false;
		FVector OverridePos = FVector::ZeroVector;
		double OverrideRate = 10.0;
		// bodies 3 + 3 × leg: upper, lower, paw
		int32 Upper = 0;
	};

	enum class EShape : uint8 { Sphere, Capsule, Box };

	// A collider in its body's frame (Godot space, metres): what heroes' blows find and what blocks.
	struct FShape
	{
		int32 Body = 0;
		EShape Kind = EShape::Sphere;
		RttG::FXf Local;
		double Radius = 1.0;
		double Height = 2.0;
		FVector Size = FVector::OneVector;
		int32 Part = 2;
		UPrimitiveComponent* Blocker = nullptr;
	};

	// a mesh on a body (bear units, scaled by S)
	struct FLook
	{
		int32 Body = 0;
		UStaticMeshComponent* Mesh = nullptr;
	};

	EMode Mode = EMode::Idle;
	double ModeTime = 0.0;
	bool bRage = false;
	bool bEngaged = false;
	bool bDead = false;
	// -RttBearIdle (screenshots): it stands where it is and ignores heroes
	bool bCalm = false;
	TWeakObjectPtr<ARttHero> Target;

	// the root in Godot space: the ground under its middle, and its heading
	FVector RootPos = FVector::ZeroVector;
	double Yaw = 0.0;
	FVector HomePos = FVector::ZeroVector;
	double HomeYaw = 0.0;

	double Height = 0.0;
	double Pitch = 0.0;
	double Roll = 0.0;
	double PitchPivotZ = 0.0;
	FVector2D HeadLook = FVector2D::ZeroVector;
	RttG::FXf BodyXf;
	RttG::FXf HeadXf;
	// torso, neck, head, then upper / lower / paw of each leg
	TArray<RttG::FXf> Bodies;
	TArray<FLeg> Legs;
	TArray<FShape> Shapes;
	TArray<FLook> Looks;
	std::vector<Rtt::BossPart> Parts;
	UStaticMeshComponent* Antlers = nullptr;
	TArray<UStaticMeshComponent*> Greaves;

	Rtt::BearCooldowns Cooldowns;
	double Speed = 0.0;
	double Phase = 0.0;
	FVector WanderGoal = FVector::ZeroVector;
	double WanderWait = 2.0;
	double GoalHeight = 0.0;
	double GoalPitch = 0.0;
	double GoalRoll = 0.0;
	FVector2D GoalHead = FVector2D::ZeroVector;
	bool bHeadFree = true;
	double PoseRate = 0.0;
	double ShakeAmount = 0.0;
	TSet<FName> Done;
	TSet<TWeakObjectPtr<AActor>> HitThisMove;
	double SwipeSide = 1.0;
	FVector StompPoint = FVector::ZeroVector;
	FVector ChargeFrom = FVector::ZeroVector;
	FVector ChargeTo = FVector::ZeroVector;
	bool bChargeOpen = false;
	double StepScale = 1.0;
	double StunFor = 7.0;
	bool bStunDown = true;
	double SlowFactor = 1.0;
	double SlowUntil = 0.0;
	double RespawnAt = 0.0;
	double FlashUntil = 0.0;
	int32 PendingPart = -1;

	Rtt::Beats SwipeBeats{0.18, 0.7, 0.2};
	Rtt::Beats StompBeats{0.22, 0.93, 0.25};
	Rtt::Beats ChargeBeats{0.22, 0.68, 0.2};

	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> Skin;
	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> Eyes;
	UPROPERTY(Transient)
	TObjectPtr<UMaterialInstanceDynamic> Marks;

	// building
	UStaticMeshComponent* AddLook(int32 Body, UStaticMesh* Mesh, const TCHAR* Name);
	void AddShape(int32 Body, EShape Kind, const RttG::FXf& Local, double Radius, double Height, const FVector& Size, int32 Part, bool bBlock);
	void AddLeg(const TCHAR* Name, int32 Part, const FVector& Hip, double L1, double L2, double R1, double R2, double Bend, int32 Group);

	// every frame
	double Now() const;
	double TimeFactor();
	RttG::FXf Root() const;
	double GroundAt(const FVector& G) const;
	void SnapToGround();
	void PlantFeet();
	void SetMode(EMode M);
	bool Once(const FName& Key);
	void Think(double Dt);
	void Wander(double Dt);
	void WalkTo(const FVector& Goal, double WalkSpeed, double StopAt, double Dt);
	void MoveForward(double Dist);
	void Face(const FVector& P, double Rate, double Dt);
	void RunMode(double Dt);
	void Swipe(double Dt);
	void Stomp(double Dt);
	void Charge(double Dt);
	void Ram();
	void Stunned(double Dt);
	void Dying(double Dt);
	void EasePose(double Dt);
	void UpdateFeet(double Dt);
	bool GroupCanStep(int32 Group) const;
	void Footfall(const FLeg& Leg);
	void UpdateBody(double Dt);
	void PlaceLegs();
	void PlaceComponents();
	void UpdateCues(double Dt);

	// reacting
	void Stun(double Seconds, bool bStaggered = true, bool bDown = true);
	void OnStaggered();
	void Interrupt();
	void BreakPart(int32 Part);
	void Die();
	void Respawn();
	void Word(const FString& Text, const FLinearColor& Color = FLinearColor(1.f, 0.86f, 0.55f));

	// heroes
	TArray<ARttHero*> Heroes() const;
	ARttHero* NearestHero(double MaxDistance) const;
	FVector HeroFeet(const ARttHero* Hero) const;
	int32 HitArea(const FVector& Center, double Radius, double Amount, double Knock, double Lift, const FString& Skill);
	float HurtHero(ARttHero* Hero, double Amount, const FVector& From, double Knock, double Lift, const FString& Skill);
	void ShakeNear(const FVector& P, double Amount) const;
	void Telegraph(const FVector& G, double Radius, double Seconds) const;

	// shapes
	RttG::FXf ShapeXf(const FShape& S) const;
	static FVector ClosestOn(const FShape& S, const RttG::FXf& Xf, const FVector& P, bool& bInside);
};
