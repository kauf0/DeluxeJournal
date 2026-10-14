using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using DeluxeJournal.Framework;

namespace DeluxeJournal.Menus
{
    public abstract class IOverlay : IClickableMenu, IDisposable
    {
        /// <summary>Minimum resize width.</summary>
        public const int MinWidth = 100;

        /// <summary>Minumum resize height.</summary>
        public const int MinHeight = 100;

        /// <summary>Minimum text scale.</summary>
        public const float MinTextScale = 0.6f;

        /// <summary>Maximum text scale.</summary>
        public const float MaxTextScale = 1f;

        /// <summary>Size of the resize boxes shown in edit-mode.</summary>
        public const int ResizeBoxSize = 16;

        /// <summary>Background texture box source.</summary>
        public static readonly Rectangle BackgroundSource = new(48, 58, 3, 3);

        /// <summary>Outline texture box source.</summary>
        public static readonly Rectangle OutlineSource = new(48, 61, 3, 3);

        /// <summary>Background texture box color per screen.</summary>
        private static readonly PerScreen<Color> BackgroundColorPerScreen = new(() => Context.ScreenId == 0 ? Color.Black * 0.25f : BackgroundColorPerScreen!.GetValueForScreen(0));

        /// <summary>Background texture box color.</summary>
        public static Color BackgroundColor
        {
            get => BackgroundColorPerScreen.Value;
            set => BackgroundColorPerScreen.Value = value;
        }

        /// <summary>Background color opacity value between <c>0f</c> and <c>1f</c>.</summary>
        public static float BackgroundOpacity => BackgroundColor.A / 255f;

        private Rectangle _preferredBounds;
        private Point _preferredWindowSize;
        private int _width;
        private int _height;

        /// <summary>Registered page ID value assigned by the <see cref="PageRegistry"/> (this value is set immediately AFTER construction).</summary>
        public string PageId { get; set; } = string.Empty;

        /// <summary>Overlay bounds snapped to the screen edge if within the <see cref="SnapDistance"/>.</summary>
        public Rectangle EdgeSnappedBounds { get; private set; }

        /// <summary><see cref="Rectangle"/> wrapper for the <see cref="IClickableMenu"/> bounds.</summary>
        public virtual Rectangle Bounds
        {
            get => new(xPositionOnScreen, yPositionOnScreen, _width, _height);

            set
            {
                Resize(value.Width, value.Height);
                Move(value.X, value.Y);
                KeepBounds(Bounds);
            }
        }

        /// <summary>Bounds chosen by the player, relative to the <see cref="PreferredWindowSize"/>.</summary>
        public Rectangle PreferredBounds => _preferredBounds;

        /// <summary>Window size the <see cref="PreferredBounds"/> were chosen in.</summary>
        public Point PreferredWindowSize => _preferredWindowSize;

        /// <summary>On-screen coordinates of this overlay.</summary>
        public new Point Position
        {
            get => new(xPositionOnScreen, yPositionOnScreen);

            set
            {
                Move(value.X, value.Y);
                KeepBounds(Bounds);
            }
        }

        /// <summary>Size of this overlay.</summary>
        public Point Size
        {
            get => new(_width, _height);

            set
            {
                Resize(value.X, value.Y);
                KeepBounds(Bounds);
            }
        }

        /// <summary>Whether the overlay content should be interactable.</summary>
        public virtual bool IsContentInteractable => IsVisible && !IsEditing && Game1.activeClickableMenu == null;

        /// <summary>Whether this overlay visible on-screen.</summary>
        public virtual bool IsVisible { get; set; }

        /// <summary>Whether the visibility of overlay can be toggled via hotkey.</summary>
        public bool IsVisibilityLocked { get; set; }

        /// <summary>Whether the overlay is currently in edit-mode.</summary>
        public bool IsEditing { get; set; }

        /// <summary>Whether the custom color is optional or is always used.</summary>
        public virtual bool IsColorOptional { get; set; }

        /// <summary>Whether the custom color should be used.</summary>
        public virtual bool IsColorSelected { get; set; }

        /// <summary>Custom color selected by the player.</summary>
        public Color CustomColor { get; set; }

        /// <summary>Scale of the overlay text relative to the default font size.</summary>
        public virtual float TextScale { get; set; } = 1f;

        /// <summary>Distance from the screen edge to start snapping.</summary>
        public int SnapDistance { get; set; } = 4;

        /// <summary>Margin between the screen edge when snapped.</summary>
        public int SnapMargin { get; set; } = -4;

        /// <summary>Unique split-screen ID that this overlay is visible on. Set on instantiation.</summary>
        protected int ScreenId { get; }

        public IOverlay(Rectangle bounds)
            : this(bounds.X, bounds.Y, bounds.Width, bounds.Height)
        {
        }

        public IOverlay(int x, int y, int width, int height)
            : base(x, y, 0, 0, false)
        {
            _width = width;
            _height = height;
            _preferredBounds = new(x, y, width, height);

            if (Game1.uiMode)
            {
                _preferredWindowSize = new(Game1.uiViewport.Width, Game1.uiViewport.Height);
            }
            else
            {
                float uiMultiplier = Game1.options.zoomLevel / Game1.options.uiScale;
                _preferredWindowSize = new((int)(Game1.viewport.Width * uiMultiplier), (int)(Game1.viewport.Height * uiMultiplier));
            }

            ScreenId = Context.ScreenId;
        }

