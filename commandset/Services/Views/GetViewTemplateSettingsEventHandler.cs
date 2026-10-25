using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Views
{
    /// <summary>
    /// Reports the settings of one view template (or view), section by section:
    /// general, includes, categories, filters, viewRange.
    /// </summary>
    public class GetViewTemplateSettingsEventHandler : JsonEventHandler
    {
        private static readonly string[] DefaultSections = { "general", "includes", "categories", "filters", "viewRange" };

        protected override JToken Run(Document doc, JObject parameters)
        {
            View view = ViewGraphicsUtils.ResolveView(doc, parameters["view"]);
            var sections = new HashSet<string>(
                parameters["sections"] is JArray requested && requested.Count > 0
                    ? requested.Select(s => s.ToString())
                    : DefaultSections,
                StringComparer.OrdinalIgnoreCase);
            bool allCategories = parameters.Value<bool?>("allCategories") ?? false;

            JObject result = sections.Contains("general")
                ? ViewSettings.ReadGeneral(doc, view)
                : ViewGraphicsUtils.DescribeView(view);

            if (sections.Contains("includes"))
                result["includes"] = ViewSettings.ReadIncludes(doc, view);
            if (sections.Contains("categories"))
                result["categories"] = ViewSettings.ReadCategories(doc, view, allCategories);
            if (sections.Contains("filters"))
                result["filters"] = ViewSettings.ReadFilters(doc, view);
            if (sections.Contains("viewRange"))
            {
                JObject range = ViewSettings.ReadViewRange(doc, view);
                if (range != null)
                    result["viewRange"] = range;
            }

            return result;
        }

        public override string GetName() => "Get view template settings";
    }
}
