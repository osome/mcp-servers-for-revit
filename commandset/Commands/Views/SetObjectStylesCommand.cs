using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
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
