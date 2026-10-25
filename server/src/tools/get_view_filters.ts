import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand } from "../utils/viewTools.js";

export function registerGetViewFiltersTool(server: McpServer) {
  server.tool(
    "get_view_filters",
    "List the view filters in the open Revit model. Rule-based filters come with their categories and rules ({ parameter, operator, value }, numbers in the parameter's display units); selection filters with their element count. Each gives how many templates and views use it, naming up to 5 templates (all of them when one filter is asked for).",
    {
      filter: z
        .union([z.string(), z.number()])
        .optional()
        .describe("Only this filter: name or id"),
    },
    async (args) => runRevitCommand("get_view_filters", args)
  );
}
