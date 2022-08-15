﻿//
// Copyright 2020 Electronic Arts Inc.
//
// The Command & Conquer Map Editor and corresponding source code is free 
// software: you can redistribute it and/or modify it under the terms of 
// the GNU General Public License as published by the Free Software Foundation, 
// either version 3 of the License, or (at your option) any later version.

// The Command & Conquer Map Editor and corresponding source code is distributed 
// in the hope that it will be useful, but with permitted additional restrictions 
// under Section 7 of the GPL. See the GNU General Public License in LICENSE.TXT 
// distributed with this program. You should have received a copy of the 
// GNU General Public License along with permitted additional restrictions 
// with this program. If not, see https://github.com/electronicarts/CnC_Remastered_Collection
using MobiusEditor.Interface;
using MobiusEditor.Render;
using MobiusEditor.Utility;
using System;
using System.Drawing;

namespace MobiusEditor.Model
{
    [Flags]
    public enum TemplateTypeFlag
    {
        None       = 0,
        Clear      = (1 << 1),
        Water      = (1 << 2),
        OreMine    = (1 << 3),
        RandomCell = (1 << 4),
    }

    public class TemplateType : IBrowsableType
    {
        public ushort ID { get; private set; }

        public string Name { get; private set; }

        public string DisplayName => Name;

        public int IconWidth { get; private set; }

        public int IconHeight { get; private set; }

        public Size IconSize => new Size(IconWidth, IconHeight);

        public int NumIcons { get; private set; }

        public bool[,] IconMask { get; set; }

        public Image Thumbnail { get; set; }

        public TheaterType[] Theaters { get; private set; }

        public TemplateTypeFlag Flag { get; private set; }

        public bool[,] MaskOverride { get; set; }

        public TemplateType(ushort id, string name, int iconWidth, int iconHeight, TheaterType[] theaters, TemplateTypeFlag flag, String maskOverride)
        {
            ID = id;
            Name = name;
            IconWidth = iconWidth;
            IconHeight = iconHeight;
            NumIcons = IconWidth * IconHeight;
            Theaters = theaters;
            Flag = flag;
            // Mask override for tiles that contain too many graphics in the Remaster. Indices with '0' are removed from the tiles.
            if (!String.IsNullOrEmpty(maskOverride))
            {
                MaskOverride = new bool[iconWidth, iconHeight];
                int icon = 0;
                for (var y = 0; y < IconHeight; ++y)
                {
                    for (var x = 0; x < IconWidth; ++x, ++icon)
                    {
                        MaskOverride[x, y] = icon < maskOverride.Length && maskOverride[icon] != '0';
                    }
                }
            }
        }

        public TemplateType(ushort id, string name, int iconWidth, int iconHeight, TheaterType[] theaters, TemplateTypeFlag flag)
            : this(id, name, iconWidth, iconHeight, theaters, flag, null)
        {
        }

        public TemplateType(ushort id, string name, int iconWidth, int iconHeight, TheaterType[] theaters, String maskOverride)
            : this(id, name, iconWidth, iconHeight, theaters, TemplateTypeFlag.None, maskOverride)
        {
        }

        public TemplateType(ushort id, string name, int iconWidth, int iconHeight, TheaterType[] theaters)
            : this(id, name, iconWidth, iconHeight, theaters, TemplateTypeFlag.None, null)
        {
        }

        public override bool Equals(object obj)
        {
            if (obj is TemplateType)
            {
                return this == obj;
            }
            else if (obj is byte)
            {
                return ID == (byte)obj;
            }
            else if (obj is ushort)
            {
                return ID == (ushort)obj;
            }
            else if (obj is string)
            {
                return string.Equals(Name, obj as string, StringComparison.OrdinalIgnoreCase);
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }

        public void Init(TheaterType theater)
        {
            // Cheeky hack for Nod SpecOps '99 mission 2.
            // This allows adding tiles to existing 1x1 tiles. However, it excludes clear terrain and the specific RA 1x1 random cell selector tiles.
            if (IconWidth == 1 & IconHeight == 1 && (Flag & TemplateTypeFlag.Clear) == TemplateTypeFlag.None && (Flag & TemplateTypeFlag.RandomCell) == TemplateTypeFlag.None)
            {
                if (Globals.TheTilesetManager.GetTileDataLength(theater.Tilesets, Name) > 1)
                {
                    Flag |= TemplateTypeFlag.RandomCell;
                }
            }
            var oldImage = Thumbnail;
            var size = new Size(Globals.PreviewTileWidth, Globals.PreviewTileHeight);
            var iconSize = Math.Max(IconWidth, IconHeight);
            var thumbnail = new Bitmap(iconSize * size.Width, iconSize * size.Height);
            var mask = new bool[IconWidth, IconHeight];
            Array.Clear(mask, 0, mask.Length);
            bool found = false;
            using (var g = Graphics.FromImage(thumbnail))
            {
                MapRenderer.SetRenderSettings(g, Globals.PreviewSmoothScale);
                g.Clear(Color.Transparent);
                int icon = 0;
                if ((Flag & TemplateTypeFlag.RandomCell) != TemplateTypeFlag.None)
                {
                    int numIcons = Globals.TheTilesetManager.GetTileDataLength(theater.Tilesets, Name);
                    if (numIcons >= 1)
                    {
                        NumIcons = numIcons;
                        if (Globals.TheTilesetManager.GetTileData(theater.Tilesets, Name, 0, out Tile tile))
                        {
                            g.DrawImage(tile.Image, 0, 0, size.Width, size.Height);
                            found = mask[0, 0] = true;
                        }
                    }
                }
                else
                {
                    for (var y = 0; y < IconHeight; ++y)
                    {
                        for (var x = 0; x < IconWidth; ++x, ++icon)
                        {
                            if (Globals.TheTilesetManager.GetTileData(theater.Tilesets, Name, icon, out Tile tile))
                            {
                                if (MaskOverride == null || MaskOverride[x, y])
                                {
                                    g.DrawImage(tile.Image, x * size.Width, y * size.Height, size.Width, size.Height);
                                    found = mask[x, y] = true;
                                }
                            }
                        }
                    }
                }
            }
            if (!found)
            {
                try { thumbnail.Dispose(); }
                catch { /* ignore */ }
            }
            Thumbnail = found ? thumbnail : null;
            IconMask = mask;
            if (oldImage != null)
            {
                try { oldImage.Dispose(); }
                catch { /* ignore */ }
            }
        }
    }
}
