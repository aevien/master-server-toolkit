# Censor Module

This module provides generic text filtering utilities.

## Main Types

- `CensorModule` is the server module wrapper.
- `CensorshipSystem` contains word matching/filtering logic.
- `LanguageBadWords` and `WordFileFormat` describe language word lists.

## Rules

- Initialize `CensorshipSystem` before using it.
- Treat censor output as a content helper, not a security boundary.
- Keep language files generic and configurable.
- Keep dictionary entries as canonical words where possible. Masked forms such as leetspeak should
  be detected through normalization, not by adding many short variants to the word list.
- Avoid one- and two-character dictionary entries. They create high false-positive risk in chat.
- Do not hard-code game-specific moderation policy into MST core.
- Use MST logging for initialization or malformed-list diagnostics.

## Bundled Dictionaries

- `Russian.txt` contains Russian profanity and general rude language.
- `RussianHateSpeech.txt` contains optional Russian hate-speech, protected-group slur,
  and extremist marker entries.
- `American.txt` contains English profanity and general rude language.
- `AmericanHateSpeech.txt` contains optional American English hate-speech, protected-group slur,
  and extremist marker entries.

`CensorModule` processes every active file from its `languageFiles` array as one combined filter.
Keep policy-specific dictionaries separate when a project may want to enable or disable them
independently.

Each `LanguageBadWords` entry exposes a diagnostic language/category name, a `TextAsset`, its parser,
an enabled flag, default severity from `1` to `3`, and a designer-only description. Auto-detection uses
comma-separated parsing only when commas are present; otherwise it reads one rule per non-empty line.
All active entries are combined and evaluated for every checked text. Transliteration, digit
substitution and separator removal apply only to advanced normalization and should be disabled only
when exact policy matching is required.

Hate-speech dictionaries should prefer exact `word:` entries. Use `prefix:` or `contains:` only
for long, unambiguous roots after false-positive testing.

Plain exact lines are still useful for exceptional short markers that would be shortened by
normalization, such as repeated-letter extremist abbreviations.

## Dictionary Patterns

Plain dictionary lines keep the legacy behavior and are treated as exact words.

Explicit patterns can be used when a language needs controlled root matching:

- `word:value` matches one normalized token exactly.
- `prefix:value` matches one normalized token that starts with `value`.
- `contains:value` matches one normalized token that contains `value`.
- `allow:value` excludes one normalized token from explicit pattern matching.

`prefix:` and `contains:` require at least four normalized characters. Keep them conservative and
prefer obvious profanity roots over ambiguous short roots. For example, Russian `prefix:выеб` catches
forms such as `выебу`, while a short ambiguous root should stay as explicit `word:` entries or be
omitted.

Generated dictionaries may use `word:` for exact entries that are not covered by safer root rules.
When a `word:` entry is matched, public result methods return the entry value without the `word:`
prefix.

## Current Status

The censor module cleanup is complete as of 2026-06-30 and committed in `306288e0`
(`Update MST censor dictionaries`).

Completed work:

- `CensorshipSystem` now reports explicit `word:` matches without leaking the `word:` prefix.
- Russian profanity is converted to explicit dictionary patterns:
  - `word:` for exact words.
  - `prefix:` for safe profanity roots.
  - `contains:` for long, unambiguous inner roots.
- Russian hate-speech and extremist entries are separated into `RussianHateSpeech.txt`.
- American English hate-speech and extremist entries are separated into `AmericanHateSpeech.txt`.
- The bundled American dictionary was cleaned from known short/common false-positive entries.
- `RussianHateSpeech.txt` and `AmericanHateSpeech.txt` are connected in the project Master scene
  and room-server prefab.
- The repository no longer ignores new `Resources/` files, so future dictionary resources can be
  versioned normally.

Verification performed:

- Dictionary loading and matching behavior was checked with local parsing scripts.
- Expected-hit and expected-clear samples were tested for Russian and American hate-speech files.
- Manual in-game chat checks passed for direct words, generated roots, and distorted words with
  inserted separators.
- `git diff --check` passed for the committed censor changes.

Unity compilation, Unity build, and platform builds were not run for this cleanup. The project
owner performs those checks manually.

No more planned censor-module work is pending. Reopen this area only for a new feature request,
a confirmed false positive, a confirmed missed violation, or a project policy change.
