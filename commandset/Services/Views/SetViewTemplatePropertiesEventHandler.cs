using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Services.Views
{
    /// <summary>
    /// Changes properties of view templates or views: name, scale, detail level, discipline, display style,
    /// parts visibility, phase filter, view range, other parameters by name, and which settings a template includes.
    /// Each change is tried on its own and reported, so one setting a view doesn't have doesn't block the rest.
    /// </summary>
    public class SetViewTemplatePropertiesEventHandler : JsonEventHandler
    {
        protected override JToken Run(Document doc, JObject parameters)
        {
            List<View> views = ViewGraphicsUtils.ResolveViews(doc, parameters["view"]);
            if (parameters["name"] != null && views.Count > 1)
                throw new ArgumentException("A new name can only be given to one view at a time.");

            var results = new JArray();
            InTransaction(doc, "Set view template properties", () =>
            {
                foreach (View view in views)
                    results.Add(Apply(doc, view, parameters));
                return true;
            });

            return new JObject { ["views"] = results };
        }

        private static JObject Apply(Document doc, View view, JObject p)
        {
            var changed = new JArray();
            var failed = new JArray();

            void Try(string setting, Action change)
            {
                try
                {
                    change();
                    changed.Add(setting);
                }
                catch (Exception ex)
                {
                    failed.Add(new JObject { ["setting"] = setting, ["reason"] = ex.Message });
                }
            }

            if (p["name"] != null)
                Try("name", () => view.Name = p.Value<string>("name"));
            if (p["scale"] != null)
                Try("scale", () => view.Scale = p.Value<int>("scale"));
            if (p["detailLevel"] != null)
                Try("detailLevel", () => view.DetailLevel = ViewGraphicsUtils.ParseEnum<ViewDetailLevel>(p["detailLevel"], "detailLevel"));
            if (p["discipline"] != null)
                Try("discipline", () => view.Discipline = ViewGraphicsUtils.ParseEnum<ViewDiscipline>(p["discipline"], "discipline"));
            if (p["displayStyle"] != null)
                Try("displayStyle", () => view.DisplayStyle = ViewGraphicsUtils.ParseEnum<DisplayStyle>(p["displayStyle"], "displayStyle"));
            if (p["partsVisibility"] != null)
                Try("partsVisibility", () => view.PartsVisibility = ViewGraphicsUtils.ParseEnum<PartsVisibility>(p["partsVisibility"], "partsVisibility"));
            if (p["phaseFilter"] != null)
                Try("phaseFilter", () => SetElementParameter(doc, view.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER), p["phaseFilter"], typeof(PhaseFilter)));
            if (p["viewRange"] is JObject viewRange)
                Try("viewRange", () => ViewSettings.ApplyViewRange(doc, view, viewRange));

            if (p["parameters"] is JObject named)
            {
                foreach (JProperty property in named.Properties())
                    Try(property.Name, () => SetParameter(doc, view, property.Name, property.Value));
            }

            if (p["include"] != null || p["exclude"] != null)
                Try("includes", () => SetIncludes(doc, view, p["include"] as JArray, p["exclude"] as JArray));

            var result = ViewGraphicsUtils.DescribeView(view);
            result["changed"] = changed;
            if (failed.Count > 0)
                result["failed"] = failed;
            return result;
        }

        /// <summary>
        /// Sets a parameter by name. Strings and numbers are entered as in the Properties palette
        /// (numbers in the project's display units); element parameters take an element name or id.
        /// </summary>
        private static void SetParameter(Document doc, View view, string name, JToken value)
        {
            Parameter parameter = view.LookupParameter(name)
                ?? throw new ArgumentException($"'{view.Name}' has no parameter '{name}'.");
            if (parameter.IsReadOnly)
                throw new ArgumentException($"'{name}' is read-only here (a view template may control it).");

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    parameter.Set(ViewGraphicsUtils.IsNull(value) ? "" : value.ToString());
                    break;
                case StorageType.Integer:
                    if (value.Type == JTokenType.Boolean)
                        parameter.Set(value.Value<bool>() ? 1 : 0);
                    else if (value.Type == JTokenType.Integer)
                        parameter.Set(value.Value<int>());
                    else if (!parameter.SetValueString(value.ToString()))
                        throw new ArgumentException($"Revit didn't accept '{value}' for '{name}'.");
                    break;
                case StorageType.Double:
                    if (!parameter.SetValueString(value.ToString()))
                        throw new ArgumentException($"Revit didn't accept '{value}' for '{name}'.");
                    break;
                case StorageType.ElementId:
                    SetElementParameter(doc, parameter, value, null);
                    break;
                default:
                    throw new ArgumentException($"'{name}' can't be set.");
            }
        }

        private static void SetElementParameter(Document doc, Parameter parameter, JToken value, Type elementClass)
        {
            if (parameter == null)
                throw new ArgumentException("This view doesn't have that setting.");

            if (ViewGraphicsUtils.IsNull(value) || string.Equals(value.ToString(), "none", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Set(ElementId.InvalidElementId);
                return;
            }

            if (value.Type == JTokenType.Integer)
            {
                parameter.Set(value.Value<long>().ToRevitElementId());
                return;
            }

            // Look among elements of the same class as the current value, so a name like "Show Complete"
            // finds the phase filter and not something else with that name.
            Type searchClass = elementClass ?? doc.GetElement(parameter.AsElementId())?.GetType();
            string name = value.ToString().Trim();
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = searchClass != null ? collector.OfClass(searchClass) : collector.WhereElementIsNotElementType();
            Element match = collector.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"No element named '{name}' fits '{parameter.Definition.Name}'.");
            parameter.Set(match.Id);
        }

        private static void SetIncludes(Document doc, View view, JArray include, JArray exclude)
        {
            if (!view.IsTemplate)
                throw new ArgumentException($"'{view.Name}' is not a view template; only templates include or exclude settings.");

            Dictionary<string, ElementId> byName = view.GetTemplateParameterIds()
                .GroupBy(id => ViewGraphicsUtils.ParameterName(doc, id), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            ElementId Find(JToken token)
            {
                if (token.Type == JTokenType.Integer)
                    return token.Value<long>().ToRevitElementId();
                if (byName.TryGetValue(token.ToString().Trim(), out ElementId id))
                    return id;
                throw new ArgumentException($"Template '{view.Name}' has no setting '{token}'. Settings: {string.Join(", ", byName.Keys)}.");
            }

            var notControlled = new HashSet<ElementId>(view.GetNonControlledTemplateParameterIds());
            foreach (JToken token in include ?? new JArray())
                notControlled.Remove(Find(token));
            foreach (JToken token in exclude ?? new JArray())
                notControlled.Add(Find(token));

            view.SetNonControlledTemplateParameterIds(notControlled.ToList());
        }

        public override string GetName() => "Set view template properties";
    }
}
