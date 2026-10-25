using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
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
