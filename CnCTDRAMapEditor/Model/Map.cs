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
using MobiusEditor.Event;
using MobiusEditor.Interface;
using MobiusEditor.Render;
using MobiusEditor.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using TGASharpLib;

namespace MobiusEditor.Model
{
    [Flags]
    public enum MapLayerFlag: int
    {
        None            = 0,
        Template        = 1 << 0,
        Terrain         = 1 << 1,
        Resources       = 1 << 2,
        Walls           = 1 << 3,
        Overlay         = 1 << 4,
        Smudge          = 1 << 5,
        Infantry        = 1 << 6,
        Units           = 1 << 7,
        Buildings       = 1 << 8,
        Waypoints       = 1 << 9,

        Boundaries      = 1 << 10,
        WaypointsIndic  = 1 << 11,
        FootballArea    = 1 << 12,
        CellTriggers    = 1 << 13,
        TechnoTriggers  = 1 << 14,
        BuildingRebuild = 1 << 15,
        BuildingFakes   = 1 << 16,

        OverlayAll = Resources | Walls | Overlay,
        Technos = Terrain | Walls | Infantry | Units | Buildings | BuildingFakes,
        MapLayers = Terrain | Resources | Walls | Overlay | Smudge | Infantry | Units | Buildings | Waypoints,
        Indicators = Boundaries | WaypointsIndic | CellTriggers | TechnoTriggers | BuildingRebuild | BuildingFakes | FootballArea,
        All = int.MaxValue
    }

    public class MapContext : ITypeDescriptorContext
    {
        public IContainer Container { get; private set; }

        public object Instance { get; private set; }

        public PropertyDescriptor PropertyDescriptor { get; private set; }

        public Map Map => Instance as Map;

        public readonly bool FractionalPercentages;

        public MapContext(Map map, bool fractionalPercentages)
        {
            Instance = map;
            FractionalPercentages = fractionalPercentages;
        }

        public object GetService(Type serviceType) => null;

        public void OnComponentChanged() { }

        public bool OnComponentChanging() => true;
    }

    public class Map : ICloneable
    {
        private int updateCount = 0;
        private bool updating = false;
        private IDictionary<MapLayerFlag, ISet<Point>> invalidateLayers = new Dictionary<MapLayerFlag, ISet<Point>>();
        private bool invalidateOverlappers;

        public event EventHandler<MapRefreshEventArgs> RulesChanged;
        public void NotifyRulesChanges(ISet<Point> refreshPoints)
        {
            if (RulesChanged != null)
            {
                this.RulesChanged(this, new MapRefreshEventArgs(refreshPoints));
            }
        }

        public event EventHandler<MapRefreshEventArgs> MapContentsChanged;
        public void NotifyMapContentsChanged(ISet<Point> refreshPoints)
        {
            if (MapContentsChanged != null)
            {
                this.MapContentsChanged(this, new MapRefreshEventArgs(refreshPoints));
            }
        }

        public readonly BasicSection BasicSection;

        public readonly MapSection MapSection;

        public readonly BriefingSection BriefingSection = new BriefingSection();

        public readonly SteamSection SteamSection = new SteamSection();

        public TheaterType Theater { get => MapSection.Theater; set => MapSection.Theater = value; }

        public Point TopLeft
        {
            get => new Point(MapSection.X, MapSection.Y);
            set { MapSection.X = value.X; MapSection.Y = value.Y; }
        }

        public Size Size
        {
            get => new Size(MapSection.Width, MapSection.Height);
            set { MapSection.Width = value.Width; MapSection.Height = value.Height; }
        }

        public Rectangle Bounds
        {
            get => new Rectangle(TopLeft, Size);
            set { MapSection.X = value.Left; MapSection.Y = value.Top; MapSection.Width = value.Width; MapSection.Height = value.Height; }
        }

        public readonly Type HouseType;

        public readonly HouseType[] HouseTypes;

        public readonly HouseType[] HouseTypesIncludingNone;

        public readonly IEnumerable<TeamColor> FlagColors;

        public readonly List<TheaterType> TheaterTypes;

        public readonly List<TemplateType> TemplateTypes;

        public readonly List<TerrainType> TerrainTypes;

        public readonly List<OverlayType> OverlayTypes;

        public readonly List<SmudgeType> SmudgeTypes;

        public readonly string[] EventTypes;
        public readonly HashSet<string> CellEventTypes;
        public readonly HashSet<string> UnitEventTypes;
        public readonly HashSet<string> StructureEventTypes;
        public readonly HashSet<string> TerrainEventTypes;

        public readonly string[] ActionTypes;
        public readonly HashSet<string> CellActionTypes;
        public readonly HashSet<string> UnitActionTypes;
        public readonly HashSet<string> StructureActionTypes;
        public readonly HashSet<string> TerrainActionTypes;

        public readonly string[] MissionTypes;

        public readonly string DefaultMissionArmed;

        public readonly string DefaultMissionUnarmed;

        public readonly string DefaultMissionHarvest;

        public readonly List<DirectionType> BuildingDirectionTypes;

        public readonly List<DirectionType> UnitDirectionTypes;

        public readonly List<InfantryType> AllInfantryTypes;

        public List<InfantryType> InfantryTypes
        {
            get
            {
                if (BasicSection == null || !BasicSection.ExpansionEnabled)
                {
                    return AllInfantryTypes.Where(inf => !inf.IsExpansionUnit).ToList();
                }
                return AllInfantryTypes.ToList();
            }
        }

        public readonly List<UnitType> AllUnitTypes;
        public List<UnitType> UnitTypes
        {
            get
            {
                if (BasicSection == null || !BasicSection.ExpansionEnabled)
                {
                    return AllUnitTypes.Where(un => !un.IsExpansionUnit).ToList();
                }
                return AllUnitTypes.ToList();
            }
        }

        public readonly List<BuildingType> BuildingTypes;

        public readonly List<ITechnoType> AllTeamTechnoTypes;
        public List<ITechnoType> TeamTechnoTypes
        {
            get
            {
                if (BasicSection == null || !BasicSection.ExpansionEnabled)
                {
                    return AllTeamTechnoTypes.Where(tc => (tc is UnitType ut && !ut.IsExpansionUnit) || (tc is InfantryType it && !it.IsExpansionUnit)).ToList();
                }
                return AllTeamTechnoTypes.ToList();
            }
        }

        public readonly TeamMission[] TeamMissionTypes;

        public readonly CellMetrics Metrics;

        public readonly CellGrid<Template> Templates;

        public readonly CellGrid<Overlay> Overlay;

        public readonly CellGrid<Smudge> Smudge;

        public readonly OccupierSet<ICellOccupier> Technos;

        public readonly OccupierSet<ICellOccupier> Buildings;

        public readonly OverlapperSet<ICellOverlapper> Overlappers;

        public readonly Waypoint[] Waypoints;
        
        public event EventHandler<EventArgs> WaypointsUpdated;

        public void NotifyWaypointsUpdate()
        {
            if (WaypointsUpdated != null)
                WaypointsUpdated(this, new EventArgs());
        }

        public readonly CellGrid<CellTrigger> CellTriggers;

        public event EventHandler<EventArgs> TriggersUpdated;

        public void NotifyTriggersUpdate()
        {
            if (TriggersUpdated != null)
                TriggersUpdated(this, new EventArgs());
        }

        private List<Trigger> triggers;
        public List<Trigger> Triggers
        {
            get { return triggers; }
            set
            {
                triggers = value;
                // Only an actual replacing of the list will call these, but they can be called manually after an update.
                // A bit more manual than the whole ObservableCollection system, but a lot less cumbersome.
                //CleanUpTriggers();
                NotifyTriggersUpdate();
            }
        }

