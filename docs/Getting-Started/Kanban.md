[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Kanban <!-- wikidown:breadcrumb -->

# Kanban

Keep a simple to-do board in your wiki: one dashboard page with To Do, Doing and Done sections, and one page per item.

## Set up the board

```powershell
wikidown kanban init
```

This creates a dashboard page, `/Kanban`, with three sub-pages: To Do, Doing and Done. The dashboard has one `##` section per sub-page, and each heading links to it:

```markdown
## [To Do](Kanban/To-Do.md)
## [Doing](Kanban/Doing.md)
## [Done](Kanban/Done.md)
```

Pass `--path` to use a different name, e.g. `wikidown kanban init --path /Home-Projects`.

There is nothing special about these pages. Each item is an ordinary sub-page of To Do, Doing or Done (for example `/Kanban/To-Do/Buy-Paint`), and every page follows the same rules as any other wiki page. You can edit any of them by hand.

## Add an item

Create the item's page:

```powershell
wikidown new --path Kanban/To-Do/Buy-Paint --title "Buy paint"
```

Then list it in two places: under "To Do" on the dashboard, and on the To Do page. A one-line block can be piped in from PowerShell:

```powershell
"- [Buy paint](Kanban/To-Do/Buy-Paint.md)" | wikidown append --path Kanban --after "To Do" --stdin
"- [Buy paint](To-Do/Buy-Paint.md)" | wikidown append --path Kanban/To-Do --stdin
```

Or save the line to a file and pass `--file line.md` instead of `--stdin`; that works in any shell. `--after "To Do"` finds the heading by its visible text, even though the heading is a link.

## Move an item

When you start on an item, move its page to Doing:

```powershell
wikidown move --from Kanban/To-Do/Buy-Paint --to Kanban/Doing/Buy-Paint
```

This updates both links to point at the new page, but it leaves the lines where they were. Finish by moving the lines yourself:

1. On the dashboard, remove the line from under "To Do" and add it under "Doing":

   ```powershell
   wikidown edit --path Kanban --old "- [Buy paint](Kanban/Doing/Buy-Paint.md)" --delete
   "- [Buy paint](Kanban/Doing/Buy-Paint.md)" | wikidown append --path Kanban --after "Doing" --stdin
   ```

2. Remove the line from the To Do page and add it to the Doing page:

   ```powershell
   wikidown edit --path Kanban/To-Do --old "- [Buy paint](Doing/Buy-Paint.md)" --delete
   "- [Buy paint](Doing/Buy-Paint.md)" | wikidown append --path Kanban/Doing --stdin
   ```

Do the same again to move it to Done.

## Add a section

Want a "Waiting" column? Create its page and add a matching section to the dashboard:

```powershell
wikidown new --path Kanban/Waiting
"## [Waiting](Kanban/Waiting.md)" | wikidown append --path Kanban --stdin
```

## Check the board

```powershell
wikidown check-links
```

This reports broken links, and any item page that isn't listed on its section page — a sign that a line was missed after adding or moving an item. See the [CLI](../CLI.md) page for every command.
