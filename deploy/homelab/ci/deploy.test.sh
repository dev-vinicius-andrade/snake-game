#!/usr/bin/env bash
# Runs deploy.sh against stand-ins for kubectl, helm and docker, and checks what it asked them to do.
# No cluster, registry or network is touched. Run: bash deploy/homelab/ci/deploy.test.sh
set -uo pipefail

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
DEPLOY="$HERE/../deploy.sh"
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
FAILED=0

mkdir "$WORK/bin"
for tool in kubectl helm docker podman; do
  cat > "$WORK/bin/$tool" <<'STUB'
#!/usr/bin/env bash
printf '%s %s\n' "$(basename "$0")" "$*" >> "$STUB_LOG"
case "$(basename "$0") $*" in
  *"jsonpath={.type}"*) printf 'kubernetes.io/dockerconfigjson' ;;
  *"jsonpath={.data"*) printf 'dGVzdA==' ;;
  "podman search"*) printf '%s\n' "$STUB_TAGS" ;;
  "docker manifest inspect"*) [ "${STUB_REGISTRY_HAS:-yes}" = "yes" ] || exit 1 ;;
esac
exit 0
STUB
  chmod +x "$WORK/bin/$tool"
done

run() { # name, env assignments... ; leaves the log in $WORK/log
  local name=$1
  shift
  : > "$WORK/log"
  env -i PATH="$WORK/bin:$PATH" HOME="$WORK" STUB_LOG="$WORK/log" STUB_TAGS="" ENV_FILE=/dev/null \
    REGISTRY=reg.test/proj "$@" bash "$DEPLOY" > "$WORK/out" 2>&1
  local code=$?
  if [ "$code" -ne 0 ]; then echo "not ok - $name: deploy.sh exited $code"; sed 's/^/    /' "$WORK/out" | tail -5; FAILED=1; return 1; fi
}

check() { # description, command...
  local description=$1
  shift
  if "$@"; then echo "ok - $description"; else echo "not ok - $description"; FAILED=1; fi
}

log_has() { grep -qE -- "$1" "$WORK/log"; }
log_lacks() { ! grep -qE -- "$1" "$WORK/log"; }

# --- in a cluster: no kube context, docker, a version
run "in-cluster run" KUBE_CONTEXT=in-cluster CONTAINER_TOOL=docker VERSION=1.2.3 && {
  check "in-cluster: kubectl is not given a context" log_lacks '^kubectl .*--context'
  check "in-cluster: helm is not given a context" log_lacks '^helm .*--kube-context'
  check "in-cluster: kubectl still runs" log_has '^kubectl .*-n home-apps'
  check "in-cluster: namespaces are looked up, not forced" log_has '^kubectl get namespace home-apps'
  check "in-cluster: helm gets the version for the labels" log_has '^helm .*appVersion=v1\.2\.3'
  check "in-cluster: images are given the version as a v tag" log_has '^docker tag reg\.test/proj/snake-ui:src-[0-9a-f]{12} reg\.test/proj/snake-ui:v1\.2\.3'
  check "in-cluster: the version tag is pushed" log_has '^docker push -q reg\.test/proj/snake-game-server:v1\.2\.3'
  check "in-cluster: images are given latest too" log_has '^docker tag reg\.test/proj/snake-ui:src-[0-9a-f]{12} reg\.test/proj/snake-ui:latest'
  check "in-cluster: latest is pushed" log_has '^docker push -q reg\.test/proj/snake-manager:latest'
  check "in-cluster: no tag without the v" log_lacks 'snake-ui:1\.2\.3'
  check "in-cluster: images come from the registry check, not podman" log_lacks '^podman'
  check "in-cluster: all three workloads are installed" bash -c "[ \$(grep -c '^helm .*upgrade --install' '$WORK/log') -eq 3 ]"
  check "in-cluster: the manager pod is told what its secrets are, so a changed secret restarts it" bash -c "grep '^helm .*upgrade --install snake-manager' '$WORK/log' | grep -qE 'podAnnotations\.secrets=[0-9a-f]{16}'"
  check "in-cluster: the other pods are not restarted by it" bash -c "! grep '^helm .*upgrade --install snake-\(game\|ui\)' '$WORK/log' | grep -q 'podAnnotations'"
}

