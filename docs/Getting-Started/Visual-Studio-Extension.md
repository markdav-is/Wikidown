# Visual Studio Extension

The **Wikidown** Visual Studio extension surfaces your wiki's `docs/` folder directly in Solution Explorer — without it participating in the build.

## Install

1. Open **Extensions → Manage Extensions** in Visual Studio 2022 or later.
2. Search for **Wikidown** in the Marketplace tab and click **Install**.
3. Restart Visual Studio when prompted.

Alternatively, download `Wikidown.Vs.vsix` directly from the
[Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=MarkDavis.Wikidown)
and double-click to install.

## Add a Wikidown project to your solution

1. Right-click the solution node in **Solution Explorer** and choose
   **Add → New Project**.
2. Search for **Wikidown** (or filter by project type **Wikidown**).
3. Select **Wikidown Wiki** and click **Next**.
4. Give the project a name (default: `wiki`) and choose the same folder as your
   `.sln` file, then click **Create**.

A `wiki.wikidownproj` file is created alongside your solution. The `docs/`
folder appears immediately under the new project node in Solution Explorer.

## Configure the wiki root

By default the project looks for a `docs/` folder next to the `.wikidownproj`
file. Edit the file to point to a different location:

```xml
<WikidownProject>
  <WikiRoot>my-wiki</WikiRoot>   <!-- relative to the .wikidownproj file -->
</WikidownProject>
```

## Features

- Recursively shows all `.md` pages and `.order` files.
- Double-clicking a `.md` file opens it in VS's built-in markdown editor.
- The project node is **display-only** — it never appears in Build, Rebuild, or
  Clean, and adds no compile items to the solution.

## Add an existing project to a `.sln` manually

If you prefer to hand-edit the solution file, use the Wikidown project type GUID:

```
Project("{6a9c3f4b-d5e8-4f0a-b1c2-345678901bcd}") = "wiki", "wiki.wikidownproj", "{<new-guid>}"
EndProject
```

## Related

- [CLI](/CLI) — `wikidown` dotnet tool for editing from the terminal or a CI pipeline.
- [MCP Server](/MCP-Server) — stdio MCP server for AI agents (Claude, Copilot).
- [Editor](/Editor) — browser-based Blazor editor that commits directly to GitHub or Azure DevOps.
