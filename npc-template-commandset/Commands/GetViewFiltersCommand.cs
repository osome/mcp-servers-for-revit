using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
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
