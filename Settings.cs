using ModSettings;

namespace DetailedMaps
{
    internal class Settings : JsonModSettings
    {
        internal static readonly Settings Instance = new();

        [Name("Map Style")]
        [Description("Charcoal stylized maps, or full-color topographic maps. Color requires DetailedMaps.Color.zip next to the DLL.")]
        [Choice("Charcoal", "Color")]
        public int MapStyle = 0;

        protected override void OnConfirm()
        {
            base.OnConfirm();
            Implementation.OnMapStyleChanged();
        }
    }
}