        /// <summary>Move the overlay in the specified on-screen coordinates.</summary>
        public virtual void Move(int x, int y)
        {
            xPositionOnScreen = Math.Clamp(x, 0, Game1.uiViewport.Width - _width);
            yPositionOnScreen = Math.Clamp(y, 0, Game1.uiViewport.Height - _height);
            CalculateEdgeSnappedBounds();
        }

        /// <summary>Resize the overlay to the specified dimensions.</summary>
        public virtual void Resize(int width, int height)
        {
            _width = Math.Clamp(width, MinWidth, Game1.uiViewport.Width - ResizeBoxSize);
            _height = Math.Clamp(height, MinHeight, Game1.uiViewport.Height - ResizeBoxSize);
            CalculateEdgeSnappedBounds();
        }

        /// <summary>Set the bounds chosen by the player and fit them to the current window.</summary>
        /// <param name="bounds">Preferred bounds.</param>
        /// <param name="windowSize">Window size the bounds were chosen in, or <see cref="Point.Zero"/> for the current window size.</param>
        public void SetPreferredBounds(Rectangle bounds, Point windowSize)
        {
            _preferredBounds = bounds;
            _preferredWindowSize = windowSize == Point.Zero ? new(Game1.uiViewport.Width, Game1.uiViewport.Height) : windowSize;
            FitToWindow();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            FitToWindow();
        }

        public override void draw(SpriteBatch b)
        {
            if (IsVisible && Game1.activeClickableMenu is not DeluxeJournalMenu)
            {
                DrawContents(b);
            }
        }

        /// <summary>Draw with edit-mode graphics.</summary>
        public virtual void DrawInEditMode(SpriteBatch b)
        {
            DrawContents(b);
        }

        /// <summary>Draw the overlay contents.</summary>
        public abstract void DrawContents(SpriteBatch b);

        /// <summary>Switch to linear filtering for scaled text, since point sampling skips pixel columns of the font.</summary>
        protected void BeginScaledText(SpriteBatch b)
        {
            if (TextScale != 1f)
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
            }
        }

        /// <summary>Restore the point sampler after <see cref="BeginScaledText"/>.</summary>
        protected void EndScaledText(SpriteBatch b)
        {
            if (TextScale != 1f)
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <param name="disposing">Whether this method was invoked from the <see cref="IDisposable.Dispose"/> implementation or the finalizer.</param>
        /// <inheritdoc cref="IDisposable.Dispose"/>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>Keep the bounds set by the player as the preferred bounds for the current window size.</summary>
        private void KeepBounds(Rectangle bounds)
        {
            _preferredBounds = bounds;
            _preferredWindowSize = new(Game1.uiViewport.Width, Game1.uiViewport.Height);
        }

        /// <summary>
        /// Place the overlay at the same relative position between the screen edges as its preferred bounds, clamped to the window.
        /// The preferred bounds are left unchanged, so the overlay goes back to them when the window grows again.
        /// </summary>
        private void FitToWindow()
        {
            Point windowSize = new(Game1.uiViewport.Width, Game1.uiViewport.Height);
            Rectangle bounds = _preferredBounds;

            if (FitsWindow(_preferredBounds, _preferredWindowSize))
            {
                bounds.Width = Math.Min(bounds.Width, windowSize.X - ResizeBoxSize);
                bounds.Height = Math.Min(bounds.Height, windowSize.Y - ResizeBoxSize);
                bounds.X = (windowSize.X - bounds.Width) * _preferredBounds.X / (_preferredWindowSize.X - _preferredBounds.Width);
                bounds.Y = (windowSize.Y - bounds.Height) * _preferredBounds.Y / (_preferredWindowSize.Y - _preferredBounds.Height);
            }

            Resize(bounds.Width, bounds.Height);
            Move(bounds.X, bounds.Y);
        }

        /// <summary>Whether the bounds fit within a window of the given size without being clamped.</summary>
        private static bool FitsWindow(Rectangle bounds, Point windowSize)
        {
            return bounds.X >= 0 && bounds.Y >= 0
                && bounds.Width <= windowSize.X - ResizeBoxSize && bounds.Height <= windowSize.Y - ResizeBoxSize
                && bounds.Right <= windowSize.X && bounds.Bottom <= windowSize.Y;
        }

        /// <summary>Get the overlay bounds snapped to the screen edge.</summary>
        protected virtual void CalculateEdgeSnappedBounds()
        {
            Rectangle bounds = Bounds;

            if (bounds.X <= SnapDistance)
            {
                bounds.X = SnapMargin;
                bounds.Width -= SnapMargin;
            }
            else if (bounds.Right >= Game1.uiViewport.Width - SnapDistance)
            {
                bounds.X = Game1.uiViewport.Width - bounds.Width;
                bounds.Width -= SnapMargin;
            }

            if (bounds.Y <= SnapDistance)
            {
                bounds.Y = SnapMargin;
                bounds.Height -= SnapMargin;
            }
            else if (bounds.Bottom >= Game1.uiViewport.Height - SnapDistance)
            {
                bounds.Y = Game1.uiViewport.Height - bounds.Height;
                bounds.Height -= SnapMargin;
            }

            EdgeSnappedBounds = bounds;
        }
    }
}
