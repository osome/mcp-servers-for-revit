import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand } from "../utils/viewTools.js";

export function registerSetObjectStylesTool(server: McpServer) {
  server.tool(
    "set_object_styles",
    "Change object styles (Manage > Object Styles) in Revit: projection and cut line weights, line color, line pattern and material per category or subcategory. With create: true a missing subcategory is created, which is how to add a line style (\"Lines|NPC Setout\"). These are project-wide; per-view changes belong in set_category_overrides.",
    {
      styles: z
        .array(
          z.object({
            category: z
              .union([z.string(), z.number()])
              .describe('Category ("Walls"), subcategory as "Walls|Common Edges", built-in name or id'),
            create: z.boolean().optional().describe("Create the subcategory if it doesn't exist"),
            projectionWeight: z.number().int().min(1).max(16).optional(),
            cutWeight: z.number().int().min(1).max(16).optional().describe("Only for categories that are cut"),
            color: z.string().optional().describe('Line color "#RRGGBB" or "r,g,b"'),
            pattern: z.string().optional().describe('Line pattern name, or "Solid"'),
            material: z.string().nullable().optional().describe("Material name; null clears it"),
          })
        )
        .describe("Styles to change"),
    },
    async (args) => runRevitCommand("set_object_styles", args)
  );
}