        public readonly List<TeamType> TeamTypes;

        public House[] Houses;

        public House[] HousesIncludingNone;

        public readonly List<string> MovieTypes;

        public readonly List<string> ThemeTypes;

        public int TiberiumOrGoldValue { get; set; }

        public int GemValue { get; set; }

        public int TotalResources
        {
            get
            {
                int totalResources = 0;
                foreach (var (cell, value) in Overlay)
                {
                    if (value.Type.IsResource)
                    {
                        totalResources += (value.Icon + 1) * (value.Type.IsGem ? GemValue : TiberiumOrGoldValue);
                    }
                }
                return totalResources;
            }
        }

        public Map(BasicSection basicSection, TheaterType theater, Size cellSize, Type houseType, IEnumerable<HouseType> houseTypes,
            IEnumerable<TeamColor> flagColors, IEnumerable<TheaterType> theaterTypes, IEnumerable<TemplateType> templateTypes,
            IEnumerable<TerrainType> terrainTypes, IEnumerable<OverlayType> overlayTypes, IEnumerable<SmudgeType> smudgeTypes,
            IEnumerable<string> eventTypes, IEnumerable<string> cellEventTypes, IEnumerable<string> unitEventTypes, IEnumerable<string> structureEventTypes, IEnumerable<string> terrainEventTypes,
            IEnumerable<string> actionTypes, IEnumerable<string> cellActionTypes, IEnumerable<string> unitActionTypes, IEnumerable<string> structureActionTypes, IEnumerable<string> terrainActionTypes,
            IEnumerable<string> missionTypes, IEnumerable<DirectionType> unitDirectionTypes, IEnumerable<DirectionType> buildingDirectionTypes, IEnumerable<InfantryType> infantryTypes,
            IEnumerable<UnitType> unitTypes, IEnumerable<BuildingType> buildingTypes, IEnumerable<TeamMission> teamMissionTypes,
            IEnumerable<ITechnoType> teamTechnoTypes, IEnumerable<Waypoint> waypoints, IEnumerable<string> movieTypes, IEnumerable<string> themeTypes)
        {
            MapSection = new MapSection(cellSize);
            BasicSection = basicSection;
            HouseType = houseType;
            HouseTypesIncludingNone = houseTypes.ToArray();
            HouseTypes = HouseTypesIncludingNone.Where(h => h.ID >= 0).ToArray();
            FlagColors = flagColors.ToArray();
            TheaterTypes = new List<TheaterType>(theaterTypes);
            TemplateTypes = new List<TemplateType>(templateTypes);
            TerrainTypes = new List<TerrainType>(terrainTypes);
            OverlayTypes = new List<OverlayType>(overlayTypes);
            SmudgeTypes = new List<SmudgeType>(smudgeTypes);
            EventTypes = eventTypes.ToArray();
            CellEventTypes = cellEventTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            UnitEventTypes = unitEventTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            StructureEventTypes = structureEventTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            TerrainEventTypes = terrainEventTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            CellActionTypes = cellActionTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            UnitActionTypes = unitActionTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            StructureActionTypes = structureActionTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            TerrainActionTypes = terrainActionTypes.ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            ActionTypes = actionTypes.ToArray();
            MissionTypes = missionTypes.ToArray();
            DefaultMissionArmed = MissionTypes.Where(m => m.Equals("Guard")).FirstOrDefault() ?? MissionTypes.First();
            DefaultMissionUnarmed = MissionTypes.Where(m => m.Equals("Stop")).FirstOrDefault() ?? MissionTypes.First();
            // Reverts to "stop" if there are no resources (RA indoor)
            DefaultMissionHarvest = OverlayTypes.Any(ov => ov.IsResource) ? MissionTypes.Where(m => m.Equals("Harvest")).FirstOrDefault() ?? DefaultMissionUnarmed : DefaultMissionUnarmed;
            UnitDirectionTypes = new List<DirectionType>(unitDirectionTypes);
            BuildingDirectionTypes = new List<DirectionType>(buildingDirectionTypes);
            AllInfantryTypes = new List<InfantryType>(infantryTypes);
            AllUnitTypes = new List<UnitType>(unitTypes);
            BuildingTypes = new List<BuildingType>(buildingTypes);
            TeamMissionTypes = teamMissionTypes.ToArray();
            AllTeamTechnoTypes = new List<ITechnoType>(teamTechnoTypes);
            MovieTypes = new List<string>(movieTypes);
            ThemeTypes = new List<string>(themeTypes);

            Metrics = new CellMetrics(cellSize);
            Templates = new CellGrid<Template>(Metrics);
            Overlay = new CellGrid<Overlay>(Metrics);
            Smudge = new CellGrid<Smudge>(Metrics);
            Technos = new OccupierSet<ICellOccupier>(Metrics);
            Buildings = new OccupierSet<ICellOccupier>(Metrics);
            Overlappers = new OverlapperSet<ICellOverlapper>(Metrics);
            triggers = new List<Trigger>();
            TeamTypes = new List<TeamType>();
            HousesIncludingNone = HouseTypesIncludingNone.Select(t => { var h = (House)Activator.CreateInstance(HouseType, t); h.SetDefault(); return h; }).ToArray();
            Houses = HousesIncludingNone.Where(h => h.Type.ID >= 0).ToArray();
            Waypoints = waypoints.ToArray();
            for (int i = 0; i < Waypoints.Length; ++i)
            {
                // Deep clone, with current metric to allow showing waypoints as cell coordinates.
                Waypoints[i] = new Waypoint(Waypoints[i].Name, Waypoints[i].Flag, Metrics, Waypoints[i].Cell);
            }
            CellTriggers = new CellGrid<CellTrigger>(Metrics);

            MapSection.SetDefault();
            BriefingSection.SetDefault();
            SteamSection.SetDefault();
            Templates.Clear();
            Overlay.Clear();
            Smudge.Clear();
            Technos.Clear();
            Overlappers.Clear();
            CellTriggers.Clear();

            TopLeft = new Point(1, 1);
            Size = Metrics.Size - new Size(2, 2);
            Theater = theater;

            Overlay.CellChanged += Overlay_CellChanged;
            Technos.OccupierAdded += Technos_OccupierAdded;
            Technos.OccupierRemoved += Technos_OccupierRemoved;
            Buildings.OccupierAdded += Buildings_OccupierAdded;
            Buildings.OccupierRemoved += Buildings_OccupierRemoved;
        }

        public void BeginUpdate()
        {
            updateCount++;
        }

        public void EndUpdate()
        {
            if (--updateCount == 0)
            {
                Update();
            }
        }

