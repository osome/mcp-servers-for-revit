using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Views
{
    /// <summary>
    /// Reads a category's object style (Manage > Object Styles) in the shape set_object_styles takes:
    /// { category, type, projectionWeight, cutWeight, color, pattern, material }.
    /// </summary>
    public static class ObjectStyles
    {
        public static JObject Read(Document doc, Category category)
        {
            var result = new JObject
            {
                ["category"] = ViewGraphicsUtils.CategoryPath(category),
                ["type"] = category.CategoryType.ToString(),
            };

            int? projection = Try(() => category.GetLineWeight(GraphicsStyleType.Projection));
            if (projection.HasValue)
                result["projectionWeight"] = projection.Value;
            if (category.IsCuttable)
            {
                int? cut = Try(() => category.GetLineWeight(GraphicsStyleType.Cut));
                if (cut.HasValue)
                    result["cutWeight"] = cut.Value;
            }

            string color = ViewGraphicsUtils.DescribeColor(category.LineColor);
            if (color != null)
                result["color"] = color;

            string pattern = ViewGraphicsUtils.DescribeLinePattern(doc, Try(() => category.GetLinePatternId(GraphicsStyleType.Projection)));
            if (pattern != null)
                result["pattern"] = pattern;

            if (category.Material != null)
                result["material"] = category.Material.Name;

            return result;
        }

        private static T Try<T>(Func<T> read)
        {
            try
            {
                return read();
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}
