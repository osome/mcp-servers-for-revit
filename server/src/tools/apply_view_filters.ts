import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { overrides, runRevitCommand, viewRefs } from "../utils/viewTools.js";

const filterRef = z.union([z.string(), z.number()]).describe("View filter name or id");

export function registerApplyViewFiltersTool(server: McpServer) {
  server.tool(
    "apply_view_filters",
    "Add, update or remove view filters on view templates or views in Revit (the Filters tab of Visibility/Graphics), and optionally set their order. Returns each view's filters afterwards. A view whose template controls filters can't change them; change the template instead.",
    {
      view: viewRefs,
      filters: z
        .array(
          z.object({
            filter: filterRef,
            action: z
              .enum(["set", "remove"])
              .optional()
              .describe("set (default): add the filter if missing and apply the settings given; remove: take it off"),
            visible: z.boolean().optional().describe("Visibility of elements that pass the filter"),
            enabled: z.boolean().optional().describe("Whether the filter is enabled (Revit 2022+)"),
            reset: z.boolean().optional().describe("Clear the filter's overrides first"),
            overrides: overrides.optional(),
          })
        )
        .optional()
        .describe("Filters to change"),
      order: z
        .array(filterRef)
        .optional()
        .describe("Filters to put first, in this order (the top filter wins); others keep their order after them"),
    },
    async (args) => runRevitCommand("apply_view_filters", args)
  );
}
