# Home lab deployment

Runs the snake game in the home lab cluster. Same shape as the Empower.Documents home lab deploy: a script builds and
pushes the images, then Helm installs the workloads behind the **external** Traefik (the three hosts are public).

- **Kube context:** `home-lab-main` (the script always passes it).
- **Namespaces:** `home-apps` (manager api, game server, site) and `infrastructure` (RabbitMQ, Redis).
- **Script:** [deploy.sh](deploy.sh), run as root in WSL.

## What runs

| Workload | Image | Where | Address |
| --- | --- | --- | --- |
| Site (Vue, nginx) | `snake-ui` | `home-apps`, release `snake-ui` | https://snake.andradev.io |
| Manager api + manager hub + `/admin` api | `snake-manager` | `home-apps`, release `snake-manager` | https://snake-api.andradev.io |
| Game server (game hub, rooms in memory) | `snake-game-server` | `home-apps`, release `snake-game`, **1 replica** | https://snake-game.andradev.io |
| RabbitMQ 3.13 (2 Gi volume) | `rabbitmq:3.13-management-alpine` | `infrastructure`, StatefulSet `rabbitmq` | `rabbitmq.infrastructure.svc.cluster.local:5672` |
| Redis 7 (cache, no persistence) | `redis:7-alpine` | `infrastructure`, Deployment `redis` | `redis.infrastructure.svc.cluster.local:6379` |

The three workloads use one chart, [charts/snake-app](charts/snake-app), with a values file each in [values](values).
RabbitMQ and Redis are plain manifests in [manifests](manifests); the script applies them only when no Service of that
name exists yet in `infrastructure`, so an existing broker or cache is never replaced.

**Redis is not needed to play.** The manager api has a Redis cache wired in but nothing reads it. It is deployed because
you asked for it; delete `redis.yaml` and the two Redis lines in `values/manager.yaml` and `deploy.sh` to drop it.

## Hosts, DNS and TLS

Everything the browser talks to is exposed through the **external Traefik** (`INGRESS_CLASS`, default `traefik-external`;
check the class name of your external Traefik with `kubectl get ingressclass`): `snake.andradev.io` (site),
`snake-api.andradev.io` (manager) and `snake-game.andradev.io` (game server). RabbitMQ and Redis are not exposed.

- **DNS:** each host must resolve to the external Traefik's public address (and your router must forward 443 to it).
- **TLS:** each Ingress asks cert-manager for its own certificate with `CLUSTER_ISSUER` (default
  `letsencrypt-production-homel4b-dev-issuer`, which belongs to homel4b.dev: for andradev.io pass the name of the issuer that
  serves it, see `kubectl get clusterissuer`) into the Secret `<release>-tls`.
  Set `CLUSTER_ISSUER=` (empty) to use Traefik's default certificate instead.
- `DOMAIN=<other>` changes the base domain.

The browser talks to **three** hosts: the site, the manager (REST and its hub) and the game server (its hub, websocket).
The manager's address is baked into the site at build time (`VITE_MANAGER_URL`); the game server's address is told to the
browser by the manager at join time (`ServerConfiguration__Domain`). When an administrator deletes a room the manager calls
the game server on its in-cluster address (`ServerConfiguration__InternalUrl`), not through the internet.

**Public exposure:** anyone can reach the site, create rooms and join. `/admin` and `/admin/rooms` are protected only by the
admin key (random, 32 characters), so keep it private; nothing rate-limits the endpoints. Add a Traefik middleware (rate
limit, or an IP allow list on `snake-api.../admin`) if that worries you.

## Before the first run

1. **Registry login**, in WSL, with a robot account that can push to the project:

   ```bash
   podman login cr.homel4b.dev
   ```

2. **Image pull Secret** in `home-apps` (the namespace is created by the script's first step, or by hand):

   ```bash
   kubectl --context home-lab-main create namespace home-apps
   kubectl --context home-lab-main -n home-apps create secret docker-registry cr-homel4b \
     --docker-server=cr.homel4b.dev --docker-username='<robot account>' --docker-password='<token>'
   ```

## Deploy

```bash
REGISTRY=cr.homel4b.dev/<project> ./deploy/homelab/deploy.sh
```

What the script does:

1. Checks the cluster and the registry login, applies the two namespaces, checks the `cr-homel4b` Secret.
2. Builds and pushes the three images. Each is tagged `src-<hash>` of the files it is built from; a tag already in the
   registry is not rebuilt and its release keeps its pods. `TAG`, `FORCE_BUILD=1` and `SKIP_BUILD=1` work as in the
   Empower script.
3. Creates, once, the Secrets `rabbitmq-credentials` and `redis-credentials` in `infrastructure` (random passwords), and
   applies RabbitMQ and Redis unless they already exist.
4. Creates the Secret `snake-secrets` in `home-apps`: `api-key` and `admin-key` (random, created once, kept), plus the
   RabbitMQ URL and Redis password copied from `infrastructure`.
5. Installs the three Helm releases and prints the addresses.

## Secrets and the admin page

- **Admin key** for `https://snake.andradev.io/admin`:

  ```bash
  kubectl --context home-lab-main -n home-apps get secret snake-secrets -o go-template='{{index .data "admin-key"}}' | base64 -d
  ```

- **API key** (`api-key`) is the shared key between the game server and the manager, in both directions. It replaces the
  demo key committed in `appsettings.json`.
- Rotate a key by deleting that key from `snake-secrets` and re-running the script (or edit the Secret and restart the pods).

## Design notes and limits

- **One game server replica.** Rooms live in the pod's memory and the browser reaches a pod through the ingress, not
  through a room, so a second replica would split players and break invites. Scaling out needs one address per pod; not done.
  The Deployment uses `Recreate`, so a rollout briefly closes every room (players see the disconnected notice).
- **Manager registry is in memory** (one replica). A restart forgets rooms until the next game server heartbeat (5 s).
- **RabbitMQ** keeps its queues (durable) on a 2 Gi volume of the cluster's default storage class.
- **Pods run non-root with a read-only root filesystem**; `/tmp` is an `emptyDir`.
- **Not validated against the cluster.** The YAML parses and the script passes `bash -n`, but `helm template`,
  `kubectl apply --dry-run=server` and a real run have not been done (no cluster access from the machine that wrote this).
  Run `helm template` / `--dry-run=server` first, as the Empower README does.
