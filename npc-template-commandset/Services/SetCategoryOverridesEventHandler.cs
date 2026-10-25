using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Sets category visibility and graphic overrides (Visibility/Graphics) on view templates or views.
    /// </summary>
    public class SetCategoryOverridesEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            List<View> views = ViewGraphicsUtils.ResolveViews(doc, parameters["view"]);
            if (!(parameters["categories"] is JArray entries) || entries.Count == 0)
                throw new ArgumentException("Give at least one entry in categories.");

            // Resolve everything first, so a typo fails the call before anything changes.
            var resolved = entries.Cast<JObject>()
                .Select(e => (entry: e, category: ViewGraphicsUtils.ResolveCategory(doc, e["category"])))
                .ToList();

            var applied = new JArray();
            var skipped = new JArray();

            InTransaction(doc, "Set category overrides", () =>
            {
                foreach (View view in views)
                {
                    foreach (var (entry, category) in resolved)
                    {
                        string reason = Apply(doc, view, category, entry);
                        var item = new JObject { ["view"] = view.Name, ["category"] = ViewGraphicsUtils.CategoryPath(category) };
                        if (reason == null)
                        {
                            applied.Add(item);
                        }
                        else
                        {
                            item["reason"] = reason;
                            skipped.Add(item);
                        }
                    }
                }
                return true;
            });

            return new JObject { ["applied"] = applied, ["skipped"] = skipped };
        }

        /// <returns>Null when applied, otherwise why it was skipped.</returns>
        private static string Apply(Document doc, View view, Category category, JObject entry)
        {
            ElementId id = category.Id;
            bool reset = entry.Value<bool?>("reset") ?? false;
            JObject overrides = entry["overrides"] as JObject;

            if (entry["visible"] != null && !ViewGraphicsUtils.IsNull(entry["visible"]))
            {
                if (!view.CanCategoryBeHidden(id))
                    return "its visibility can't be changed in this view";
                view.SetCategoryHidden(id, !entry.Value<bool>("visible"));
            }

            if (reset || overrides != null)
            {
                if (!view.IsCategoryOverridable(id))
                    return "it can't be overridden in this view";
                OverrideGraphicSettings current = reset ? new OverrideGraphicSettings() : view.GetCategoryOverrides(id);
                OverrideGraphicSettings wanted = ViewGraphicsUtils.ApplyOverrides(doc, current, overrides);
                view.SetCategoryOverrides(id, wanted);

                // Revit reports some subcategories as overridable but drops the change, e.g. the wall layer
                // subcategories (Structure, Finish 1, Common Edges...), which V/G doesn't offer either.
                JObject kept = ViewGraphicsUtils.ReadOverrides(doc, view.GetCategoryOverrides(id));
                if (!JToken.DeepEquals(kept, ViewGraphicsUtils.ReadOverrides(doc, wanted)))
                    return "Revit didn't keep the overrides; this category can't be overridden per view";
            }

            return null;
        }

        public override string GetName() => "Set category overrides";
    }
}
