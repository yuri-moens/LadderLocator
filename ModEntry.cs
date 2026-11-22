using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Object = StardewValley.Object;

namespace LadderLocator
{
    internal class ModEntry : Mod
    {
        private readonly PerScreen<List<LadderStone>> _ladderStones = new(() => new List<LadderStone>());
        private readonly PerScreen<Dictionary<Vector2, Stone>> _nodeStones = new(() => new Dictionary<Vector2, Stone>());
        private readonly PerScreen<bool> _nextIsShaft = new();
        private readonly PerScreen<bool> _needsMineScan = new();
        private readonly Dictionary<int, Color> _stoneAverageColors = new();

        private List<int> _selectedNodeTypeIndices;
        private ModConfig _config;
        private Texture2D _pixelTexture;
        private Texture2D _imageTexture;
        private string _loadedImageFilename;

        public override void Entry(IModHelper helper)
        {
            _config = Helper.ReadConfig<ModConfig>();
            _pixelTexture = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            _pixelTexture.SetData(new[] { Color.White });

            ApplyConfigChanges();

            Helper.Events.World.ObjectListChanged += OnObjectListChanged;
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Player.Warped += OnWarped;
            Helper.Events.GameLoop.OneSecondUpdateTicked += OnUpdateTicked;
            Helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _ladderStones.ResetAllScreens();
            _nodeStones.ResetAllScreens();
            _nextIsShaft.ResetAllScreens();
            _needsMineScan.ResetAllScreens();
        }

        private void OnObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            if (!e.IsCurrentLocation) return;

            if (e.Location is StardewValley.Locations.MineShaft mine)
            {
                var ladderHasSpawned = Helper.Reflection.GetField<bool>(mine, "ladderHasSpawned").GetValue();
                if (ladderHasSpawned)
                {
                    _ladderStones.Value.Clear();
                    _needsMineScan.Value = false;
                }
                else
                {
                    // Re-scan even if stones were already highlighted, since removing stones raises
                    // ladder chance for remaining stones and clears any broken stone from the list.
                    _needsMineScan.Value = !FindLadders(mine);
                }
            }

            if (LocationHasNodes(e.Location))
            {
                var nodeStones = _nodeStones.Value;
                foreach (var pair in e.Removed)
                    nodeStones.Remove(pair.Key);

                foreach (var pair in e.Added)
                {
                    if (pair.Value.Name.Equals("Stone") && _selectedNodeTypeIndices.Contains(pair.Value.ParentSheetIndex))
                        nodeStones[pair.Key] = new Stone(pair.Value);
                }
            }
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer) return;

            _ladderStones.Value.Clear();
            _nodeStones.Value.Clear();
            _nextIsShaft.Value = false;
            _needsMineScan.Value = false;

            if (LocationHasNodes(e.NewLocation))
                FindNodes(e.NewLocation);

