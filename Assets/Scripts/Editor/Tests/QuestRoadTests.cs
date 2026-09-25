using System.Text.RegularExpressions;
using NUnit.Framework;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// The main quests are one road (players' feedback, 25/09/2026): every step needs the one
    /// before and leads to the next, the levels they are for only go up, and the last step of a
    /// region leads into the next region (the bear → the swamp's road, the snake mother → the cave,
    /// the spider queen → the steppe, Hắc Phong's chief → the snow peaks, when they open).
    /// </summary>
    public class QuestRoadTests
    {
        static readonly string[] Road =
        {
            "talk_chief", "clear_forest", "slay_bear",
            "swamp_road", "swamp_toads", "swamp_hunters", "swamp_mud", "slay_toadking", "slay_snake",
            "cave_enter", "cave_bats", "cave_spiders", "cave_golems", "cave_beetles", "cave_slimes", "cave_eyes",
            "slay_crystalgolem", "slay_queen",
            "steppe_enter", "steppe_hyenas", "steppe_eagles", "steppe_bisons",
            "steppe_scarecrows", "steppe_ironbison", "hacphong_archers", "hacphong_blades", "slay_blackwind",
        };

        static int Recommended(QuestDef q)
        {
            var m = Regex.Match(q.summary ?? "", @"Cấp đề nghị: (\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : 1;
        }

        [Test]
        public void TheMainQuestsAreOneRoadGettingHarder()
        {
            var db = AssetFactory.Database;
            QuestDef Q(string id) => db.quests.Find(q => q != null && q.id == id);
            int level = 0;
            for (int i = 0; i < Road.Length; i++)
            {
                var q = Q(Road[i]);
                Assert.NotNull(q, Road[i]);
                Assert.AreEqual(QuestKind.Main, q.kind, $"{q.id} is on the main road");
                if (i > 0) Assert.IsTrue(q.requires.Exists(r => r != null && r.id == Road[i - 1]), $"{q.id} needs {Road[i - 1]}");
                if (i + 1 < Road.Length) Assert.IsTrue(q.followUps.Exists(f => f != null && f.id == Road[i + 1]), $"{q.id} leads to {Road[i + 1]}");
                int rec = Recommended(q);
                Assert.GreaterOrEqual(rec, level, $"{q.id} is no easier than the step before");
                level = rec;
            }
            Assert.AreEqual(ObjectiveKind.Reach, Q("swamp_road").objectives[0].kind, "after the bear, the road to the swamp");
            Assert.AreEqual(ObjectiveKind.Reach, Q("cave_enter").objectives[0].kind, "after the snake mother, the cave");
            Assert.AreEqual(ObjectiveKind.Reach, Q("steppe_enter").objectives[0].kind, "after the spider queen, the steppe");
            Assert.AreNotEqual(QuestKind.Main, Q("swamp_wisps").kind, "the night's wisps are a side bounty, never in the way");
            Assert.AreNotEqual(QuestKind.Main, Q("cave_mimic").kind, "the hidden mimic is a rumour, never in the way");
        }
    }
}
