#!/usr/bin/env bash
# Deploys the snake game to the home lab cluster (kube context home-lab-main): the manager api, the game server and
# the site in namespace home-apps, RabbitMQ and Redis in namespace infrastructure.
#
# Run as root inside WSL, from anywhere:
#   REGISTRY=cr.homel4b.dev/<project> ./deploy/homelab/deploy.sh
#
# Options (environment):
#   REGISTRY       Required. Registry and project the images are pushed to and pulled from.
#   TUNNEL         Default true. The host reaches the cluster through a Cloudflare Tunnel (nothing open on the router):
#                  deploys cloudflared, which needs the Secret cloudflared-token (see README.md) or CLOUDFLARE_TUNNEL_TOKEN.
#   TUNNEL_IMAGE   cloudflared image. Default docker.io/cloudflare/cloudflared:2025.8.1.
#   INGRESS        Default false. true also creates Ingresses on INGRESS_CLASS (default traefik-external) with a certificate
#                  from CLUSTER_ISSUER (default letsencrypt-production-andradev-io-issuer; empty to skip), for a direct path.
#   PUBLIC_HOST    The one public host. Default snake.$DOMAIN. A name two levels or deeper under the domain needs a Cloudflare
#                  Advanced Certificate covering it (see README.md), e.g. PUBLIC_HOST=snake.web.games.andradev.io.
#   DOMAIN         Default andradev.io (public). One host, PUBLIC_HOST: / the site, /api the manager (and /api/admin),
#                  /game the game server. The site pod (nginx) routes /api and /game to the other Services.
#   TAG            One tag for every image (all are rebuilt). Default: per-image source tags.
#   FORCE_BUILD=1  Rebuild every image even when the registry already has its tag.
#   SKIP_BUILD=1   Build nothing; the images must already be in the registry under their tags.
#
# Only what changed is built: each image is tagged src-<hash> of the files it is built from (working tree included).
# An image whose tag is already in the registry is not rebuilt and its pods are not restarted.
#
# Settings can live in deploy/homelab/.env (copy .env.example; not committed) as KEY=value lines, or in the file named by
# ENV_FILE. A variable already set in the environment wins over the file.
#
# Before the first run (see README.md): `podman login` to the registry, and the Secret cr-homel4b (image pulls) in home-apps.
set -euo pipefail

HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

