using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;

namespace NPCTemplateCommandSet.Utils
{
    /// <summary>
    /// Lookups and conversions shared by the view template, view filter and object style commands,
    /// so those tools can take names (as a person would type them) as well as element ids.
    /// Overrides are read and written as one JSON shape:
    /// <code>
    /// { halftone, transparency, detailLevel,
    ///   projectionLine: { color, weight, pattern }, cutLine: { ... },
    ///   surfaceForeground: { pattern, color, visible }, surfaceBackground: { ... },
    ///   cutForeground: { ... }, cutBackground: { ... } }
    /// </code>
    /// On write, an omitted field is left as it is and a null field is cleared.
    /// </summary>
    public static class ViewGraphicsUtils
    {
        public const double MillimetersPerFoot = 304.8;

        private const string ModelPatternSuffix = " (model)";

        public static double FromMillimeters(double millimeters) => millimeters / MillimetersPerFoot;

        public static double ToMillimeters(double feet) => feet * MillimetersPerFoot;

        // ---------- Views ----------

        /// <summary>
        /// Finds a view or view template by element id (number or numeric string) or by exact name.
        /// </summary>
        /// <param name="templates">true: templates only; false: ordinary views only; null: either.</param>
        public static View ResolveView(Document doc, JToken reference, bool? templates = null)
        {
            if (reference == null || reference.Type == JTokenType.Null)
                throw new ArgumentException("No view given.");

            string kind = templates == true ? "view template" : templates == false ? "view" : "view or view template";

            if (TryGetId(reference, out long id))
            {
                if (!(doc.GetElement(id.ToRevitElementId()) is View byId))
                    throw new ArgumentException($"No {kind} has id {id}.");
                if (templates.HasValue && byId.IsTemplate != templates.Value)
                    throw new ArgumentException($"Element {id} ('{byId.Name}') is not a {kind}.");
                return byId;
            }

            string name = reference.ToString().Trim();
            List<View> matches = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => (!templates.HasValue || v.IsTemplate == templates.Value)
                            && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                throw new ArgumentException($"No {kind} is named '{name}'.");
            if (matches.Count > 1)
            {
                string candidates = string.Join(", ", matches.Select(v => $"{v.Id.GetValue()} ({v.ViewType}{(v.IsTemplate ? " template" : "")})"));
                throw new ArgumentException($"'{name}' matches several views; pass one of these ids instead: {candidates}.");
            }

            return matches[0];
        }

        /// <summary>Resolves a single reference or an array of references.</summary>
        public static List<View> ResolveViews(Document doc, JToken references, bool? templates = null)
        {
            if (references is JArray array)
                return array.Select(r => ResolveView(doc, r, templates)).ToList();
            return new List<View> { ResolveView(doc, references, templates) };
        }

        public static JObject DescribeView(View view)
        {
            return new JObject
            {
                ["id"] = view.Id.GetValue(),
                ["name"] = view.Name,
                ["viewType"] = view.ViewType.ToString(),
                ["isTemplate"] = view.IsTemplate,
            };
        }

        // ---------- Categories ----------

        /// <summary>All categories in the document with their subcategories.</summary>
        public static IEnumerable<Category> AllCategories(Document doc)
        {
            foreach (Category category in doc.Settings.Categories)
            {
                yield return category;
                foreach (Category sub in category.SubCategories)
                    yield return sub;
            }
        }

        /// <summary>"Walls", or "Walls|Common Edges" for a subcategory.</summary>
        public static string CategoryPath(Category category) =>
            category.Parent == null ? category.Name : $"{category.Parent.Name}|{category.Name}";

