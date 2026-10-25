using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Makes a view template from a view, or duplicates an existing template, and names it.
    /// </summary>
    public class CreateViewTemplateEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            View source = ViewGraphicsUtils.ResolveView(doc, parameters["source"]);
            string name = parameters.Value<string>("name")?.Trim();
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Give the new template a name.");

            bool nameTaken = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Any(v => v.IsTemplate && string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (nameTaken)
                throw new ArgumentException($"A view template named '{name}' already exists.");

            if (!source.IsTemplate && !source.IsViewValidForTemplateCreation())
                throw new ArgumentException($"Revit can't make a template from '{source.Name}' ({source.ViewType}).");

            View template = InTransaction(doc, "Create view template", () =>
            {
                View created = source.IsTemplate
                    ? (View)doc.GetElement(source.Duplicate(ViewDuplicateOption.Duplicate))
                    : source.CreateViewTemplate();
                created.Name = name;
                return created;
            });

            JObject result = ViewSettings.ReadGeneral(doc, template);
            result["createdFrom"] = ViewGraphicsUtils.DescribeView(source);
            return result;
        }

        public override string GetName() => "Create view template";
    }
}
