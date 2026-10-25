using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    public class CreateViewTemplateCommand : ViewsCommandBase
    {
        public override string CommandName => "create_view_template";

        public CreateViewTemplateCommand(UIApplication uiApp)
            : base(new CreateViewTemplateEventHandler(), uiApp)
        {
        }
    }
}
