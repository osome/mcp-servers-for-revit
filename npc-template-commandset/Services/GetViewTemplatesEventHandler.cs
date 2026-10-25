using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Lists the view templates in the model with their main settings and how many views use each.
    /// </summary>
    public class GetViewTemplatesEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            string viewType = parameters.Value<string>("viewType");
            Dictionary<long, List<View>> usage = ViewSettings.ViewsByTemplate(doc);

            var templates = new JArray();
            foreach (View template in new FilteredElementCollector(doc)
                         .OfClass(typeof(View))
                         .Cast<View>()
                         .Where(v => v.IsTemplate)
                         .OrderBy(v => v.ViewType.ToString())
                         .ThenBy(v => v.Name))
            {
                if (!string.IsNullOrEmpty(viewType)
                    && !string.Equals(template.ViewType.ToString(), viewType.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                    continue;

                JObject entry = ViewSettings.ReadGeneral(doc, template);
                entry.Remove("isTemplate");
                entry["usedByViews"] = usage.TryGetValue(template.Id.GetValue(), out List<View> views) ? views.Count : 0;
                templates.Add(entry);
            }

            return new JObject
            {
                ["count"] = templates.Count,
                ["templates"] = templates,
            };
        }

        public override string GetName() => "Get view templates";
    }
}
