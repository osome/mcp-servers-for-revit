using System;
using System.Linq;
using Autodesk.Revit.UI;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// The shared "NPC" ribbon tab. Every NPC add-in (MCP, NPC Library, ...) calls GetOrCreatePanel with the
    /// same tab name, so whichever add-in Revit loads first creates the tab and the rest add their panels to it.
    /// Copy this class (or move it to a shared NPC library) rather than referencing another add-in's assembly:
    /// each add-in may load in its own context, so they must not depend on each other at runtime.
    /// </summary>
    internal static class NpcRibbon
    {
        public const string TabName = "NPC";

        public static RibbonPanel GetOrCreatePanel(UIControlledApplication application, string panelName)
        {
            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Another NPC add-in already created the tab.
            }

            RibbonPanel existing = application.GetRibbonPanels(TabName)
                .FirstOrDefault(p => string.Equals(p.Name, panelName, StringComparison.Ordinal));

            return existing ?? application.CreateRibbonPanel(TabName, panelName);
        }
    }
}
