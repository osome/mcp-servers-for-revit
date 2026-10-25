---
name: install-revit
description: Build the NPC MCP add-in for Revit 2026 and install it into this user's Revit 2026 add-ins folder, waiting for Revit to close first. Use when the user asks to install, deploy or update the add-in on this machine, or to try a change in Revit.
---

# Install the add-in into Revit 2026

1. Run the install script **in the background**, because it may wait a long time for Revit to close:
   ```
   pwsh -NoProfile -File .claude/skills/install-revit/install.ps1
   ```
   It builds `plugin` and `commandset` in `Release R26`, waits for Revit 2026 to exit (up to 60 minutes; `-TimeoutMinutes` changes that), copies `plugin\bin\AddIn 2026 Release R26\` into `%AppData%\Autodesk\Revit\Addins\2026\`, and checks every file's hash. Pass `-SkipBuild` to install the existing build.
2. If Revit 2026 is open, tell the user to close it. Don't ask them to report back: the script carries on by itself once Revit exits.
3. When it finishes, report the result line, which names the commit installed. Exit codes: `0` installed and verified, `2` installed but some files differ, `3` timed out with Revit still open (nothing copied). A build failure throws before anything is copied.
4. Tell the user what to check in Revit after restarting, based on what changed.

Before installing a change to startup, ribbon, WPF or image code, test it outside Revit first (see CLAUDE.md). A startup exception makes Revit drop the whole add-in.
