using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
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
