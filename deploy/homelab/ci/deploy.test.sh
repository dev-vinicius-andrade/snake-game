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
  check "in-cluster: helm gets the version for the labels" log_has '^helm .*appVersion=1\.2\.3'
  check "in-cluster: images are given the version as a tag" log_has '^docker tag reg\.test/proj/snake-ui:src-[0-9a-f]{12} reg\.test/proj/snake-ui:1\.2\.3'
  check "in-cluster: the version tag is pushed" log_has '^docker push -q reg\.test/proj/snake-game-server:1\.2\.3'
  check "in-cluster: images come from the registry check, not podman" log_lacks '^podman'
  check "in-cluster: all three workloads are installed" bash -c "[ \$(grep -c '^helm .*upgrade --install' '$WORK/log') -eq 3 ]"
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

# --- from a workstation: the named context, podman, no version
run "workstation run" KUBE_CONTEXT=mine CONTAINER_TOOL=podman STUB_TAGS="" && {
  check "workstation: kubectl gets the context" log_has '^kubectl --context mine'
  check "workstation: helm gets the context" log_has '^helm --kube-context mine'
  check "workstation: without a version nothing is tagged with one" log_lacks '^podman tag'
  check "workstation: without a version helm is not given one" log_lacks 'appVersion='
}

exit "$FAILED"
