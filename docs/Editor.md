[Home](Home.md) / Editor <!-- wikidown:breadcrumb -->

# Editor

Wikidown ships a Blazor WebAssembly PWA at `https://wikidown.app/` that lets
you browse and edit any Wikidown wiki straight from the browser. Page commits
go directly from the browser to your provider's REST API; the only
server-side piece is a small endpoint at `/api/*` that completes OAuth
sign-in.

## Sign in and open a wiki

The home page (`/`) is the sign-in page. It offers:

- **Sign in with GitHub** — OAuth. The button is shown only where the
  Functions app is configured, as it is on `wikidown.app`.
- **A GitHub personal access token** — a classic token with the `repo`
  scope, or a fine-grained token with *Contents: Read and write*.
- **Try the sample wiki** — see [The sample wiki](#the-sample-wiki).
- **GitLab or Azure DevOps** — goes to `/connect`; see
  [Providers](#providers).

After you sign in, `/repos` ("Open a wiki") lists your repositories, most
recently pushed first. The page has:

- a search box that filters the list,
- a **Recent** row of the wikis you have opened on this device,
- an **owner/repository** box for a repository that is not in the list. The
  box also works without signing in, read-only, for public repositories.

Click a repository to open it. It opens on its default branch, with the
`docs` folder as the wiki. To use another branch or folder, open the wiki's
⋮ menu in the tree header and choose **Branch and folder…**. An empty folder
means the whole repository is the wiki. A repository with no pages yet shows
**Create the first page**.

The app bar shows the name of the open wiki — click it to open another — and
an account menu with **Open a wiki**, **GitLab or Azure DevOps** and
**Sign out**.

### The sample wiki

**Try the sample wiki** on the home page opens a sample wiki that is stored
only in this browser (`localStorage`). Nothing is committed anywhere, so it
is a safe place to try the editor. To start over, choose
**Reset the sample wiki** from the wiki's ⋮ menu.

## Providers

- **GitHub** — Sign in with GitHub (OAuth) or a personal access token, both
  on the home page. Page saves commit via the Contents API.
- **GitLab** — Personal Access Token with the `api` scope (Settings →
  Access tokens), entered on `/connect`. Works with gitlab.com and
  self-managed instances (set the instance URL on the connect form). Reads
  via the Repository Files/Tree APIs, commits via the Commits API; conflict
  detection uses each file's `last_commit_id`. gitlab.com serves CORS-open
  API responses, so no proxy is involved.
- **Azure DevOps** — Personal Access Token, entered on `/connect`. Commits
  via the Pushes API. (OAuth is planned but not yet wired.)

The `/connect` page is titled "GitLab or Azure DevOps" and covers only those
two providers; GitHub sign-in is on the home page.

Your sign-in and access tokens are kept in browser `localStorage`. The
Functions app never sees your repo contents and never stores your token — it
only swaps an OAuth `code` for an access token and redirects back.

## Sign-in flow (GitHub)

1. Click **Sign in with GitHub** on the home page (`/`). You don't enter an
   owner or repository first; you pick the repository after signing in.
2. Redirect to `github.com/login/oauth/authorize` for the `Wikidown` OAuth
   App.
3. Return to `/oauth/github/return.html` — a tiny static page served by ASWA.
   Its JS reads `code` + `state` from the query string and `fetch`-POSTs them
   as a JSON body to `/api/auth/github/exchange`.
4. The `GitHubExchange` Function swaps `code` + `client_secret` for an access
   token against `https://github.com/login/oauth/access_token` and returns
   `{"token":"…"}` (or `{"error":"…"}`).
5. The relay page redirects to `/#gh_token=...&gh_state=...` — the home
   page. The SPA validates the CSRF `state`, stores the sign-in in
   `localStorage`, scrubs the hash, and navigates to `/repos`.

The two-piece relay exists because Azure Functions on ASWA managed backends
treats `?code=` as a reserved function-key candidate and rejects requests
that carry it with a pre-dispatch HTTP 500 — even on `AuthorizationLevel.Anonymous`
routes. Since GitHub's OAuth redirect always appends `?code=…`, the callback
URL has to land somewhere other than `/api/*`. See
[Vibing Phase Recap](Meta/Vibing-Phase-Recap.md) for the war story.

**Use a personal access token instead** on the home page is kept as a
fallback.

GitHub OAuth Apps support a single `Authorization callback URL`, so the
`Wikidown` app is registered against `https://wikidown.app/oauth/github/return.html`.
Sign-in on the ASWA default hostname will fail with a `redirect_uri`
mismatch — use `wikidown.app` for OAuth, or a personal access token.

## Pages and folders

The tree on the left lists the wiki's pages. Its header has **New page**,
**New kanban** and a **Find a page…** filter. Opening a wiki lands on Home
if there is one.

Every page in the tree has a ⋮ menu:

- **New subpage**
- **Rename**
- **Move to…**
- **Move up** and **Move down**
- **Delete**

The open page has the same actions in its own ⋮ menu.

A "folder" is a page with subpages, or a folder with no page of its own.
Rename, move and delete take the subpages along. Delete asks first and says
how many pages will go.

Each of these changes is one commit, and it uses the same code as the
[CLI](CLI.md). So `.order`, breadcrumbs and links from other pages are
updated the way `wikidown new`, `wikidown move` and `wikidown delete` would
update them. In addition:

- Rename updates the page's `# heading` when it matched the old name, and
  link text on other pages that was the old name.
- **New page** and **New kanban** offer a checkbox, on by default, to add a
  link to the new page on its parent page.

### How a change becomes one commit

The wiki's `.md` and `.order` files are checked out into the browser's
in-memory filesystem. The change is made there with `WikiRepository`, the
same class the CLI uses, and the difference is committed in one go:

- **GitHub** — the Git Data API: blobs, a tree, a commit, then a ref update.
- **GitLab** — the Commits API, with several actions in one commit.
- **Azure DevOps** — one push.

If the branch moved in the meantime, the editor retries once on top of the
new head.

## Browse and edit

The editor is Monaco. There are three views, and the editor remembers your
choice on each device: editor only, side by side, and preview only. In side
by side, the preview follows the scroll position of the editor.

The toolbar has undo and redo, bold, italic, strikethrough, headings,
bulleted, numbered and task lists, quote, inline code, code block, table,
link, image, horizontal line, and a **Link to page…** picker that inserts a
link to another page in the wiki.

| Shortcut | Action |
|---|---|
| `Ctrl+B` | Bold |
| `Ctrl+I` | Italic |
| `Ctrl+K` | Link |
| `Ctrl+S` | Save |

Lists continue when you press Enter. Enter on an empty item ends the list.

The generated breadcrumb line is hidden while you edit and kept when you
save.

Save commits the change with an optional message. The default is
"Update" followed by the page name.

### Images

Paste an image, drop one on the editor, or pick one with the image button on
the toolbar. The image is committed straight away to `.attachments/` at the
wiki root, and a relative `![...](...)` link is inserted. The limit is
10 MB. A pasted screenshot is named after the page plus a timestamp.

## Kanban

A kanban dashboard page opens as a board of columns and cards. A
**Board / Page** toggle switches to the plain page. See
[Kanban](Getting-Started/Kanban.md#in-the-web-editor) for what the board can
do.

## Conflict handling

The editor records the file SHA (or the branch-head commit OID, or on GitLab
the `last_commit_id` of the file, depending on the provider) before you
edit. If the remote has moved when you save, the commit fails, so you do not
clobber a change made by someone else without knowing. The banner then
offers **Keep my text and save over it**: it takes the server version as the
new base and saves the text in the editor.

## Drafts

In-progress edits are kept per `(provider, owner, project, repo, branch, page)`
in `localStorage`. They survive page reloads and crashes, and clear on a
successful commit.
