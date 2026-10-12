using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using DeluxeJournal.Util;

namespace DeluxeJournal.Menus
{
    public class NotesOverlay : IOverlay
    {
        private readonly SpriteFontTools _fontTools;
        private readonly StringBuilder _text;
        private string _displayText;

        public override bool IsColorSelected => true;

        public override float TextScale
        {
            get => base.TextScale;

            set
            {
                base.TextScale = value;
                WrapText();
            }
        }

        private int Padding => (int)(8 * TextScale);

        public NotesOverlay(Rectangle bounds, string text) : base(bounds)
        {
            _fontTools = new(Game1.smallFont, string.Empty);
            _text = new(text.Length);
            _displayText = string.Empty;

            CalculateEdgeSnappedBounds();
            UpdateText(text);
        }

        public void UpdateText(string text)
        {
            _text.Clear();
            _text.Append(text);
            WrapText();
        }

        public override void Resize(int width, int height)
        {
            Point oldSize = Size;

            base.Resize(width, height);

            if (oldSize.X != Size.X)
            {
                WrapText();
            }
            else if (oldSize.Y != Size.Y)
            {
                BuildDisplayText();
            }
        }

        public override void DrawContents(SpriteBatch b)
        {
            drawTextureBox(b,
                DeluxeJournalMod.UiTexture,
                BackgroundSource,
                EdgeSnappedBounds.X,
                EdgeSnappedBounds.Y,
                EdgeSnappedBounds.Width,
                EdgeSnappedBounds.Height,
                BackgroundColor,
                4f,
                false);

            BeginScaledText(b);
            Utility.drawTextWithColoredShadow(b,
                _displayText,
                _fontTools.Font,
                new(EdgeSnappedBounds.X + Padding, EdgeSnappedBounds.Y + Padding),
                CustomColor,
                BackgroundColor,
                TextScale);
            EndScaledText(b);
        }

        private void BuildDisplayText()
        {
            if (_text.Length == 0)
            {
                _displayText = string.Empty;
                return;
            }

            int maxLine = (int)((Size.Y - Padding * 2) / (_fontTools.LineSpacing * TextScale));
            int currentLine = 0;
            int i;

            for (i = 0; i < _text.Length; i++)
            {
                if (_text[i] == '\n' && ++currentLine >= maxLine)
                {
                    break;
                }
            }

            _displayText = _text.ToString(0, i);
        }

        private void WrapText()
        {
            _fontTools.Wrap(_text, (Size.X - Padding * 2) / TextScale);
            BuildDisplayText();
        }
    }
}
