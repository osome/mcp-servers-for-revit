using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
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
