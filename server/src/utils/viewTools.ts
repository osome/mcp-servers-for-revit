import { z } from "zod";
import { withRevitConnection } from "./ConnectionManager.js";
import { reportsFailure } from "./toolResults.js";

/**
 * Shared pieces of the view template, view filter and object style tools.
 */

/** A view or view template, by element id or exact name. */
export const viewRef = z
  .union([z.number(), z.string()])
  .describe("View or view template: element id, or exact name");

export const viewRefs = z
  .union([viewRef, z.array(viewRef)])
  .describe("One view/template or an array of them: element ids or exact names");

const color = z
  .string()
  .describe('Color as "#RRGGBB" or "r,g,b"');

const lineOverride = z
  .object({
    color: color.nullable().optional(),
    weight: z.number().int().min(1).max(16).nullable().optional().describe("Pen weight 1-16"),
    pattern: z
      .string()
      .nullable()
      .optional()
      .describe('Line pattern name, e.g. "Dash", or "Solid"'),
  })
  .nullable()
  .optional();

const fillOverride = z
  .object({
    pattern: z
      .string()
      .nullable()
      .optional()
      .describe('Fill pattern name, e.g. "<Solid fill>" or "Solid". Drafting patterns are used first; add " (model)" for a model pattern'),
    color: color.nullable().optional(),
    visible: z.boolean().nullable().optional(),
  })
  .nullable()
  .optional();

/**
 * Graphic overrides, the same shape that get_view_template_settings reports.
 * An omitted field is left unchanged; null clears it.
 */
export const overrides = z
  .object({
    halftone: z.boolean().nullable().optional(),
    transparency: z.number().int().min(0).max(100).nullable().optional().describe("Surface transparency 0-100"),
    detailLevel: z.enum(["Coarse", "Medium", "Fine"]).nullable().optional(),
    projectionLine: lineOverride,
    cutLine: lineOverride,
    surfaceForeground: fillOverride,
    surfaceBackground: fillOverride,
    cutForeground: fillOverride,
    cutBackground: fillOverride,
  })
  .describe("Graphic overrides. Omitted fields stay as they are; null clears a field or a whole group");

/** Sends a command to Revit and wraps the reply as an MCP tool result. */
export async function runRevitCommand(command: string, params: unknown) {
  try {
    const response = await withRevitConnection(async (revitClient) => {
      return await revitClient.sendCommand(command, params);
    });

    return {
      content: [
        {
          type: "text" as const,
          text: JSON.stringify(response, null, 2),
        },
      ],
      isError: reportsFailure(response),
    };
  } catch (error) {
    return {
      content: [
        {
          type: "text" as const,
          text: `${command} failed: ${error instanceof Error ? error.message : String(error)}`,
        },
      ],
      isError: true,
    };
  }
}