        /// <summary>
        /// Finds a category by name ("Walls"), subcategory path ("Walls|Common Edges"),
        /// built-in name ("OST_Walls") or id.
        /// </summary>
        public static Category ResolveCategory(Document doc, JToken reference)
        {
            if (TryGetId(reference, out long id))
            {
                Category byId = AllCategories(doc).FirstOrDefault(c => c.Id.GetValue() == id);
                return byId ?? throw new ArgumentException($"No category has id {id}.");
            }

            string text = reference?.ToString().Trim() ?? "";
            if (text.StartsWith("OST_", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse(text, true, out BuiltInCategory builtIn))
            {
                Category byEnum = Category.GetCategory(doc, builtIn);
                return byEnum ?? throw new ArgumentException($"Category {text} isn't available in this document.");
            }

            string[] parts = text.Split('|');
            Category parent = FindByName(doc.Settings.Categories.Cast<Category>(), parts[0].Trim());
            if (parent == null)
                throw new ArgumentException($"No category is named '{parts[0].Trim()}'.");
            if (parts.Length == 1)
                return parent;

            Category sub = FindByName(parent.SubCategories.Cast<Category>(), parts[1].Trim());
            return sub ?? throw new ArgumentException($"Category '{parent.Name}' has no subcategory '{parts[1].Trim()}'.");
        }

        private static Category FindByName(IEnumerable<Category> categories, string name) =>
            categories.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

        // ---------- Patterns and colors ----------

        /// <summary>
        /// Finds a fill pattern by name. Drafting patterns win over model patterns of the same name;
        /// add " (model)" to the name to pick the model one. "Solid" finds the solid drafting fill.
        /// </summary>
        public static ElementId ResolveFillPattern(Document doc, string name)
        {
            List<FillPatternElement> patterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .ToList();

            bool wantModel = name.EndsWith(ModelPatternSuffix, StringComparison.OrdinalIgnoreCase);
            string bareName = wantModel ? name.Substring(0, name.Length - ModelPatternSuffix.Length) : name;
            FillPatternTarget target = wantModel ? FillPatternTarget.Model : FillPatternTarget.Drafting;

            FillPatternElement match = patterns.FirstOrDefault(p =>
                string.Equals(p.Name, bareName, StringComparison.OrdinalIgnoreCase) && p.GetFillPattern().Target == target);
            if (match == null && !wantModel)
                match = patterns.FirstOrDefault(p => string.Equals(p.Name, bareName, StringComparison.OrdinalIgnoreCase));
            if (match == null && string.Equals(bareName, "Solid", StringComparison.OrdinalIgnoreCase))
                match = patterns.FirstOrDefault(p => p.GetFillPattern().IsSolidFill && p.GetFillPattern().Target == target);

            return match?.Id ?? throw new ArgumentException($"No fill pattern is named '{name}'.");
        }

        public static string DescribeFillPattern(Document doc, ElementId id)
        {
            if (!(doc.GetElement(id) is FillPatternElement pattern))
                return null;
            return pattern.GetFillPattern().Target == FillPatternTarget.Model ? pattern.Name + ModelPatternSuffix : pattern.Name;
        }

        /// <summary>Finds a line pattern by name; "Solid" is the built-in solid pattern.</summary>
        public static ElementId ResolveLinePattern(Document doc, string name)
        {
            if (string.Equals(name, "Solid", StringComparison.OrdinalIgnoreCase))
                return LinePatternElement.GetSolidPatternId();

            LinePatternElement pattern = LinePatternElement.GetLinePatternElementByName(doc, name)
                ?? new FilteredElementCollector(doc)
                    .OfClass(typeof(LinePatternElement))
                    .Cast<LinePatternElement>()
                    .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

            return pattern?.Id ?? throw new ArgumentException($"No line pattern is named '{name}'.");
        }

        public static string DescribeLinePattern(Document doc, ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId)
                return null;
            if (id == LinePatternElement.GetSolidPatternId())
                return "Solid";
            return doc.GetElement(id)?.Name;
        }

        /// <summary>Accepts "#RRGGBB", "RRGGBB", "r,g,b" or { r, g, b }.</summary>
        public static Color ParseColor(JToken token)
        {
            if (token is JObject rgb)
                return new Color((byte)rgb.Value<int>("r"), (byte)rgb.Value<int>("g"), (byte)rgb.Value<int>("b"));

            string text = token?.ToString().Trim() ?? "";
            if (text.Contains(","))
            {
                byte[] parts = text.Split(',').Select(p => byte.Parse(p.Trim())).ToArray();
                if (parts.Length == 3)
                    return new Color(parts[0], parts[1], parts[2]);
            }
            else
            {
                string hex = text.TrimStart('#');
                if (hex.Length == 6)
                {
                    return new Color(
                        Convert.ToByte(hex.Substring(0, 2), 16),
                        Convert.ToByte(hex.Substring(2, 2), 16),
                        Convert.ToByte(hex.Substring(4, 2), 16));
                }
            }

            throw new ArgumentException($"'{text}' isn't a color; use \"#RRGGBB\" or \"r,g,b\".");
        }

        public static string DescribeColor(Color color) =>
            color != null && color.IsValid ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}" : null;

        // ---------- Overrides ----------

