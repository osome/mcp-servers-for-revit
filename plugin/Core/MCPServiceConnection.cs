using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 获取socket服务
                // Obtain socket service.
                SocketService service = SocketService.Instance;

                // Revit prefixes dialog titles with the add-in name, so these read "NPC MCP - MCP Service".
                if (service.IsRunning)
                {
                    service.Stop();
                    TaskDialog.Show("MCP Service", "MCP service stopped.");
                }
                else
                {
                    service.Initialize(commandData.Application);
                    service.Start();
                    TaskDialog.Show("MCP Service", $"MCP service started on 127.0.0.1:{service.Port}.");
                }

                return Result.Succeeded;
            }
            catch (System.Net.Sockets.SocketException ex) when (ex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
            {
                TaskDialog.Show("MCP Service",
                    $"Port {SocketService.Instance.Port} is already in use, so the MCP service didn't start.\n\n" +
                    "Choose another port in MCP Settings > Connection, then switch the service on again.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
            finally
            {
                // The button's icon shows whether the service is on, so update it whichever way this went.
                RibbonIcons.Refresh();
            }
        }
    }
}
