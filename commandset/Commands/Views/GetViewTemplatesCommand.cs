using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
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
