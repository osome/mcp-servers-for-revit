import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { overrides, runRevitCommand, viewRefs } from "../utils/viewTools.js";

export function registerSetCategoryOverridesTool(server: McpServer) {
  server.tool(
    "set_category_overrides",
    "Set category visibility and graphic overrides (Visibility/Graphics) on view templates or views in Revit. Each entry names a category and what to change; the same entries are applied to every view given. Categories that can't be hidden or overridden in a view are reported as skipped.",
    {
      view: viewRefs,
      categories: z
        .array(
          z.object({
            category: z
              .union([z.string(), z.number()])
              .describe('Category name ("Walls"), subcategory as "Walls|Common Edges", built-in name ("OST_Walls") or id'),
            visible: z.boolean().optional().describe("Show or hide the category"),
            reset: z.boolean().optional().describe("Clear all overrides first"),
            overrides: overrides.optional(),
          })
        )
        .describe("Changes per category"),
    },
    async (args) => runRevitCommand("set_category_overrides", args)
  );
}
