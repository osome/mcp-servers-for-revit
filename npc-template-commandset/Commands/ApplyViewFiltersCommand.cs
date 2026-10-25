using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
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
