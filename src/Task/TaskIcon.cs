using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;

namespace DeluxeJournal.Task
{
    public class TaskIcon
    {
        /// <summary>A custom icon texture, or null if using the default.</summary>
        public Texture2D? CustomIconTexture { get; set; }

        /// <summary>The icon source rect for the given texture.</summary>
        public Rectangle SourceRect { get; set; }

        /// <summary>The qualified ID of an item drawn on top of the icon, or null if none.</summary>
        public string? ItemId { get; set; }

        public TaskIcon(Texture2D? texture, Rectangle sourceRect, string? itemId = null)
        {
            CustomIconTexture = texture;
            SourceRect = sourceRect;
            ItemId = itemId;
        }

        public void DrawIcon(SpriteBatch b, Rectangle bounds, Color color, float scale = 4f, bool drawShadow = false)
        {
            if ((CustomIconTexture ?? DeluxeJournalMod.UiTexture) is Texture2D texture)
            {
                Vector2 position = new Vector2(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                Vector2 origin = new Vector2(SourceRect.Width / 2, SourceRect.Height / 2);

                if (drawShadow)
                {
                    Utility.drawWithShadow(b, texture, position, SourceRect, color, 0, origin, scale);
                }
                else
                {
                    b.Draw(texture, position, SourceRect, color, 0, origin, scale, SpriteEffects.None, position.Y / 10000f);
                }

                if (ItemId != null)
                {
                    ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(ItemId);
                    Rectangle itemSourceRect = itemData.GetSourceRect();

                    b.Draw(itemData.GetTexture(),
                        position,
                        itemSourceRect,
                        color,
                        0,
                        new Vector2(itemSourceRect.Width / 2, itemSourceRect.Height / 2),
                        scale * 0.75f,
                        SpriteEffects.None,
                        position.Y / 10000f + 0.0001f);
                }
            }
        }
    }
}
