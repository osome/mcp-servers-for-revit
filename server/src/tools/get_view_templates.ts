import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand } from "../utils/viewTools.js";

export function registerGetViewTemplatesTool(server: McpServer) {
  server.tool(
    "get_view_templates",
    "List the view templates in the open Revit model: id, name, view type, scale, detail level, discipline, display style, phase filter, and how many views use each. Use get_view_template_settings for the full settings of one.",
    {
      viewType: z
        .string()
        .optional()
        .describe("Only templates of this view type, e.g. FloorPlan, CeilingPlan, Section, Elevation, ThreeD, Schedule, DraftingView"),
    },
    async (args) => runRevitCommand("get_view_templates", args)
  );
}
