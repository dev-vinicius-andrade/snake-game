#!/usr/bin/env bash
# Deploys the snake game to the home lab cluster (kube context home-lab-main): the manager api, the game server and
# the site in namespace home-apps, RabbitMQ and Redis in namespace infrastructure.
#
# Run as root inside WSL, from anywhere:
#   REGISTRY=cr.homel4b.dev/<project> ./deploy/homelab/deploy.sh
#
# Options (environment):
#   REGISTRY       Required. Registry and project the images are pushed to and pulled from.
#   INGRESS_CLASS  Default traefik-external. The three hosts are exposed to the internet through it.
#   CLUSTER_ISSUER Default letsencrypt-production-andradev-io-issuer. cert-manager issues each host's certificate; empty to skip.
#   DOMAIN         Default andradev.io (public). Hosts: snake.$DOMAIN (site), snake-api.$DOMAIN (manager), snake-game.$DOMAIN (game).
#   TAG            One tag for every image (all are rebuilt). Default: per-image source tags.
#   FORCE_BUILD=1  Rebuild every image even when the registry already has its tag.
#   SKIP_BUILD=1   Build nothing; the images must already be in the registry under their tags.
#
# Only what changed is built: each image is tagged src-<hash> of the files it is built from (working tree included).
# An image whose tag is already in the registry is not rebuilt and its pods are not restarted.
#
# Before the first run (see README.md): `podman login` to the registry, and the Secret cr-homel4b (image pulls) in home-apps.
set -euo pipefail

CONTEXT=home-lab-main
NS=home-apps
INFRA_NS=infrastructure
HERE=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO=$(cd "$HERE/../.." && pwd)
SRC="$REPO/src"
CHART="$HERE/charts/snake-app"
: "${REGISTRY:?Set REGISTRY, e.g. REGISTRY=cr.homel4b.dev/<project>}"
DOMAIN=${DOMAIN:-andradev.io}
INGRESS_CLASS=${INGRESS_CLASS:-traefik-external}
CLUSTER_ISSUER=${CLUSTER_ISSUER-letsencrypt-production-andradev-io-issuer}
UI_HOST="snake.$DOMAIN"
API_HOST="snake-api.$DOMAIN"
GAME_HOST="snake-game.$DOMAIN"
MANAGER_URL="https://$API_HOST"
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
kube() { kubectl --context "$CONTEXT" -n "$NS" "$@"; }
infra() { kubectl --context "$CONTEXT" -n "$INFRA_NS" "$@"; }
helm_install() { helm --kube-context "$CONTEXT" -n "$NS" upgrade --install "$@" --wait --timeout 10m; }
step() { printf '\n==> %s\n' "$*"; }
random_secret() { head -c 24 /dev/urandom | base64 | tr -d '/+=\n' | cut -c1-32; }
secret_value() { # namespace secret key -> decoded value, empty when missing
  kubectl --context "$CONTEXT" -n "$1" get secret "$2" -o "jsonpath={.data['$3']}" 2>/dev/null | base64 -d 2>/dev/null || true
}

step "Checks (context $CONTEXT, namespaces $NS and $INFRA_NS, registry $REGISTRY)"
kubectl --context "$CONTEXT" cluster-info >/dev/null
podman login --get-login "${REGISTRY%%/*}" >/dev/null 2>&1 \
  || { echo "Not logged in to ${REGISTRY%%/*}: run podman login ${REGISTRY%%/*}"; exit 1; }
kubectl --context "$CONTEXT" apply -f "$HERE/manifests/namespaces.yaml" >/dev/null
kube get secret cr-homel4b >/dev/null 2>&1 \
  || { echo "Missing Secret cr-homel4b in $NS: see deploy/homelab/README.md"; exit 1; }

# ── Images ────────────────────────────────────────────────────────────────────
in_registry() { # name tag
  podman search --list-tags --limit 10000 --format '{{.Tag}}' "$REGISTRY/$1" 2>/dev/null | grep -qx -- "$2"
}

push() { # image: the registry sometimes answers a layer upload with 500; retry before giving up
  local attempt
  for attempt in 1 2 3; do
    podman push -q "$1" && return
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
  podman build --network host -q -t "$image" -f "$dockerfile" "$@" "$context" | tail -1
  push "$image"
}

if [ "${SKIP_BUILD:-0}" != "1" ]; then
  build_push snake-manager "$MANAGER_TAG" "$SRC/Application/Application.Manager.Api/Dockerfile" "$SRC"
  build_push snake-game-server "$GAME_TAG" "$SRC/Application/Application.Game.Server/Dockerfile" "$SRC"
  build_push snake-ui "$UI_TAG" "$SRC/Application/Application.Ui/Dockerfile" "$SRC/Application/Application.Ui" \
    --build-arg "VITE_MANAGER_URL=$MANAGER_URL"
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
    kubectl --context "$CONTEXT" apply -f "$HERE/manifests/$service.yaml"
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
kube create secret generic snake-secrets \
  --from-literal=api-key="$API_KEY" \
  --from-literal=admin-key="$ADMIN_KEY" \
  --from-literal=rabbitmq-url="amqp://$RABBIT_USER:$RABBIT_PASSWORD@rabbitmq.$INFRA_NS.svc.cluster.local:5672" \
  --from-literal=redis-password="$REDIS_PASSWORD" \
  --dry-run=client -o yaml | kube apply -f -

# ── Application (Helm, one chart for the three workloads) ─────────────────────
step "Releases"
install() { # release name values-file tag host [extra helm args...]
  local release=$1 image=$2 values=$3 tag=$4 host=$5
  shift 5
  helm_install "$release" "$CHART" -f "$HERE/values/$values.yaml" \
    --set-string "image.repository=$REGISTRY/$image" --set-string "image.tag=$tag" --set-string "ingress.host=$host" \
    --set-string "ingress.className=$INGRESS_CLASS" --set-string "ingress.clusterIssuer=$CLUSTER_ISSUER" "$@"
}
install snake-manager snake-manager manager "$MANAGER_TAG" "$API_HOST"
install snake-game snake-game-server game-server "$GAME_TAG" "$GAME_HOST" \
  --set-string "env.ServerConfiguration__Domain=$GAME_HOST" \
  --set-string "env.ServerConfiguration__InternalUrl=$GAME_INTERNAL_URL" \
  --set-string "env.AppSettings__ManagementApiConfiguration__BaseUrl=$MANAGER_INTERNAL_URL"
install snake-ui snake-ui ui "$UI_TAG" "$UI_HOST"

step "Done"
kube get pods
echo
echo "Site   https://$UI_HOST"
echo "Admin  https://$UI_HOST/admin"
echo "Admin key:  kubectl --context $CONTEXT -n $NS get secret snake-secrets -o go-template='{{index .data \"admin-key\"}}' | base64 -d"
echo "Manager https://$API_HOST   Game server https://$GAME_HOST"
