using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Views
{
    /// <summary>
    /// Adds, updates or removes view filters on view templates or views, and optionally reorders them.
    /// </summary>
    public class ApplyViewFiltersEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            List<View> views = ViewGraphicsUtils.ResolveViews(doc, parameters["view"]);
            var entries = (parameters["filters"] as JArray ?? new JArray()).Cast<JObject>()
                .Select(e => (entry: e, filter: ViewGraphicsUtils.ResolveFilter(doc, e["filter"])))
                .ToList();
            List<ElementId> order = (parameters["order"] as JArray)?
                .Select(f => ViewGraphicsUtils.ResolveFilter(doc, f).Id)
                .ToList();
            if (entries.Count == 0 && order == null)
                throw new ArgumentException("Give filters to change, an order, or both.");

            var results = new JArray();
            InTransaction(doc, "Apply view filters", () =>
            {
                foreach (View view in views)
                {
                    var result = ViewGraphicsUtils.DescribeView(view);
                    var skipped = new JArray();

                    if (!view.AreGraphicsOverridesAllowed())
                    {
                        result["reason"] = "this view doesn't take graphic overrides";
                        results.Add(result);
                        continue;
                    }

                    foreach (var (entry, filter) in entries)
                    {
                        try
                        {
                            Apply(doc, view, filter, entry);
                        }
                        catch (Exception ex)
                        {
                            skipped.Add(new JObject { ["filter"] = filter.Name, ["reason"] = ex.Message });
                        }
                    }

                    if (order != null)
                    {
                        try
                        {
                            Reorder(view, order);
                        }
                        catch (Exception ex)
                        {
                            skipped.Add(new JObject { ["filter"] = "(order)", ["reason"] = ex.Message });
                        }
                    }

                    result["filters"] = ViewSettings.ReadFilters(doc, view);
                    if (skipped.Count > 0)
                        result["skipped"] = skipped;
                    results.Add(result);
                }
                return true;
            });

            return new JObject { ["views"] = results };
        }

        private static void Apply(Document doc, View view, FilterElement filter, JObject entry)
        {
            ElementId id = filter.Id;
            if (string.Equals(entry.Value<string>("action"), "remove", StringComparison.OrdinalIgnoreCase))
            {
                if (view.IsFilterApplied(id))
                    view.RemoveFilter(id);
                return;
            }

            if (!view.IsFilterApplied(id))
                view.AddFilter(id);

            if (entry["visible"] != null)
                view.SetFilterVisibility(id, entry.Value<bool>("visible"));
#if REVIT2022_OR_GREATER
            if (entry["enabled"] != null)
                view.SetIsFilterEnabled(id, entry.Value<bool>("enabled"));
#endif

            bool reset = entry.Value<bool?>("reset") ?? false;
            JObject overrides = entry["overrides"] as JObject;
            if (reset || overrides != null)
            {
                OverrideGraphicSettings current = reset ? new OverrideGraphicSettings() : view.GetFilterOverrides(id);
                view.SetFilterOverrides(id, ViewGraphicsUtils.ApplyOverrides(doc, current, overrides));
            }
        }

        /// <summary>
        /// Puts the filters in <paramref name="order"/> first, in that order, followed by the others as they were.
        /// Revit has no reorder call, so every filter is removed and added back with its settings.
        /// </summary>
        private static void Reorder(View view, List<ElementId> order)
        {
            List<ElementId> current = ViewSettings.OrderedFilterIds(view);
            List<ElementId> wanted = order.Where(current.Contains)
                .Concat(current.Where(id => !order.Contains(id)))
                .ToList();
            if (wanted.SequenceEqual(current))
                return;

            var saved = current.ToDictionary(id => id, id => (
                visible: view.GetFilterVisibility(id),
#if REVIT2022_OR_GREATER
                enabled: view.GetIsFilterEnabled(id),
#else
                enabled: true,
#endif
                overrides: view.GetFilterOverrides(id)));

            foreach (ElementId id in current)
                view.RemoveFilter(id);

            foreach (ElementId id in wanted)
            {
                view.AddFilter(id);
                view.SetFilterVisibility(id, saved[id].visible);
#if REVIT2022_OR_GREATER
                view.SetIsFilterEnabled(id, saved[id].enabled);
#endif
                view.SetFilterOverrides(id, saved[id].overrides);
            }
        }

        public override string GetName() => "Apply view filters";
    }
}
