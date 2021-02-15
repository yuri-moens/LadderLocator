using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Object = StardewValley.Object;

namespace LadderLocator
{
    internal class ModEntry : Mod
    {
        private static ModConfig _config;
        private static Texture2D _pixelTexture;
        private static Texture2D _imageTexture;
        private static List<LadderStone> _ladderStones;
        private static bool _nextIsShaft;

        public override void Entry(IModHelper helper)
        {
            _config = Helper.ReadConfig<ModConfig>();
            _pixelTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            var colorArray = Enumerable.Range(0, 1).Select(i => Color.White).ToArray();
            _pixelTexture.SetData(colorArray);
            _imageTexture = LoadPicture("Mods/LadderLocator/" + _config.HighlightImageFilename);
            _ladderStones = new List<LadderStone>();
            Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Player.Warped += OnWarped;
        }

        private Texture2D LoadPicture(string filename)
        {
            FileStream stream = File.Open(filename, FileMode.Open);
            Texture2D texture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
            stream.Dispose();
            return texture;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!e.IsOneSecond) return;
            if (Game1.mine == null) return;
            var ladderHasSpawned = Helper.Reflection.GetField<bool>(Game1.mine, "ladderHasSpawned").GetValue();
            if (ladderHasSpawned) _ladderStones.Clear();
            else if (_ladderStones.Count == 0) FindLadders();
            if (!_config.ForceShafts || Game1.mine.getMineArea() != StardewValley.Locations.MineShaft.desertArea || _nextIsShaft ||
                _ladderStones.Count <= 0) return;
            var mineRandom = Helper.Reflection.GetField<Random>(Game1.mine, "mineRandom").GetValue();
            var r = Clone(mineRandom);
            var next = r.NextDouble();
            while (next >= 0.2)
            {
                next = r.NextDouble();
                mineRandom.NextDouble();
            }

            _nextIsShaft = true;
        }

        private void FindLadders()
        {
            var layerWidth = Game1.mine.map.Layers[0].LayerWidth;
            var layerHeight = Game1.mine.map.Layers[0].LayerHeight;
            var netStonesLeftOnThisLevel = Helper.Reflection
                .GetField<NetIntDelta>(Game1.mine, "netStonesLeftOnThisLevel").GetValue().Value;
            var ladderHasSpawned = Helper.Reflection.GetField<bool>(Game1.mine, "ladderHasSpawned").GetValue();
            if (ladderHasSpawned || Game1.mine.mustKillAllMonstersToAdvance() || !Game1.mine.shouldCreateLadderOnThisLevel()) return;
            var chance = 0.02 + 1.0 / Math.Max(1, netStonesLeftOnThisLevel) + Game1.player.LuckLevel / 100.0 +
                         Game1.player.DailyLuck / 5.0;
            if (Game1.mine.EnemyCount == 0) chance += 0.04;
            for (var x = 0; x < layerWidth; x++)
            for (var y = 0; y < layerHeight; y++)
            {
                var obj = Game1.mine.getObjectAtTile(x, y);
                if (obj == null || !obj.Name.Equals("Stone")) continue;
                // ladder chance calculation taken from checkStoneForItems function in MineShaft class
                var r = new Random(x * 1000 + y + Game1.mine.mineLevel + (int) Game1.uniqueIDForThisGame / 2);
                r.NextDouble();
                var next = r.NextDouble();
                if (netStonesLeftOnThisLevel == 0 || next < chance) _ladderStones.Add(new LadderStone(obj));
                }
        }

