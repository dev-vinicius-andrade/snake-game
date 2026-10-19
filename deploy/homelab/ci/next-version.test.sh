#!/usr/bin/env bash
# Checks next-version.sh. Run: bash deploy/homelab/ci/next-version.test.sh
set -uo pipefail
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
FAILED=0

expect() { # description, expected, bump, tags...
  local description=$1 expected=$2 bump=$3 actual
  shift 3
  actual=$(printf '%s\n' "$@" | bash "$HERE/next-version.sh" "$bump" 2>&1)
  if [ "$actual" = "$expected" ]; then echo "ok - $description"; else echo "not ok - $description: wanted '$expected', got '$actual'"; FAILED=1; fi
}

expect "no tags yet starts at 1.0.0" 1.0.0 patch
expect "a patch goes one up" 1.0.1 patch v1.0.0
expect "patches are compared as numbers, not text" 1.0.11 patch v1.0.0 v1.0.9 v1.0.10
expect "the latest tag counts, wherever it is in the list" 1.2.4 patch v1.2.3 v1.0.0 v1.1.9
expect "a minor goes one up and starts the patches again" 1.3.0 minor v1.0.5 v1.2.7
expect "a major starts the minors and patches again" 2.0.0 major v1.4.2
expect "tags that are not releases are ignored" 1.0.1 patch v1.0.0 v0.0.1-beta1 nightly v1.0.0-rc1 1.5.0
expect "a tag without the v is not a release" 1.0.0 patch 1.5.0 latest
expect "an empty bump is a patch" 1.0.2 "" v1.0.1

if printf 'v1.0.0\n' | bash "$HERE/next-version.sh" sideways >/dev/null 2>&1; then
  echo "not ok - an unknown bump is refused"; FAILED=1
else
  echo "ok - an unknown bump is refused"
fi
exit "$FAILED"
