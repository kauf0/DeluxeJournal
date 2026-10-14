using Microsoft.Xna.Framework;
using DeluxeJournal.Menus;

namespace DeluxeJournal.Framework.Data
{
    internal class OverlaySettings(Rectangle bounds, bool isVisible, bool isVisibilityLocked, bool isColorSelected, Color customColor)
    {
        public Rectangle Bounds { get; set; } = bounds;

        public Point WindowSize { get; set; }

        public bool IsVisible { get; set; } = isVisible;

        public bool IsVisibilityLocked { get; set; } = isVisibilityLocked;

        public bool IsColorSelected { get; set; } = isColorSelected;

        public Color CustomColor { get; set; } = customColor;

        public float TextScale { get; set; } = 1f;

        /// <summary>Apply these settings to an <see cref="IOverlay"/>.</summary>
        public void Apply(IOverlay overlay)
        {
            overlay.SetPreferredBounds(Bounds, WindowSize);
            overlay.IsVisible = IsVisible;
            overlay.IsVisibilityLocked = IsVisibilityLocked;
            overlay.IsColorSelected = IsColorSelected;
            overlay.CustomColor = CustomColor;
            overlay.TextScale = Math.Clamp(TextScale, IOverlay.MinTextScale, IOverlay.MaxTextScale);
        }

        /// <summary>Update these settings using the active state of an <see cref="IOverlay"/>.</summary>
        public void Update(IOverlay overlay)
        {
            Bounds = overlay.PreferredBounds;
            WindowSize = overlay.PreferredWindowSize;
            IsVisible = overlay.IsVisible;
            IsVisibilityLocked = overlay.IsVisibilityLocked;
            IsColorSelected = overlay.IsColorSelected;
            CustomColor = overlay.CustomColor;
            TextScale = overlay.TextScale;
        }

        /// <summary>Create a new <see cref="OverlaySettings"/> instance with default values.</summary>
        public static OverlaySettings NewDefault()
        {
            return new(new(0, 0, IOverlay.MinWidth, IOverlay.MinHeight), true, false, true, Color.White);
        }
    }
}