        private static void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || _ladderStones.Count <= 0) return;
                foreach (var obj in _ladderStones)
                { 
                    var rect = obj.BoundingBox;
                    rect.Offset(-Game1.viewport.X, -Game1.viewport.Y);
                    var rectColor = (_config.HighlightUsesStoneTint ? obj.Tint : _config.HighlightRectangleRGBA) * Convert.ToSingle(_config.HighlightAlpha);
                    if (_config.HighlightTypes.Contains(HighlightType.Rectangle)) DrawRectangle(rect, rectColor);
                    var imageColor = (_config.HighlightUsesStoneTint ? obj.Tint : Color.Black) * Convert.ToSingle(_config.HighlightAlpha);
                if (_config.HighlightTypes.Contains(HighlightType.Image)) DrawImage(rect, imageColor, obj.SpriteIndex, obj.Flipped);
                    if (_config.HighlightTypes.Contains(HighlightType.Sprite)) DrawSprite(rect, imageColor, obj.SpriteIndex, obj.Flipped);
                }
        }

        private static void DrawRectangle(Rectangle rect, Color color)
        {
            Game1.InUIMode(() =>
            {
                Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left + 3, rect.Top, rect.Width - 6, 3), color);
                Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left + 3, rect.Bottom - 3, rect.Width - 6, 3), color);
                Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left, rect.Top + 3, 3, rect.Height - 6), color);
                Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - 3, rect.Top + 3, 3, rect.Height - 6), color);
            });
        }
        private static void DrawSprite(Rectangle rect, Color color, int spriteIndex, bool flipped)
        {
            Game1.InUIMode(() =>
            {
                Game1.spriteBatch.Draw(Game1.objectSpriteSheet, rect, new Rectangle((spriteIndex % 24) * 16, (int)(spriteIndex / 24) * 16, 16, 16), color, 0.0f, Vector2.Zero, flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f );
            });
        }

        private static void DrawImage(Rectangle rect, Color color, int spriteIndex, bool flipped)
        {
            Game1.InUIMode(() =>
            {
                Game1.spriteBatch.Draw(_imageTexture, rect, null, color, 0.0f, Vector2.Zero, flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0.0f);
            });
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (_config.CycleAlpha.JustPressed())
            {
                _config.HighlightAlpha = Math.Round((_config.HighlightAlpha + 0.15M) % 1.0M, 2);
                Game1.addHUDMessage(new HUDMessage($"Highlight alpha now {_config.HighlightAlpha}"));
            }
            else if (_config.ToggleTint.JustPressed())
            {
                _config.HighlightUsesStoneTint = !_config.HighlightUsesStoneTint;
                Game1.addHUDMessage(new HUDMessage("Highlight using stone tint toggled " + (_config.HighlightUsesStoneTint ? "on" : "off"), 2));
                Helper.WriteConfig(_config);
            }
            else if (_config.ToggleHighlightTypeKey.JustPressed())
            {
                if (_config.HighlightTypes.Contains(HighlightType.Rectangle))
                {
                    if (_config.HighlightTypes.Contains(HighlightType.Image))
                        ToggleHighlightType(HighlightType.Sprite);
                    ToggleHighlightType(HighlightType.Image);
                }
                ToggleHighlightType(HighlightType.Rectangle);
                Game1.addHUDMessage(_config.HighlightTypes.Count > 0
                    ? new HUDMessage("Ladder highlight: " + string.Join(" + ", _config.HighlightTypes), 2)
                    : new HUDMessage("Ladder highlight disabled", 2));
                Helper.WriteConfig(_config);
            }
            else if (_config.ToggleShaftsKey.JustPressed())
            {
                _config.ForceShafts = !_config.ForceShafts;
                Game1.addHUDMessage(_config.ForceShafts
                    ? new HUDMessage("Force shafts toggled on", 2)
                    : new HUDMessage("Force shafts toggled off", 2));
                Helper.WriteConfig(_config);
            }
        }

        private static void ToggleHighlightType(HighlightType type)
        {
            if (_config.HighlightTypes.Contains(type)) _config.HighlightTypes.Remove(type);
            else _config.HighlightTypes.Add(type);
        }

        private static void OnWarped(object sender, WarpedEventArgs e)
        {
            _ladderStones.Clear();
            _nextIsShaft = false;
        }

        private static T Clone<T>(T source)
        {
            IFormatter fmt = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                fmt.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T) fmt.Deserialize(stream);
            }
        }

        class LadderStone
        {
            public LadderStone(Object obj)
            {
                BoundingBox = obj.getBoundingBox(obj.TileLocation);
                SpriteIndex = obj.ParentSheetIndex;
                Flipped = obj.Flipped;
                Tint = GetObjectSpriteAverageColor(SpriteIndex);
            }

            public Rectangle BoundingBox { get; }
            public int SpriteIndex { get; }
            public bool Flipped { get; }
            public Color Tint { get; }
        }

        /// <summary>
        /// Gets the average color of a particular stone from the center 8x8 square of its pixels.
        /// </summary>
        /// <param name="spriteIndex">Index of sprite to get color of from SDV object sprite sheet</param>
        /// <returns>average color of given sprite</returns>
        private static Color GetObjectSpriteAverageColor(int spriteIndex)
        {
            Color[] colors = new Color[8 * 8];
            Game1.objectSpriteSheet.GetData(0, new Rectangle((spriteIndex % 24) * 16 + 4, (int)(spriteIndex / 24) * 16 + 4, 8, 8), colors, 0, 8 * 8);
            var average = new Color(Convert.ToByte(colors.Sum(c => c.R) / colors.Count()), Convert.ToByte(colors.Sum(c => c.G) / colors.Count()), Convert.ToByte(colors.Sum(c => c.B) / colors.Count()));
            return average;
        }
    }
}