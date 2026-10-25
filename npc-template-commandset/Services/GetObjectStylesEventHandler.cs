using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Lists object styles: line weights, color, pattern and material per category and subcategory.
    /// Line styles are the subcategories of "Lines".
    /// </summary>
    public class GetObjectStylesEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            string categoryType = parameters.Value<string>("categoryType");
            bool subcategories = parameters.Value<bool?>("includeSubcategories") ?? true;

            IEnumerable<Category> parents = parameters["category"] != null
                ? new[] { ViewGraphicsUtils.ResolveCategory(doc, parameters["category"]) }
                : doc.Settings.Categories.Cast<Category>()
                    .Where(c => string.IsNullOrEmpty(categoryType)
                                || string.Equals(c.CategoryType.ToString(), categoryType, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Name);

            var styles = new JArray();
            foreach (Category category in parents)
            {
                styles.Add(ObjectStyles.Read(doc, category));
                if (!subcategories || category.Parent != null)
                    continue;
                foreach (Category sub in category.SubCategories.Cast<Category>().OrderBy(c => c.Name))
                    styles.Add(ObjectStyles.Read(doc, sub));
            }

            return new JObject { ["count"] = styles.Count, ["styles"] = styles };
        }

        public override string GetName() => "Get object styles";
    }
}
