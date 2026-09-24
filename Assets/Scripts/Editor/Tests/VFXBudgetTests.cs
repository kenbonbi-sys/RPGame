using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// VFX budget of plan §13 (T21). The Light2D limit is enforced; particle peaks are simulated for
    /// every effect and the ones over budget are logged for the weekly VFX review, not failed.
    /// </summary>
    public class VFXBudgetTests
    {
        [Test]
        public void EveryEffectStaysWithinTheLightBudget()
        {
            var lib = VFXFactory.Library;
            Assert.NotNull(lib, "Assets/Data/VFXLibrary.asset");
            foreach (var e in lib.entries.Where(e => e != null && e.prefab != null))
                Assert.LessOrEqual(VFXGallery.Lights(e.prefab), lib.LightLimit(e.id), e.id);
        }

        [Test]
        public void ParticleReportMeasuresEveryEffect()
        {
            var lib = VFXFactory.Library;
            var rows = VFXGalleryBuilder.MeasureAll();
            Assert.AreEqual(lib.entries.Count(e => e != null && e.prefab != null), rows.Count);
            Assert.IsTrue(rows.Any(r => r.peak > 0), "the simulation produced particles");
            var over = rows.Where(r => r.peak > r.particleLimit).Select(r => $"{r.id} {r.peak}/{r.particleLimit}").ToList();
            if (over.Count > 0) Debug.LogWarning("[VFX] Over the particle budget (see the VFX Gallery): " + string.Join(", ", over));
        }

        [Test]
        public void GalleryPagesAreCentredGrids()
        {
            var go = new GameObject("gallery");
            try
            {
                var g = go.AddComponent<VFXGallery>();
                Assert.AreEqual(new Vector3(-g.spacing, g.spacing, 0f), g.SlotPosition(0), "top left");
                Assert.AreEqual(Vector3.zero, g.SlotPosition(4), "centre of the 3×3 page");
                Assert.AreEqual(new Vector3(g.spacing, -g.spacing, 0f), g.SlotPosition(8), "bottom right");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UltimatesGetTwiceTheBudget()
        {
            var lib = ScriptableObject.CreateInstance<VFXLibrary>();
            try
            {
                Assert.AreEqual(150, lib.ParticleLimit("hit_spark"));
                Assert.AreEqual(300, lib.ParticleLimit("lightning_strike"), "Lôi Phạt is the Tuyệt kỹ");
                Assert.AreEqual(2, lib.LightLimit("storm_circle"));
            }
            finally
            {
                Object.DestroyImmediate(lib);
            }
        }
    }
}
