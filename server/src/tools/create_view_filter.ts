import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand } from "../utils/viewTools.js";

export function registerCreateViewFilterTool(server: McpServer) {
  server.tool(
    "create_view_filter",
    "Create a rule-based view filter in Revit (Manage > Filters). Each rule's parameter must apply to all the chosen categories; if it doesn't, the error lists parameters that do. Numbers are in the units Revit displays for that parameter (e.g. mm for lengths in a metric project). Apply the filter to templates or views with apply_view_filters.",
    {
      name: z.string().describe("Filter name"),
      categories: z
        .array(z.union([z.string(), z.number()]))
        .describe('Categories the filter applies to: names ("Walls"), built-in names ("OST_Walls") or ids'),
      rules: z
        .array(
          z.object({
            parameter: z
              .union([z.string(), z.number()])
              .describe('Parameter name as shown in Revit ("Type Mark", "Fire Rating"), built-in name ("WALL_BASE_CONSTRAINT") or id'),
            operator: z
              .enum([
                "equals", "notEquals", "contains", "notContains", "beginsWith", "notBeginsWith",
                "endsWith", "notEndsWith", "greater", "greaterOrEqual", "less", "lessOrEqual",
                "hasValue", "hasNoValue",
              ])
              .describe("contains/beginsWith/endsWith and their negations are for text parameters only"),
            value: z
              .union([z.string(), z.number(), z.boolean()])
              .optional()
              .describe("Value to compare with; not needed for hasValue/hasNoValue. For element parameters (Level, Phase...) the element's name or id; for yes/no parameters true/false"),
          })
        )
        .describe("Filter rules"),
      logic: z.enum(["and", "or"]).optional().describe("How rules combine (default and)"),
      overwrite: z
        .boolean()
        .optional()
        .describe("If a filter with this name exists, replace its categories and rules instead of failing"),
    },
    async (args) => runRevitCommand("create_view_filter", args)
  );
}
