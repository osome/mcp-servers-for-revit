using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services.Views;

namespace RevitMCPCommandSet.Commands.Views
{
    public class GetObjectStylesCommand : ViewsCommandBase
    {
        public override string CommandName => "get_object_styles";

        public GetObjectStylesCommand(UIApplication uiApp)
            : base(new GetObjectStylesEventHandler(), uiApp)
        {
        }
    }
}
