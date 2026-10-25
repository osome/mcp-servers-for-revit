using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    public class GetViewTemplateSettingsCommand : ViewsCommandBase
    {
        public override string CommandName => "get_view_template_settings";

        public GetViewTemplateSettingsCommand(UIApplication uiApp)
            : base(new GetViewTemplateSettingsEventHandler(), uiApp)
        {
        }
    }
}
