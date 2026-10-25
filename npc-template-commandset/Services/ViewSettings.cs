using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Reads and writes the settings of a view or view template that several commands report or change.
    /// Properties a view type doesn't have (a schedule has no scale) are left out rather than failing the call.
    /// </summary>
    public static class ViewSettings
    {
        public static JObject ReadGeneral(Document doc, View view)
        {
            var result = ViewGraphicsUtils.DescribeView(view);
            Add(result, "scale", () => view.Scale);
            Add(result, "detailLevel", () => view.DetailLevel.ToString());
            Add(result, "discipline", () => view.Discipline.ToString());
            Add(result, "displayStyle", () => view.DisplayStyle.ToString());
            Add(result, "partsVisibility", () => view.PartsVisibility.ToString());
            Add(result, "phase", () => ViewGraphicsUtils.ElementName(doc, view.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId()));
            Add(result, "phaseFilter", () => ViewGraphicsUtils.ElementName(doc, view.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER)?.AsElementId()));

            if (!view.IsTemplate)
            {
                result["viewTemplate"] = view.ViewTemplateId == ElementId.InvalidElementId
                    ? null
                    : new JObject { ["id"] = view.ViewTemplateId.GetValue(), ["name"] = ViewGraphicsUtils.ElementName(doc, view.ViewTemplateId) };
            }

            return result;
        }

        /// <summary>
        /// For a template: every setting it can control, its value, and whether it's included (controlled).
        /// For a view with a template: the same for its template, so it's clear what the view can still change.
        /// </summary>
        public static JArray ReadIncludes(Document doc, View view)
        {
            View template = view.IsTemplate ? view : doc.GetElement(view.ViewTemplateId) as View;
            var result = new JArray();
            if (template == null)
                return result;

            var notControlled = new HashSet<long>(template.GetNonControlledTemplateParameterIds().Select(id => id.GetValue()));
            var parametersById = template.Parameters.Cast<Parameter>()
                .GroupBy(p => p.Id.GetValue())
                .ToDictionary(g => g.Key, g => g.First());

            foreach (ElementId id in template.GetTemplateParameterIds())
            {
                var entry = new JObject
                {
                    ["id"] = id.GetValue(),
                    ["name"] = ViewGraphicsUtils.ParameterName(doc, id),
                    ["included"] = !notControlled.Contains(id.GetValue()),
                };
                if (parametersById.TryGetValue(id.GetValue(), out Parameter parameter))
                {
                    string value = ParameterValue(doc, parameter);
                    if (value != null)
                        entry["value"] = value;
                }
                result.Add(entry);
            }

            return result;
        }

        /// <summary>Hidden or overridden categories, or every category the view can show when <paramref name="all"/>.</summary>
        public static JArray ReadCategories(Document doc, View view, bool all)
        {
            var result = new JArray();
            foreach (Category category in ViewGraphicsUtils.AllCategories(doc))
            {
                ElementId id = category.Id;
                bool canHide = Try(() => view.CanCategoryBeHidden(id));
                bool overridable = Try(() => view.IsCategoryOverridable(id));
                if (!canHide && !overridable)
                    continue;

                bool hidden = canHide && Try(() => view.GetCategoryHidden(id));
                JObject overrides = overridable
                    ? ViewGraphicsUtils.ReadOverrides(doc, view.GetCategoryOverrides(id))
                    : new JObject();

                if (!all && !hidden && !overrides.HasValues)
                    continue;

                var entry = new JObject
                {
                    ["category"] = ViewGraphicsUtils.CategoryPath(category),
                    ["type"] = category.CategoryType.ToString(),
                    ["visible"] = !hidden,
                };
                if (overrides.HasValues)
                    entry["overrides"] = overrides;
                result.Add(entry);
            }

            return result;
        }

        /// <summary>Filter ids applied to a view, in the order Revit applies them.</summary>
        public static List<ElementId> OrderedFilterIds(View view)
        {
#if REVIT2022_OR_GREATER
            return view.GetOrderedFilters().ToList();
#else
            return view.GetFilters().ToList();
#endif
        }

        public static JArray ReadFilters(Document doc, View view)
        {
            var result = new JArray();
            foreach (ElementId id in OrderedFilterIds(view))
            {
                var entry = new JObject
                {
                    ["id"] = id.GetValue(),
                    ["name"] = ViewGraphicsUtils.ElementName(doc, id),
                    ["visible"] = view.GetFilterVisibility(id),
                };
#if REVIT2022_OR_GREATER
                entry["enabled"] = view.GetIsFilterEnabled(id);
#endif
                JObject overrides = ViewGraphicsUtils.ReadOverrides(doc, view.GetFilterOverrides(id));
                if (overrides.HasValues)
                    entry["overrides"] = overrides;
                result.Add(entry);
            }

            return result;
        }

        private static readonly PlanViewPlane[] ViewRangePlanes =
        {
            PlanViewPlane.TopClipPlane, PlanViewPlane.CutPlane, PlanViewPlane.BottomClipPlane, PlanViewPlane.ViewDepthPlane,
        };

        /// <summary>View range of a plan in mm, keyed top, cut, bottom, depth. Null for other views.</summary>
        public static JObject ReadViewRange(Document doc, View view)
        {
            if (!(view is ViewPlan plan))
                return null;

            PlanViewRange range = plan.GetViewRange();
            var result = new JObject();
            foreach (PlanViewPlane plane in ViewRangePlanes)
            {
                result[PlaneKey(plane)] = new JObject
                {
                    ["level"] = DescribeRangeLevel(doc, range.GetLevelId(plane)),
                    ["offset"] = Math.Round(ViewGraphicsUtils.ToMillimeters(range.GetOffset(plane)), 2),
                };
            }
            return result;
        }

        /// <summary>
        /// Changes the planes given in <paramref name="spec"/>: { cut: { offset, level }, ... }, offsets in mm,
        /// level "Associated", "Above", "Below", "Unlimited" or a level name.
        /// </summary>
        public static void ApplyViewRange(Document doc, View view, JObject spec)
        {
            if (!(view is ViewPlan plan))
                throw new ArgumentException($"'{view.Name}' is a {view.ViewType}; only plans have a view range.");

            PlanViewRange range = plan.GetViewRange();
            foreach (PlanViewPlane plane in ViewRangePlanes)
            {
                if (!(spec[PlaneKey(plane)] is JObject planeSpec))
                    continue;
                if (planeSpec["level"] != null)
                    range.SetLevelId(plane, ParseRangeLevel(doc, planeSpec["level"].ToString()));
                if (planeSpec["offset"] != null)
                    range.SetOffset(plane, ViewGraphicsUtils.FromMillimeters(planeSpec.Value<double>("offset")));
            }
            plan.SetViewRange(range);
        }

        private static string PlaneKey(PlanViewPlane plane)
        {
            switch (plane)
            {
                case PlanViewPlane.TopClipPlane: return "top";
                case PlanViewPlane.CutPlane: return "cut";
                case PlanViewPlane.BottomClipPlane: return "bottom";
                default: return "depth";
            }
        }

        private static string DescribeRangeLevel(Document doc, ElementId id)
        {
            if (id == PlanViewRange.Current) return "Associated";
            if (id == PlanViewRange.LevelAbove) return "Above";
            if (id == PlanViewRange.LevelBelow) return "Below";
            if (id == PlanViewRange.Unlimited) return "Unlimited";
            return ViewGraphicsUtils.ElementName(doc, id) ?? id.GetValue().ToString();
        }

        private static ElementId ParseRangeLevel(Document doc, string text)
        {
            switch (text.Trim().ToLowerInvariant())
            {
                case "associated":
                case "current":
                    return PlanViewRange.Current;
                case "above":
                    return PlanViewRange.LevelAbove;
                case "below":
                    return PlanViewRange.LevelBelow;
                case "unlimited":
                    return PlanViewRange.Unlimited;
            }

            Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => string.Equals(l.Name, text.Trim(), StringComparison.OrdinalIgnoreCase));
            return level?.Id ?? throw new ArgumentException($"View range level '{text}' isn't Associated, Above, Below, Unlimited or a level name.");
        }

        /// <summary>A parameter's value as Revit shows it, or null when it has none.</summary>
        public static string ParameterValue(Document doc, Parameter parameter)
        {
            if (!parameter.HasValue)
                return null;
            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString();
                case StorageType.ElementId:
                    return ViewGraphicsUtils.ElementName(doc, parameter.AsElementId()) ?? parameter.AsValueString();
                default:
                    return parameter.AsValueString();
            }
        }

        /// <summary>Views (not templates) grouped by the id of the template they use.</summary>
        public static Dictionary<long, List<View>> ViewsByTemplate(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.ViewTemplateId != ElementId.InvalidElementId)
                .GroupBy(v => v.ViewTemplateId.GetValue())
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private static void Add(JObject target, string key, Func<JToken> read)
        {
            try
            {
                // A null string converts to a String-typed JValue holding null, so check the value too.
                JToken value = read();
                if (value != null && value.Type != JTokenType.Null && !(value is JValue { Value: null }))
                    target[key] = value;
            }
            catch (Exception)
            {
                // This view type doesn't have the property.
            }
        }

        private static bool Try(Func<bool> check)
        {
            try
            {
                return check();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
