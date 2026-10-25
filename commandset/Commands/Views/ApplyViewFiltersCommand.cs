using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
{
    public class ApplyViewFiltersCommand : ViewsCommandBase
    {
        public override string CommandName => "apply_view_filters";

        public ApplyViewFiltersCommand(UIApplication uiApp)
            : base(new ApplyViewFiltersEventHandler(), uiApp)
        {
        }
    }
}
