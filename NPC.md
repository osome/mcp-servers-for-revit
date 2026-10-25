# NPC MCP for Revit

No Pretty Colors' internal build of [mcp-servers-for-revit](https://github.com/mcp-servers-for-revit/mcp-servers-for-revit). Work happens on `npc/main`; `main` tracks upstream.

## What differs from upstream

- **Upstream PR #60 merged in** (not yet merged upstream): listener on 127.0.0.1 only, a per-session token, newline-framed messages, and fixes for timeouts, errors, IDs and units.
- **Port can be chosen** in MCP Settings > Connection (default 8080), stored per user in `%LOCALAPPDATA%\mcp-servers-for-revit\settings.json`. The MCP server reads the port in use from `connection.json` in the same folder, so it needs no setting.
- **Revit 2026: the add-in loads in its own context** (`ManifestSettings` in `NPC.MCP.Isolated.addin`), and the command set gets its own load context inside it, so the DLLs we ship can't clash with pyRevit or other add-ins (upstream #31, #47).
- **`send_code_to_revit`** skips duplicate and unreadable assemblies when compiling (upstream #26, #59), and reports clearly when no model is open.
- **NPC identity:** manifest `NPC.MCP.addin`, ClientId `CE5E1204-59A7-4022-A6B1-3475CCE74D87`, vendor `NoPrettyColors`. Buttons sit on the shared **NPC** ribbon tab, panel **MCP**.
- **Revit 2026 only** for builds and releases (`.github/workflows/npc-release.yml`). Upstream's `release.yml` is skipped on forks. The 2020–2025 build configurations and code paths stay, so upstream merges stay simple.

Assembly, namespace and folder names (`revit_mcp_plugin`, `RevitMCPPlugin.dll`) stay as upstream has them, to keep upstream merges simple.

## Adding another NPC add-in to the NPC tab

Copy `plugin/Core/NpcRibbon.cs` into the new add-in and call `NpcRibbon.GetOrCreatePanel(application, "<Panel>")` from `OnStartup`. Give each add-in its own ClientId and, for Revit 2026+, its own `ManifestSettings` block. Don't reference one add-in's DLL from another.

## Install (Revit 2026)

1. Build: `dotnet build mcp-servers-for-revit.sln -c "Release R26"` (or build `plugin` and `commandset` separately).
2. Close Revit. Remove any old `mcp-servers-for-revit.addin` or `revit-mcp.addin` from `%AppData%\Autodesk\Revit\Addins\2026\`.
3. Copy the contents of `plugin\bin\AddIn 2026 Release R26\` into `%AppData%\Autodesk\Revit\Addins\2026\`.
4. Build the MCP server: `cd server && npm ci && npm run build`. The npm release of `mcp-server-for-revit` (1.0.0) doesn't send the session token, so use this build:
   ```json
   { "mcpServers": { "npc-revit": { "command": "node", "args": ["C:\\DEV\\mcp-servers-for-revit\\server\\build\\index.js"] } } }
   ```
5. In Revit: NPC tab > MCP Settings, enable the commands you want, Save. Then NPC tab > MCP Service.

`send_code_to_revit` runs any C# it's sent inside Revit. Leave it disabled unless the user needs it.

## Releasing

Tag `npc/main` and push the tag; the NPC Release workflow builds Revit 2026 and attaches two ZIPs to a GitHub release:

```bash
git tag npc-v1.1.0
git push origin npc-v1.1.0
```

- `npc-mcp-<tag>-Revit2026.zip`: extract into `%AppData%\Autodesk\Revit\Addins\2026\`.
- `npc-mcp-<tag>-server.zip`: extract anywhere, run `npm ci --omit=dev`, and point Claude at `node <folder>\build\index.js`.

To get the ZIPs without publishing a release, run the workflow manually (Actions > NPC Release > Run workflow) and download the build artifact. GitHub only offers manual runs for workflows on the default branch, so this needs `npc/main` set as the fork's default branch.

This fork is public (GitHub keeps forks of public repos public), so releases on it are public too. Don't use `scripts/release.ps1`: it resets to `main` and makes upstream-style `v*` tags.

## Updating from upstream

```bash
git fetch upstream
git checkout npc/main
git merge upstream/main
```

When upstream merges PR #60, expect conflicts in the files it touched; take upstream's version unless an NPC change sits on top.