# --- values in .env are cleaned: a space or a line ending after a key must not become part of the key
printf 'OAUTH_GITHUB_CLIENT_ID=the-id  \r\nOAUTH_GITHUB_CLIENT_SECRET = "the-secret" \t\r\n  # comment\r\n' > "$WORK/dirty.env"
run "dirty env file" KUBE_CONTEXT=in-cluster CONTAINER_TOOL=docker VERSION=1.2.3 ENV_FILE="$WORK/dirty.env" && {
  check "an env value loses the space and the line ending after it" log_has '--from-literal=oauth-github-id=the-id( |$)'
  check "a quoted value with spaces around the sign and after it is read as what is quoted" log_has '--from-literal=oauth-github-secret=the-secret( |$)'
  check "nothing of the whitespace is kept" log_lacks 'the-id {2,}|the-secret {2,}'
}

# --- a version given with the v is the same version
run "v version run" KUBE_CONTEXT=in-cluster CONTAINER_TOOL=docker VERSION=v1.2.3 && {
  check "a leading v is accepted and not doubled" log_has '^docker push -q reg\.test/proj/snake-ui:v1\.2\.3$'
  check "a leading v does not make vv" log_lacks ':vv1'
}

# --- an image that is not in the registry is built and pushed
run "build run" KUBE_CONTEXT=in-cluster CONTAINER_TOOL=docker STUB_REGISTRY_HAS=no && {
  check "builds an image the registry does not have" log_has '^docker build .*-t reg\.test/proj/snake-manager:src-'
  check "pushes what it built" log_has '^docker push -q reg\.test/proj/snake-manager:src-'
}

# --- a registry that has the image: nothing is built
run "unchanged run" KUBE_CONTEXT=in-cluster CONTAINER_TOOL=docker STUB_REGISTRY_HAS=yes && {
  check "does not build what the registry has" log_lacks '^docker build'
}

# --- from a workstation with nothing in the registry: everything is built and the next version is worked out
run "workstation first run" KUBE_CONTEXT=mine CONTAINER_TOOL=podman STUB_TAGS="" && {
  check "workstation: kubectl gets the context" log_has '^kubectl --context mine'
  check "workstation: helm gets the context" log_has '^helm --kube-context mine'
  check "workstation: a build gets a version of its own, vMAJOR.MINOR.PATCH" log_has '^podman push -q reg\.test/proj/snake-ui:v[0-9]+\.[0-9]+\.[0-9]+$'
  check "workstation: and latest" log_has '^podman push -q reg\.test/proj/snake-ui:latest$'
  check "workstation: helm gets the worked out version" log_has '^helm .*appVersion=v[0-9]+\.[0-9]+\.[0-9]+'
  check "workstation: the registry is asked which versions exist" log_has '^podman search --list-tags .*snake-ui'
}

# --- the next number comes after the highest the registry has, so a local deploy never reuses one
run "next after the registry" KUBE_CONTEXT=mine CONTAINER_TOOL=podman STUB_TAGS=$'v9.9.8
v9.9.9
v2.0.0
latest
src-abc
v1.0.0-beta' && {
  check "the next version is the patch after the highest in the registry" log_has '^podman push -q reg\.test/proj/snake-ui:v9\.9\.10$'
  check "tags that are not vX.Y.Z are ignored" log_lacks ':v2\.0\.1'
}

# --- docker cannot list the registry: the git tags are all it has, and it still versions
run "docker local run" KUBE_CONTEXT=mine CONTAINER_TOOL=docker STUB_REGISTRY_HAS=no && {
  check "docker: a build gets a version of its own" log_has '^docker push -q reg\.test/proj/snake-ui:v[0-9]+\.[0-9]+\.[0-9]+$'
  check "docker: and latest" log_has '^docker push -q reg\.test/proj/snake-ui:latest$'
}

# --- nothing changed: no new version, nothing re-tagged
run "unchanged local run" KUBE_CONTEXT=mine CONTAINER_TOOL=docker STUB_REGISTRY_HAS=yes && {
  check "unchanged: nothing is built" log_lacks '^docker build'
  check "unchanged: no new version is made" log_lacks '^docker tag'
  check "unchanged: helm is not given a version" log_lacks 'appVersion='
}

# --- a fixed TAG is a request for that tag: no version of its own
run "fixed tag run" KUBE_CONTEXT=mine CONTAINER_TOOL=docker TAG=mytag STUB_REGISTRY_HAS=no && {
  check "a fixed tag gets no worked out version" log_lacks ':v[0-9]+\.[0-9]+\.[0-9]+'
}

exit "$FAILED"
