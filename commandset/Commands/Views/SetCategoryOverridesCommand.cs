using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
{
    public class SetCategoryOverridesCommand : ViewsCommandBase
    {
        public override string CommandName => "set_category_overrides";

        public SetCategoryOverridesCommand(UIApplication uiApp)
            : base(new SetCategoryOverridesEventHandler(), uiApp)
        {
        }
    }
}
