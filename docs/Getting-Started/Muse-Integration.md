[Home](../Home.md) / [Getting Started](../Getting-Started.md) / Muse Integration <!-- wikidown:breadcrumb -->

# Muse Integration

Two Muse accounts can share one memory: a Wikidown wiki in a GitHub repo
that both of them read, write, commit and push to. Whatever one Muse writes
down, the other can read. The two accounts might belong to one person (work
and personal, say) or to two people working together.

## How it works

- The wiki lives in one GitHub repo, in its `docs` folder.
- Each Muse has its own SSH key, added to the repo as its own deploy key
  with write access.
- Each Muse pulls the latest changes before it reads or changes the wiki,
  and commits and pushes after every change, so both always work from the
  same version.

## Before you start

- A GitHub repo for the wiki. It can be empty; the first Muse sets it up.
- Admin access to that repo, so you can add deploy keys.

## Set up each Muse

Do these three steps in the first Muse account, then again in the second.
Each account gets its own key.

1. Paste this into a new Muse side chat:

   > Install wikidown from wikidown.org using the terminal curl command for
   > their install script. Then generate a new SSH keypair and show me the
   > public key.

2. On GitHub, open the repo's **Settings → Deploy keys → Add deploy key**.
   Paste the public key, name it after the Muse account (for example
   "Muse – Alex"), tick **Allow write access**, and save.

3. Paste this, with your repo's address and a name for this Muse in place
   of the examples:

   > Clone https://github.com/you/your-repo using that SSH key, and use
   > "Muse (Alex)" as your git author name in that repo. If the repo has no
   > docs folder yet, run wikidown init in it, then commit and push.
   > Another Muse account shares this wiki, so before you read or change
   > anything in it, pull the latest changes. From now on, whenever a
   > decision, preference, or fact worth keeping comes up while we work,
   > write it to the wiki in that repo's docs folder using wikidown, then
   > commit and push the change. If the push is rejected because the other
   > Muse pushed first, pull, keep both sets of changes, and push again.

## Check that they're connected

1. In the first Muse:

   > Add a page to the wiki called Muse Test that says hello from Alex's
   > Muse, then commit and push.

2. In the second Muse:

   > Pull the wiki and tell me what the Muse Test page says.

3. Once it answers, in either Muse:

   > Delete the Muse Test page from the wiki, then commit and push.

## Day to day

- The repo's commit history shows which Muse wrote what, by author name.
- Ask either Muse to "walk the wiki and tell me what's in it" to see
  everything the two have saved.
- Now and then, ask a Muse to run `wikidown check-links` and fix anything it
  reports.

## Disconnecting a Muse

Delete that Muse's deploy key under **Settings → Deploy keys**. It can no
longer pull or push; the other Muse and the wiki are unaffected.
