﻿using System.Linq;
using System.Windows.Forms;
using MobiusEditor.Controls;
using MobiusEditor.Event;
using MobiusEditor.Interface;
using MobiusEditor.Model;
using MobiusEditor.Utility;

namespace MobiusEditor.Tools.Dialogs
{
    public class OverlayToolDialog : GenericToolDialog<OverlaysTool>
    {
        public OverlayToolDialog(Form parentForm)
            : base(parentForm)
        {
            Text = "Overlay";
        }

        public override void Initialize(MapPanel mapPanel, MapLayerFlag activeLayers, ToolStripStatusLabel toolStatusLabel, ToolTip mouseToolTip, IGamePlugin plugin, UndoRedoList<UndoRedoEventArgs> undoRedoList)
        {
            GenericTypeListBox.Types = plugin.Map.OverlayTypes.
                Where(t => t.IsPlaceable && ((t.Theaters == null) || t.Theaters.Contains(plugin.Map.Theater))).
                OrderBy(t => t.Name);

            Tool = new OverlaysTool(mapPanel, activeLayers, toolStatusLabel,
                GenericTypeListBox, GenericTypeMapPanel, plugin, undoRedoList);
        }
    }
}
