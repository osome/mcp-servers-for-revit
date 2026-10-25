using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    public class SetObjectStylesCommand : ViewsCommandBase
    {
        public override string CommandName => "set_object_styles";

        public SetObjectStylesCommand(UIApplication uiApp)
            : base(new SetObjectStylesEventHandler(), uiApp)
        {
        }
    }
}