        public void InitTheater(GameType gameType)
        {
            foreach (var templateType in TemplateTypes.Where(itm => itm.Theaters == null || itm.Theaters.Contains(Theater)))
            {
                templateType.Init(Theater);
            }
            foreach (var smudgeType in SmudgeTypes.Where(itm => itm.Theaters == null || itm.Theaters.Contains(Theater)))
            {
                smudgeType.Init(Theater);
            }
            foreach (var overlayType in OverlayTypes.Where(itm => itm.Theaters == null || itm.Theaters.Contains(Theater)))
            {
                overlayType.Init(gameType, Theater);
            }
            foreach (var terrainType in TerrainTypes.Where(itm => itm.Theaters == null || itm.Theaters.Contains(Theater)))
            {
                terrainType.Init(Theater);
            }
            // Ignore expansion status for these; they can still be enabled later.
            DirectionType infDir = UnitDirectionTypes.Where(d => d.Facing == FacingType.South).First();
            foreach (var infantryType in AllInfantryTypes)
            {
                infantryType.Init(gameType, Theater, HouseTypesIncludingNone.Where(h => h.Equals(infantryType.OwnerHouse)).FirstOrDefault(), infDir);
            }
            DirectionType unitDir = UnitDirectionTypes.Where(d => d.Facing == FacingType.SouthWest).First();
            foreach (var unitType in AllUnitTypes)
            {
                unitType.Init(gameType, Theater, HouseTypesIncludingNone.Where(h => h.Equals(unitType.OwnerHouse)).FirstOrDefault(), unitDir);
            }
            DirectionType bldDir = UnitDirectionTypes.Where(d => d.Facing == FacingType.North).First();
            foreach (var buildingType in BuildingTypes.Where(itm => itm.Theaters == null || itm.Theaters.Contains(Theater)))
            {
                buildingType.Init(gameType, Theater, HouseTypesIncludingNone.Where(h => h.Equals(buildingType.OwnerHouse)).FirstOrDefault(), bldDir);
            }
        }

        private void Update()
        {
            updating = true;
            if (invalidateLayers.TryGetValue(MapLayerFlag.Resources, out ISet<Point> locations))
            {
                UpdateResourceOverlays(locations);
            }
            if (invalidateLayers.TryGetValue(MapLayerFlag.Walls, out locations))
            {
                UpdateWallOverlays(locations);
            }
            if (invalidateLayers.TryGetValue(MapLayerFlag.Overlay, out locations))
            {
                UpdateConcreteOverlays(locations);
                //UpdateConcreteOverlays_ORIG(locations);
            }
            if (invalidateOverlappers)
            {
                Overlappers.Clear();
                foreach (var (location, techno) in Technos)
                {
                    if (techno is ICellOverlapper)
                    {
                        Overlappers.Add(location, techno as ICellOverlapper);
                    }
                }
            }
            invalidateLayers.Clear();
            invalidateOverlappers = false;
            updating = false;
        }

        private void UpdateResourceOverlays(ISet<Point> locations)
        {
            var tiberiumCounts = new int[] { 0, 1, 3, 4, 6, 7, 8, 10, 11 };
            var gemCounts = new int[] { 0, 0, 0, 1, 1, 1, 2, 2, 2 };
            foreach (var (cell, overlay) in Overlay.IntersectsWith(locations).Where(o => o.Value.Type.IsResource))
            {
                int count = 0;
                foreach (var facing in CellMetrics.AdjacentFacings)
                {
                    var adjacentTiberium = Overlay.Adjacent(cell, facing);
                    if (adjacentTiberium?.Type.IsResource ?? false)
                    {
                        count++;
                    }
                }
                overlay.Icon = overlay.Type.IsGem ? gemCounts[count] : tiberiumCounts[count];
            }
        }

        private void UpdateWallOverlays(ISet<Point> locations)
        {
            foreach (var (cell, overlay) in Overlay.IntersectsWith(locations).Where(o => o.Value.Type.IsWall))
            {
                var northWall = Overlay.Adjacent(cell, FacingType.North);
                var eastWall = Overlay.Adjacent(cell, FacingType.East);
                var southWall = Overlay.Adjacent(cell, FacingType.South);
                var westWall = Overlay.Adjacent(cell, FacingType.West);
                int icon = 0;
                if (northWall?.Type == overlay.Type)
                {
                    icon |= 1;
                }
                if (eastWall?.Type == overlay.Type)
                {
                    icon |= 2;
                }
                if (southWall?.Type == overlay.Type)
                {
                    icon |= 4;
                }
                if (westWall?.Type == overlay.Type)
                {
                    icon |= 8;
                }
                overlay.Icon = icon;
            }
        }

        private void UpdateConcreteOverlays(ISet<Point> locations)
        {
            foreach (var (cell, overlay) in Overlay.IntersectsWith(locations).Where(o => o.Value.Type.IsConcrete))
            {
                // in order: top, topnext, next, bottomnext, bottom
                FacingType[] even = { FacingType.North, FacingType.NorthWest, FacingType.West, FacingType.SouthWest, FacingType.South };
                FacingType[] odd = { FacingType.North, FacingType.NorthEast, FacingType.East, FacingType.SouthEast, FacingType.South };
                int isodd = cell & 1;
                FacingType[] cells = isodd != 0 ? odd : even;
                Boolean[] conc = new bool[cells.Length];
                for (int i = 0; i < cells.Length; i++)
                {
                    var neighbor = Overlay.Adjacent(cell, cells[i]);
                    if (neighbor != null && neighbor.Type == overlay.Type)
                    {
                        int ic = overlay.Icon;
                        if (ic < 4 || (ic > 7 && ic < 12))
                        {
                            conc[i] = true;
                        }
                    }
                }
                // Unified logic so the operation becomes identical for the even and odd cells.
                // This still isn't a 100% match with the game, but that's because the version in-game is a buggy mess.
                bool top = conc[0];
                bool topnext = conc[1];
                bool next = conc[2];
                bool bottomnext = conc[3];
                bool bottom = conc[4];

                int icon = 0;
                if (top && next || topnext && next)
                {
                    icon = bottom ? 1 : 5;
                }
                else if (bottom && next || bottom && bottomnext)
                {
                    icon = topnext ? 1 : 4;
                }
                else if (top && topnext)
                {
                    icon = 5;
                }
                icon = icon == 0 ? isodd : (icon * 2) + 1 - isodd;
                overlay.Icon = icon;
            }
        }

        private enum ConcreteEnum
        {
            C_NONE = -1,
            C_LEFT = 0,
            C_RIGHT = 1,
            C_RIGHT_UPDOWN = 2,
            C_LEFT_UPDOWN = 3,
            C_UP_RIGHT = 4,
            C_UP_LEFT = 5,
            C_DOWN_RIGHT = 6,
            C_DOWN_LEFT = 7,
            C_RIGHT_DOWN = 8,
            C_LEFT_DOWN = 9,
            C_RIGHT_UP = 10,
            C_LEFT_UP = 11,
            C_UPDOWN_RIGHT = 12,
            C_UPDOWN_LEFT = 13
        }

