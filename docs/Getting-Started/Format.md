# Format

Wikidown's on-disk format is a subset of the Azure DevOps Wiki format.

| Concept  | On disk                               | Example            |
| -------- | ------------------------------------- | ------------------ |
| Page     | `Title-With-Hyphens.md`               | `Release-Notes.md` |
| Subpages | folder with the same base name        | `Release-Notes/`   |
| Order    | `.order` file, one base-name per line | `Install`          |
| Link     | title path                            | `/Install/Windows` |

An MVP renderer only needs standard CommonMark plus the filename↔title mapping.
