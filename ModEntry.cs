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
    class ModEntry : Mod
    {
        private static ModConfig _config;
        private static Texture2D _pixelTexture;
        private static List<Object> _ladderStones;
        private static bool _nextIsShaft;

        public override void Entry(IModHelper helper)
        {
            _config = Helper.ReadConfig<ModConfig>();

            _pixelTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            var colorArray = Enumerable.Range(0, 1).Select(i => Color.White).ToArray();
            _pixelTexture.SetData(colorArray);

            _ladderStones = new List<Object>();
            
            Helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            Helper.Events.Display.Rendered += OnRendered;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Player.Warped += OnWarped;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!e.IsOneSecond) return;
            if (Game1.mine == null) return;
            var ladderHasSpawned = Helper.Reflection.GetField<bool>(Game1.mine, "ladderHasSpawned").GetValue();

            if (ladderHasSpawned)
            {
                _ladderStones.Clear();
            }
            else if (_ladderStones.Count == 0)
            {
                FindLadders();
            }

            if (!_config.ForceShafts || Game1.mine.getMineArea() != 121 || _nextIsShaft ||
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
            var netStonesLeftOnThisLevel = Helper.Reflection.GetField<NetIntDelta>(Game1.mine, "netStonesLeftOnThisLevel").GetValue().Value;
            var ladderHasSpawned = Helper.Reflection.GetField<bool>(Game1.mine, "ladderHasSpawned").GetValue();
            
            for (var x = 0; x < layerWidth; x++)
            {
                for (var y = 0; y < layerHeight; y++)
                {
                    var obj = Game1.mine.getObjectAtTile(x, y);

                    if (obj == null || !obj.Name.Equals("Stone")) continue;
                    // ladder chance calculation taken from checkStoneForItems function in MineShaft class
                    var r = new Random(x * 1000 + y + Game1.mine.mineLevel + (int) Game1.uniqueIDForThisGame / 2);
                    r.NextDouble();
                    var chance = 0.02 + 1.0 / Math.Max(1, netStonesLeftOnThisLevel) + Game1.player.LuckLevel / 100.0 + Game1.player.DailyLuck / 5.0;

                    if (Game1.mine.characters.Count == 0)
                    {
                        chance += 0.04;
                    }

                    if (!ladderHasSpawned && (netStonesLeftOnThisLevel == 0 || r.NextDouble() < chance))
                    {
                        _ladderStones.Add(obj);
                    }
                }
            }
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            foreach (var rect in _ladderStones.Select(obj => new Rectangle((int)obj.getLocalPosition(Game1.viewport).X, (int)obj.getLocalPosition(Game1.viewport).Y, 64, 64)))
            {
                DrawRectangle(rect, Color.Lime);
            }
        }

        private static void DrawRectangle(Rectangle rect, Color color)
        {
            Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left, rect.Top, rect.Width, 3), color);
            Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left, rect.Bottom, rect.Width, 3), color);
            Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left, rect.Top, 3, rect.Height), color);
            Game1.spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right, rect.Top, 3, rect.Height), color);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (e.Button != _config.ToggleShaftsKey) return;
            _config.ForceShafts = !_config.ForceShafts;

            Game1.addHUDMessage(_config.ForceShafts
                ? new HUDMessage("Force shafts toggled on", 2)
                : new HUDMessage("Force shafts toggled off", 2));

            Helper.WriteConfig(_config);
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
                return (T)fmt.Deserialize(stream);
            }
        }

    }
}
