// Reference stubs for TextMeshPro (com.unity.ugui 2.x). Signatures only.
using System;
using UnityEngine;
using UnityEngine.UI;
namespace TMPro
{
    public enum TextAlignmentOptions { TopLeft = 257, Top = 258, TopRight = 260, TopJustified = 264, TopFlush = 272, TopGeoAligned = 288, Left = 513, Center = 514, Right = 516, Justified = 520, Flush = 528, CenterGeoAligned = 544, BottomLeft = 1025, Bottom = 1026, BottomRight = 1028, BottomJustified = 1032, BottomFlush = 1040, BottomGeoAligned = 1056, BaselineLeft = 2049, Baseline = 2050, BaselineRight = 2052, BaselineJustified = 2056, BaselineFlush = 2064, BaselineGeoAligned = 2080, MidlineLeft = 4097, Midline = 4098, MidlineRight = 4100, MidlineJustified = 4104, MidlineFlush = 4112, MidlineGeoAligned = 4128, CaplineLeft = 8193, Capline = 8194, CaplineRight = 8196, CaplineJustified = 8200, CaplineFlush = 8208, CaplineGeoAligned = 8224, Converted = 65535 }
    public enum HorizontalAlignmentOptions { Left = 1, Center = 2, Right = 4, Justified = 8, Flush = 16, Geometry = 32 }
    public enum VerticalAlignmentOptions { Top = 256, Middle = 512, Bottom = 1024, Baseline = 2048, Geometry = 4096, Capline = 8192 }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64, Superscript = 128, Subscript = 256, Highlight = 512 }
    public enum FontWeight { Thin = 100, ExtraLight = 200, Light = 300, Regular = 400, Medium = 500, SemiBold = 600, Bold = 700, Heavy = 800, Black = 900 }
    public enum TextOverflowModes { Overflow = 0, Ellipsis = 1, Masking = 2, Truncate = 3, ScrollRect = 4, Page = 5, Linked = 6 }
    public enum TextWrappingModes { NoWrap = 0, Normal = 1, PreserveWhitespace = 2, PreserveWhitespaceNoWrap = 3 }
    public enum TextRenderFlags { DontRender = 0, Render = 255 }
    public enum AtlasPopulationMode { Static = 0, Dynamic = 1, DynamicOS = 2 }
    public class TMP_Asset : ScriptableObject { public int hashCode; public Material material; public int materialHashCode; }
    public class TMP_FontAsset : TMP_Asset
    {
        public System.Collections.Generic.List<TMP_FontAsset> fallbackFontAssetTable;
        public AtlasPopulationMode atlasPopulationMode { get; set; }
        public Texture2D[] atlasTextures { get; set; }
        public Texture2D atlasTexture => null;
        public bool isMultiAtlasTexturesEnabled { get; set; }
        public UnityEngine.TextCore.FaceInfo faceInfo { get; set; }
        public static TMP_FontAsset CreateFontAsset(Font font) => null;
        public static TMP_FontAsset CreateFontAsset(Font font, int samplingPointSize, int atlasPadding, UnityEngine.TextCore.LowLevel.GlyphRenderMode renderMode, int atlasWidth, int atlasHeight, AtlasPopulationMode atlasPopulationMode = AtlasPopulationMode.Dynamic, bool enableMultiAtlasSupport = true) => null;
        public bool HasCharacter(char character, bool searchFallbacks = false, bool tryAddCharacter = false) => false;
        public bool HasCharacters(string text) => false;
        public bool TryAddCharacters(string characters, bool includeFontFeatures = false) => false;
        public bool TryAddCharacters(string characters, out string missingCharacters, bool includeFontFeatures = false) { missingCharacters = null; return false; }
        public void ClearFontAssetData(bool setAtlasSizeToZero = false) { }
        public Font sourceFontFile => null;
    }
    public class TMP_SpriteAsset : TMP_Asset { }
    public class TMP_StyleSheet : ScriptableObject { }
    public class TMP_ColorGradient : ScriptableObject { }
    public class TMP_Settings : ScriptableObject
    {
        public static TMP_Settings instance => null;
        public static TMP_FontAsset defaultFontAsset { get; set; }
        public static System.Collections.Generic.List<TMP_FontAsset> fallbackFontAssets { get; set; }
        public static TMP_SpriteAsset defaultSpriteAsset { get; set; }
        public static bool enableWordWrapping => true;
        public static bool isTextObjectScaleStatic { get; set; }
    }
    public struct TMP_CharacterInfo { public char character; public int index; public bool isVisible; public Vector3 bottomLeft, topLeft, topRight, bottomRight; public int materialReferenceIndex, vertexIndex; }
    public class TMP_TextInfo { public int characterCount, spriteCount, spaceCount, wordCount, linkCount, lineCount, pageCount, materialCount; public TMP_CharacterInfo[] characterInfo; public TMP_WordInfo[] wordInfo; public TMP_LinkInfo[] linkInfo; public TMP_LineInfo[] lineInfo; public TMP_MeshInfo[] meshInfo; }
    public struct TMP_WordInfo { public int firstCharacterIndex, lastCharacterIndex, characterCount; public string GetWord() => null; }
    public struct TMP_LinkInfo { public int linkIdFirstCharacterIndex, linkIdLength, linkTextfirstCharacterIndex, linkTextLength; public string GetLinkID() => null; public string GetLinkText() => null; }
    public struct TMP_LineInfo { public int characterCount, visibleCharacterCount, firstCharacterIndex, lastCharacterIndex; }
    public struct TMP_MeshInfo { public Mesh mesh; public Vector3[] vertices; public Color32[] colors32; }
    public abstract class TMP_Text : MaskableGraphic
    {
        public virtual string text { get; set; }
        public TMP_FontAsset font { get; set; }
        public virtual Material fontSharedMaterial { get; set; }
        public virtual Material fontMaterial { get; set; }
        public float fontSize { get; set; }
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public bool enableAutoSizing { get; set; }
        public FontStyles fontStyle { get; set; }
        public FontWeight fontWeight { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public HorizontalAlignmentOptions horizontalAlignment { get; set; }
        public VerticalAlignmentOptions verticalAlignment { get; set; }
        public TextWrappingModes textWrappingMode { get; set; }
        [Obsolete] public bool enableWordWrapping { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public bool richText { get; set; }
        public float alpha { get; set; }
        public Color32 color32 { get; set; }
        public bool isTextObjectScaleStatic { get; set; }
        public float characterSpacing { get; set; }
        public float wordSpacing { get; set; }
        public float lineSpacing { get; set; }
        public float paragraphSpacing { get; set; }
        public Vector4 margin { get; set; }
        public int maxVisibleCharacters { get; set; }
        public int maxVisibleWords { get; set; }
        public int maxVisibleLines { get; set; }
        public int firstVisibleCharacter { get; set; }
        public bool enableVertexGradient { get; set; }
        public Color32 faceColor { get; set; }
        public Color32 outlineColor { get; set; }
        public float outlineWidth { get; set; }
        public bool extraPadding { get; set; }
        public bool parseCtrlCharacters { get; set; }
        public bool isOverlay { get; set; }
        public bool autoSizeTextContainer { get; set; }
        public virtual float preferredWidth => 0;
        public virtual float preferredHeight => 0;
        public virtual float flexibleWidth => -1;
        public virtual float flexibleHeight => -1;
        public virtual float minWidth => 0;
        public virtual float minHeight => 0;
        public virtual float maxWidth => 0;
        public virtual float maxHeight => 0;
        public virtual int layoutPriority => 0;
        public float renderedWidth => 0;
        public float renderedHeight => 0;
        public TMP_TextInfo textInfo => null;
        public bool havePropertiesChanged { get; set; }
        public TMP_SpriteAsset spriteAsset { get; set; }
        public Bounds bounds => default;
        public Bounds textBounds => default;
        public int pageToDisplay { get; set; }
        public virtual void ForceMeshUpdate(bool ignoreActiveState = false, bool forceTextReparsing = false) { }
        public void SetText(string sourceText) { }
        public void SetText(string sourceText, float arg0) { }
        public void SetText(string sourceText, float arg0, float arg1) { }
        public void SetText(System.Text.StringBuilder sourceText) { }
        public void SetCharArray(char[] sourceText) { }
        public void SetCharArray(char[] sourceText, int start, int length) { }
        public Vector2 GetPreferredValues() => default;
        public Vector2 GetPreferredValues(float width, float height) => default;
        public Vector2 GetPreferredValues(string text) => default;
        public Vector2 GetPreferredValues(string text, float width, float height) => default;
        public TMP_TextInfo GetTextInfo(string text) => null;
        public virtual void UpdateVertexData() { }
        public string GetParsedText() => null;
    }
    public class TextMeshProUGUI : TMP_Text, ILayoutElement
    {
        public virtual void CalculateLayoutInputHorizontal() { }
        public virtual void CalculateLayoutInputVertical() { }
    }
    public class TextMeshPro : TMP_Text
    {
        public int sortingOrder { get; set; }
        public int sortingLayerID { get; set; }
        public new Renderer renderer => null;
        public MeshFilter meshFilter => null;
    }
    public class TMP_InputField : Selectable
    {
        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public string text { get; set; }
        public TMP_Text textComponent { get; set; }
        public RectTransform textViewport { get; set; }
        public Graphic placeholder { get; set; }
        public int characterLimit { get; set; }
        public LineType lineType { get; set; }
        public ContentType contentType { get; set; }
        public char asteriskChar { get; set; }
        public Color caretColor { get; set; }
        public Color selectionColor { get; set; }
        public UnityEngine.Events.UnityEvent<string> onSubmit;
        public UnityEngine.Events.UnityEvent<string> onValueChanged;
        public UnityEngine.Events.UnityEvent<string> onEndEdit;
        public bool isFocused => false;
        public void ActivateInputField() { }
        public void DeactivateInputField(bool clearSelection = false) { }
    }
}