        private void UpdateConcreteOverlays_ORIG(ISet<Point> locations)
        {
            foreach (var (cell, overlay) in Overlay.IntersectsWith(locations).Where(o => o.Value.Type.IsConcrete))
            {
                // Original logic as it is in the game code. Still doesn't match reality, probably due to bugs in the logic to add side cells.
                FacingType[] odd = { FacingType.North, FacingType.NorthEast, FacingType.East, FacingType.SouthEast, FacingType.South };
                FacingType[] even = { FacingType.North, FacingType.South, FacingType.SouthWest, FacingType.West, FacingType.NorthWest };
                int isodd = cell & 1;
                FacingType[] cells = isodd != 0 ? odd : even;
                int index = 0;
                for (int i = 0; i < cells.Length; i++)
                {
                    var neighbor = Overlay.Adjacent(cell, cells[i]);
                    if (neighbor != null && neighbor.Type == overlay.Type)
                    {
                        int ic = overlay.Icon;
                        if (ic < 4 || (ic > 7 && ic < 12))
                        {
                            index |= (1 << i);
                        }
                    }
                }
                const int OF_N  = 0x01;
                const int OF_NE = 0x02;
                const int OF_E  = 0x04;
                const int OF_SE = 0x08;
                const int OF_S  = 0x10;
                const int EF_N  = 0x01;
                const int EF_NW = 0x10;
                const int EF_W  = 0x08;
                const int EF_SW = 0x04;
                const int EF_S  = 0x02;
                ConcreteEnum icon = 0;
                if (isodd != 0)
                {
                    switch (index)
                    {
                        case OF_NE:
                        case OF_N | OF_NE:
                        case OF_E | OF_N:
                        case OF_E | OF_NE:
                        case OF_N | OF_NE | OF_E:
                        case OF_S | OF_N | OF_NE:
                            icon = ConcreteEnum.C_RIGHT_UP;      // right - up
                            break;
                        case OF_SE:
                        case OF_E | OF_SE:
                        case OF_S | OF_SE:
                        case OF_S | OF_E:
                        case OF_S | OF_SE | OF_E:
                        case OF_S | OF_SE | OF_N:
                            icon = ConcreteEnum.C_RIGHT_DOWN;        // right - down
                            break;
                        case OF_SE | OF_NE:
                        case OF_SE | OF_NE | OF_N:
                        case OF_SE | OF_NE | OF_S:
                        case OF_SE | OF_NE | OF_S | OF_N:
                        case OF_SE | OF_E | OF_N:
                        case OF_SE | OF_E | OF_NE | OF_N:
                        case OF_S | OF_E | OF_N:
                        case OF_S | OF_E | OF_NE:
                        case OF_S | OF_E | OF_NE | OF_N:
                        case OF_S | OF_SE | OF_E | OF_N:
                        case OF_S | OF_SE | OF_E | OF_NE | OF_N:
                        case OF_S | OF_SE | OF_E | OF_NE:
                            icon = ConcreteEnum.C_RIGHT_UPDOWN;      // right - up - down
                            break;
                        default:
                            icon = ConcreteEnum.C_RIGHT;     // right
                            break;
                    }
                }
                else
                {
                    switch (index)
                    {
                        case EF_NW:
                        case EF_NW | EF_N:
                        case EF_W | EF_N:
                        case EF_NW | EF_W | EF_N:
                        case EF_NW | EF_W:
                        case EF_NW | EF_S | EF_N:
                            icon = ConcreteEnum.C_LEFT_UP;       // left - up
                            break;
                        case EF_SW:
                        case EF_SW | EF_S:
                        case EF_W | EF_S:
                        case EF_W | EF_SW | EF_S:
                        case EF_W | EF_SW:
                        case EF_SW | EF_S | EF_N:
                            icon = ConcreteEnum.C_LEFT_DOWN;     // left - down
                            break;
                        case EF_NW | EF_SW:
                        case EF_NW | EF_SW | EF_N:
                        case EF_NW | EF_SW | EF_S:
                        case EF_NW | EF_SW | EF_S | EF_N:
                        case EF_W | EF_S | EF_N:
                        case EF_W | EF_SW | EF_N:
                        case EF_W | EF_SW | EF_S | EF_N:
                        case EF_NW | EF_W | EF_S:
                        case EF_NW | EF_W | EF_S | EF_N:
                        case EF_NW | EF_W | EF_SW | EF_S | EF_N:
                        case EF_NW | EF_W | EF_SW | EF_N:
                        case EF_NW | EF_W | EF_SW | EF_S:
                            icon = ConcreteEnum.C_LEFT_UPDOWN;       // left - up - down
                            break;
                        default:
                            icon = ConcreteEnum.C_LEFT;      // left
                            break;
                    }
                }
                overlay.Icon = (int)icon;
            }
        }

        public string GetCellDescription(Point location, Point subPixel)
        {
            if (!Metrics.GetCell(location, out int cell))
            {
                return "No cell";
            }
            var sb = new StringBuilder();
            sb.AppendFormat("X = {0}, Y = {1}, Cell = {2}", location.X, location.Y, cell);
            var template = Templates[cell];
            var templateType = template?.Type;
            if (templateType != null)
            {
                sb.AppendFormat(", Template = {0} ({1})", templateType.DisplayName, template.Icon);
            }
            var smudge = Smudge[cell];
            var smudgeType = smudge?.Type;
            if (smudgeType != null)
            {
                sb.AppendFormat(", Smudge = {0}", smudgeType.DisplayName);
            }
            var overlay = Overlay[cell];
            var overlayType = overlay?.Type;
            if (overlayType != null)
            {
                sb.AppendFormat(", Overlay = {0}", overlayType.DisplayName);
            }
            var terrain = Technos[location] as Terrain;
            var terrainType = terrain?.Type;
            if (terrainType != null)
            {
                sb.AppendFormat(", Terrain = {0}", terrainType.DisplayName);
            }
            if (Technos[location] is InfantryGroup infantryGroup)
            {
                InfantryStoppingType i = InfantryGroup.ClosestStoppingTypes(subPixel).First();
                Infantry inf = infantryGroup.Infantry[(int)i];
                if (inf != null)
                {
                    sb.AppendFormat(", Infantry = {0} ({1})", inf.Type.DisplayName, InfantryGroup.GetStoppingTypeName(i));
                }
            }
            var unit = Technos[location] as Unit;
            var unitType = unit?.Type;
            if (unitType != null)
            {
                sb.AppendFormat(", Unit = {0}", unitType.DisplayName);
            }
            var building = Buildings[location] as Building;
            var buildingType = building?.Type;
            if (buildingType != null)
            {
                sb.AppendFormat(", Building = {0}", buildingType.DisplayName);
            }
            return sb.ToString();
        }

        public HouseType GetBaseHouse(GameType gameType)
        {
            House noneHouse = HousesIncludingNone.Where(h => h.Type.ID < 0).FirstOrDefault();
            if (noneHouse != null && gameType == GameType.TiberianDawn)
            {
                return noneHouse.Type;
            }
            else
            {
                return HouseTypes.Where(h => h.Equals(BasicSection.BasePlayer)).FirstOrDefault() ?? HouseTypes.First();
            }
        }

        private void RemoveBibs(Building building)
        {
            var bibCells = Smudge.IntersectsWith(building.BibCells).Where(x => x.Value.Type.IsAutoBib).Select(x => x.Cell).ToArray();
            foreach (var cell in bibCells)
            {
                Smudge[cell] = null;
            }
            building.BibCells.Clear();
        }

        private void AddBibs(Point location, Building building)
        {
            Dictionary<Point, Smudge> bibPoints = building.GetBib(location, SmudgeTypes);
            if (bibPoints == null)
            {
                return;
            }
            foreach(Point p in bibPoints.Keys)
            {
                if (Metrics.GetCell(p, out int subCell))
                {
                    Smudge[subCell] = bibPoints[p];
                    building.BibCells.Add(subCell);
                }
            }
        }

