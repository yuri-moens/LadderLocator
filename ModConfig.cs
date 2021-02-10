using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using System.Collections.Generic;

namespace LadderLocator
{
    class ModConfig
    {
        public KeybindList ToggleShaftsKey { get; set; } = KeybindList.Parse("OemTilde");
        public bool ForceShafts { get; set; } = false;
        public KeybindList ToggleHighlightTypeKey { get; set; } = KeybindList.Parse("LeftShift + OemTilde");
        public HashSet<HighlightType> HighlightTypes { get; set; } = new HashSet<HighlightType>() { HighlightType.Rectangle };
        public Color HighlightRectangleRGBA { get; set; } = Color.Lime;
        public string HighlightImageFilename { get; set; } = "cracked.png";
        public decimal HighlightAlpha { get; set; } = 0.45M;
        public KeybindList CycleAlpha { get; set; } = KeybindList.Parse("LeftAlt + OemTilde");
        public bool HighlightUsesStoneTint { get; set; } = false;
        public KeybindList ToggleTint { get; set; } = KeybindList.Parse("LeftControl + OemTilde");
    }

    enum HighlightType
    {
        Rectangle,
        Image,
        Sprite
    }
}
