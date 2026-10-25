using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
{
    public class AssignViewTemplateCommand : ViewsCommandBase
    {
        public override string CommandName => "assign_view_template";

        public AssignViewTemplateCommand(UIApplication uiApp)
            : base(new AssignViewTemplateEventHandler(), uiApp)
        {
        }
    }
}
