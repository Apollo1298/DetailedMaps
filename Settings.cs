using ModSettings;

namespace DetailedMaps
{
    internal class Settings : JsonModSettings
    {
        internal static readonly Settings Instance = new();

        [Name("Map Style")]
        [Description(
            "Charcoal (full desat) or Color (chroma keep 0.80). " +
            "Levels and color balance are prebaked into the base maps.")]
        [Choice("Charcoal", "Color")]
        public int MapStyle = 0;

        [Name("Grid")]
        [Description("100 m world grid.")]
        public bool Grid = true;

        [Name("Noise")]
        [Description("Stipple + paper grain (generator defaults).")]
        public bool Noise = true;

        protected override void OnConfirm()
        {
            base.OnConfirm();
            Implementation.OnSettingsChanged();
        }
    }
}
