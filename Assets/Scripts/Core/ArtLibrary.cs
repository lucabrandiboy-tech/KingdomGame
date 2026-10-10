using System;
using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// The baked art: texture sheets made by KingdomGame > Build Game Scenes (in
    /// Assets/Art/Generated) and where each picture sits in them. The game turns
    /// these into sprites with <see cref="ArtSprites"/>. Don't edit it by hand:
    /// change the art code and build the scenes again.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Art Library", fileName = "ArtLibrary")]
    public sealed class ArtLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Sheet
        {
            public string name;
            public Texture2D albedo;
            [Tooltip("Normal map with the same layout, or none.")]
            public Texture2D normals;
            [Tooltip("Whether this sheet was baked with a normal map (if it's missing now, it's baked again).")]
            public bool hasNormals;
        }

        [Serializable]
        public struct Entry
        {
            public string key;
            public int sheet;
            public RectInt rect;
            public Vector2 pivot;
            public float pixelsPerTile;
        }

        [Tooltip("Which art version and wall colors this was baked from. Build Game Scenes bakes again when they change.")]
        public string stamp;
        public Sheet[] sheets = new Sheet[0];
        public Entry[] entries = new Entry[0];
    }
}
