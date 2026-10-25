import { z } from "zod";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { runRevitCommand, viewRefs } from "../utils/viewTools.js";

const viewRangePlane = z
  .object({
    level: z
      .string()
      .optional()
      .describe('"Associated", "Above", "Below", "Unlimited" or a level name'),
    offset: z.number().optional().describe("Offset from that level in mm"),
  })
  .optional();

export function registerSetViewTemplatePropertiesTool(server: McpServer) {
  server.tool(
    "set_view_template_properties",
    "Change properties of one or more view templates (or views) in Revit. Only the fields given are changed; each is reported as changed or failed. Use include/exclude to choose which settings a template controls (the 'Include' column of the View Template dialog); get_view_template_settings lists the setting names.",
    {
      view: viewRefs,
      name: z.string().optional().describe("New name (one view only)"),
      scale: z.number().int().optional().describe("Scale denominator, e.g. 100 for 1:100"),
      detailLevel: z.enum(["Coarse", "Medium", "Fine"]).optional(),
      discipline: z
        .enum(["Architectural", "Structural", "Mechanical", "Electrical", "Plumbing", "Coordination"])
        .optional(),
      displayStyle: z
        .enum(["Wireframe", "HLR", "Shading", "ShadingWithEdges", "Rendering", "Realistic", "FlatColors", "RealisticWithEdges"])
        .optional()
        .describe("Visual style; HLR is Hidden Line, FlatColors is Consistent Colors"),
      partsVisibility: z.enum(["ShowPartsOnly", "ShowOriginalOnly", "ShowPartsAndOriginal"]).optional(),
      phaseFilter: z.union([z.string(), z.number()]).optional().describe("Phase filter name or id"),
      viewRange: z
        .object({ top: viewRangePlane, cut: viewRangePlane, bottom: viewRangePlane, depth: viewRangePlane })
        .optional()
        .describe("Plan view range; only the planes given change"),
      parameters: z
        .record(z.union([z.string(), z.number(), z.boolean(), z.null()]))
        .optional()
        .describe("Other parameters by name, values as typed in the Properties palette (numbers in project units). Element parameters take an element name or id"),
      include: z
        .array(z.union([z.string(), z.number()]))
        .optional()
        .describe("Template settings to include (control), by name as listed in includes, e.g. \"V/G Overrides Model\", \"View Range\""),
      exclude: z
        .array(z.union([z.string(), z.number()]))
        .optional()
        .describe("Template settings to exclude, so views using the template can set them themselves"),
    },
    async (args) => runRevitCommand("set_view_template_properties", args)
  );
}
