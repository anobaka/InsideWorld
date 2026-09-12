#!/usr/bin/env python3
"""Builds the thin-client download manifest the Bakabase server reads.

Same mechanism as the mobile one, and deliberately so: the page that offers
these downloads must not compose URLs itself. If it did, the next time the
release pipeline renamed a file the page would go on showing links that 404,
and nothing would say so. CI writes what it actually published; the page shows
what CI wrote.

    https://cdn-public.anobaka.com/app/bakabase-client/manifest.json

Content: the newest release carrying client packages, with a GitHub URL and an
Aliyun CDN URL per file. Both halves of the pipeline name a file the same way
(see _deploy.yml's archive step and _release.yml's collect step), which is what
lets the CDN URL be composed from the asset name rather than guessed.

Stdlib only — runs on a bare GitHub Actions runner.
"""

import argparse
import json
import os
import re
import sys
import urllib.request

CDN_BASE = "https://cdn-public.anobaka.com/app/bakabase-client"

# Bakabase-Client-2.4.0-beta.250-win-x64-Setup.exe
#                  └ version ┘ └ rid ┘ └ shape ┘
ASSET_RE = re.compile(
    r"^Bakabase-Client-(?P<version>.+?)-(?P<rid>win-x64|osx-x64|osx-arm64)-(?P<shape>Setup|Portable)\.(?:exe|pkg|zip)$"
)


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


def client_assets_of(release: dict) -> list:
    """[(match, asset)] for every client package attached to a release."""
    found = []
    for asset in release.get("assets", []):
        match = ASSET_RE.match(asset.get("name", ""))
        if match:
            found.append((match, asset))
    return found


def build_manifest(releases: list) -> dict | None:
    candidates = []
    for release in releases:
        if release.get("draft"):
            continue
        assets = client_assets_of(release)
        if assets:
            candidates.append(
                (release.get("published_at") or release.get("created_at") or "", release, assets))

    if not candidates:
        return None

    candidates.sort(key=lambda c: c[0], reverse=True)
    published_at, release, assets = candidates[0]
    version = assets[0][0].group("version")

    return {
        "version": version,
        "publishedAt": published_at,
        "releaseUrl": release.get("html_url"),
        "files": [
            {
                "name": asset["name"],
                "platform": match.group("rid"),
                "shape": match.group("shape").lower(),
                "size": asset.get("size", 0),
                "githubUrl": asset["browser_download_url"],
                "cdnUrl": f"{CDN_BASE}/archives/{match.group('version')}/{match.group('rid')}/{asset['name']}",
            }
            for match, asset in assets
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--owner", default="anobaka")
    parser.add_argument("--repo", default="Bakabase")
    parser.add_argument("--out", default="manifest.json")
    args = parser.parse_args()

    manifest = build_manifest(fetch_releases(args.owner, args.repo))
    if manifest is None:
        print("no releases carry client packages; refusing to publish an empty manifest", file=sys.stderr)
        return 1

    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(manifest, f, ensure_ascii=False, indent=2)
        f.write("\n")

    print(f"wrote {args.out}: version {manifest['version']} with {len(manifest['files'])} file(s)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