        /// <summary>The overrides that are set, in the shared JSON shape. Empty when nothing is overridden.</summary>
        public static JObject ReadOverrides(Document doc, OverrideGraphicSettings ogs)
        {
            var result = new JObject();

            if (ogs.Halftone)
                result["halftone"] = true;
            if (ogs.Transparency != 0)
                result["transparency"] = ogs.Transparency;
            if (ogs.DetailLevel != ViewDetailLevel.Undefined)
                result["detailLevel"] = ogs.DetailLevel.ToString();

            AddIfAny(result, "projectionLine", ReadLine(doc, ogs.ProjectionLineColor, ogs.ProjectionLineWeight, ogs.ProjectionLinePatternId));
            AddIfAny(result, "cutLine", ReadLine(doc, ogs.CutLineColor, ogs.CutLineWeight, ogs.CutLinePatternId));
            AddIfAny(result, "surfaceForeground", ReadPattern(doc, ogs.SurfaceForegroundPatternId, ogs.SurfaceForegroundPatternColor, ogs.IsSurfaceForegroundPatternVisible));
            AddIfAny(result, "surfaceBackground", ReadPattern(doc, ogs.SurfaceBackgroundPatternId, ogs.SurfaceBackgroundPatternColor, ogs.IsSurfaceBackgroundPatternVisible));
            AddIfAny(result, "cutForeground", ReadPattern(doc, ogs.CutForegroundPatternId, ogs.CutForegroundPatternColor, ogs.IsCutForegroundPatternVisible));
            AddIfAny(result, "cutBackground", ReadPattern(doc, ogs.CutBackgroundPatternId, ogs.CutBackgroundPatternColor, ogs.IsCutBackgroundPatternVisible));

            return result;
        }

        private static void AddIfAny(JObject target, string key, JObject value)
        {
            if (value.HasValues)
                target[key] = value;
        }

        private static JObject ReadLine(Document doc, Color color, int weight, ElementId patternId)
        {
            var line = new JObject();
            if (color.IsValid)
                line["color"] = DescribeColor(color);
            if (weight != OverrideGraphicSettings.InvalidPenNumber)
                line["weight"] = weight;
            string pattern = DescribeLinePattern(doc, patternId);
            if (pattern != null)
                line["pattern"] = pattern;
            return line;
        }

        private static JObject ReadPattern(Document doc, ElementId patternId, Color color, bool visible)
        {
            var fill = new JObject();
            string pattern = DescribeFillPattern(doc, patternId);
            if (pattern != null)
                fill["pattern"] = pattern;
            if (color.IsValid)
                fill["color"] = DescribeColor(color);
            if (!visible)
                fill["visible"] = false;
            return fill;
        }

        /// <summary>
        /// Returns a copy of <paramref name="current"/> with the fields in <paramref name="spec"/> applied.
        /// Pass <c>new OverrideGraphicSettings()</c> as current to start from no overrides.
        /// </summary>
        public static OverrideGraphicSettings ApplyOverrides(Document doc, OverrideGraphicSettings current, JObject spec)
        {
            var ogs = new OverrideGraphicSettings(current);
            if (spec == null)
                return ogs;

            if (spec.TryGetValue("halftone", out JToken halftone))
                ogs.SetHalftone(!IsNull(halftone) && halftone.Value<bool>());
            if (spec.TryGetValue("transparency", out JToken transparency))
                ogs.SetSurfaceTransparency(IsNull(transparency) ? 0 : transparency.Value<int>());
            if (spec.TryGetValue("detailLevel", out JToken detailLevel))
                ogs.SetDetailLevel(IsNull(detailLevel) ? ViewDetailLevel.Undefined : ParseEnum<ViewDetailLevel>(detailLevel, "detailLevel"));

            ApplyLine(doc, spec["projectionLine"], ogs.SetProjectionLineColor, ogs.SetProjectionLineWeight, ogs.SetProjectionLinePatternId);
            ApplyLine(doc, spec["cutLine"], ogs.SetCutLineColor, ogs.SetCutLineWeight, ogs.SetCutLinePatternId);
            ApplyPattern(doc, spec["surfaceForeground"], ogs.SetSurfaceForegroundPatternId, ogs.SetSurfaceForegroundPatternColor, ogs.SetSurfaceForegroundPatternVisible);
            ApplyPattern(doc, spec["surfaceBackground"], ogs.SetSurfaceBackgroundPatternId, ogs.SetSurfaceBackgroundPatternColor, ogs.SetSurfaceBackgroundPatternVisible);
            ApplyPattern(doc, spec["cutForeground"], ogs.SetCutForegroundPatternId, ogs.SetCutForegroundPatternColor, ogs.SetCutForegroundPatternVisible);
            ApplyPattern(doc, spec["cutBackground"], ogs.SetCutBackgroundPatternId, ogs.SetCutBackgroundPatternColor, ogs.SetCutBackgroundPatternVisible);

            return ogs;
        }

