using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Lists view filters: rule-based ones with their categories and rules, selection filters with their size,
    /// and which view templates and views use each.
    /// </summary>
    public class GetViewFiltersEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            // Listing every filter, a filter used by every template would repeat 40-odd template names; show a few.
            bool oneFilter = parameters["filter"] != null;
            List<FilterElement> filters = oneFilter
                ? new List<FilterElement> { ViewGraphicsUtils.ResolveFilter(doc, parameters["filter"]) }
                : new FilteredElementCollector(doc).OfClass(typeof(FilterElement)).Cast<FilterElement>().OrderBy(f => f.Name).ToList();

            Dictionary<long, List<View>> usage = FilterUsage(doc);

            var result = new JArray();
            foreach (FilterElement filter in filters)
            {
                var entry = new JObject
                {
                    ["id"] = filter.Id.GetValue(),
                    ["name"] = filter.Name,
                };

                if (filter is ParameterFilterElement ruleFilter)
                {
                    ICollection<ElementId> categoryIds = ruleFilter.GetCategories();
                    entry["type"] = "rule";
                    entry["categories"] = new JArray(categoryIds
                        .Select(id => Category.GetCategory(doc, id))
                        .Where(c => c != null)
                        .Select(ViewGraphicsUtils.CategoryPath)
                        .OrderBy(n => n));

                    ElementFilter elementFilter = ruleFilter.GetElementFilter();
                    if (elementFilter != null)
                        entry["rules"] = new FilterRules(doc, categoryIds).Describe(elementFilter);
                }
                else if (filter is SelectionFilterElement selection)
                {
                    entry["type"] = "selection";
                    entry["elementCount"] = selection.GetElementIds().Count;
                }

                usage.TryGetValue(filter.Id.GetValue(), out List<View> users);
                users = users ?? new List<View>();
                List<string> templates = users.Where(v => v.IsTemplate).Select(v => v.Name).OrderBy(n => n).ToList();
                entry["usedByTemplateCount"] = templates.Count;
                entry["usedByTemplates"] = new JArray(oneFilter ? templates : templates.Take(5));
                entry["usedByViewCount"] = users.Count(v => !v.IsTemplate);
                result.Add(entry);
            }

            return new JObject { ["count"] = result.Count, ["filters"] = result };
        }

        /// <summary>Views and templates that apply each filter, keyed by filter id.</summary>
        public static Dictionary<long, List<View>> FilterUsage(Document doc)
        {
            var usage = new Dictionary<long, List<View>>();
            foreach (View view in new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>())
            {
                ICollection<ElementId> applied;
                try
                {
                    if (!view.AreGraphicsOverridesAllowed())
                        continue;
                    applied = view.GetFilters();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (ElementId id in applied)
                {
                    if (!usage.TryGetValue(id.GetValue(), out List<View> views))
                        usage[id.GetValue()] = views = new List<View>();
                    views.Add(view);
                }
            }
            return usage;
        }

        public override string GetName() => "Get view filters";
    }
}
