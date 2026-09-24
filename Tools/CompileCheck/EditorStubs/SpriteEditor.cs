// Reference stubs for com.unity.2d.sprite editor API (Unity.2D.Sprite.Editor).
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEditor
{
    [Serializable]
    public class SpriteRect
    {
        public string name { get; set; }
        public Vector2 pivot { get; set; }
        public SpriteAlignment alignment { get; set; }
        public Vector4 border { get; set; }
        public Rect rect { get; set; }
        public string spriteName { get; set; }
        public GUID spriteID { get; set; }
        public long internalID { get; set; }
    }
}
namespace UnityEditor.U2D.Sprites
{
    public interface ISpriteEditorDataProvider
    {
        SpriteImportMode spriteImportMode { get; }
        float pixelsPerUnit { get; }
        UnityEngine.Object targetObject { get; }
        SpriteRect[] GetSpriteRects();
        void SetSpriteRects(SpriteRect[] spriteRects);
        void Apply();
        void InitSpriteEditorDataProvider();
        T GetDataProvider<T>() where T : class;
        bool HasDataProvider(Type type);
    }
    public interface ITextureDataProvider { Texture2D texture { get; } Texture2D previewTexture { get; } void GetTextureActualWidthAndHeight(out int width, out int height); Texture2D GetReadableTexture2D(); }
    public interface ISpriteOutlineDataProvider { List<Vector2[]> GetOutlines(GUID guid); void SetOutlines(GUID guid, List<Vector2[]> data); float GetTessellationDetail(GUID guid); void SetTessellationDetail(GUID guid, float value); }
    public interface ISpritePhysicsOutlineDataProvider { List<Vector2[]> GetOutlines(GUID guid); void SetOutlines(GUID guid, List<Vector2[]> data); float GetTessellationDetail(GUID guid); void SetTessellationDetail(GUID guid, float value); }
    public struct SpriteNameFileIdPair : IEquatable<SpriteNameFileIdPair>
    {
        public SpriteNameFileIdPair(string name, GUID fileId) { this.name = name; this.fileId = fileId; }
        public string name { get; set; }
        public GUID fileId { get; set; }
        public GUID GetFileGUID() => fileId;
        public bool Equals(SpriteNameFileIdPair other) => false;
    }
    public interface ISpriteNameFileIdDataProvider { IEnumerable<SpriteNameFileIdPair> GetNameFileIdPairs(); void SetNameFileIdPairs(IEnumerable<SpriteNameFileIdPair> nameFileIdPairs); }
    public class SpriteDataProviderFactories
    {
        public void Init() { }
        public ISpriteEditorDataProvider GetSpriteEditorDataProviderFromObject(UnityEngine.Object obj) => null;
    }
}