# Reads KEY=value lines (no code is run) and exports each key that is not already set. Quotes around a value are removed.
load_env_file() { # file
  local line key value
  [ -f "$1" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    line=${line%$'\r'}
    case "$line" in ''|'#'*) continue ;; esac
    line=${line#export }
    key=${line%%=*}
    value=${line#*=}
    [[ "$key" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || continue
    value=${value#\"}; value=${value%\"}; value=${value#\'}; value=${value%\'}
    [ -n "${!key+x}" ] || export "$key=$value"
  done < "$1"
}
load_env_file "${ENV_FILE:-$HERE/.env}"

# Kube context to deploy to. Override with KUBE_CONTEXT=<name> (see `kubectl config get-contexts`). KUBE_CONTEXT=in-cluster
# is for a pod (a CI runner): kubectl and helm then use the pod's own service account and no context at all.
CONTEXT="${KUBE_CONTEXT:-home-lab-main}"
# Builds and pushes with podman, or with docker where there is no podman (CONTAINER_TOOL overrides).
CONTAINER_TOOL=${CONTAINER_TOOL:-$(command -v podman >/dev/null 2>&1 && echo podman || echo docker)}
# The release being deployed, such as 1.4.2. Optional. Images also get it as a tag and the workloads as a label.
VERSION=${VERSION:-}
NS=home-apps
INFRA_NS=infrastructure
REPO=$(cd "$HERE/../.." && pwd)
SRC="$REPO/src"
CHART="$HERE/charts/snake-app"
: "${REGISTRY:?Set REGISTRY, e.g. REGISTRY=cr.homel4b.dev/<project>}"
DOMAIN=${DOMAIN:-andradev.io}
TUNNEL=${TUNNEL:-true}
TUNNEL_IMAGE=${TUNNEL_IMAGE:-docker.io/cloudflare/cloudflared:2025.8.1}
INGRESS=${INGRESS:-false}
INGRESS_CLASS=${INGRESS_CLASS:-traefik-external}
CLUSTER_ISSUER=${CLUSTER_ISSUER-letsencrypt-production-andradev-io-issuer}
UI_HOST=${PUBLIC_HOST:-snake.$DOMAIN}
MANAGER_URL="/api"
MANAGER_INTERNAL_URL="http://snake-manager.$NS.svc.cluster.local:8080"
GAME_INTERNAL_URL="http://snake-game.$NS.svc.cluster.local:8080"

# Files each image is built from (repository-relative pathspecs).
DOTNET_COMMON=(src/SnakeGame.sln src/Library src/Domain)
MANAGER_SOURCES=("${DOTNET_COMMON[@]}" src/Application/Application.Manager.Api)
GAME_SOURCES=("${DOTNET_COMMON[@]}" src/Application/Application.Game.Server)
UI_SOURCES=(src/Application/Application.Ui ':(exclude)src/Application/Application.Ui/dist' ':(exclude)src/Application/Application.Ui/node_modules')

# src-<12 hex>: hash of every listed file's path and working-tree content (tracked and untracked, not .gitignored),
# plus an optional salt (a build argument that changes the image).
source_tag() { # salt pathspecs...
  local salt=$1 files
  shift
  files=$(cd "$REPO" && git ls-files --cached --others --exclude-standard -- "$@" | sort -u \
    | while IFS= read -r file; do [ -f "$file" ] && printf '%s\n' "$file"; done)
  printf 'src-%s' "$( { (cd "$REPO" && paste <(printf '%s\n' "$files") <(printf '%s\n' "$files" | git hash-object --stdin-paths)); printf '%s\n' "$salt"; } \
    | sha256sum | cut -c1-12)"
}
image_tag() { if [ -n "${TAG:-}" ]; then printf '%s' "$TAG"; else source_tag "$@"; fi; }
MANAGER_TAG=$(image_tag "" "${MANAGER_SOURCES[@]}")
GAME_TAG=$(image_tag "" "${GAME_SOURCES[@]}")
UI_TAG=$(image_tag "$MANAGER_URL" "${UI_SOURCES[@]}")

# Every kubectl / helm call names the context: the shared kubeconfig's current context can point anywhere.
k() { if [ "$CONTEXT" = in-cluster ]; then kubectl "$@"; else kubectl --context "$CONTEXT" "$@"; fi; }
kube() { k -n "$NS" "$@"; }
infra() { k -n "$INFRA_NS" "$@"; }
helm_install() {
  if [ "$CONTEXT" = in-cluster ]; then helm -n "$NS" upgrade --install "$@" --wait --timeout 10m
  else helm --kube-context "$CONTEXT" -n "$NS" upgrade --install "$@" --wait --timeout 10m; fi
}
step() { printf '\n==> %s\n' "$*"; }
random_secret() { head -c 24 /dev/urandom | base64 | tr -d '/+=\n' | cut -c1-32; }
secret_value() { # namespace secret key -> decoded value, empty when missing
  k -n "$1" get secret "$2" -o "jsonpath={.data['$3']}" 2>/dev/null | base64 -d 2>/dev/null || true
}

step "Checks (context $CONTEXT, namespaces $NS and $INFRA_NS, registry $REGISTRY)"
if [ "$CONTEXT" != in-cluster ]; then
  kubectl config get-contexts "$CONTEXT" >/dev/null 2>&1 || {
    echo "error: kube context '$CONTEXT' does not exist. Set KUBE_CONTEXT to one of:" >&2
    kubectl config get-contexts -o name >&2
    exit 1
  }
  k cluster-info >/dev/null
fi
if [ "$CONTAINER_TOOL" = podman ]; then
  # Credentials are stored under the key given to `podman login`: the host, or host/project (robot accounts).
  podman login --get-login "${REGISTRY%%/*}" >/dev/null 2>&1 \
    || podman login --get-login "$REGISTRY" >/dev/null 2>&1 \
    || { echo "Not logged in to ${REGISTRY%%/*} or $REGISTRY: run podman login $REGISTRY"; exit 1; }
fi
# The namespaces are made once. A pod that only has rights inside them can look them up but not create them.
k get namespace "$NS" >/dev/null 2>&1 && k get namespace "$INFRA_NS" >/dev/null 2>&1 \
  || k apply -f "$HERE/manifests/namespaces.yaml" >/dev/null
kube get secret cr-homel4b >/dev/null 2>&1 \
  || { echo "Missing Secret cr-homel4b in $NS: see deploy/homelab/README.md"; exit 1; }
# Pods can only pull with a docker-registry Secret; any other type leaves them in ImagePullBackOff.
[ "$(kube get secret cr-homel4b -o jsonpath='{.type}')" = "kubernetes.io/dockerconfigjson" ] \
  || { echo "Secret cr-homel4b in $NS is not a docker-registry Secret: recreate it as in deploy/homelab/README.md"; exit 1; }
if [ "$TUNNEL" = "true" ]; then
  if [ -n "${CLOUDFLARE_TUNNEL_TOKEN:-}" ]; then
    kube create secret generic cloudflared-token --from-literal=token="$CLOUDFLARE_TUNNEL_TOKEN" --dry-run=client -o yaml | kube apply -f - >/dev/null
  fi
  kube get secret cloudflared-token >/dev/null 2>&1 \
    || { echo "Missing Secret cloudflared-token in $NS (key: token): see the Tunnel section of deploy/homelab/README.md"; exit 1; }
fi

# ── Images ────────────────────────────────────────────────────────────────────
in_registry() { # name tag
  if [ "$CONTAINER_TOOL" = podman ]; then
    podman search --list-tags --limit 10000 --format '{{.Tag}}' "$REGISTRY/$1" 2>/dev/null | grep -qx -- "$2"
  else
    "$CONTAINER_TOOL" manifest inspect "$REGISTRY/$1:$2" >/dev/null 2>&1
  fi
}

push() { # image: the registry sometimes answers a layer upload with 500; retry before giving up
  local attempt
  for attempt in 1 2 3; do
    "$CONTAINER_TOOL" push -q "$1" && return
    [ "$attempt" -lt 3 ] && { echo "push failed (attempt $attempt), retrying in 10 s" >&2; sleep 10; }
  done
  return 1
}

build_push() { # name tag dockerfile context [podman build args...]
  local image="$REGISTRY/$1:$2" name=$1 tag=$2 dockerfile=$3 context=$4
  shift 4
  step "Image $image"
  # A fixed TAG is a request to rebuild under that tag; a source tag already pushed means nothing changed.
  if [ -z "${TAG:-}" ] && [ "${FORCE_BUILD:-0}" != "1" ] && in_registry "$name" "$tag"; then
    echo "unchanged, already in the registry: not rebuilt"
    return
  fi
  "$CONTAINER_TOOL" build --network host -q -t "$image" -f "$dockerfile" "$@" "$context" | tail -1
  push "$image"
}

# The image that is deployed also gets the release version as a tag, so the registry reads as a list of releases. The workloads
# keep running the source tag: a release that did not change an image does not restart it.
tag_version() { # name tag
  [ -n "$VERSION" ] || return 0
  local source="$REGISTRY/$1:$2" release="$REGISTRY/$1:$VERSION"
  "$CONTAINER_TOOL" image inspect "$source" >/dev/null 2>&1 || "$CONTAINER_TOOL" pull -q "$source" >/dev/null
  "$CONTAINER_TOOL" tag "$source" "$release"
  push "$release"
}

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  build_push snake-manager "$MANAGER_TAG" "$SRC/Application/Application.Manager.Api/Dockerfile" "$SRC"
  build_push snake-game-server "$GAME_TAG" "$SRC/Application/Application.Game.Server/Dockerfile" "$SRC"
  build_push snake-ui "$UI_TAG" "$SRC/Application/Application.Ui/Dockerfile" "$SRC/Application/Application.Ui" \
    --build-arg "VITE_MANAGER_URL=$MANAGER_URL"
  tag_version snake-manager "$MANAGER_TAG"
  tag_version snake-game-server "$GAME_TAG"
  tag_version snake-ui "$UI_TAG"
fi

# ── Infrastructure: RabbitMQ and Redis (an existing Service of that name is left alone) ─────────────────────────────
step "Infrastructure"
[ -n "$(secret_value "$INFRA_NS" rabbitmq-credentials password)" ] \
  || infra create secret generic rabbitmq-credentials --from-literal=username=snake --from-literal=password="$(random_secret)"
[ -n "$(secret_value "$INFRA_NS" redis-credentials password)" ] \
  || infra create secret generic redis-credentials --from-literal=password="$(random_secret)"
for service in rabbitmq redis; do
  if infra get service "$service" >/dev/null 2>&1; then
    echo "$service already exists in $INFRA_NS: left as it is"
  else
    k apply -f "$HERE/manifests/$service.yaml"
  fi
done
infra rollout status statefulset/rabbitmq --timeout 5m
infra rollout status deployment/redis --timeout 5m

# ── Secrets of the apps ───────────────────────────────────────────────────────
# api-key and admin-key are created once and kept. The broker and cache credentials are copied from infrastructure
# (an app's pods can only read Secrets of their own namespace).
step "Secrets"
API_KEY=$(secret_value "$NS" snake-secrets api-key); API_KEY=${API_KEY:-$(random_secret)}
ADMIN_KEY=$(secret_value "$NS" snake-secrets admin-key); ADMIN_KEY=${ADMIN_KEY:-$(random_secret)}
RABBIT_USER=$(secret_value "$INFRA_NS" rabbitmq-credentials username)
RABBIT_PASSWORD=$(secret_value "$INFRA_NS" rabbitmq-credentials password)
REDIS_PASSWORD=$(secret_value "$INFRA_NS" redis-credentials password)
# Keys for signing in with a social account (docs/ACCOUNTS.md): from .env, else what the Secret already has. A provider with no keys is not offered.
OAUTH_ARGS=()
for provider in github discord google; do
  upper=$(printf '%s' "$provider" | tr '[:lower:]' '[:upper:]')
  for part in id secret; do
    variable="OAUTH_${upper}_CLIENT_$(printf '%s' "$part" | tr '[:lower:]' '[:upper:]')"
    value=${!variable:-$(secret_value "$NS" snake-secrets "oauth-$provider-$part")}
    [ -z "$value" ] || OAUTH_ARGS+=("--from-literal=oauth-$provider-$part=$value")
  done
done
# Steam: only the Web API key, which asks Steam for the player's name.
steam_key=${OAUTH_STEAM_API_KEY:-$(secret_value "$NS" snake-secrets oauth-steam-secret)}
[ -z "$steam_key" ] || OAUTH_ARGS+=("--from-literal=oauth-steam-secret=$steam_key")
# The secret that keys the hash of social account ids: made once and kept. Losing it makes every social account look new.
LOGIN_PEPPER=$(secret_value "$NS" snake-secrets login-pepper); LOGIN_PEPPER=${LOGIN_PEPPER:-$(random_secret)}
kube create secret generic snake-secrets \
  --from-literal=api-key="$API_KEY" \
  --from-literal=login-pepper="$LOGIN_PEPPER" \
  --from-literal=admin-key="$ADMIN_KEY" \
  --from-literal=rabbitmq-url="amqp://$RABBIT_USER:$RABBIT_PASSWORD@rabbitmq.$INFRA_NS.svc.cluster.local:5672" \
  --from-literal=redis-password="$REDIS_PASSWORD" \
  "${OAUTH_ARGS[@]}" \
  --dry-run=client -o yaml | kube apply -f -

# ── Application (Helm, one chart for the three workloads) ─────────────────────
step "Releases"
install() { # release name values-file tag host [extra helm args...]
  local release=$1 image=$2 values=$3 tag=$4 host=$5
  shift 5
  helm_install "$release" "$CHART" -f "$HERE/values/$values.yaml" \
    --set-string "image.repository=$REGISTRY/$image" --set-string "image.tag=$tag" --set-string "ingress.host=$host" --set "ingress.enabled=$INGRESS" \
    --set-string "ingress.className=$INGRESS_CLASS" --set-string "ingress.clusterIssuer=$CLUSTER_ISSUER" \
    ${VERSION:+--set-string "appVersion=$VERSION"} "$@"
}
# Only the site is reached from outside; it passes /api and /game on, so the other two get no ingress of their own.
install snake-manager snake-manager manager "$MANAGER_TAG" "" --set "ingress.enabled=false" \
  --set-string "env.OAuth__PublicApiUrl=https://$UI_HOST/api" \
  --set-string "env.OAuth__PublicUiUrl=https://$UI_HOST"
install snake-game snake-game-server game-server "$GAME_TAG" "" --set "ingress.enabled=false" \
  --set-string "env.ServerConfiguration__Domain=$UI_HOST" \
  --set-string "env.ServerConfiguration__InternalUrl=$GAME_INTERNAL_URL" \
  --set-string "env.AppSettings__ManagementApiConfiguration__BaseUrl=$MANAGER_INTERNAL_URL"
install snake-ui snake-ui ui "$UI_TAG" "$UI_HOST"

if [ "$TUNNEL" = "true" ]; then
  step "Cloudflare Tunnel"
  export TUNNEL_IMAGE
  envsubst '${TUNNEL_IMAGE}' < "$HERE/manifests/cloudflared.yaml" | kube apply -f -
  kube rollout status deployment/cloudflared --timeout 5m
fi

step "Done"
kube get pods
echo
echo "Site   https://$UI_HOST"
echo "Admin  https://$UI_HOST/admin"
[ -z "$VERSION" ] || echo "Version $VERSION"
echo "Admin key:  kubectl ${CONTEXT:+--context $CONTEXT }-n $NS get secret snake-secrets -o go-template='{{index .data \"admin-key\"}}' | base64 -d"
echo "Manager https://$UI_HOST/api   Game server https://$UI_HOST/game"
