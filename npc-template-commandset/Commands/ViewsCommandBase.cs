using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Services;

namespace NPCTemplateCommandSet.Commands
{
    /// <summary>
    /// Base for the view template, filter and object style commands: hands the tool's JSON parameters
    /// to the handler, runs it on Revit's thread and returns its JSON result.
    /// </summary>
    public abstract class ViewsCommandBase : RevitCommandBase
    {
        protected ViewsCommandBase(JsonEventHandler handler, UIApplication uiApp)
            : base(handler, uiApp)
        {
        }

        /// <summary>How long to wait for Revit to start the request.</summary>
        protected virtual int TimeoutMilliseconds => 30000;

        public override object Execute(JObject parameters, string requestId)
        {
            var handler = (JsonEventHandler)Handler;
            handler.SetParameters(parameters);
            RaiseAndWaitForCompletion(TimeoutMilliseconds);
            return handler.Result;
        }
    }
}
