# wiki/ — the player wiki

> **Keep this in sync with the game.** The wiki is what players read to learn the game, on
> GitHub and in the game itself (main menu → Wiki, pause menu → Wiki). Whenever a change affects
> what a player sees or does — a rule, a number, a control, a screen, the map, a setting — update
> the matching page (and its screenshots) **in the same change**. A wrong wiki is a bug.

`docs/` is for developers (how the code works). `wiki/` is for players (how the game works).
Never mix them: no code, file paths, class names or internals in the wiki.

## How it's shown

- The same `.md` files are rendered by GitHub and, in the game, by `ui/Wiki.cs` (loading,
  markdown → BBCode, full-text search) and `ui/WikiView.cs` (the reader: page tabs, links, Back,
  search). `WikiDialog` puts the reader in the pause menu.
- **Tab order** is the order `intro.md` links the pages in. Every page must be linked from the
  Contents table in `intro.md`.
- The pages ship with the game through the export presets' `include_filter` (`wiki/*.md`);
  images are imported textures. This file is excluded by name (it isn't a page).

## Page template

```
# Title                 short: it's the tab name in the game
One or two sentences: what this page covers.

![Caption](images/name.jpg)      optional hero image

## Section               H2 for sections, H3 for sub-sections (both are link targets)
...

## See also              always last: 2–3 related pages
- [Page](page.md)
```

## Writing rules

- Write for a new player: second person ("you"), short sentences, no jargon without a
  [Glossary](glossary.md) entry.
- **Numbers must match `tuning/tuning.tres` exactly.** Put them in tables. When a tuning value
  changes, grep the wiki for the old number.
- One `> **Tip:** …` line per tip (a single line: the game renders one line per quote).
- Link terms the first time they appear on a page: `[roll](combat.md#the-roll-dodge)`.
- Characters: only real, shipped characters (Pillar 3). Never describe placeholders.

## Markdown the in-game reader supports

Headings `#`–`####`, paragraphs, `**bold**`, `*italic*`, `` `code` ``, links, images **on their own
line**, lists (`-`, `1.`, nested by two spaces), single-line `>` quotes, tables, `---`.
Not supported: HTML, code fences (other than in this file), footnotes, images inside a sentence.

## Links

- Relative page links: `[Combat](combat.md)`, sections: `[Healing](combat.md#healing)`.
- Anchors are GitHub heading slugs: lower case, punctuation dropped, spaces → `-`
  ("The roll (dodge)" → `the-roll-dodge`). Renaming a heading breaks its links: update them.
- `make test` (`tests/WikiTests.cs`) fails on a broken page link, anchor or image.

## Screenshots

- In `wiki/images/`, `lower-case-hyphenated.jpg`, 960 × 540, JPEG quality 85.
- Rendered from the game with the movie writer (see `docs/testing.md`), **debug overlay off**
  (F3), a clean HUD, readable at the in-game reader's width.
- Every image has a caption (the alt text): it's shown under the image in the game.
- Explaining UI: annotate with numbered gold badges and a numbered table (see `hud.md`).
- Retake a screenshot when what it shows changes.

## Adding a page

1. Create `wiki/<name>.md` from the template.
2. Link it from the Contents table in `intro.md` (its position is its tab position).
3. Add it to the "See also" of 1–2 related pages.
4. `make test`, then look at it in the game (main menu → Wiki).
