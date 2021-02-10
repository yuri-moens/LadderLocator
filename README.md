This mod for Stardew Valley will mark the stones which will spawn a ladder or shaft to a lower level. If you can't see any marking it simply means you have bad luck and no stone will generate a ladder or shaft. Just clear some stones until the RNG is in your favor and the marks will eventually appear.

## Install
1. [Install the latest version of SMAPI](https://smapi.io/).
2. Install [this mod from Nexus mods](https://www.nexusmods.com/stardewvalley/mods/3094).
3. Run the game using SMAPI.

## Compatibility
* Works with Stardew Valley 1.5 on Linux/Mac/Windows.
* Works in single player and multiplayer.

## Settings

For information on specifying keybinds for Key Combination List type settings, see [the SMAPI key bindings documentation](https://stardewcommunitywiki.com/Modding:Player_Guide/Key_Bindings#Multi-key_bindings).

| Key | Values | Default | Description |
| --- | --- | --- | --- |
| ForceShafts | `true` or `false` | false | When true, will modify game RNG to make the next eligible ladder stone spawn a shaft instead |
| ToggleShaftsKey | Key Combination List | OemTilde | Keys that will toggle ForceShafts |
| ToggleHighlightTypeKey | Key Combination List | LeftShift + OemTilde | Key combination list that will toggle between the highlight types (individually, combinations, or none) |
| HighlightTypes | Type array | Rectangle | Array of types of ladder stone highlighting that are enabled; See table below for details on the options |
| HighlightRectangleRGBA | 4 comma separated integers | 0, 255, 0, 255 | RGBA values in integers (0-255) to define highlight rectangle color |
| HighlightImageFilename | string filename | cracked.png | Image name relative to the `Mods/LadderLocator/` directory |
| HighlightAlpha | decimal | 0.45 | Decimal from 0 to 1 to apply transparency to the all highlight types, 0 is fully transparent, 1 is fully opaque |
| CycleAlpha | Key Combination List | LeftAlt + OemTilde | Key combination lsit that will cycle through `HighlightAlpha` values, 0.15 at a time (wrapping when over 1.0) |
| HighlightUsesStoneTint | `true` or `false` | false | Instead of using the defined rectangle color or black (for images), use a color similar to the highlighted stone as a tint |
| ToggleTint | Key Combination List | LeftControl + OemTilde | Key combination list that will toggle the value of `HighlightUSesStoneTint` |

| Highlight Type | Description |
| --- | --- |
| Rectangle | Draws a rectangle around the tile, colored by `HighlightRectangleRGBA` or stone average color if `HighlightUsesStoneTint` |
| Image | Draws an image over the rock, 16x16 png best. Tinted by stone color or black. |
| Sprite | Draws a duplicate over the rock sprite, making it a bit darker. Same tint rules as `Image` |

## Source Code
[The Github](https://github.com/yuri-moens/LadderLocator)