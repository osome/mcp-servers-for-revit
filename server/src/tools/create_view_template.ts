import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand, viewRef } from "../utils/viewTools.js";

export function registerCreateViewTemplateTool(server: McpServer) {
  server.tool(
    "create_view_template",
    "Create a view template in Revit, either from an existing view (like 'Create Template from Current View') or by duplicating an existing view template. Returns the new template's id and settings.",
    {
      source: viewRef.describe("View to make the template from, or view template to duplicate: element id or exact name"),
      name: z.string().describe("Name of the new view template; must not already exist"),
    },
    async (args) => runRevitCommand("create_view_template", args)
  );
}
