using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using NPCTemplateCommandSet.Utils;

namespace NPCTemplateCommandSet.Services
{
    /// <summary>
    /// Builds the element filter of a rule-based view filter from JSON rules, and describes an existing one
    /// in the same shape: { parameter, operator, value }.
    /// Numbers are in the units Revit displays for that parameter (as typed in the Filters dialog), worked out
    /// from an element in the filter's categories that has the parameter; with no such element, internal units.
    /// </summary>
    public class FilterRules
    {
        private const double DoubleTolerance = 1e-6;

        private static readonly Dictionary<string, string> Inverses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["equals"] = "notEquals",
            ["contains"] = "notContains",
            ["beginsWith"] = "notBeginsWith",
            ["endsWith"] = "notEndsWith",
            ["greater"] = "lessOrEqual",
            ["greaterOrEqual"] = "less",
            ["less"] = "greaterOrEqual",
            ["lessOrEqual"] = "greater",
            ["hasValue"] = "hasNoValue",
        };

        private readonly Document _doc;
        private readonly ICollection<ElementId> _categoryIds;
        private readonly Dictionary<long, Parameter> _samples = new Dictionary<long, Parameter>();
        private Dictionary<string, ElementId> _parametersByName;

        public FilterRules(Document doc, ICollection<ElementId> categoryIds)
        {
            _doc = doc;
            _categoryIds = categoryIds;
        }

        // ---------- Building ----------

        /// <summary>Rules combined with "and" (default) or "or".</summary>
        public ElementFilter Build(JArray rules, string logic)
        {
            if (rules == null || rules.Count == 0)
                throw new ArgumentException("Give at least one rule.");

            List<FilterRule> built = rules.Cast<JObject>().Select(BuildRule).ToList();
            bool or = string.Equals(logic, "or", StringComparison.OrdinalIgnoreCase);
            if (!or || built.Count == 1)
                return new ElementParameterFilter(built);

            return new LogicalOrFilter(built.Select(r => (ElementFilter)new ElementParameterFilter(r)).ToList());
        }

        private FilterRule BuildRule(JObject rule)
        {
            ElementId parameterId = ResolveParameter(rule["parameter"]);
            string op = rule.Value<string>("operator") ?? "equals";
            JToken value = rule["value"];

            switch (op.ToLowerInvariant())
            {
                case "hasvalue":
#if REVIT2023_OR_GREATER
                    return ParameterFilterRuleFactory.CreateHasValueParameterRule(parameterId);
#else
                    throw new NotSupportedException("hasValue rules need Revit 2023 or later.");
#endif
                case "hasnovalue":
#if REVIT2023_OR_GREATER
                    return ParameterFilterRuleFactory.CreateHasNoValueParameterRule(parameterId);
#else
                    throw new NotSupportedException("hasNoValue rules need Revit 2023 or later.");
#endif
            }

            if (ViewGraphicsUtils.IsNull(value))
                throw new ArgumentException($"Rule on '{rule["parameter"]}' needs a value.");

            Parameter sample = Sample(parameterId);
            StorageType storage = sample?.StorageType ?? Guess(value);

            switch (storage)
            {
                case StorageType.Double:
                    return NumericRule(op, parameterId, ToInternal(sample, value.Value<double>()));
                case StorageType.Integer:
                    return NumericRule(op, parameterId, value.Type == JTokenType.Boolean ? (value.Value<bool>() ? 1 : 0) : value.Value<int>());
                case StorageType.ElementId:
                    return NumericRule(op, parameterId, ResolveElementValue(sample, value));
                default:
                    return StringRule(op, parameterId, value.ToString());
            }
        }

        private static StorageType Guess(JToken value)
        {
            switch (value.Type)
            {
                case JTokenType.Integer:
                case JTokenType.Boolean:
                    return StorageType.Integer;
                case JTokenType.Float:
                    return StorageType.Double;
                default:
                    return StorageType.String;
            }
        }

        private static FilterRule StringRule(string op, ElementId id, string value)
        {
            switch (op.ToLowerInvariant())
            {
#if REVIT2023_OR_GREATER
                case "equals": return ParameterFilterRuleFactory.CreateEqualsRule(id, value);
                case "notequals": return ParameterFilterRuleFactory.CreateNotEqualsRule(id, value);
                case "contains": return ParameterFilterRuleFactory.CreateContainsRule(id, value);
                case "notcontains": return ParameterFilterRuleFactory.CreateNotContainsRule(id, value);
                case "beginswith": return ParameterFilterRuleFactory.CreateBeginsWithRule(id, value);
                case "notbeginswith": return ParameterFilterRuleFactory.CreateNotBeginsWithRule(id, value);
                case "endswith": return ParameterFilterRuleFactory.CreateEndsWithRule(id, value);
                case "notendswith": return ParameterFilterRuleFactory.CreateNotEndsWithRule(id, value);
                case "greater": return ParameterFilterRuleFactory.CreateGreaterRule(id, value);
                case "greaterorequal": return ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value);
                case "less": return ParameterFilterRuleFactory.CreateLessRule(id, value);
                case "lessorequal": return ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value);
#else
                case "equals": return ParameterFilterRuleFactory.CreateEqualsRule(id, value, false);
                case "notequals": return ParameterFilterRuleFactory.CreateNotEqualsRule(id, value, false);
                case "contains": return ParameterFilterRuleFactory.CreateContainsRule(id, value, false);
                case "notcontains": return ParameterFilterRuleFactory.CreateNotContainsRule(id, value, false);
                case "beginswith": return ParameterFilterRuleFactory.CreateBeginsWithRule(id, value, false);
                case "notbeginswith": return ParameterFilterRuleFactory.CreateNotBeginsWithRule(id, value, false);
                case "endswith": return ParameterFilterRuleFactory.CreateEndsWithRule(id, value, false);
                case "notendswith": return ParameterFilterRuleFactory.CreateNotEndsWithRule(id, value, false);
                case "greater": return ParameterFilterRuleFactory.CreateGreaterRule(id, value, false);
                case "greaterorequal": return ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value, false);
                case "less": return ParameterFilterRuleFactory.CreateLessRule(id, value, false);
                case "lessorequal": return ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value, false);
