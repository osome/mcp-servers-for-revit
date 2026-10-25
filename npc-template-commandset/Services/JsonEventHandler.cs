using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Interfaces;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Base for the view template, filter and object style handlers: they take the tool's JSON
    /// parameters as they come and return JSON, so the shape the AI sends and reads stays in one place.
    /// Exceptions propagate to RevitCommandBase, which reports them to the caller.
    /// </summary>
    public abstract class JsonEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public JObject Parameters { get; private set; } = new JObject();

        public JToken Result { get; private set; }

        public void SetParameters(JObject parameters)
        {
            Parameters = parameters ?? new JObject();
            Result = null;
            _resetEvent.Reset();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument?.Document
                    ?? throw new InvalidOperationException("No model is open in Revit.");
                Result = Run(doc, Parameters);
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        protected abstract JToken Run(Document doc, JObject parameters);

        public abstract string GetName();

        /// <summary>Runs <paramref name="action"/> in one transaction, rolled back if it throws.</summary>
        protected static T InTransaction<T>(Document doc, string name, Func<T> action)
        {
            using (var transaction = new Transaction(doc, name))
            {
                transaction.Start();
                try
                {
                    T result = action();
                    transaction.Commit();
                    return result;
                }
                catch
                {
                    if (transaction.HasStarted() && !transaction.HasEnded())
                        transaction.RollBack();
                    throw;
                }
            }
        }
    }
}
