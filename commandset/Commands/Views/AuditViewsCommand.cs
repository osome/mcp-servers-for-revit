using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
{
    public class AuditViewsCommand : ViewsCommandBase
    {
        public override string CommandName => "audit_views";

        public AuditViewsCommand(UIApplication uiApp)
            : base(new AuditViewsEventHandler(), uiApp)
        {
        }
    }
}
