using System;
using Autodesk.Revit.UI;
using System.Reflection;



namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel mcpPanel = NpcRibbon.GetOrCreatePanel(application, "MCP");

            // Icons and the service button's tooltip are set by RibbonIcons, which tracks the service state and theme.
            PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "MCP\r\nService",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
            var serviceButton = (PushButton)mcpPanel.AddItem(pushButtonData);

            PushButtonData mcp_settings_pushButtonData = new PushButtonData("ID_EXCMD_MCP_SETTINGS", "MCP\r\nSettings",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.Settings");
            mcp_settings_pushButtonData.ToolTip = "Choose which MCP commands are enabled and which port the service uses.";
            var settingsButton = (PushButton)mcpPanel.AddItem(mcp_settings_pushButtonData);

            RibbonIcons.Register(serviceButton, settingsButton);
#if NET
            application.ThemeChanged += (sender, args) => RibbonIcons.Refresh();
#endif

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }
}
