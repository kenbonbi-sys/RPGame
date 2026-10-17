// Unreal automation tests (Tools/Unreal/ue.cmd test). The full rules suite runs without the engine
// in Tools/RulesCheck; these check the same rules and data work inside the engine build, and that
// the game is put together (the game mode brings the hero with the Godot hero's numbers).
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "Camera/CameraComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Rules/RttAbility.h"
#include "Rules/RttDamage.h"
#include "Rules/RttSheet.h"
#include "Rules/RttStatus.h"
#include "Rules/RttWeapons.h"
#include "RttGameMode.h"
#include "RttHero.h"
#include "RttRules.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FRttRulesDataTest, "RungThiTham.Rules.Data",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::ProductFilter)

bool FRttRulesDataTest::RunTest(const FString& Parameters)
{
	FRttRuleSet Rules;
	FString Error;
	if (!TestTrue(TEXT("Content/Data loads ") + Error, RttLoadRules(RttDataDir(), Rules, Error)))
	{
		return false;
	}
	TestEqual(TEXT("35 armour sets as on the wiki"), static_cast<int32>(Rules.Armour.Sets.size()), 35);
	TestEqual(TEXT("level cap 40"), Rules.Tuning.MaxLevel, 40);

	Rtt::Defender Target;
	Target.Side = Rtt::Team::Enemy;
	Target.Resistances[static_cast<int>(Rtt::Element::Fire)] = -0.3;
	Rtt::Hit Fireball;
	Fireball.Amount = 56.0;
	Fireball.Type = Rtt::Element::Fire;
	Fireball.SourceTeam = Rtt::Team::Player;
	Fireball.AttackScaled = true;
	TestEqual(TEXT("C\u1ea7u L\u1eeda 56 on a -30% fire weakness lands for 73"), Rtt::ResolveDamage(Fireball, Target, Rules.Tuning, Rules.Armour, 1.0).Amount, 73.0);

	Rtt::Sheet Fighter(Rules.Tuning);
	Fighter.Class = Rtt::ReadClass(Rules.Classes["fighter"]);
	Fighter.WeaponId = Fighter.Class.Weapons.empty() ? "sword" : Fighter.Class.Weapons[0];
	Fighter.Recalculate();
	TestEqual(TEXT("a level-1 fighter has 106 life"), Fighter.HealthMax(), 106.0);

	const Rtt::ResistPoints Charrogg = Rules.Armour.Points(Rules.Armour.FullSet("charrogg"));
	TestEqual(TEXT("Charrogg takes Frost hits +20%"), Rules.Armour.Multiplier(Charrogg, Rtt::DElement::Frost), 1.2, 1e-9);

	TestEqual(TEXT("seven weapon families"), static_cast<int32>(Rules.Weapons.Families.size()), 7);
	TestEqual(TEXT("a full Tr\u1ea3m hits for 3.2x Attack"), Rtt::HeavyBlowAt(Rules.Weapons.FamilyOf("w_kiem_hong_anh").Heavy, 10.0, 1.0, 1.0).Amount, 32.0, 1e-9);
	Rtt::StatusEffects BossStatus(Rules.Tuning);
	Rtt::Poise BossPoise(300.0);
	Rtt::Link(BossStatus, BossPoise);
	BossPoise.Add(300.0, 0.0);
	TestTrue(TEXT("a full poise bar breaks into a stun"), BossPoise.IsBroken(0.0) && BossStatus.IsStunned(0.0));
	TestTrue(TEXT("abilities.json has L\u01b0\u1edbt"), Rtt::FindAbility(Rules.Abilities, "dash").HasTag(Rtt::TagMovement));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FRttGameSetupTest, "RungThiTham.Game.Setup",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::ProductFilter)

bool FRttGameSetupTest::RunTest(const FString& Parameters)
{
	const ARttGameMode* Mode = GetDefault<ARttGameMode>();
	TestTrue(TEXT("the game mode brings the hero"), Mode->DefaultPawnClass == ARttHero::StaticClass());
	const ARttHero* Hero = GetDefault<ARttHero>();
	TestNotNull(TEXT("the hero has a camera"), Hero->GetCamera());
	const UCharacterMovementComponent* Moves = Hero->GetCharacterMovement();
	TestEqual(TEXT("runs at 5.4 m/s"), Moves->MaxWalkSpeed, 540.f);
	TestEqual(TEXT("jumps at 7.4 m/s"), Moves->JumpZVelocity, 740.f);
	TestTrue(TEXT("turns to where it runs"), Moves->bOrientRotationToMovement);
	return true;
}

#endif
