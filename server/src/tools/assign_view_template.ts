import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand, viewRef, viewRefs } from "../utils/viewTools.js";

export function registerAssignViewTemplateTool(server: McpServer) {
  server.tool(
    "assign_view_template",
    "Assign a view template to views in Revit, remove their template, or apply a template's settings once without linking it. Views Revit won't take the template on are reported as skipped. Revit accepts some templates of another view type (e.g. a section template on a plan), so match view types yourself.",
    {
      views: viewRefs.describe("Views (not templates) to change: element ids or exact names"),
      template: viewRef.optional().describe("View template to assign or apply; not needed for mode remove"),
      mode: z
        .enum(["assign", "applyOnce", "remove"])
        .optional()
        .describe("assign (default): link the template; applyOnce: copy its settings without linking; remove: unlink the current template"),
    },
    async (args) => runRevitCommand("assign_view_template", args)
  );
}
