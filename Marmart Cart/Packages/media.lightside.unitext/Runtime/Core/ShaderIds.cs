using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Cached <see cref="Shader.PropertyToID"/> constants for every shader property the engine
    /// touches from C#, grouped by shader family — the single home for property names so ids
    /// never drift between call sites.
    /// </summary>
    internal static class ShaderIds
    {
        /// <summary>Lit text surface shaders.</summary>
        internal static class Lit
        {
            public static readonly int LightInfluence = Shader.PropertyToID("_LightInfluence");
        }

        /// <summary>User materials cloned by <see cref="MaterialModifier"/>.</summary>
        internal static class Custom
        {
            /// <summary>Name prefix that marks a shader property as a per-text UV slot; the suffix names the slot (<c>Uv2</c>, <c>Uv3</c>) or one of its components (<c>Uv2X</c>…<c>Uv3W</c>).</summary>
            public const string InstPrefix = "_LightSideInst";

            public static readonly int MeshPadding = Shader.PropertyToID("_LightSideMeshPadding");
        }

        /// <summary>Global glyph transform table binding (<c>Shader.SetGlobalTexture</c>).</summary>
        internal static class GlyphTable
        {
            public static readonly int Table = Shader.PropertyToID("_LightSideGlyphTable");
        }

        /// <summary>Editor font-atlas preview shader.</summary>
        internal static class AtlasPreview
        {
            public static readonly int SliceIndex = Shader.PropertyToID("_SliceIndex");
            public static readonly int Mode = Shader.PropertyToID("_Mode");
            public static readonly int Rendered = Shader.PropertyToID("_Rendered");
        }
    }
}
