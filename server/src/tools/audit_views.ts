import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand, viewRef } from "../utils/viewTools.js";

export function registerAuditViewsTool(server: McpServer) {
  server.tool(
    "audit_views",
    "Audit view template use in the open Revit model: a summary (views with and without a template, and those without one that are on sheets), the matching views with their template and sheet, and for each template in use the settings it leaves to the views (not included), which is where views can drift from the standard.",
    {
      viewType: z.string().optional().describe("Only this view type, e.g. FloorPlan, Section, ThreeD"),
      withoutTemplate: z.boolean().optional().describe("Only views with no template"),
      template: viewRef.optional().describe("Only views using this template"),
      onSheetsOnly: z.boolean().optional().describe("Only views placed on sheets"),
      limit: z.number().int().optional().describe("Most views to list (default 500); the summary always counts all"),
    },
    async (args) => runRevitCommand("audit_views", args)
  );
}
