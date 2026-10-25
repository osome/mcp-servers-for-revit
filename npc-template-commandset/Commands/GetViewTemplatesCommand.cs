using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    public class GetViewTemplatesCommand : ViewsCommandBase
    {
        public override string CommandName => "get_view_templates";

        public GetViewTemplatesCommand(UIApplication uiApp)
            : base(new GetViewTemplatesEventHandler(), uiApp)
        {
        }
    }
}
