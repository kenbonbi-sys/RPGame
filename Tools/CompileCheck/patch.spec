# Members Unity 6 has but the 2021.x reference assemblies (NuGet) do not.
# One line per member; see RefPatch/Program.cs for the syntax. Add a line when the
# game starts using another Unity 6 API and check.sh reports it as missing.
#
#   prop    <assembly> <Type> <name> <Type> [static] [set]
#   getter  <assembly> <Type> <existing property> <Type>
#   method  <assembly> <Type> <name> <return Type> <param,Types|-> [static]
#   gmethod <assembly> <Type> <name> <T|T[]|Type> <param,Types|-> [static]   (generic <T> : Object)
#   field   <assembly> <Type> <name> <Type> [static]
#   movens  <assembly> <Type> <new namespace>

# UnityEngine
prop    UnityEngine.CoreModule      UnityEngine.MonoBehaviour  destroyCancellationToken  System.Threading.CancellationToken
gmethod UnityEngine.CoreModule      UnityEngine.Object         FindObjectsByType  T[]  UnityEngine.FindObjectsInactive  static
gmethod UnityEngine.CoreModule      UnityEngine.Object         FindObjectsByType  T[]  -  static
prop    UnityEngine.Physics2DModule UnityEngine.Rigidbody2D    linearVelocity   UnityEngine.Vector2  set
prop    UnityEngine.Physics2DModule UnityEngine.Rigidbody2D    linearVelocityX  float  set
prop    UnityEngine.Physics2DModule UnityEngine.Rigidbody2D    linearVelocityY  float  set
prop    UnityEngine.Physics2DModule UnityEngine.Rigidbody2D    linearDamping    float  set
prop    UnityEngine.Physics2DModule UnityEngine.Rigidbody2D    angularDamping   float  set

# UnityEditor (the reference is 2021.1)
getter  UnityEditor  UnityEditor.SerializedProperty  managedReferenceValue  object
movens  UnityEditor  UnityEditor.Experimental.SceneManagement.PrefabStageUtility  UnityEditor.SceneManagement
movens  UnityEditor  UnityEditor.Experimental.SceneManagement.PrefabStage         UnityEditor.SceneManagement
