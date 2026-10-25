using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Reports which views use which template, which have none, and where they sit on sheets,
    /// plus for each template in use the settings it leaves to the views (not included).
    /// </summary>
    public class AuditViewsEventHandler : JsonEventHandler
    {
        private static readonly HashSet<ViewType> NotAudited = new HashSet<ViewType>
        {
            ViewType.DrawingSheet, ViewType.ProjectBrowser, ViewType.SystemBrowser, ViewType.Internal, ViewType.Undefined,
        };

        protected override JToken Run(Document doc, JObject parameters)
        {
            string viewType = parameters.Value<string>("viewType")?.Replace(" ", "");
            bool withoutTemplateOnly = parameters.Value<bool?>("withoutTemplate") ?? false;
            bool onSheetsOnly = parameters.Value<bool?>("onSheetsOnly") ?? false;
            int limit = parameters.Value<int?>("limit") ?? 500;
            View templateFilter = parameters["template"] != null
                ? ViewGraphicsUtils.ResolveView(doc, parameters["template"], templates: true)
                : null;

            Dictionary<long, string> sheets = SheetsByView(doc);

            List<View> all = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && !NotAudited.Contains(v.ViewType))
                .ToList();

            List<View> selected = all
                .Where(v => string.IsNullOrEmpty(viewType) || string.Equals(v.ViewType.ToString(), viewType, StringComparison.OrdinalIgnoreCase))
                .Where(v => !withoutTemplateOnly || v.ViewTemplateId == ElementId.InvalidElementId)
                .Where(v => templateFilter == null || v.ViewTemplateId == templateFilter.Id)
                .Where(v => !onSheetsOnly || sheets.ContainsKey(v.Id.GetValue()))
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .ToList();

            var views = new JArray();
            foreach (View view in selected.Take(limit))
            {
                var entry = new JObject
                {
                    ["id"] = view.Id.GetValue(),
                    ["name"] = view.Name,
                    ["viewType"] = view.ViewType.ToString(),
                    ["template"] = ViewGraphicsUtils.ElementName(doc, view.ViewTemplateId),
                    ["sheet"] = sheets.TryGetValue(view.Id.GetValue(), out string sheet) ? sheet : null,
                };
                views.Add(entry);
            }

            var templates = new JArray();
            foreach (var group in selected.Where(v => v.ViewTemplateId != ElementId.InvalidElementId).GroupBy(v => v.ViewTemplateId.GetValue()))
            {
                if (!(doc.GetElement(group.First().ViewTemplateId) is View template))
                    continue;
                templates.Add(new JObject
                {
                    ["id"] = template.Id.GetValue(),
                    ["name"] = template.Name,
                    ["usedByViews"] = group.Count(),
                    ["notIncluded"] = new JArray(template.GetNonControlledTemplateParameterIds()
                        .Select(id => ViewGraphicsUtils.ParameterName(doc, id))
                        .OrderBy(n => n)),
                });
            }

            List<View> withoutTemplate = all.Where(v => v.ViewTemplateId == ElementId.InvalidElementId).ToList();
            return new JObject
            {
                ["summary"] = new JObject
                {
                    ["views"] = all.Count,
                    ["withTemplate"] = all.Count - withoutTemplate.Count,
                    ["withoutTemplate"] = withoutTemplate.Count,
                    ["withoutTemplateOnSheets"] = withoutTemplate.Count(v => sheets.ContainsKey(v.Id.GetValue())),
                },
                ["matching"] = selected.Count,
                ["truncated"] = selected.Count > limit,
                ["views"] = views,
                ["templates"] = templates,
            };
        }

        /// <summary>"A101 - Ground Floor" for every view or schedule placed on a sheet, keyed by view id.</summary>
        private static Dictionary<long, string> SheetsByView(Document doc)
        {
            var result = new Dictionary<long, string>();
            string Label(ElementId sheetId) =>
                doc.GetElement(sheetId) is ViewSheet sheet ? $"{sheet.SheetNumber} - {sheet.Name}" : null;

            foreach (Viewport viewport in new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>())
                result[viewport.ViewId.GetValue()] = Label(viewport.SheetId);

            foreach (ScheduleSheetInstance instance in new FilteredElementCollector(doc).OfClass(typeof(ScheduleSheetInstance)).Cast<ScheduleSheetInstance>())
            {
                if (!instance.IsTitleblockRevisionSchedule)
                    result[instance.ScheduleId.GetValue()] = Label(instance.OwnerViewId);
            }

            return result;
        }

        public override string GetName() => "Audit views";
    }
}
