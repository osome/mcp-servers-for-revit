using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Views
{
    /// <summary>
    /// Creates a rule-based view filter, or replaces the categories and rules of an existing one with overwrite.
    /// </summary>
    public class CreateViewFilterEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            string name = parameters.Value<string>("name")?.Trim();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Give the filter a name.");
            bool overwrite = parameters.Value<bool?>("overwrite") ?? false;

            if (!(parameters["categories"] is JArray categoryNames) || categoryNames.Count == 0)
                throw new ArgumentException("Give at least one category.");
            List<ElementId> categoryIds = categoryNames
                .Select(c => ViewGraphicsUtils.ResolveCategory(doc, c).Id)
                .Distinct()
                .ToList();

            var filterable = new HashSet<long>(ParameterFilterUtilities.GetAllFilterableCategories().Select(id => id.GetValue()));
            List<string> notFilterable = categoryIds
                .Where(id => !filterable.Contains(id.GetValue()))
                .Select(id => Category.GetCategory(doc, id)?.Name ?? id.GetValue().ToString())
                .ToList();
            if (notFilterable.Count > 0)
                throw new ArgumentException($"These categories can't be used in a view filter: {string.Join(", ", notFilterable)}.");

            var rules = new FilterRules(doc, categoryIds);
            ElementFilter elementFilter = rules.Build(parameters["rules"] as JArray, parameters.Value<string>("logic"));
            if (!ParameterFilterElement.ElementFilterIsAcceptableForParameterFilterElement(doc, new HashSet<ElementId>(categoryIds), elementFilter))
                throw new ArgumentException("Revit doesn't accept these rules for these categories; each rule's parameter must apply to all the categories.");

            ParameterFilterElement existing = new FilteredElementCollector(doc)
                .OfClass(typeof(ParameterFilterElement))
                .Cast<ParameterFilterElement>()
                .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null && !overwrite)
                throw new ArgumentException($"A filter named '{name}' already exists; pass overwrite: true to replace its categories and rules.");

            ParameterFilterElement filter = InTransaction(doc, existing != null ? "Update view filter" : "Create view filter", () =>
            {
                if (existing == null)
                    return ParameterFilterElement.Create(doc, name, categoryIds, elementFilter);

                existing.SetCategories(categoryIds);
                existing.SetElementFilter(elementFilter);
                return existing;
            });

            return new JObject
            {
                ["id"] = filter.Id.GetValue(),
                ["name"] = filter.Name,
                ["updated"] = existing != null,
                ["categories"] = new JArray(categoryIds.Select(id => Category.GetCategory(doc, id)).Where(c => c != null).Select(ViewGraphicsUtils.CategoryPath)),
                ["rules"] = rules.Describe(filter.GetElementFilter()),
            };
        }

        public override string GetName() => "Create view filter";
    }
}
