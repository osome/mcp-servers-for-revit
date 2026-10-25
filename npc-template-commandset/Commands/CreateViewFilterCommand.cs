using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    public class CreateViewFilterCommand : ViewsCommandBase
    {
        public override string CommandName => "create_view_filter";

        public CreateViewFilterCommand(UIApplication uiApp)
            : base(new CreateViewFilterEventHandler(), uiApp)
        {
        }
    }
}
