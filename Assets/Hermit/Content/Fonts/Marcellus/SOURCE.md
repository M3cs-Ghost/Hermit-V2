# Marcellus — source and license

**Font:** Marcellus (Regular, weight 400 — the family's only weight; there is
no separate bold/semibold cut).
**Designer:** Astigmatic (Brian J. Bonislawsky).
**License:** SIL Open Font License 1.1 (OFL) — see `OFL.txt` in this folder.
Freely usable, embeddable, and redistributable with the game; the only OFL
restriction relevant here is not selling the font file by itself, which
does not apply to embedding it in Hermit.
**Source:** Google Fonts' font repository —
`https://github.com/google/fonts/tree/main/ofl/marcellus`
(file fetched directly: `ofl/marcellus/Marcellus-Regular.ttf`,
`ofl/marcellus/OFL.txt`).
**Fetched:** 2026-09-12, for C9.1b (Hub Recomposition and Typographic
Identity — see `Docs/C9_1_START_SCREEN_HUB_IMPLEMENTATION.md`).

## Why Marcellus

C9.1b's brief asked for a monumental, refined, editorial display serif for
the Hub's destination signage and (evaluated case-by-case) the Start
Screen's PRESS START prompt — distinct from the plain LiberationSans SDF
UI font already used everywhere else in this project's interface chrome.
Marcellus reads as inscriptional/architectural without being medieval or
sci-fi, matching the Hub's monumental-plaza illustration.

## Generated TMP asset

`TMP/Marcellus-Regular SDF.asset` is a dynamic-atlas SDF `TMP_FontAsset`
generated from `Marcellus-Regular.ttf`, created with the same procedure
Unity's own "Assets > Create > TextMeshPro > Font Asset > SDF" context menu
action uses (`TMP_FontAsset.CreateFontAsset` + `FontEngine`), via
`Assets/Hermit/Editor/MarcellusFontAssetImporter.cs`. Dynamic atlas mode
means glyphs are rasterized into the atlas texture on first use at
runtime — no fixed character set was pre-baked.
