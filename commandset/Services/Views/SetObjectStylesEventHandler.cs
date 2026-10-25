using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Views
{
    /// <summary>
    /// Changes object styles (line weights, color, line pattern, material) and can create subcategories,
    /// including new line styles under "Lines".
    /// </summary>
    public class SetObjectStylesEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            if (!(parameters["styles"] is JArray entries) || entries.Count == 0)
                throw new ArgumentException("Give at least one entry in styles.");

            var updated = new JArray();
            var failed = new JArray();

            InTransaction(doc, "Set object styles", () =>
            {
                foreach (JObject entry in entries.Cast<JObject>())
                {
                    try
                    {
                        Category category = ResolveOrCreate(doc, entry);
                        Apply(doc, category, entry);
                        updated.Add(ObjectStyles.Read(doc, category));
                    }
                    catch (Exception ex)
                    {
                        failed.Add(new JObject { ["category"] = entry["category"]?.ToString(), ["reason"] = ex.Message });
                    }
                }
                return true;
            });

            var result = new JObject { ["updated"] = updated };
            if (failed.Count > 0)
                result["failed"] = failed;
            return result;
        }

        private static Category ResolveOrCreate(Document doc, JObject entry)
        {
            JToken reference = entry["category"];
            try
            {
                return ViewGraphicsUtils.ResolveCategory(doc, reference);
            }
            catch (ArgumentException) when (entry.Value<bool?>("create") == true)
            {
                string[] parts = reference.ToString().Split('|');
                if (parts.Length != 2)
                    throw new ArgumentException("To create a subcategory, name it as \"Parent|New name\", e.g. \"Lines|NPC Setout\".");

                Category parent = ViewGraphicsUtils.ResolveCategory(doc, parts[0].Trim());
                if (!parent.CanAddSubcategory)
                    throw new ArgumentException($"'{parent.Name}' can't take new subcategories.");
                return doc.Settings.Categories.NewSubcategory(parent, parts[1].Trim());
            }
        }

        private static void Apply(Document doc, Category category, JObject entry)
        {
            if (entry["projectionWeight"] != null)
                category.SetLineWeight(entry.Value<int>("projectionWeight"), GraphicsStyleType.Projection);
            if (entry["cutWeight"] != null)
            {
                if (!category.IsCuttable)
                    throw new ArgumentException($"'{category.Name}' isn't cut, so it has no cut line weight.");
                category.SetLineWeight(entry.Value<int>("cutWeight"), GraphicsStyleType.Cut);
            }
            if (entry["color"] != null)
                category.LineColor = ViewGraphicsUtils.ParseColor(entry["color"]);
            if (entry["pattern"] != null)
                category.SetLinePatternId(ViewGraphicsUtils.ResolveLinePattern(doc, entry["pattern"].ToString()), GraphicsStyleType.Projection);
            if (entry["material"] != null)
            {
                if (ViewGraphicsUtils.IsNull(entry["material"]))
                {
                    category.Material = null;
                }
                else
                {
                    string name = entry["material"].ToString().Trim();
                    category.Material = new FilteredElementCollector(doc).OfClass(typeof(Material)).Cast<Material>()
                        .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new ArgumentException($"No material is named '{name}'.");
                }
            }
        }

        public override string GetName() => "Set object styles";
    }
}
