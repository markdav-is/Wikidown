# Wikidown Wiki Project

Surface your [Wikidown](https://github.com/markdav-is/Wikidown) / Azure DevOps Wiki `docs/` folder directly in Visual Studio's Solution Explorer — **without it participating in the build**.

## Features

- Adds a **Wikidown project type** (`.wikidownproj`) to Visual Studio 2022+.
- Recursively shows all `.md` pages and `.order` files under your configured wiki root.
- Double-clicking a `.md` file opens it in VS's built-in markdown editor.
- The project node never appears in Build / Rebuild / Clean — it is display-only.

## Getting started

1. Install the extension from the Visual Studio Marketplace.
2. In your solution, choose **Add → New Project → Wikidown Wiki**.  
   A `wiki.wikidownproj` file is created alongside your `.sln`.
3. The `docs/` folder is now visible in Solution Explorer under the **wiki** node.

## Configuring the wiki root

Edit `wiki.wikidownproj` to point to a different folder:

```xml
<WikidownProject>
  <WikiRoot>my-wiki</WikiRoot>   <!-- relative to the .wikidownproj file -->
</WikidownProject>
```

## Project type GUID

If you are adding the project to a `.sln` manually:

```
Project("{6a9c3f4b-d5e8-4f0a-b1c2-345678901bcd}") = "wiki", "wiki.wikidownproj", "{<new-guid>}"
EndProject
```

## Links

- [GitHub repository](https://github.com/markdav-is/Wikidown)
- [CLI & MCP server](https://www.nuget.org/packages/Wikidown.Cli)
- [WASM editor](https://markdav-is.github.io/Wikidown/app/)