        public Map Clone()
        {
            Waypoint[] wpPreview = new Waypoint[Waypoints.Length + 1];
            Array.Copy(Waypoints, wpPreview, Waypoints.Length);
            wpPreview[Waypoints.Length] = new Waypoint("", null);
            // This is a shallow clone; the map is new, but the placed content all still reference the original objects.
            // These shallow copies are used for map preview during editing, where dummy objects can be added without any issue.
            var map = new Map(BasicSection, Theater, Metrics.Size, HouseType, HouseTypesIncludingNone,
                FlagColors, TheaterTypes, TemplateTypes, TerrainTypes, OverlayTypes, SmudgeTypes,
                EventTypes, CellEventTypes, UnitEventTypes, StructureEventTypes, TerrainEventTypes,
                ActionTypes, CellActionTypes, UnitActionTypes, StructureActionTypes, TerrainActionTypes,
                MissionTypes, UnitDirectionTypes, BuildingDirectionTypes, AllInfantryTypes, AllUnitTypes, BuildingTypes, TeamMissionTypes,
                AllTeamTechnoTypes, wpPreview, MovieTypes, ThemeTypes)
            {
                TopLeft = TopLeft,
                Size = Size
            };
            map.BeginUpdate();
            MapSection.CopyTo(map.MapSection);
            BriefingSection.CopyTo(map.BriefingSection);
            SteamSection.CopyTo(map.SteamSection);
            Array.Copy(Houses, map.Houses, map.Houses.Length);
            map.Triggers.AddRange(Triggers);
            Templates.CopyTo(map.Templates);
            Overlay.CopyTo(map.Overlay);
            Smudge.CopyTo(map.Smudge);
            CellTriggers.CopyTo(map.CellTriggers);
            foreach (var (location, occupier) in Technos)
            {
                if (occupier is InfantryGroup infantryGroup)
                {
                    // This creates an InfantryGroup not linked to its infantry, but it is necessary
                    // to ensure that the real InfantryGroups are not polluted with dummy objects.
                    var newInfantryGroup = new InfantryGroup();
                    Array.Copy(infantryGroup.Infantry, newInfantryGroup.Infantry, newInfantryGroup.Infantry.Length);
                    map.Technos.Add(location, newInfantryGroup);
                }
                else if (!(occupier is Building))
                {
                    map.Technos.Add(location, occupier);
                }
            }
            foreach (var (location, building) in Buildings)
            {
                // Silly side effect: this fixes any building bibs.
                map.Buildings.Add(location, building);
            }
            map.TeamTypes.AddRange(TeamTypes);
            map.EndUpdate();
            return map;
        }

        public IEnumerable<Trigger> FilterCellTriggers()
        {
            return FilterCellTriggers(this.Triggers);
        }

        public IEnumerable<Trigger> FilterCellTriggers(IEnumerable<Trigger> triggers)
        {
            foreach (Trigger trigger in FilterTriggersByEvent(CellEventTypes, triggers).Concat(FilterTriggersByAction(CellActionTypes, triggers).Distinct()))
            {
                yield return trigger;
            }
        }

        public IEnumerable<Trigger> FilterUnitTriggers()
        {
            return FilterUnitTriggers(this.Triggers);
        }

        public IEnumerable<Trigger> FilterUnitTriggers(IEnumerable<Trigger> triggers)
        {
            foreach (Trigger trigger in FilterTriggersByEvent(UnitEventTypes, triggers).Concat(FilterTriggersByAction(UnitActionTypes, triggers).Distinct()))
            {
                yield return trigger;
            }
        }

        public IEnumerable<Trigger> FilterStructureTriggers()
        {
            return FilterStructureTriggers(this.Triggers);
        }

        public IEnumerable<Trigger> FilterStructureTriggers(IEnumerable<Trigger> triggers)
        {
            foreach (Trigger trigger in FilterTriggersByEvent(StructureEventTypes, triggers).Concat(FilterTriggersByAction(StructureActionTypes, triggers).Distinct()))
            {
                yield return trigger;
            }
        }

        public IEnumerable<Trigger> FilterTerrainTriggers()
        {
            return FilterTerrainTriggers(this.Triggers);
        }

        public IEnumerable<Trigger> FilterTerrainTriggers(IEnumerable<Trigger> triggers)
        {
            foreach (Trigger trigger in FilterTriggersByEvent(TerrainEventTypes, triggers).Concat(FilterTriggersByAction(TerrainActionTypes, triggers).Distinct()))
            {
                yield return trigger;
            }
        }

        public static IEnumerable<Trigger> FilterTriggersByEvent(HashSet<String> allowedEventTypes, IEnumerable<Trigger> triggers)
        {
            foreach (Trigger trig in triggers)
            {
                if (trig.Event1 != null && allowedEventTypes.Contains(trig.Event1.EventType)
                    || ((trig.EventControl == TriggerMultiStyleType.Or || trig.EventControl == TriggerMultiStyleType.And)
                        && trig.Event2 != null && allowedEventTypes.Contains(trig.Event2.EventType)))
                {
                    yield return trig;
                }
            }
        }

        public static String MakeAllowedTriggersToolTip(string[] filteredEvents, string[] filteredActions)
        {
            StringBuilder tooltip = new StringBuilder();
            bool hasEvents = filteredEvents != null && filteredEvents.Length > 0;
            bool hasActions = filteredActions != null && filteredActions.Length > 0;
            if (hasEvents)
            {
                tooltip.Append("Allowed trigger events:\n\u2022 ")
                    .Append(String.Join("\n\u2022 ", filteredEvents));
                if (hasActions)
                    tooltip.Append('\n');
            }
            if (hasActions)
            {
                tooltip.Append("Allowed trigger actions:\n\u2022 ")
                    .Append(String.Join("\n\u2022 ", filteredActions));
            }
            return hasEvents || hasActions ? tooltip.ToString() : null;
        }

        public IEnumerable<Trigger> FilterTriggersByAction(HashSet<String> allowedActionTypes, IEnumerable<Trigger> triggers)
        {
            foreach (Trigger trig in triggers)
            {
                if (trig.Action1 != null && allowedActionTypes.Contains(trig.Action1.ActionType)
                    || (trig.Action2 != null && allowedActionTypes.Contains(trig.Action2.ActionType)))
                {
                    yield return trig;
                }
            }
        }

        public IEnumerable<ITechno> GetAllTechnos()
        {
            foreach (var (location, occupier) in Technos)
            {
                if (occupier is InfantryGroup infantryGroup)
                {
                    foreach (var inf in infantryGroup.Infantry)
                    {
                        if (inf != null)
                        {
                            yield return inf;
                        }
                    }
                }
                else if (occupier is ITechno techno)
                {
                    yield return techno;
                }
            }
        }

        public string GetDefaultMission(ITechnoType techno)
        {
            return GetDefaultMission(techno, DefaultMissionArmed);
        }

        public string GetDefaultMission(ITechnoType techno, String currentMission)
        {
            if (techno.IsHarvester)
            {
                return DefaultMissionHarvest;
            }
            if (!techno.IsArmed)
            {
                return DefaultMissionUnarmed;
            }
            if (currentMission == DefaultMissionHarvest || currentMission == DefaultMissionUnarmed)
            {
                return DefaultMissionArmed;
            }
            return currentMission;
        }

        public ICellOccupier FindBlockingObject(int cell, ICellOccupier obj, out int blockingCell)
        {
            blockingCell = -1;
            if (Metrics.GetLocation(cell, out Point p))
            {
                bool[,] mask;
                int ylen;
                int xlen;
                // First check bounds without bib.
                if (obj is BuildingType bld)
                {
                    mask = bld.BaseOccupyMask;
                    ylen = mask.GetLength(0);
                    xlen = mask.GetLength(1);
                    for (var y = 0; y < ylen; ++y)
                    {
                        for (var x = 0; x < xlen; ++x)
                        {
                            if (mask[y, x])
                            {
                                Metrics.GetCell(new Point(p.X + x, p.Y + y), out int targetCell);
                                ICellOccupier techno = Technos[targetCell];
                                ICellOccupier b = Buildings[targetCell];
                                if (techno != null || b != null)
                                {
                                    blockingCell = targetCell;
                                    return techno ?? b;
                                }
                            }
                        }
                    }
                }
                mask = obj.OccupyMask;
                ylen = mask.GetLength(0);
                xlen = mask.GetLength(1);
                for (var y = 0; y < ylen; ++y)
                {
                    for (var x = 0; x < xlen; ++x)
                    {
                        if (mask[y, x])
                        {
                            Metrics.GetCell(new Point(p.X + x, p.Y + y), out int targetCell);
                            ICellOccupier techno = Technos[targetCell];
                            ICellOccupier b = Buildings[targetCell];
                            if (techno != null || b != null)
                            {
                                blockingCell = targetCell;
                                return techno ?? b;
                            }
                        }
                    }
                }
            }
            return null;
        }

