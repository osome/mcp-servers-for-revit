using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
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
