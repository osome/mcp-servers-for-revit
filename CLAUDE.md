# NPC MCP for Revit

No Pretty Colors' internal build of the upstream project [mcp-servers-for-revit](https://github.com/mcp-servers-for-revit/mcp-servers-for-revit). [NPC.md](NPC.md) says what differs from upstream and how to install and release; read it before changing anything.

This repository is a **public** fork. Commits, PRs, issues and release files are visible to anyone, so never put client names, project paths, model data, tokens or credentials in them.

## Branches and pull requests

- `npc/main` is NPC's branch and the default. It's what gets installed and released.
- `main` mirrors upstream. Never commit to it; only fast-forward it from `upstream/main`.
- Work on short branches (`fix/...`, `feature/...`) and open a PR into `npc/main`.
- **PRs go to the fork, never upstream.** On a fork, `gh pr create` and GitHub's web page default to the upstream repository, so always pass `--repo osome/mcp-servers-for-revit --base npc/main`.
- To take upstream changes: fast-forward `main` from `upstream/main`, then open a PR from `main` into `npc/main`.
- Release by tagging `npc/main` with `npc-vX.Y.Z`. Don't use `scripts/release.ps1`: it resets to `main` and makes upstream-style `v*` tags.

### What Claude may do without asking

RJ has authorized Claude to commit, merge and push as the work needs:

- Commit to and push feature branches, and open PRs into `npc/main` on the fork.
- Merge a PR into `npc/main` once it builds for Revit 2026 and the PR says what was tested in Revit. Use a merge commit (`gh pr merge --merge`), so upstream merges keep working.
- Fast-forward `main` from `upstream/main` and push it.

Never, even when asked casually (`.claude/settings.json` blocks these too):

- Force-push, or rewrite history that's already pushed.
- Push to the `upstream` remote, or open PRs or issues on the upstream repository.
- Commit straight to `main`.

Ask first before tagging a release, deleting a branch or tag, or changing repository settings.

## Keep upstream merges easy

- Keep NPC changes small. Keep upstream's assembly, namespace and folder names (`revit_mcp_plugin`, `RevitMCPPlugin.dll`).
- Prefer new files over editing upstream ones. When an upstream file has to change, change as few lines as possible, and append to it rather than rewriting it (see `.gitignore`).
- NPC documentation goes in `NPC.md`, not `README.md`.

## Revit

- NPC uses **Revit 2026 only**. Build the two projects, not the solution: the test project needs the .NET 10 SDK and a licensed Revit.
  ```
  dotnet build plugin/RevitMCPPlugin.csproj -c "Release R26"
  dotnet build commandset/RevitMCPCommandSet.csproj -c "Release R26"
  ```
  The 2020–2025 configurations stay for upstream's sake; don't delete them, and keep them compiling.
- Revit 2026 loads the add-in in its own context through `ManifestSettings` in `plugin/Manifests/NPC.MCP.Isolated.addin`. Revit 2025 and earlier reject that element, so they use `NPC.MCP.addin`. Keep the two `AddIn` blocks identical.
- The command set loads through `CommandSetLoadContext`, never `Assembly.LoadFrom`. `LoadFrom` puts it in Revit's default context, where its `IRevitCommand` stops matching the plugin's.
- **Revit locks the add-in's DLLs while it's open.** Install with the `install-revit` skill: it waits for Revit to close before copying.
- Release builds don't touch `%AppData%\Autodesk\Revit\Addins`; Debug builds copy into it.

## Before installing into Revit

A failure in `Application.OnStartup` makes Revit drop the whole add-in, and every Revit restart costs the user minutes. So:

- Test anything that doesn't need a model (WPF, images, settings files, JSON) outside Revit first. For example, load the built `RevitMCPPlugin.dll` in a small .NET 8 WPF console app and call the method through reflection.
- Don't decode WPF resources from relative pack URIs (`/RevitMCPPlugin;component/...`): there's no base URI to resolve against, so WPF treats them as file paths. Read them from the `RevitMCPPlugin.g` resources instead, as `RibbonIcons` does.
- Wrap cosmetic work (icons, tooltips) in a try/catch so it can't stop the add-in loading.
- Say plainly what was and wasn't tested in Revit.

## Security

- The listener binds to 127.0.0.1 only and checks a per-session token. Never widen the bind address, and never log raw requests: they carry the token.
- `send_code_to_revit` runs any C# it receives inside Revit. Keep it opt-in.
- Use the MCP server built from `server/`: the npm release (`mcp-server-for-revit` 1.0.0) doesn't send the token.

## Commits

Use a short imperative summary line, then a body explaining why. Add a `Co-Authored-By` line when Claude wrote the change.