        public TGA GeneratePreview(Size previewSize, GameType gameType, bool renderAll, bool sharpen)
        {
            MapLayerFlag toRender = MapLayerFlag.Template | (renderAll ? MapLayerFlag.OverlayAll | MapLayerFlag.Smudge | MapLayerFlag.Technos : MapLayerFlag.Resources);
            return GeneratePreview(previewSize, gameType, toRender, true, true, sharpen);
        }

        public TGA GeneratePreview(Size previewSize, GameType gameType, MapLayerFlag toRender, bool smooth, bool crop, bool sharpen)
        {
            Rectangle mapBounds;
            HashSet<Point> locations;
            if (crop)
            {
                mapBounds = new Rectangle(Bounds.Left * Globals.OriginalTileWidth, Bounds.Top * Globals.OriginalTileHeight,
                    Bounds.Width * Globals.OriginalTileWidth, Bounds.Height * Globals.OriginalTileHeight);
                locations = Bounds.Points().ToHashSet();
            }
            else
            {
                mapBounds = new Rectangle(0, 0, Metrics.Width * Globals.OriginalTileWidth, Metrics.Height * Globals.OriginalTileHeight);
                locations = Metrics.Bounds.Points().ToHashSet();
            }
            var previewScale = Math.Min(previewSize.Width / (float)mapBounds.Width, previewSize.Height / (float)mapBounds.Height);
            var scaledSize = new Size((int)(previewSize.Width / previewScale), (int)(previewSize.Height / previewScale));

            using (var fullBitmap = new Bitmap(Metrics.Width * Globals.OriginalTileWidth, Metrics.Height * Globals.OriginalTileHeight))
            using (var croppedBitmap = new Bitmap(previewSize.Width, previewSize.Height))
            {
                using (var g = Graphics.FromImage(fullBitmap))
                {
                    MapRenderer.SetRenderSettings(g, smooth);
                    MapRenderer.Render(gameType, this, g, locations, toRender, 1);
                }
                using (var g = Graphics.FromImage(croppedBitmap))
                {
                    MapRenderer.SetRenderSettings(g, true);
                    Matrix transform = new Matrix();
                    transform.Scale(previewScale, previewScale);
                    transform.Translate((scaledSize.Width - mapBounds.Width) / 2, (scaledSize.Height - mapBounds.Height) / 2);
                    g.Transform = transform;
                    g.Clear(Color.Black);
                    g.DrawImage(fullBitmap, new Rectangle(0, 0, mapBounds.Width, mapBounds.Height), mapBounds, GraphicsUnit.Pixel);
                }
                if (sharpen)
                {
                    using (var sharpenedImage = croppedBitmap.Sharpen(1.0f))
                    {
                        return TGA.FromBitmap(sharpenedImage);
                    }
                }
                else
                {
                    return TGA.FromBitmap(croppedBitmap);
                }
            }
        }

        public TGA GenerateMapPreview(GameType gameType, bool renderAll)
        {
            return GeneratePreview(Globals.MapPreviewSize, gameType, renderAll, false);
        }

