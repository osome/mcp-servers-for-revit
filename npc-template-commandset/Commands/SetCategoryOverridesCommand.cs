using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
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