        private static void ApplyLine(Document doc, JToken token, Func<Color, OverrideGraphicSettings> setColor,
            Func<int, OverrideGraphicSettings> setWeight, Func<ElementId, OverrideGraphicSettings> setPattern)
        {
            if (token == null)
                return;
            if (IsNull(token))
            {
                setColor(Color.InvalidColorValue);
                setWeight(OverrideGraphicSettings.InvalidPenNumber);
                setPattern(ElementId.InvalidElementId);
                return;
            }

            var line = (JObject)token;
            if (line.TryGetValue("color", out JToken color))
                setColor(IsNull(color) ? Color.InvalidColorValue : ParseColor(color));
            if (line.TryGetValue("weight", out JToken weight))
                setWeight(IsNull(weight) ? OverrideGraphicSettings.InvalidPenNumber : weight.Value<int>());
            if (line.TryGetValue("pattern", out JToken pattern))
                setPattern(IsNull(pattern) ? ElementId.InvalidElementId : ResolveLinePattern(doc, pattern.ToString()));
        }

        private static void ApplyPattern(Document doc, JToken token, Func<ElementId, OverrideGraphicSettings> setPattern,
            Func<Color, OverrideGraphicSettings> setColor, Func<bool, OverrideGraphicSettings> setVisible)
        {
            if (token == null)
                return;
            if (IsNull(token))
            {
                setPattern(ElementId.InvalidElementId);
                setColor(Color.InvalidColorValue);
                setVisible(true);
                return;
            }

            var fill = (JObject)token;
            if (fill.TryGetValue("pattern", out JToken pattern))
                setPattern(IsNull(pattern) ? ElementId.InvalidElementId : ResolveFillPattern(doc, pattern.ToString()));
            if (fill.TryGetValue("color", out JToken color))
                setColor(IsNull(color) ? Color.InvalidColorValue : ParseColor(color));
            if (fill.TryGetValue("visible", out JToken visible))
                setVisible(IsNull(visible) || visible.Value<bool>());
        }

        // ---------- Filters ----------

        /// <summary>Finds a view filter (rule-based or selection) by id or name.</summary>
        public static FilterElement ResolveFilter(Document doc, JToken reference)
        {
            if (TryGetId(reference, out long id))
            {
                return doc.GetElement(id.ToRevitElementId()) as FilterElement
                    ?? throw new ArgumentException($"No view filter has id {id}.");
            }

            string name = reference?.ToString().Trim() ?? "";
            return new FilteredElementCollector(doc)
                    .OfClass(typeof(FilterElement))
                    .Cast<FilterElement>()
                    .FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"No view filter is named '{name}'.");
        }

        // ---------- Parameters and general ----------

        /// <summary>The display name of a parameter id: built-in label, or the project/shared parameter's name.</summary>
        public static string ParameterName(Document doc, ElementId parameterId)
        {
            long value = parameterId.GetValue();
            if (value < 0)
            {
                try
                {
                    return LabelUtils.GetLabelFor((BuiltInParameter)parameterId.GetIntValue());
                }
                catch (Exception)
                {
                    return ((BuiltInParameter)parameterId.GetIntValue()).ToString();
                }
            }

            return doc.GetElement(parameterId)?.Name ?? value.ToString();
        }

        public static string ElementName(Document doc, ElementId id)
        {
            if (id == null || id == ElementId.InvalidElementId)
                return null;
            return doc.GetElement(id)?.Name;
        }

        public static T ParseEnum<T>(JToken token, string field) where T : struct
        {
            string text = token?.ToString().Trim().Replace(" ", "") ?? "";
            if (Enum.TryParse(text, true, out T value))
                return value;
            throw new ArgumentException($"'{token}' isn't a valid {field}; use one of: {string.Join(", ", Enum.GetNames(typeof(T)))}.");
        }

        public static bool IsNull(JToken token) => token == null || token.Type == JTokenType.Null;

        private static bool TryGetId(JToken reference, out long id)
        {
            id = 0;
            if (reference == null)
                return false;
            if (reference.Type == JTokenType.Integer)
            {
                id = reference.Value<long>();
                return true;
            }
            return reference.Type == JTokenType.String && long.TryParse(reference.ToString().Trim(), out id);
        }
    }
}
