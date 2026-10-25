using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    public class SetViewTemplatePropertiesCommand : ViewsCommandBase
    {
        public override string CommandName => "set_view_template_properties";

        public SetViewTemplatePropertiesCommand(UIApplication uiApp)
            : base(new SetViewTemplatePropertiesEventHandler(), uiApp)
        {
        }
    }
}
