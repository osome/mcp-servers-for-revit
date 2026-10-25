import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand } from "../utils/viewTools.js";

export function registerGetObjectStylesTool(server: McpServer) {
  server.tool(
    "get_object_styles",
    "Read object styles (Manage > Object Styles) in the open Revit model: projection and cut line weights, line color, line pattern and material for each category and subcategory. Line styles are the subcategories of \"Lines\" (category: \"Lines\").",
    {
      category: z
        .union([z.string(), z.number()])
        .optional()
        .describe('One category and its subcategories, e.g. "Walls" or "Lines"'),
      categoryType: z
        .enum(["Model", "Annotation", "AnalyticalModel", "Internal"])
        .optional()
        .describe("Only categories of this type (ignored when category is given)"),
      includeSubcategories: z.boolean().optional().describe("Include subcategories (default true)"),
    },
    async (args) => runRevitCommand("get_object_styles", args)
  );
}
