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
    public class InfantryType : ITechnoType
    {
        public sbyte ID { get; private set; }
        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public string OwnerHouse { get; private set; }
        public UnitTypeFlag Flag { get; private set; }
        public bool IsArmed => (Flag & UnitTypeFlag.IsArmed) == UnitTypeFlag.IsArmed;
        public bool IsAircraft => false;
        public bool IsFixedWing => false;
        public bool IsExpansionUnit => (Flag & UnitTypeFlag.IsExpansionUnit) == UnitTypeFlag.IsExpansionUnit;
        public bool IsHarvester => false;
        public bool CanRemap => (this.Flag & UnitTypeFlag.NoRemap) != UnitTypeFlag.NoRemap;
        public string ClassicGraphicsSource { get; private set; }
        public Byte[] ClassicGraphicsRemap { get; private set; }

        private Size _RenderSize;
        public Size GetRenderSize(Size cellSize)
        {
            //RenderSize = new Size(tile.Image.Width / Globals.MapTileScale, tile.Image.Height / Globals.MapTileScale);
            return new Size(_RenderSize.Width * cellSize.Width / Globals.OriginalTileWidth, _RenderSize.Height * cellSize.Height / Globals.OriginalTileHeight);
        }
        public Bitmap Thumbnail { get; set; }
        private string nameId;

        public InfantryType(sbyte id, string name, string textId, string ownerHouse, string remappedFrom, byte[] remapTable, UnitTypeFlag flags)
        {
            this.ID = id;
            this.Name = name;
            this.nameId = textId;
            this.OwnerHouse = ownerHouse;
            this.Flag = flags;
            this.ClassicGraphicsSource = remappedFrom;
            this.ClassicGraphicsRemap = remapTable;
        }

        public InfantryType(sbyte id, string name, string textId, string ownerHouse, UnitTypeFlag flags)
            : this(id, name, textId, ownerHouse, null, null, flags)
        {
        }
        public InfantryType(sbyte id, string name, string textId, string ownerHouse)
        : this(id, name, textId, ownerHouse, null, null, UnitTypeFlag.None)
        {
        }

        public override bool Equals(object obj)
        {
            if (obj is InfantryType)
            {
                return this == obj;
            }
            else if (obj is sbyte)
            {
                return ID == (sbyte)obj;
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

        public void Init(HouseType house, DirectionType direction)
        {
            this.DisplayName = !String.IsNullOrEmpty(nameId) && !String.IsNullOrEmpty(Globals.TheGameTextManager[nameId])
                ? Globals.TheGameTextManager[nameId] + " (" + Name.ToUpperInvariant() + ")"
                : Name.ToUpperInvariant();
            Bitmap oldImage = Thumbnail;
            Tile tile;
            // RA classic infantry remap support.
            if (this.ClassicGraphicsSource != null && Globals.TheTilesetManager is TilesetManagerClassic tsmc)
            {
                tsmc.GetTeamColorTileData(this.Name, 4, null, out tile, true, false, this.ClassicGraphicsSource, this.ClassicGraphicsRemap);
            }
            else
            {
                Globals.TheTilesetManager.GetTeamColorTileData(this.Name, 4, null, out tile, true, false);
            }
            if (tile != null && tile.Image != null)
            {
                _RenderSize = tile.Image.Size;
            }

            Infantry mockInfantry = new Infantry(null)
            {
                Type = this,
                House = house,
                Strength = 256,
                Direction = direction
            };
            Bitmap infantryThumbnail = new Bitmap(Globals.PreviewTileWidth, Globals.PreviewTileHeight);
            using (Graphics g = Graphics.FromImage(infantryThumbnail))
            {
                MapRenderer.SetRenderSettings(g, Globals.PreviewSmoothScale);
                MapRenderer.RenderInfantry(Point.Empty, Globals.PreviewTileSize, mockInfantry, InfantryStoppingType.Center).Item2(g);
            }
            Thumbnail = infantryThumbnail;
            if (oldImage != null)
            {
                try { oldImage.Dispose(); }
                catch { /* ignore */ }
            }
        }
        public void Reset()
        {
            Bitmap oldImage = this.Thumbnail;
            this.Thumbnail = null;
            if (oldImage != null)
            {
                try { oldImage.Dispose(); }
                catch { /* ignore */ }
            }
        }
    }
}
