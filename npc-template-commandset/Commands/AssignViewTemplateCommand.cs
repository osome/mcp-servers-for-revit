using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
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
