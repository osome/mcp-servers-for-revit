using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Assigns a view template to views, removes it, or applies its settings once without linking.
    /// </summary>
    public class AssignViewTemplateEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            List<View> views = ViewGraphicsUtils.ResolveViews(doc, parameters["views"], templates: false);
            string mode = (parameters.Value<string>("mode") ?? "assign").Trim().ToLowerInvariant();
            if (mode != "assign" && mode != "applyonce" && mode != "remove")
                throw new ArgumentException($"Unknown mode '{mode}'; use assign, applyOnce or remove.");

            View template = null;
            if (mode != "remove")
                template = ViewGraphicsUtils.ResolveView(doc, parameters["template"], templates: true);

            var done = new JArray();
            var skipped = new JArray();

            InTransaction(doc, "Assign view template", () =>
            {
                foreach (View view in views)
                {
                    var item = ViewGraphicsUtils.DescribeView(view);
                    item.Remove("isTemplate");
                    try
                    {
                        switch (mode)
                        {
                            case "assign":
                                if (!view.IsValidViewTemplate(template.Id))
                                    throw new ArgumentException($"'{template.Name}' ({template.ViewType}) can't be the template of a {view.ViewType}.");
                                view.ViewTemplateId = template.Id;
                                break;
                            case "applyonce":
                                view.ApplyViewTemplateParameters(template);
                                break;
                            default:
                                view.ViewTemplateId = ElementId.InvalidElementId;
                                break;
                        }
                        done.Add(item);
                    }
                    catch (Exception ex)
                    {
                        item["reason"] = ex.Message;
                        skipped.Add(item);
                    }
                }
                return true;
            });

            return new JObject
            {
                ["mode"] = mode,
                ["template"] = template?.Name,
                ["done"] = done,
                ["skipped"] = skipped,
            };
        }

        public override string GetName() => "Assign view template";
    }
}
