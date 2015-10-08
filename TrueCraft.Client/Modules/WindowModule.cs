﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TrueCraft.Client.Rendering;
using TrueCraft.Core.Logic.Items;
using TrueCraft.API.Logic;
using TrueCraft.Client.Input;
using Microsoft.Xna.Framework.Input;
using TrueCraft.Core.Networking.Packets;
using TrueCraft.API.Windows;
using System;
using TrueCraft.API;
using TrueCraft.Core.Windows;

namespace TrueCraft.Client.Modules
{
    public class WindowModule : InputModule, IGraphicalModule
    {
        private TrueCraftGame Game { get; set; }
        private SpriteBatch SpriteBatch { get; set; }
        private Texture2D Inventory { get; set; }
        private Texture2D Items { get; set; }
        private FontRenderer Font { get; set; }
        private int SelectedSlot { get; set; }

        private enum RenderStage
        {
            Sprites,
            Models,
            Text
        }

        public WindowModule(TrueCraftGame game, FontRenderer font)
        {
            Game = game;
            Font = font;
            SpriteBatch = new SpriteBatch(game.GraphicsDevice);
            Inventory = game.TextureMapper.GetTexture("gui/inventory.png");
            Items = game.TextureMapper.GetTexture("gui/items.png");
            SelectedSlot = -1;
        }

        private static readonly Rectangle InventoryWindowRect = new Rectangle(0, 0, 176, 166);

        public void Draw(GameTime gameTime)
        {
            if (Game.Client.CurrentWindow != null)
            {
                SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.NonPremultiplied);
                SpriteBatch.Draw(Game.White1x1, new Rectangle(0, 0,
                    Game.GraphicsDevice.Viewport.Width, Game.GraphicsDevice.Viewport.Height), new Color(Color.Black, 180));
                switch (Game.Client.CurrentWindow.Type)
                {
                    case -1:
                        SpriteBatch.Draw(Inventory, new Vector2(
                            Game.GraphicsDevice.Viewport.Width / 2 - Scale(InventoryWindowRect.Width / 2),
                            Game.GraphicsDevice.Viewport.Height / 2 - Scale(InventoryWindowRect.Height / 2)),
                            InventoryWindowRect, Color.White, 0, Vector2.Zero, Game.ScaleFactor * 2, SpriteEffects.None, 1);
                        DrawInventoryWindow(RenderStage.Sprites);
                        break;
                }
                SpriteBatch.End();
                switch (Game.Client.CurrentWindow.Type)
                {
                    case -1:
                        DrawInventoryWindow(RenderStage.Models);
                        break;
                }
                SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.NonPremultiplied);
                switch (Game.Client.CurrentWindow.Type)
                {
                    case -1:
                        DrawInventoryWindow(RenderStage.Text);
                        break;
                }
                SpriteBatch.End();
            }
        }

        public override bool MouseMove(GameTime gameTime, MouseMoveEventArgs e)
        {
            if (Game.Client.CurrentWindow != null)
                return true;
            return base.MouseMove(gameTime, e);
        }

        public override bool KeyDown(GameTime gameTime, KeyboardKeyEventArgs e)
        {
            if (Game.Client.CurrentWindow != null)
            {
                if (e.Key == Keys.Escape)
                {
                    if (Game.Client.CurrentWindow.Type != -1)
                        Game.Client.QueuePacket(new CloseWindowPacket(Game.Client.CurrentWindow.ID));
                    Game.Client.CurrentWindow = null;
                    Mouse.SetPosition(Game.GraphicsDevice.Viewport.Width / 2, Game.GraphicsDevice.Viewport.Height / 2);
                }
                return true;
            }
            return base.KeyDown(gameTime, e);
        }

        private void DrawInventoryWindow(RenderStage stage)
        {
            DrawWindowArea(Game.Client.Inventory.MainInventory, 8, 84, InventoryWindowRect, stage);
            DrawWindowArea(Game.Client.Inventory.Hotbar, 8, 142, InventoryWindowRect, stage);
            DrawWindowArea(Game.Client.Inventory.CraftingGrid, 88, 26, InventoryWindowRect, stage);
            DrawWindowArea(Game.Client.Inventory.Armor, 8, 8, InventoryWindowRect, stage);
        }

        private void DrawWindowArea(IWindowArea area, int _x, int _y, Rectangle frame, RenderStage stage)
        {
            var mouse = Mouse.GetState().Position.ToVector2();
            var scale = new Point((int)(16 * Game.ScaleFactor * 2));
            var origin = new Point((int)(
                Game.GraphicsDevice.Viewport.Width / 2 - Scale(frame.Width / 2) + Scale(_x)),
                (int)(Game.GraphicsDevice.Viewport.Height / 2 - Scale(frame.Height / 2) + Scale(_y)));
            for (int i = 0; i < area.Length; i++)
            {
                var item = area[i];
                int x = (int)((i % area.Width) * Scale(18));
                int y = (int)((i / area.Width) * Scale(18));
                if (area is CraftingWindowArea)
                {
                    // yes I know this is a crappy hack, bite me
                    if (i == 0)
                    {
                        if (area.Width == 2)
                        {
                            x = (int)Scale(144 - _x);
                            y = (int)Scale(36 - _y);
                        }
                        else
                        {
                            x = (int)Scale(119);
                            y = (int)Scale(30);
                        }
                    }
                    else
                    {
                        i--;
                        x = (int)((i % area.Width) * Scale(18));
                        y = (int)((i / area.Width) * Scale(18));
                        i++;
                    }
                }
                var position = origin + new Point(x, y);
                var rect = new Rectangle(position, scale);
                if (stage == RenderStage.Sprites && rect.Contains(mouse))
                {
                    SelectedSlot = area.StartIndex + i;
                    SpriteBatch.Draw(Game.White1x1, rect, Color.LightGray);
                }
                if (item.Empty)
                    continue;
                var provider = Game.ItemRepository.GetItemProvider(item.ID);
                var texture = provider.GetIconTexture((byte)item.Metadata);
                if (texture != null && stage == RenderStage.Sprites)
                    IconRenderer.RenderItemIcon(SpriteBatch, Items, provider,
                        (byte)item.Metadata, rect, Color.White);
                if (texture == null && stage == RenderStage.Models && provider is IBlockProvider)
                    IconRenderer.RenderBlockIcon(Game, provider as IBlockProvider, (byte)item.Metadata, rect);
                if (stage == RenderStage.Text && item.Count > 1)
                {
                    int offset = 10;
                    if (item.Count >= 10)
                        offset -= 6;
                    position += new Point((int)Scale(offset), (int)Scale(5));
                    Font.DrawText(SpriteBatch, position.X, position.Y, item.Count.ToString(), Game.ScaleFactor);
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            Game.IsMouseVisible = Game.Client.CurrentWindow != null;
            base.Update(gameTime);
        }

        private float Scale(float value)
        {
            return value * Game.ScaleFactor * 2;
        }
    }
}
