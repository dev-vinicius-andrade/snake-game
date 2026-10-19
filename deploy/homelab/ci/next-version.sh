#!/usr/bin/env bash
# Prints the next release version (X.Y.Z, no "v") from the existing tags, read from standard input one per line:
#   git tag -l 'v*' | next-version.sh [patch|minor|major]
# Only tags of the form vX.Y.Z count. With none, the first release is 1.0.0. patch is the default.
set -euo pipefail

bump=${1:-patch}
case "$bump" in
  patch | minor | major) ;;
  *) echo "next-version: bump must be patch, minor or major, not '$bump'" >&2; exit 1 ;;
esac

latest=$(grep -E '^v[0-9]+\.[0-9]+\.[0-9]+$' | sed 's/^v//' | sort -t. -k1,1n -k2,2n -k3,3n | tail -n 1 || true)
if [ -z "$latest" ]; then
  echo "1.0.0"
  exit 0
fi

IFS=. read -r major minor patch <<< "$latest"
case "$bump" in
  major) echo "$((major + 1)).0.0" ;;
  minor) echo "$major.$((minor + 1)).0" ;;
  patch) echo "$major.$minor.$((patch + 1))" ;;
esac
