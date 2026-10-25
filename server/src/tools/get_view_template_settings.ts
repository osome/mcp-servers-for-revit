import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand, viewRef } from "../utils/viewTools.js";

export function registerGetViewTemplateSettingsTool(server: McpServer) {
  server.tool(
    "get_view_template_settings",
    "Read the settings of one view template (or view) in Revit. Sections: general (scale, detail level, discipline, display style, phase filter, assigned template), includes (each setting the template can control, its value, and whether it's included), categories (hidden or overridden categories and subcategories, with their graphic overrides), filters (applied view filters in order, with visibility, enabled and overrides), viewRange (plans only, offsets in mm). For a view that uses a template, includes describes that template.",
    {
      view: viewRef,
      sections: z
        .array(z.enum(["general", "includes", "categories", "filters", "viewRange"]))
        .optional()
        .describe("Sections to return; all of them when omitted"),
      allCategories: z
        .boolean()
        .optional()
        .describe("List every category the view can show, not just hidden or overridden ones (long output)"),
    },
    async (args) => runRevitCommand("get_view_template_settings", args)
  );
}
