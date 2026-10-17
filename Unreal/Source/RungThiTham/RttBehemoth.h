// A colossus made from a Paragon monster (Rampage, added from Fab): a skeletal body with its own
// animation blueprint for walking, scaled up to colossus size, fighting by Gấu Ma's rules (the Godot
// port's gau_ma.gd): it roars when a hero comes near, closes in, and picks its move with
// Rtt::ChooseBearMove; every blow is told in beats (Rtt::Beats) by stretching the animation's
// wind-up over the tell, the wind and the hold, then letting the strike go at full speed, with a
// warning on the ground. Moves:
//   Đấm Quét    a sweeping blow in front of it (gau_ma.gd's Vồ)
//   Nện Đất     both fists down, an area (Dậm Đất)
//   Ném Đá Lớn  it tears up a rock and hurls it where the hero is going
//   Húc         head down, then it runs at the hero; a heavy hit on its head in that window
//               interrupts it and knocks it down
//   Cuồng Nộ    below half its life: faster and quicker to strike again
// Its life is one pool (Health); head, arms and legs are breakable parts found by the physics
// bodies of its bones; a stagger bar knocks it down for staggered damage. Its body, animations and
// size are in Config/DefaultGame.ini, written by Tools/Unreal/ue.cmd beast.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "RttBoss.h"
#include "Rules/RttColossus.h"

#include <vector>

#include "RttBehemoth.generated.h"

class ARttHero;
class UAnimSequenceBase;
class UPointLightComponent;
class UStaticMesh;

UCLASS(Config = Game)
class RUNGTHITHAM_API ARttBehemoth : public ACharacter, public IRttBoss
{
	GENERATED_BODY()

public:
	ARttBehemoth();

	UPROPERTY(VisibleAnywhere, Category = "Colossus")
	TObjectPtr<URttHealth> Health;

	// the Paragon body (Config/DefaultGame.ini)
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath BodyMesh;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftClassPath BodyAnimClass;
	// how much bigger than the hero-sized model the colossus is
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float BodyScale = 3.5f;
	// the model's own capsule (from its character blueprint), before BodyScale
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float CapsuleRadius = 55.f;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float CapsuleHalfHeight = 105.f;
	// the slot its blows play in, and the animation of each move
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FName MoveSlot = TEXT("DefaultSlot");
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath SwipeAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath SlamAnimation;
	// Ném Đá Lớn: tearing the rock up, then hurling it (ThrowRelease is in the toss)
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath ThrowAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath ThrowTossAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath ChargeAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath RoarAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath StunAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath FlinchAnimation;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath DeathAnimation;
	// where in each animation (0..1 of its length) the blow lands / the rock leaves the hand
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float SwipeStrike = 0.4f;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float SlamStrike = 0.45f;
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	float ThrowRelease = 0.5f;
	// a rock mesh for Ném Đá Lớn (a sphere when empty)
	UPROPERTY(Config, EditAnywhere, Category = "Body")
	FSoftObjectPath RockMesh;

	// IRttBoss
	virtual URttHealth* GetBossHealth() const override { return Health; }
	virtual FString GetBossName() const override;
	virtual bool IsEngaged() const override { return bEngaged; }
	virtual bool IsDefeated() const override { return bDead; }
	virtual bool IsRaging() const override { return bRage; }
	virtual bool IsStaggered() const override;
	virtual float StaggerFraction() const override;
	virtual float RespawnIn() const override;
	virtual FVector ClosestPoint(const FVector& Point, int32& OutPart) const override;
	virtual float TakeHeroBlow(FRttBlow Blow, int32 Part) override;
	bool CanInterrupt() const { return Mode == EMode::Charge && bChargeOpen; }

protected:
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

private:
	enum class EMode : uint8 { Idle, Walk, Swipe, Slam, Throw, Charge, Stunned, Dying, Dead };

	EMode Mode = EMode::Idle;
	double ModeTime = 0.0;
	bool bRage = false;
	bool bEngaged = false;
	bool bDead = false;
	bool bCalm = false;
	TWeakObjectPtr<ARttHero> Target;
	FVector Home = FVector::ZeroVector;
	FRotator HomeRotation = FRotator::ZeroRotator;
	std::vector<Rtt::BossPart> Parts;
	Rtt::BearCooldowns Cooldowns;
	TSet<FName> Done;
	TSet<TWeakObjectPtr<AActor>> HitThisMove;
	double SwipeSide = 1.0;
	FVector BlowAt = FVector::ZeroVector;
	FVector ChargeTo = FVector::ZeroVector;
	bool bChargeOpen = false;
	double StunFor = 7.0;
	bool bStunDown = true;
	double SlowFactor = 1.0;
	double SlowUntil = 0.0;
	double RespawnAt = 0.0;
	int32 PendingPart = -1;
	double WanderWait = 2.0;
	FVector WanderGoal = FVector::ZeroVector;

	Rtt::Beats SwipeBeats{0.18, 0.7, 0.2};
	Rtt::Beats SlamBeats{0.22, 0.93, 0.25};
	Rtt::Beats ThrowBeats{0.7, 0.65, 0.25};
	Rtt::Beats ChargeBeats{0.22, 0.68, 0.2};

	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Swipe;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Slam;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Throw;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Toss;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> ChargeAnim;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Roar;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> StunAnim;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Flinch;
	UPROPERTY(Transient)
	TObjectPtr<UAnimSequenceBase> Fall;
	UPROPERTY(Transient)
	TObjectPtr<UStaticMesh> Rock;
	UPROPERTY(Transient)
	TObjectPtr<UClass> BodyAnim;
	UPROPERTY(Transient)
	TObjectPtr<class ARttBoulder> HeldRock;
	// the interrupt cue on its head (Dauntless's rays): a pulsing gold light while Húc can be stopped
	UPROPERTY(VisibleAnywhere, Category = "Colossus")
	TObjectPtr<UPointLightComponent> Rays;

	double Now() const;
	double TimeFactor();
	void SetMode(EMode M);
	bool Once(const FName& Key);
	void Think(double Dt);
	void WalkTo(const FVector& Goal, float Speed);
	void Face(const FVector& P, double DegreesPerSecond, double Dt);
	// Plays a move's animation so its blow lands at `Beats`' strike: slow through the wind-up.
	void PlayMove(UAnimSequenceBase* Anim, float StrikeFraction, const Rtt::Beats& B);
	void RunMode(double Dt);
	void DoSwipe(double Dt);
	void DoSlam(double Dt);
	void DoThrow(double Dt);
	void DoCharge(double Dt);
	void DoStunned(double Dt);
	void Stun(double Seconds, bool bStaggered = true, bool bDown = true);
	void OnStaggered();
	void Interrupt();
	void BreakPart(int32 Part);
	void Die();
	void Respawn();
	void Word(const FString& Text, const FLinearColor& Color = FLinearColor(1.f, 0.86f, 0.55f));
	int32 PartOfBone(const FName& Bone) const;
	FVector Bone(const TCHAR* Name) const;
	FVector Ground(const FVector& P) const;

	TArray<ARttHero*> Heroes() const;
	ARttHero* NearestHero(double MaxDistance) const;
	int32 HitArea(const FVector& Center, double Radius, double Amount, double Knock, double Lift, const FString& Skill);
	float HurtHero(ARttHero* Hero, double Amount, const FVector& From, double Knock, double Lift, const FString& Skill);
	void ShakeNear(const FVector& P, double Amount) const;
	void Telegraph(const FVector& At, double Radius, double Seconds) const;
};
