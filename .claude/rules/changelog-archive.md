# Changelog Archive

How the app shows release notes — for a pending update, for the running
version, and for the whole history.

## The rule

**Never call the GitHub API from a user's machine.** Unauthenticated calls are
capped at 60/hour *per IP* (shared by everyone behind one NAT), and a large
part of the user base cannot reach github.com reliably at all. The app reads a
static mirror on the same CDN the updater already uses.

## The mirror is not hand-maintained content

Consistency with GitHub is structural, not a convention someone has to keep:

- Each version's `README.md` **is** the `CHANGELOG_{version}.md` artifact that
  `_release.yml` also hands GitHub as the release `body_path` — one file, one
  job, two destinations.
- `index.json` is **rebuilt in full** from the releases API on every release,
  so a run that failed to publish is repaired by the next one instead of
  leaving a hole.
- Bodies for versions released before the mirror existed are **backfilled**
  from the release bodies themselves; a version already on OSS is never
  overwritten, because that release's own run is the authority for its file.

Every UI surface still offers "read it on GitHub", so the authoritative copy
is always one click away.

## Layout

```
{updateBaseUrl}/changelogs/index.json          # every release, newest first
{updateBaseUrl}/changelogs/{version}/README.md # one release's notes
```

`{updateBaseUrl}` is `IAppUpdateSource.GetBaseUrl()` — the same value the
Velopack feed uses, so `BAKABASE_UPDATE_URL` repoints the changelog archive
too. **`index.json` carries no absolute URLs** for that reason; the client
composes them.

Versions in paths are **bare** (`2.4.0-beta.142`), never the tag (`v…`).

## Version ordering lives on the server

The frontend has no SemVer dependency and must not gain one. `Bakabase.Infrastructures`
already carries `semver` 3.0.0 (`SemVersion`, `SemVersion.PrecedenceComparer`), which
`Bakabase.Service` sees through its project reference — so **every question of "is this
version newer" is answered in `ChangelogService`**, and the client only renders.

Two rules a hand-rolled comparator always gets wrong, both live in this repo's tags:

- `2.4.0-beta.142` &gt; `2.4.0-beta.75` — a dot-separated all-digit identifier compares
  **numerically**.
- `1.9.0-beta10` &lt; `1.9.0-beta9` — a label with no dot is **one alphanumeric
  identifier**, compared ASCII (SemVer 2.0.0 §11.4.2).

The second one is deliberate and pinned by `ChangelogRangeTests`: `v1.9.0-beta10` is a
real tag that falls outside a span anchored at beta2..beta9. Do not "fix" it with a
natural sort — that would introduce a second version semantics into a codebase where the
updater and the migration runner already use this one.

## The update span

`GET /changelog/range?from=&to=` answers "what does this one update contain" — every
release from the version the reader has (exclusive) to the one being installed
(inclusive). `ChangelogService.BuildRange` is the pure, tested half.

Non-negotiables, each locked by a test:

- **Fail closed.** An unreadable bound, a bound that is not lower, an unreachable
  archive — all return null, and the caller falls back to the single-version modal.
  A lost bound must never degrade into "no bound", which answers a *what's new*
  question with the entire ~190-release archive.
- **Echo the resolved bounds.** Client versions come from `SemVersion.ToString()` and can
  carry `+buildmetadata`; archive entries never do. The server strips it and returns what
  it actually used; the UI renders and fetches against that, never against what it sent.
- **The target is always in the list.** The index is cached an hour and published by a
  different workflow step than the update feed, so it can lag behind the version being
  offered. A missing target row is synthesized.
- **Stable targets hide pre-releases, and say how many.** A stable release's notes are
  generated from the previous *stable* release (`_release.yml` picks the previous tag
  that way), so the betas in between are already covered — nothing is lost by hiding
  them. `HiddenPrereleaseCount` is surfaced so a one-row list does not read as a broken
  filter.

Known limitation, accepted deliberately: `index.json` carries no channel field, so
`Prerelease` stands in for "can I be offered this on my channel". A semver-stable build
published only to the beta feed would be misclassified. Add a channel field to the
archive entries if that ever stops being hypothetical.

`GET /changelog/releases` is deliberately untouched by all of this: `/changelog`'s
default selection is `releases[0]` and its rows print `publishedAt`, both of which
assume the **publish-date** order the generator emits. Version order and publish order
genuinely disagree here (v2.0.5 was published two minutes after v2.1.7-beta), so re-sorting
that response would break the history page.

## Pieces

| Concern | Location |
|---|---|
| Index + body generator | `scripts/changelog/build_changelog_archive.py` |
| Publish + backfill | `.github/workflows/_release.yml` → `generate-readme-oss` |
| Fetch, cache, span selection | `src/apps/Bakabase.Service/Components/Changelog/ChangelogService.cs` |
| Endpoints | `src/apps/Bakabase.Service/Controllers/ChangelogController.cs` |
| Span rules, pinned | `src/tests/Bakabase.Tests/ChangelogRangeTests.cs` |
| Shared rail + notes pane | `src/web/src/components/Changelog/ChangelogBrowser.tsx` |
| Update-span modal body | `src/web/src/components/Changelog/ChangelogRangeView.tsx` |
| History page | `src/web/src/pages/changelog/` (`/changelog`, under `menu.system`) |
| Post-upgrade popup | `src/web/src/components/Changelog/WhatsNewGate.tsx` |

## When touching this

- A client-supplied version reaches a URL — keep it behind
  `ChangelogService.IsSupportedVersion` (locked down by `ChangelogServiceTests`).
- The CDN sends no CORS headers, so the frontend **must** go through the
  backend endpoints rather than fetching the archive directly.
- Fetch failures are normal (offline, blocked CDN, a dev build with no
  release). Every path returns null and the UI falls back to the GitHub link;
  never surface an error.
- Notes render with `remark-gfm` — the release body contains GFM tables.