            if (e.NewLocation is StardewValley.Locations.MineShaft mine && !FindLadders(mine))
                _needsMineScan.Value = true;
        }

        private void OnUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !_needsMineScan.Value) return;

            if (Game1.currentLocation is not StardewValley.Locations.MineShaft mine)
            {
                _needsMineScan.Value = false;
                return;
            }

            if (_ladderStones.Value.Count > 0 || FindLadders(mine))
                _needsMineScan.Value = false;
        }

        private bool FindLadders(StardewValley.Locations.MineShaft mine)
        {
            var ladderStones = _ladderStones.Value;
            ladderStones.Clear();

            var ladderHasSpawned = Helper.Reflection.GetField<bool>(mine, "ladderHasSpawned").GetValue();
            if (ladderHasSpawned || mine.mustKillAllMonstersToAdvance() || !mine.shouldCreateLadderOnThisLevel()) return true;

            var netStonesLeftOnThisLevel = Helper.Reflection
                .GetField<NetIntDelta>(mine, "netStonesLeftOnThisLevel").GetValue().Value;
            var chance = 0.02 + 1.0 / Math.Max(1, netStonesLeftOnThisLevel) + Game1.player.LuckLevel / 100.0 +
                         Game1.player.DailyLuck / 5.0;
            if (mine.EnemyCount == 0) chance += 0.04;

            foreach (var pair in mine.Objects.Pairs.Where(pair => pair.Value.Name.Equals("Stone")))
            {
                var obj = pair.Value;
                var r = Utility.CreateDaySaveRandom(pair.Key.X * 1000, pair.Key.Y, mine.mineLevel);
                r.NextDouble();
                var next = r.NextDouble();
                if (netStonesLeftOnThisLevel == 0 || next < chance)
                    ladderStones.Add(new LadderStone(obj, GetObjectSpriteAverageColor(obj.ParentSheetIndex)));
            }

            if (_config.ForceShafts && mine.getMineArea() == StardewValley.Locations.MineShaft.desertArea
                && !_nextIsShaft.Value && ladderStones.Count > 0)
            {
                ForceShaft(mine);
            }

            return ladderStones.Count > 0;
        }

        private void ForceShaft(StardewValley.Locations.MineShaft mine)
        {
            if (!Context.IsMainPlayer) return;

            var mineRandom = Helper.Reflection.GetField<Random>(mine, "mineRandom").GetValue();
            if (mineRandom == null || ReferenceEquals(mineRandom, Random.Shared)) return;

            var r = Cloner.Clone(mineRandom);
            var next = r.NextDouble();
            int guard = 0;
            while (next >= 0.2 && guard++ < 1000)
            {
                next = r.NextDouble();
                mineRandom.NextDouble();
            }
            _nextIsShaft.Value = true;
        }

        private static bool LocationHasNodes(GameLocation loc)
        {
            return loc is StardewValley.Locations.MineShaft || loc is StardewValley.Locations.VolcanoDungeon || loc is StardewValley.Locations.Mountain;
        }

        private void FindNodes(GameLocation loc)
        {
            var nodeStones = _nodeStones.Value;
            nodeStones.Clear();
            foreach (var pair in loc.objects.Pairs.Where(pair => pair.Value.Name.Equals("Stone") && _selectedNodeTypeIndices.Contains(pair.Value.ParentSheetIndex)))
                nodeStones[pair.Key] = new Stone(pair.Value);
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady) return;
            if (Game1.currentLocation is not StardewValley.Locations.MineShaft mine) return;

            var ladderStones = _ladderStones.Value;
            if (ladderStones.Count == 0) return;

            var alpha = Convert.ToSingle(_config.HighlightAlpha);
            var drawRectangle = _config.HighlightTypes.Contains(HighlightType.Rectangle);
            var drawImage = _config.HighlightTypes.Contains(HighlightType.Image);
            var drawSprite = _config.HighlightTypes.Contains(HighlightType.Sprite);
            var baseRectColor = _config.UseFrostHighlightColor && mine.getMineArea() == StardewValley.Locations.MineShaft.frostArea
                ? _config.FrostHighlightRectangleRGBA
                : _config.HighlightRectangleRGBA;

            foreach (var obj in ladderStones)
            {
                var rect = obj.BoundingBox;
                rect.Offset(-Game1.viewport.X, -Game1.viewport.Y);

                var rectColor = (_config.HighlightUsesStoneTint ? obj.Tint : baseRectColor) * alpha;
                if (drawRectangle) DrawRectangle(e.SpriteBatch, rect, rectColor);

                var imageColor = (_config.HighlightUsesStoneTint ? obj.Tint : Color.Black) * alpha;
                if (drawImage) DrawImage(e.SpriteBatch, rect, imageColor, obj.Flipped);
                if (drawSprite) DrawSprite(e.SpriteBatch, rect, imageColor, obj.SpriteIndex, obj.Flipped);
            }
        }

        private void OnRenderingHud(object sender, RenderingHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.eventUp) return;
            if (!LocationHasNodes(Game1.currentLocation)) return;

            var nodeStones = _nodeStones.Value;
            if (nodeStones.Count == 0) return;

            var presentIndices = new HashSet<int>(nodeStones.Values.Select(node => node.SpriteIndex));
            var safeArea = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea;
            var radarScale = Convert.ToSingle(_config.RadarScale);
            int i = 0;

            foreach (var nodeType in _selectedNodeTypeIndices.Where(presentIndices.Contains))
            {
                if (!IsValidObjectSpriteIndex(nodeType)) continue;

                e.SpriteBatch.Draw(
                    Game1.objectSpriteSheet,
                    new Vector2(
                        safeArea.Right - (Game1.showingHealth ? 120 : 64),
                        safeArea.Bottom - 16 - Convert.ToInt32(16 * radarScale * i)
                    ),
                    new Rectangle((nodeType % 24) * 16, (nodeType / 24) * 16, 16, 16),
                    Color.White,
                    0.0f,
                    new Vector2(16, 16),
                    radarScale,
                    SpriteEffects.None,
                    1.0f
                );
                ++i;
            }
        }

        private void DrawRectangle(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left + 3, rect.Top, rect.Width - 6, 3), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left + 3, rect.Bottom - 3, rect.Width - 6, 3), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Left, rect.Top + 3, 3, rect.Height - 6), color);
            spriteBatch.Draw(_pixelTexture, new Rectangle(rect.Right - 3, rect.Top + 3, 3, rect.Height - 6), color);
        }

        private static void DrawSprite(SpriteBatch spriteBatch, Rectangle rect, Color color, int spriteIndex, bool flipped)
        {
            if (!IsValidObjectSpriteIndex(spriteIndex)) return;
            spriteBatch.Draw(
                Game1.objectSpriteSheet,
                rect,
                new Rectangle((spriteIndex % 24) * 16, (spriteIndex / 24) * 16, 16, 16),
                color,
                0.0f,
                Vector2.Zero,
                flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0.0f
            );
        }

        private void DrawImage(SpriteBatch spriteBatch, Rectangle rect, Color color, bool flipped)
        {
            if (_imageTexture == null) return;
            spriteBatch.Draw(
                _imageTexture,
                rect,
                null,
                color,
                0.0f,
                Vector2.Zero,
                flipped ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0.0f
            );
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.keyboardDispatcher.Subscriber != null) return;

            if (_config.ToggleNodeRadar.JustPressed())
            {
                _config.NodeRadar = !_config.NodeRadar;
                Game1.addHUDMessage(new HUDMessage($"Node Radar toggled {(_config.NodeRadar ? "on" : "off")}", 2));
                Helper.WriteConfig(_config);
                UpdateDisplaySubscriptions();
            }
            else if (_config.CycleAlpha.JustPressed())
            {
                _config.HighlightAlpha = Math.Round((_config.HighlightAlpha + 0.15M) % 1.0M, 2);
                Game1.addHUDMessage(new HUDMessage($"Highlight alpha now {_config.HighlightAlpha}", 2));
                Helper.WriteConfig(_config);
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
                UpdateDisplaySubscriptions();
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

        private void SetHighlightType(HighlightType type, bool enabled)
        {
            if (enabled) _config.HighlightTypes.Add(type);
            else _config.HighlightTypes.Remove(type);
        }

        private void ToggleHighlightType(HighlightType type)
        {
            if (_config.HighlightTypes.Contains(type)) _config.HighlightTypes.Remove(type);
            else _config.HighlightTypes.Add(type);
        }

        class Stone
        {
            public Stone(Object obj)
            {
                SpriteIndex = obj.ParentSheetIndex;
                this.obj = obj;
            }

            public Object obj { get; }
            public int SpriteIndex { get; }
        }

        class LadderStone : Stone
        {
            public LadderStone(Object obj, Color tint) : base(obj)
            {
                BoundingBox = obj.GetBoundingBox();
                Flipped = obj.Flipped;
                Tint = tint;
            }

            public Rectangle BoundingBox { get; }
            public bool Flipped { get; }
            public Color Tint { get; }
        }

        private static bool IsValidObjectSpriteIndex(int spriteIndex)
        {
            return spriteIndex >= 0
                && Game1.objectSpriteSheet != null
                && (spriteIndex % 24) * 16 + 16 <= Game1.objectSpriteSheet.Width
                && (spriteIndex / 24) * 16 + 16 <= Game1.objectSpriteSheet.Height;
        }

        /// <summary>
        /// Gets the average color of a particular stone from the center 8x8 square of its pixels.
        /// </summary>
        /// <param name="spriteIndex">Index of sprite to get color of from SDV object sprite sheet</param>
        /// <returns>average color of given sprite</returns>
        private Color GetObjectSpriteAverageColor(int spriteIndex)
        {
            if (!IsValidObjectSpriteIndex(spriteIndex))
                return Color.Gray;

            if (_stoneAverageColors.TryGetValue(spriteIndex, out var cached))
                return cached;

            Color[] colors = new Color[8 * 8];
            Game1.objectSpriteSheet.GetData(
                0,
                new Rectangle((spriteIndex % 24) * 16 + 4, (spriteIndex / 24) * 16 + 4, 8, 8),
                colors,
                0,
                colors.Length
            );
            var average = new Color(
                Convert.ToByte(colors.Sum(c => c.R) / colors.Length),
                Convert.ToByte(colors.Sum(c => c.G) / colors.Length),
                Convert.ToByte(colors.Sum(c => c.B) / colors.Length)
            );
            _stoneAverageColors[spriteIndex] = average;
            return average;
        }

        /// <summary>
        /// Initialize GMCM settings when the game launches.
        /// </summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcmApi = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcmApi != null)
            {
                RegisterModConfigMenu(gmcmApi);
            }
            else
            {
                Monitor.Log("Failed to access GMCM API. Is the Generic Mod Config Menu installed?", LogLevel.Warn);
            }
        }

        /// <summary>
        /// Register the mod configuration menu using GMCM.
        /// </summary>
        private void RegisterModConfigMenu(IGenericModConfigMenuApi api)
        {
            api.Register(
                mod: ModManifest,
                reset: ResetConfig,
                save: SaveConfig
            );

            AddOptions(api);
        }

        private void ResetConfig()
        {
            _config = new ModConfig();
            ApplyConfigChanges();
        }

        private void SaveConfig()
        {
            Helper.WriteConfig(_config);
            ApplyConfigChanges();
        }

        /// <summary>
        /// Add all mod configuration options to the GMCM menu.
        /// </summary>
        private void AddOptions(IGenericModConfigMenuApi api)
        {
            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.ToggleShaftsKey,
                setValue: value => _config.ToggleShaftsKey = value,
                name: () => Helper.Translation.Get("toggle-shafts-key"),
                tooltip: () => Helper.Translation.Get("toggle-shafts-key-tooltip")
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.ForceShafts,
                setValue: value => _config.ForceShafts = value,
                name: () => Helper.Translation.Get("force-shafts"),
                tooltip: () => Helper.Translation.Get("force-shafts-tooltip")
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.ToggleHighlightTypeKey,
                setValue: value => _config.ToggleHighlightTypeKey = value,
                name: () => Helper.Translation.Get("toggle-highlight-type-key"),
                tooltip: () => Helper.Translation.Get("toggle-highlight-type-key-tooltip")
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.HighlightTypes.Contains(HighlightType.Rectangle),
                setValue: value => SetHighlightType(HighlightType.Rectangle, value),
                name: () => Helper.Translation.Get("highlight-type-rectangle"),
                tooltip: () => Helper.Translation.Get("highlight-type-rectangle-tooltip")
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.HighlightTypes.Contains(HighlightType.Image),
                setValue: value => SetHighlightType(HighlightType.Image, value),
                name: () => Helper.Translation.Get("highlight-type-image"),
                tooltip: () => Helper.Translation.Get("highlight-type-image-tooltip")
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.HighlightTypes.Contains(HighlightType.Sprite),
                setValue: value => SetHighlightType(HighlightType.Sprite, value),
                name: () => Helper.Translation.Get("highlight-type-sprite"),
                tooltip: () => Helper.Translation.Get("highlight-type-sprite-tooltip")
            );

            AddColorOptions(
                api,
                getColor: () => _config.HighlightRectangleRGBA,
                setColor: color => _config.HighlightRectangleRGBA = color,
                keyPrefix: "highlight-color"
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.UseFrostHighlightColor,
                setValue: value => _config.UseFrostHighlightColor = value,
                name: () => Helper.Translation.Get("use-frost-highlight-color"),
                tooltip: () => Helper.Translation.Get("use-frost-highlight-color-tooltip")
            );

            AddColorOptions(
                api,
                getColor: () => _config.FrostHighlightRectangleRGBA,
                setColor: color => _config.FrostHighlightRectangleRGBA = color,
                keyPrefix: "frost-highlight-color"
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.ToggleTint,
                setValue: value => _config.ToggleTint = value,
                name: () => Helper.Translation.Get("toggle-tint-key"),
                tooltip: () => Helper.Translation.Get("toggle-tint-key-tooltip")
            );

            api.AddTextOption(
                mod: ModManifest,
                getValue: () => _config.HighlightImageFilename,
                setValue: value => _config.HighlightImageFilename = value,
                name: () => Helper.Translation.Get("highlight-image-filename"),
                tooltip: () => Helper.Translation.Get("highlight-image-filename-tooltip")
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => (float)_config.HighlightAlpha,
                setValue: value => _config.HighlightAlpha = Math.Round((decimal)value, 2),
                min: 0.0f,
                max: 1.0f,
                interval: 0.05f,
                name: () => Helper.Translation.Get("highlight-alpha"),
                tooltip: () => Helper.Translation.Get("highlight-alpha-tooltip")
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.CycleAlpha,
                setValue: value => _config.CycleAlpha = value,
                name: () => Helper.Translation.Get("cycle-alpha-key"),
                tooltip: () => Helper.Translation.Get("cycle-alpha-key-tooltip")
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.HighlightUsesStoneTint,
                setValue: value => _config.HighlightUsesStoneTint = value,
                name: () => Helper.Translation.Get("highlight-uses-stone-tint"),
                tooltip: () => Helper.Translation.Get("highlight-uses-stone-tint-tooltip")
            );

            api.AddBoolOption(
                mod: ModManifest,
                getValue: () => _config.NodeRadar,
                setValue: value => _config.NodeRadar = value,
                name: () => Helper.Translation.Get("node-radar"),
                tooltip: () => Helper.Translation.Get("node-radar-tooltip")
            );

            api.AddKeybindList(
                mod: ModManifest,
                getValue: () => _config.ToggleNodeRadar,
                setValue: value => _config.ToggleNodeRadar = value,
                name: () => Helper.Translation.Get("toggle-node-radar-key"),
                tooltip: () => Helper.Translation.Get("toggle-node-radar-key-tooltip")
            );

            api.AddNumberOption(
                mod: ModManifest,
                getValue: () => (float)_config.RadarScale,
                setValue: value => _config.RadarScale = Math.Round((decimal)value, 2),
                min: 0.1f,
                max: 5.0f,
                interval: 0.1f,
                name: () => Helper.Translation.Get("radar-scale"),
                tooltip: () => Helper.Translation.Get("radar-scale-tooltip")
            );
        }

        private void AddColorOptions(IGenericModConfigMenuApi api, Func<Color> getColor, Action<Color> setColor, string keyPrefix)
        {
            (string suffix, Func<Color, int> getter, Func<Color, int, Color> setter)[] channels =
            {
                ("r", c => c.R, (c, v) => new Color(v, c.G, c.B, c.A)),
                ("g", c => c.G, (c, v) => new Color(c.R, v, c.B, c.A)),
                ("b", c => c.B, (c, v) => new Color(c.R, c.G, v, c.A)),
                ("a", c => c.A, (c, v) => new Color(c.R, c.G, c.B, v))
            };

            foreach (var (suffix, getter, setter) in channels)
            {
                api.AddNumberOption(
                    mod: ModManifest,
                    getValue: () => getter(getColor()),
                    setValue: value => setColor(setter(getColor(), value)),
                    min: 0,
                    max: 255,
                    name: () => Helper.Translation.Get($"{keyPrefix}-{suffix}"),
                    tooltip: () => Helper.Translation.Get($"{keyPrefix}-{suffix}-tooltip")
                );
            }
        }

        private void ApplyConfigChanges()
        {
            _config.HighlightTypes ??= new HashSet<HighlightType> { HighlightType.Rectangle };
            _config.NodeTypes ??= new HashSet<Node>((Node[])Enum.GetValues(typeof(Node)));

            _selectedNodeTypeIndices = RadarNode.All
                .Where(radarNode => _config.NodeTypes.Contains(radarNode.type))
                .OrderBy(radarNode => radarNode.value)
                .Select(radarNode => radarNode.spriteIndex)
                .ToList();

            if (!string.IsNullOrWhiteSpace(_config.HighlightImageFilename) && _loadedImageFilename != _config.HighlightImageFilename)
            {
                try
                {
                    _imageTexture = Helper.ModContent.Load<Texture2D>(_config.HighlightImageFilename);
                    _loadedImageFilename = _config.HighlightImageFilename;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed to load highlight image '{_config.HighlightImageFilename}': {ex.Message}", LogLevel.Warn);
                }
            }
            else if (string.IsNullOrWhiteSpace(_config.HighlightImageFilename))
            {
                _imageTexture = null;
                _loadedImageFilename = null;
            }

            UpdateDisplaySubscriptions();

            if (Context.IsWorldReady)
            {
                if (LocationHasNodes(Game1.currentLocation))
                    FindNodes(Game1.currentLocation);
                if (Game1.currentLocation is StardewValley.Locations.MineShaft mine)
                    _needsMineScan.Value = !FindLadders(mine);
            }
        }

        private void UpdateDisplaySubscriptions()
        {
            Helper.Events.Display.RenderedWorld -= OnRenderedWorld;
            if (_config.HighlightTypes.Count > 0)
                Helper.Events.Display.RenderedWorld += OnRenderedWorld;

            Helper.Events.Display.RenderingHud -= OnRenderingHud;
            if (_config.NodeRadar)
                Helper.Events.Display.RenderingHud += OnRenderingHud;
        }
    }
}
