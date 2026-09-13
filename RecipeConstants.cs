namespace DetailedMaps
{
    /// <summary>
    /// Live recipe: desat/chroma + grid / noise / vignette.
    /// Levels + color balance are already in map_bg_*_base (do not alter in shader).
    /// </summary>
    internal static class RecipeConstants
    {
        public const float ColorChromaKeep = 0.80f;

        public const float GridMeters = 100.0f;
        public const float GridWidthPx = 1.5f;

        /// <summary>Panel_Map.MAP_RADIUS / detailScale (≈1) — charcoal UV ±extent.</summary>
        public const float MapExtent = 300.0f;

        public const float VignetteStrength = 0.2f;

        public const float StippleDensity = 0.012f;
        public const float StippleStrength = 55.0f;
        public const float GrainSigma = 36.0f;

        /// <summary>Longest edge for the in-game map texture (bases may be larger).</summary>
        public const int DisplayMaxEdge = 3072;
    }
}
