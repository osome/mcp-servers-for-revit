using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
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
