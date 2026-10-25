using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
{
    public class GetViewFiltersCommand : ViewsCommandBase
    {
        public override string CommandName => "get_view_filters";

        public GetViewFiltersCommand(UIApplication uiApp)
            : base(new GetViewFiltersEventHandler(), uiApp)
        {
        }
    }
}
