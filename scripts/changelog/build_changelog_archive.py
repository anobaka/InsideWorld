#!/usr/bin/env python3
"""Builds the changelog archive the Bakabase app reads.

The app must not call the GitHub API from user machines: unauthenticated
calls are capped at 60/hour per IP, and a large part of the user base cannot
reach github.com reliably at all. So the release pipeline publishes a static
mirror of GitHub Releases to the same CDN the updater already uses:

    {cdn}/app/bakabase/releases/changelogs/index.json          <- this script
    {cdn}/app/bakabase/releases/changelogs/{version}/README.md <- release body

The mirror is not hand-maintained content that could drift: each release's
README.md is the very `CHANGELOG_{version}.md` artifact that `_release.yml`
also passes to the GitHub release as `body_path`, and this script rebuilds
`index.json` from the releases API in full on every run, so a run that failed
to publish is repaired by the next one rather than leaving a hole.

`--bodies-dir` additionally writes every release body to disk so the workflow
can backfill versions published before the mirror existed (anything before
2.3.0). The index carries no absolute URLs: the client composes them from its
own update base URL, which keeps `BAKABASE_UPDATE_URL` mirrors working.

Stdlib only — runs on a bare GitHub Actions runner.
"""

import argparse
import json
import os
import sys
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


def fetch_releases(owner: str, repo: str) -> list:
    releases = []
    page = 1
    while True:
        url = f"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100&page={page}"
        request = urllib.request.Request(url)
        request.add_header("Accept", "application/vnd.github+json")
        token = os.environ.get("GITHUB_TOKEN")
        if token:
            request.add_header("Authorization", f"Bearer {token}")
        with urllib.request.urlopen(request) as response:
            batch = json.load(response)
        if not batch:
            return releases
        releases.extend(batch)
        page += 1


def version_of(tag: str) -> str:
    """Tags carry a `v` prefix; every CDN path uses the bare nbgv version."""
    return tag[1:] if tag.startswith("v") else tag


def build_index(releases: list) -> dict:
    entries = []
    for release in releases:
        if release.get("draft"):
            continue
        tag = release.get("tag_name")
        if not tag:
            continue
        entries.append(
            {
                "version": version_of(tag),
                "tag": tag,
                "name": release.get("name") or tag,
                "prerelease": bool(release.get("prerelease")),
                "publishedAt": release.get("published_at") or release.get("created_at"),
                "htmlUrl": release.get("html_url"),
            }
        )

    # Newest first. Publish date rather than semver: betas and stables of
    # different lines interleave, and users read this as a timeline.
    entries.sort(key=lambda e: e["publishedAt"] or "", reverse=True)

    return {
        "generatedAt": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "releases": entries,
    }


def write_bodies(releases: list, bodies_dir: Path) -> int:
    written = 0
    for release in releases:
        if release.get("draft"):
            continue
        tag = release.get("tag_name")
        body = release.get("body")
        if not tag or not body:
            continue
        target = bodies_dir / version_of(tag)
        target.mkdir(parents=True, exist_ok=True)
        (target / "README.md").write_text(body, encoding="utf-8")
        written += 1
    return written


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--owner", default="anobaka")
    parser.add_argument("--repo", default="Bakabase")
    parser.add_argument("--out", default="index.json")
    parser.add_argument(
        "--bodies-dir",
        help="When given, write every release body to {dir}/{version}/README.md for backfill.",
    )
    args = parser.parse_args()

    releases = fetch_releases(args.owner, args.repo)
    index = build_index(releases)

    if not index["releases"]:
        # Publishing an empty index would blank the in-app changelog page for
        # everyone; a transient API failure must not be able to do that.
        print("no releases found; refusing to publish an empty index", file=sys.stderr)
        return 1

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(index, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"wrote {args.out}: {len(index['releases'])} release(s)")

    if args.bodies_dir:
        count = write_bodies(releases, Path(args.bodies_dir))
        print(f"wrote {count} release body file(s) to {args.bodies_dir}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