#endif
                default:
                    throw new ArgumentException($"Operator '{op}' doesn't apply to text parameters.");
            }
        }

        private static FilterRule NumericRule(string op, ElementId id, double value)
        {
            switch (op.ToLowerInvariant())
            {
                case "equals": return ParameterFilterRuleFactory.CreateEqualsRule(id, value, DoubleTolerance);
                case "notequals": return ParameterFilterRuleFactory.CreateNotEqualsRule(id, value, DoubleTolerance);
                case "greater": return ParameterFilterRuleFactory.CreateGreaterRule(id, value, DoubleTolerance);
                case "greaterorequal": return ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value, DoubleTolerance);
                case "less": return ParameterFilterRuleFactory.CreateLessRule(id, value, DoubleTolerance);
                case "lessorequal": return ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value, DoubleTolerance);
                default: throw new ArgumentException($"Operator '{op}' doesn't apply to number parameters.");
            }
        }

        private static FilterRule NumericRule(string op, ElementId id, int value)
        {
            switch (op.ToLowerInvariant())
            {
                case "equals": return ParameterFilterRuleFactory.CreateEqualsRule(id, value);
                case "notequals": return ParameterFilterRuleFactory.CreateNotEqualsRule(id, value);
                case "greater": return ParameterFilterRuleFactory.CreateGreaterRule(id, value);
                case "greaterorequal": return ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value);
                case "less": return ParameterFilterRuleFactory.CreateLessRule(id, value);
                case "lessorequal": return ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value);
                default: throw new ArgumentException($"Operator '{op}' doesn't apply to integer or yes/no parameters.");
            }
        }

        private static FilterRule NumericRule(string op, ElementId id, ElementId value)
        {
            switch (op.ToLowerInvariant())
            {
                case "equals": return ParameterFilterRuleFactory.CreateEqualsRule(id, value);
                case "notequals": return ParameterFilterRuleFactory.CreateNotEqualsRule(id, value);
                case "greater": return ParameterFilterRuleFactory.CreateGreaterRule(id, value);
                case "greaterorequal": return ParameterFilterRuleFactory.CreateGreaterOrEqualRule(id, value);
                case "less": return ParameterFilterRuleFactory.CreateLessRule(id, value);
                case "lessorequal": return ParameterFilterRuleFactory.CreateLessOrEqualRule(id, value);
                default: throw new ArgumentException($"Operator '{op}' doesn't apply to element parameters.");
            }
        }

        private ElementId ResolveElementValue(Parameter sample, JToken value)
        {
            if (value.Type == JTokenType.Integer)
                return value.Value<long>().ToRevitElementId();

            string name = value.ToString().Trim();
            Type valueClass = sample != null ? _doc.GetElement(sample.AsElementId())?.GetType() : null;
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector = valueClass != null ? collector.OfClass(valueClass) : collector.WhereElementIsNotElementType();
            Element match = collector.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? throw new ArgumentException($"No element named '{name}' to compare with; pass its id instead.");
        }

        /// <summary>Finds a parameter shared by all the filter's categories, by name or id.</summary>
        public ElementId ResolveParameter(JToken reference)
        {
            if (_parametersByName == null)
            {
                _parametersByName = ParameterFilterUtilities.GetFilterableParametersInCommon(_doc, _categoryIds)
                    .GroupBy(id => ViewGraphicsUtils.ParameterName(_doc, id), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            }

            if (reference != null && reference.Type == JTokenType.Integer)
                return reference.Value<long>().ToRevitElementId();

            string name = reference?.ToString().Trim() ?? "";
            if (_parametersByName.TryGetValue(name, out ElementId id))
                return id;

            if (name.StartsWith("BuiltInParameter.", StringComparison.OrdinalIgnoreCase))
                name = name.Substring("BuiltInParameter.".Length);
            if (Enum.TryParse(name, true, out BuiltInParameter builtIn))
                return new ElementId(builtIn);

            string available = string.Join(", ", _parametersByName.Keys.OrderBy(k => k).Take(60));
            throw new ArgumentException($"No parameter '{name}' is shared by all the filter's categories. Some that are: {available}.");
        }

        // ---------- Describing ----------

        public JToken Describe(ElementFilter filter)
        {
            JToken described;
            switch (filter)
            {
                case ElementLogicalFilter logical:
                    described = new JObject
                    {
                        ["logic"] = logical is LogicalOrFilter ? "or" : "and",
                        ["rules"] = new JArray(logical.GetFilters().Select(Describe)),
                    };
                    break;
                case ElementParameterFilter parameterFilter:
                    IList<FilterRule> rules = parameterFilter.GetRules();
                    described = rules.Count == 1
                        ? DescribeRule(rules[0])
                        : new JObject { ["logic"] = "and", ["rules"] = new JArray(rules.Select(DescribeRule)) };
                    break;
                default:
                    described = new JObject { ["type"] = filter.GetType().Name };
                    break;
            }

            if (filter.Inverted && described is JObject inverted)
                inverted["not"] = true;
            return described;
        }

        public JObject DescribeRule(FilterRule rule)
        {
            if (rule is FilterInverseRule inverse)
            {
                JObject inner = DescribeRule(inverse.GetInnerRule());
                string op = inner.Value<string>("operator");
                if (op != null && Inverses.TryGetValue(op, out string inverted))
                    inner["operator"] = inverted;
                else
                    inner["not"] = true;
                return inner;
            }

            ElementId parameterId = rule.GetRuleParameter();
            var result = new JObject { ["parameter"] = ViewGraphicsUtils.ParameterName(_doc, parameterId) };

            switch (rule)
            {
                case FilterStringRule stringRule:
                    result["operator"] = OperatorName(stringRule.GetEvaluator(), "FilterString");
                    result["value"] = stringRule.RuleString;
                    break;
                case FilterDoubleRule doubleRule:
                    result["operator"] = OperatorName(doubleRule.GetEvaluator(), "FilterNumeric");
                    result["value"] = Math.Round(FromInternal(Sample(parameterId), doubleRule.RuleValue), 6);
                    break;
                case FilterIntegerRule integerRule:
                    result["operator"] = OperatorName(integerRule.GetEvaluator(), "FilterNumeric");
                    result["value"] = integerRule.RuleValue;
                    break;
                case FilterElementIdRule idRule:
                    result["operator"] = OperatorName(idRule.GetEvaluator(), "FilterNumeric");
                    result["value"] = ViewGraphicsUtils.ElementName(_doc, idRule.RuleValue) ?? idRule.RuleValue.GetValue().ToString();
                    break;
#if REVIT2023_OR_GREATER
                case HasValueFilterRule _:
                    result["operator"] = "hasValue";
                    break;
                case HasNoValueFilterRule _:
                    result["operator"] = "hasNoValue";
                    break;
#endif
                default:
                    result["operator"] = rule.GetType().Name;
                    break;
            }

            return result;
        }

        /// <summary>FilterStringBeginsWith -> beginsWith.</summary>
        private static string OperatorName(object evaluator, string prefix)
        {
            string name = evaluator.GetType().Name;
            if (name.StartsWith(prefix))
                name = name.Substring(prefix.Length);
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        // ---------- Units ----------

        /// <summary>An element in the filter's categories that has the parameter, to learn its storage and units.</summary>
        private Parameter Sample(ElementId parameterId)
        {
            long key = parameterId.GetValue();
            if (_samples.TryGetValue(key, out Parameter cached))
                return cached;

            Parameter found = null;
            if (_categoryIds.Count > 0)
            {
                var categories = new ElementMulticategoryFilter(_categoryIds);
                foreach (bool types in new[] { false, true })
                {
                    FilteredElementCollector collector = new FilteredElementCollector(_doc).WherePasses(categories);
                    collector = types ? collector.WhereElementIsElementType() : collector.WhereElementIsNotElementType();
                    found = collector.Take(500)
                        .Select(e => e.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Id.GetValue() == key))
                        .FirstOrDefault(p => p != null);
                    if (found != null)
                        break;
                }
            }

            _samples[key] = found;
            return found;
        }

        private static double ToInternal(Parameter sample, double value)
        {
            if (sample == null)
                return value;
            try
            {
#if REVIT2022_OR_GREATER
                return UnitUtils.ConvertToInternalUnits(value, sample.GetUnitTypeId());
#else
                return UnitUtils.ConvertToInternalUnits(value, sample.DisplayUnitType);
#endif
            }
            catch (Exception)
            {
                return value; // Unitless number.
            }
        }

        private static double FromInternal(Parameter sample, double value)
        {
            if (sample == null)
                return value;
            try
            {
#if REVIT2022_OR_GREATER
                return UnitUtils.ConvertFromInternalUnits(value, sample.GetUnitTypeId());
#else
                return UnitUtils.ConvertFromInternalUnits(value, sample.DisplayUnitType);
#endif
            }
            catch (Exception)
            {
                return value;
            }
        }
    }
}
