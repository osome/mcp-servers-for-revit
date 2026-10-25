using Autodesk.Revit.UI;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
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
