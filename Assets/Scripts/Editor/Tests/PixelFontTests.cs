using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools.Tests
{
    /// <summary>T19: the pixel fonts in Assets/Fonts (made by Tools/FontGen/make_pixel_fonts.py) cover every Vietnamese letter.</summary>
    public class PixelFontTests
    {
        [Test]
        public void TheCharsetHasAll134VietnameseLetters()
        {
            Assert.AreEqual(134, AssetFactory.VietnameseLetters.Distinct().Count());
        }

        [TestCase("Assets/Fonts/Galmuri7.ttf")]
        [TestCase("Assets/Fonts/Galmuri11.ttf")]
        public void PixelFontHasEveryVietnameseLetter(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null) Assert.Ignore($"{path} is not in the project yet: run python Tools/FontGen/make_pixel_fonts.py");
            var missing = AssetFactory.VietnameseLetters.Where(c => !font.HasCharacter(c)).ToArray();
            Assert.IsEmpty(missing, "missing: " + new string(missing));
        }
    }
}