        public TGA GenerateWorkshopPreview(GameType gameType, bool renderAll)
        {
            return GeneratePreview(Globals.WorkshopPreviewSize, gameType, renderAll, true);
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        private void Overlay_CellChanged(object sender, CellChangedEventArgs<Overlay> e)
        {
            if (e.OldValue?.Type.IsWall ?? false)
            {
                Buildings.Remove(e.OldValue);
            }
            if (e.Value?.Type.IsWall ?? false)
            {
                Buildings.Add(e.Location, e.Value);
            }
            if (updating)
            {
                return;
            }
            foreach (var overlay in new Overlay[] { e.OldValue, e.Value })
            {
                if (overlay == null)
                {
                    continue;
                }
                MapLayerFlag layer;
                if (overlay.Type.IsResource)
                {
                    layer = MapLayerFlag.Resources;
                }
                else if (overlay.Type.IsWall)
                {
                    layer = MapLayerFlag.Walls;
                }
                else if (overlay.Type.IsConcrete)
                {
                    layer = MapLayerFlag.Overlay;
                }
                else
                {
                    continue;
                }
                if (!invalidateLayers.TryGetValue(layer, out ISet<Point> locations))
                {
                    locations = new HashSet<Point>();
                    invalidateLayers[layer] = locations;
                }
                locations.UnionWith(Rectangle.Inflate(new Rectangle(e.Location, new Size(1, 1)), 1, 1).Points());
            }
            if (updateCount == 0)
            {
                Update();
            }
        }

        private void Technos_OccupierAdded(object sender, OccupierAddedEventArgs<ICellOccupier> e)
        {
            if (e.Occupier is ICellOverlapper overlapper)
            {
                if (updateCount == 0)
                {
                    Overlappers.Add(e.Location, overlapper);
                }
                else
                {
                    invalidateOverlappers = true;
                }
            }
        }

        private void Technos_OccupierRemoved(object sender, OccupierRemovedEventArgs<ICellOccupier> e)
        {
            if (e.Occupier is ICellOverlapper overlapper)
            {
                if (updateCount == 0)
                {
                    Overlappers.Remove(overlapper);
                }
                else
                {
                    invalidateOverlappers = true;
                }
            }
        }

        private void Buildings_OccupierAdded(object sender, OccupierAddedEventArgs<ICellOccupier> e)
        {
            if (e.Occupier is Building building)
            {
                Technos.Add(e.Location, e.Occupier, building.Type.BaseOccupyMask);
                AddBibs(e.Location, building);
            }
            else
            {
                Technos.Add(e.Location, e.Occupier);
            }
        }

        private void Buildings_OccupierRemoved(object sender, OccupierRemovedEventArgs<ICellOccupier> e)
        {
            if (e.Occupier is Building building)
            {
                RemoveBibs(building);
            }
            Technos.Remove(e.Occupier);
        }

        public bool RemoveExpansionUnits()
        {
            HashSet<Point> refreshPoints = new HashSet<Point>();
            bool changed = false;
            if (BasicSection.ExpansionEnabled)
            {
                // Expansion is enabled. Nothing to do.
                return false;
            }
            // Technos on map
            List<(Point, ICellOccupier)> toDelete = new List<(Point, ICellOccupier)>();
            foreach ((Point p, ICellOccupier occup) in Technos)
            {
                if (occup is Unit un)
                {
                    if (un.Type.IsExpansionUnit)
                    {
                        toDelete.Add((p, occup));
                    }
                }
                else if (occup is InfantryGroup ifg)
                {
                    if (ifg.Infantry.Any(inf => inf != null && inf.Type.IsExpansionUnit))
                    {
                        toDelete.Add((p, occup));
                    }
                }
            }
            foreach ((Point point, ICellOccupier occup) in toDelete)
            {
                if (occup is Unit un)
                {
                    Rectangle? refreshArea = Overlappers[un];
                    if (refreshArea.HasValue)
                    {
                        refreshPoints.UnionWith(refreshArea.Value.Points());
                    }
                    //mapPanel.Invalidate(map, un);
                    Technos.Remove(occup);
                    changed = true;
                }
                else if (occup is InfantryGroup infantryGroup)
                {
                    Infantry[] inf = infantryGroup.Infantry;
                    for (int i = 0; i < inf.Length; ++i)
                    {
                        if (inf[i] != null && (inf[i].Type.Flag & UnitTypeFlag.IsExpansionUnit) == UnitTypeFlag.IsExpansionUnit)
                        {
                            inf[i] = null;
                            changed = true;
                        }
                    }
                    bool delGroup = inf.All(i => i == null);
                    Rectangle? refreshArea = Overlappers[infantryGroup];
                    if (refreshArea.HasValue)
                    {
                        refreshPoints.UnionWith(refreshArea.Value.Points());
                    }
                    //mapPanel.Invalidate(map, infantryGroup);
                    if (delGroup)
                    {
                        Technos.Remove(infantryGroup);
                    }
                }
            }
            // Teamtypes
            foreach (TeamType teamtype in TeamTypes)
            {
                List<TeamTypeClass> toRemove = new List<TeamTypeClass>();
                foreach (TeamTypeClass ttclass in teamtype.Classes)
                {
                    if ((ttclass.Type is UnitType ut && ut.IsExpansionUnit) || (ttclass.Type is InfantryType it && it.IsExpansionUnit))
                    {
                        toRemove.Add(ttclass);
                    }
                }
                foreach (TeamTypeClass ttclass in toRemove)
                {
                    teamtype.Classes.Remove(ttclass);
                    changed = true;
                }
            }
            this.NotifyMapContentsChanged(refreshPoints);
            return changed;
        }

        /// <summary>
        /// Applies trigger renames, and does the checks to see whether all attached objects are still valid for the trigger type they are connected to.
        /// The undo and redo lists can be used to track these actions, with the celtrigger location keeping track of cell triggers that might get removed by this.
        /// </summary>
        /// <param name="renameActions">All rename actions</param>
        /// <param name="undoList">Undo list, linking objects to their original trigger value</param>
        /// <param name="redoList">Redo list, linking objects to their final trigger value</param>
        /// <param name="cellTriggerLocations">Locations for all modified cell triggers</param>
        /// <param name="newTriggers">Triggers list to use to check for trigger links to objects.</param>
        public void ApplyTriggerChanges(List<(String Name1, String Name2)> renameActions, out Dictionary<object, string> undoList, out Dictionary<object, string> redoList, out Dictionary<CellTrigger, int> cellTriggerLocations, List<Trigger> newTriggers)
        {
            undoList = new Dictionary<object, string>();
            redoList = new Dictionary<object, string>();
            cellTriggerLocations = new Dictionary<CellTrigger, int>();
            foreach ((String name1, String name2) in renameActions)
            {
                if (Trigger.IsEmpty(name1))
                {
                    continue;
                }
                foreach ((Point location, Building building) in Buildings.OfType<Building>().Where(x => x.Occupier.IsPrebuilt))
                {
                    if (String.Equals(building.Trigger, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!undoList.ContainsKey(building))
                            undoList[building] = building.Trigger;
                        redoList[building] = name2;
                        building.Trigger = name2;
                    }
                }
                foreach (ITechno techno in GetAllTechnos())
                {
                    if (String.Equals(techno.Trigger, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!undoList.ContainsKey(techno))
                        {
                            undoList[techno] = techno.Trigger;
                        }
                        redoList[techno] = name2;
                        techno.Trigger = name2;
                    }
                }
                foreach (TeamType team in TeamTypes)
                {
                    if (String.Equals(team.Trigger, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!undoList.ContainsKey(team))
                        {
                            undoList[team] = team.Trigger;
                        }
                        redoList[team] = name2;
                        team.Trigger = name2;
                    }
                }
                foreach ((int cell, CellTrigger value) in CellTriggers)
                {
                    if (String.Equals(value.Trigger, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!undoList.ContainsKey(value))
                        {
                            undoList[value] = value.Trigger;
                        }
                        redoList[value] = name2;
                        // if name2 is "None", the post-trigger-assignment sweep will clean this up.
                        value.Trigger = name2;
                        if (!cellTriggerLocations.ContainsKey(value))
                        {
                            cellTriggerLocations[value] = cell;
                        }
                    }
                }
            }
            this.CleanUpTriggers(newTriggers, undoList, redoList, cellTriggerLocations);
        }

        private void CleanUpTriggers(List<Trigger> triggers, Dictionary<object, string> undoList, Dictionary<object, string> redoList, Dictionary<CellTrigger, int> cellTriggerLocations)
        {
            // Clean techno types
            HashSet<string> availableTriggers = triggers.Select(t => t.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> availableUnitTriggers = FilterUnitTriggers(triggers).Select(t => t.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> availableBuildingTriggers = FilterStructureTriggers(triggers).Select(t => t.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> availableTerrainTriggers = FilterTerrainTriggers(triggers).Select(t => t.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            foreach (ITechno techno in GetAllTechnos())
            {
                if (techno is Infantry infantry)
                {
                    CheckTechnoTrigger(infantry, availableUnitTriggers, undoList, redoList);
                }
                else if (techno is Unit unit)
                {
                    CheckTechnoTrigger(unit, availableUnitTriggers, undoList, redoList);
                }
                else if (techno is Building building)
                {
                    CheckTechnoTrigger(building, availableBuildingTriggers, undoList, redoList);
                }
                else if (techno is Terrain terrain)
                {
                    CheckTechnoTrigger(terrain, availableTerrainTriggers, undoList, redoList);
                }
            }
            // Clean teamtypes
            foreach (var team in TeamTypes)
            {
                String trig = team.Trigger;
                if (!Trigger.IsEmpty(trig) && !availableUnitTriggers.Contains(trig))
                {
                    if (undoList != null && !undoList.ContainsKey(team))
                    {
                        undoList.Add(team, trig);
                    }
                    if (redoList != null)
                    {
                        redoList[team] = Trigger.None;
                    }
                    team.Trigger = Trigger.None;
                }
            }
            // Clean triggers. Not covered in undo/redo actions since it is applied on the new list directly.
            foreach (var trig in triggers)
            {
                if (trig == null)
                {
                    continue;
                }
                if (trig.Action1.Trigger != Trigger.None && !availableTriggers.Contains(trig.Action1.Trigger))
                {
                    trig.Action1.Trigger = Trigger.None;
                }
                if (trig.Action2.Trigger != Trigger.None && !availableTriggers.Contains(trig.Action2.Trigger))
                {
                    trig.Action2.Trigger = Trigger.None;
                }
            }
            CleanUpCellTriggers(triggers, undoList, redoList, cellTriggerLocations);
        }

        private void CheckTechnoTrigger(ITechno techno, HashSet<String> availableTriggers, Dictionary<object, string> undoList, Dictionary<object, string> redoList)
        {
            String trig = techno.Trigger;
            if (!Trigger.IsEmpty(trig) && !availableTriggers.Contains(trig))
            {
                if (undoList != null && !undoList.ContainsKey(techno))
                {
                    undoList.Add(techno, trig);
                }
                if (redoList != null)
                {
                    redoList[techno] = Trigger.None;
                }
                techno.Trigger = Trigger.None;
            }
        }

        private void CleanUpCellTriggers(List<Trigger> triggers, Dictionary<object, string> undoList, Dictionary<object, string> redoList, Dictionary<CellTrigger, int> cellTriggerLocations)
        {
            HashSet<string> placeableTrigs = FilterCellTriggers(triggers).Select(t => t.Name).ToHashSet(StringComparer.InvariantCultureIgnoreCase);
            List<int> cellsToClear = new List<int>();
            foreach ((int cell, CellTrigger value) in CellTriggers)
            {
                if (Trigger.IsEmpty(value.Trigger) || !placeableTrigs.Contains(value.Trigger))
                {
                    if (undoList != null && !undoList.ContainsKey(value))
                    {
                        undoList.Add(value, value.Trigger);
                    }
                    if (redoList != null)
                    {
                        redoList[value] = Trigger.None;
                    }
                    cellTriggerLocations[value] = cell;
                    cellsToClear.Add(cell);
                }
            }
            for (int i = 0; i < cellsToClear.Count; ++i)
            {
                CellTriggers[cellsToClear[i]] = null;
            }
        }

        public void ApplyTeamTypeRenames(List<(String Name1, String Name2)> renameActions)
        {
            foreach ((String name1, String name2) in renameActions)
            {
                if (TeamType.IsEmpty(name1))
                {
                    continue;
                }
                foreach (Trigger trigger in triggers)
                {
                    if (String.Equals(trigger.Event1.Team, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        trigger.Event1.Team = name2;
                    }
                    if (String.Equals(trigger.Event2.Team, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        trigger.Event2.Team = name2;
                    }
                    if (String.Equals(trigger.Action1.Team, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        trigger.Action1.Team = name2;
                    }
                    if (String.Equals(trigger.Action2.Team, name1, StringComparison.OrdinalIgnoreCase))
                    {
                        trigger.Action2.Team = name2;
                    }
                }
            }
        }

        public IEnumerable<string> AssessPower(HashSet<string> housesWithProd)
        {
            Dictionary<String, int[]> powerWithUnbuilt = new Dictionary<string, int[]>(StringComparer.CurrentCultureIgnoreCase);
            Dictionary<String, int[]> powerWithoutUnbuilt = new Dictionary<string, int[]>(StringComparer.CurrentCultureIgnoreCase);
            foreach (HouseType house in this.HouseTypes)
            {
                if (housesWithProd.Contains(house.Name))
                {
                    powerWithUnbuilt[house.Name] = new int[2];
                }
                powerWithoutUnbuilt[house.Name] = new int[2];
            }
            foreach ((_, Building bld) in Buildings.OfType<Building>())
            {
                int bldUsage = bld.Type.PowerUsage;
                int bldProd = bld.Type.PowerProduction;
                int[] housePwr;
                // These should all belong to the "rebuild house" due to internal property change listeners; no need to explicitly check.
                if (!bld.IsPrebuilt)
                {
                    foreach (string house in housesWithProd)
                    {
                        if (powerWithUnbuilt.TryGetValue(house, out housePwr))
                        {
                            housePwr[0] += bldUsage;
                            housePwr[1] += bldProd;
                        }
                    }
                }
                else if (powerWithUnbuilt.TryGetValue(bld.House.Name, out housePwr))
                {
                    housePwr[0] += bldUsage;
                    housePwr[1] += bldProd;
                }
                if (bld.IsPrebuilt && powerWithoutUnbuilt.TryGetValue(bld.House.Name, out housePwr))
                {
                    housePwr[0] += bldUsage;
                    housePwr[1] += bldProd;
                }
            }
            List<string> info = new List<string>();
            List<string> prodHouses = new List<string>();
            foreach (HouseType house in HouseTypes)
            {
                if (housesWithProd.Contains(house.Name))
                {
                    prodHouses.Add(house.Name);
                }
            }
            info.Add("Production-capable Houses: " + String.Join(", ", prodHouses.ToArray()));
            foreach (HouseType house in HouseTypes)
            {
                int[] housePwrAll;
                int[] housePwrBuilt;
                if (powerWithUnbuilt.TryGetValue(house.Name, out housePwrAll))
                {
                    powerWithoutUnbuilt.TryGetValue(house.Name, out housePwrBuilt);
                    int houseUsage = housePwrAll[0]; // PowerUsage;
                    int houseProd = housePwrAll[1]; // PowerProduction;
                    int houseUsageBuilt = housePwrBuilt[0]; // PowerUsage;
                    int houseProdBuilt = housePwrBuilt[1]; // PowerProduction;

                    String houseInfo = String.Format("{0}: {1} - Produces {2}, uses {3}. (Without unbuilt: {4} - Produces {5}, uses {6}.)",
                        house.Name, houseProd < houseUsage ? "!!" : "OK", houseProd, houseUsage,
                        houseProdBuilt < houseUsageBuilt ? "!!" : "OK", houseProdBuilt, houseUsageBuilt);
                    info.Add(houseInfo);
                }
                else if (powerWithoutUnbuilt.TryGetValue(house.Name, out housePwrBuilt))
                {
                    int houseUsageBuilt = housePwrBuilt[0]; // PowerUsage;
                    int houseProdBuilt = housePwrBuilt[1]; // PowerProduction;
                    String houseInfo = String.Format("{0}: {1} - Produces {2}, uses {3}.",
                        house.Name, houseProdBuilt < houseUsageBuilt ? "!!" : "OK", houseProdBuilt, houseUsageBuilt);
                    info.Add(houseInfo);
                }
            }
            return info;
        }

        public IEnumerable<string> AssessStorage(HashSet<string> housesWithProd)
        {
            Dictionary<String, int> storageWithUnbuilt = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            Dictionary<String, int> storageWithoutUnbuilt = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
            foreach (HouseType house in this.HouseTypes)
            {
                if (housesWithProd.Contains(house.Name))
                {
                    storageWithUnbuilt[house.Name] = 0;
                }
                storageWithoutUnbuilt[house.Name] = 0;
            }
            foreach ((_, Building bld) in Buildings.OfType<Building>())
            {
                int bldStorage = bld.Type.Storage;
                if (!bld.IsPrebuilt)
                {
                    foreach (string house in housesWithProd)
                    {
                        if (storageWithUnbuilt.ContainsKey(house))
                        {
                            storageWithUnbuilt[house] += bldStorage;
                        }
                    }
                }
                else if (storageWithUnbuilt.ContainsKey(bld.House.Name))
                {
                    storageWithUnbuilt[bld.House.Name] += bldStorage;
                }
                if (bld.IsPrebuilt && storageWithoutUnbuilt.ContainsKey(bld.House.Name))
                {
                    storageWithoutUnbuilt[bld.House.Name] += bldStorage;
                }
            }
            List<string> info = new List<string>();
            List<string> prodHouses = new List<string>();
            foreach (HouseType house in HouseTypes)
            {
                if (housesWithProd.Contains(house.Name))
                {
                    prodHouses.Add(house.Name);
                }
            }
            info.Add("Production-capable Houses: " + String.Join(", ", prodHouses.ToArray()));
            foreach (HouseType house in HouseTypes)
            {
                int houseStorageBuilt;
                if (storageWithUnbuilt.TryGetValue(house.Name, out int houseStorageAll))
                {
                    storageWithoutUnbuilt.TryGetValue(house.Name, out houseStorageBuilt);

                    String houseInfo = String.Format("{0}: Storage capacity: {1}. (Without unbuilt: {2})",
                        house.Name, houseStorageAll, houseStorageBuilt);
                    info.Add(houseInfo);
                }
                else if (storageWithoutUnbuilt.TryGetValue(house.Name, out houseStorageBuilt))
                {
                    String houseInfo = String.Format("{0}: Storage capacity: {1}.",
                        house.Name, houseStorageBuilt);
                    info.Add(houseInfo);
                }
            }
            return info;
        }
    }
}
