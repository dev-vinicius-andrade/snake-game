# Home lab deployment

Runs the snake game in the home lab cluster. Same shape as the Empower.Documents home lab deploy: a script builds and
pushes the images, then Helm installs the workloads. The public hosts reach the cluster through a **Cloudflare Tunnel**,
so nothing has to be open on the router.

- **Kube context:** `home-lab-main` (the script always passes it).
- **Namespaces:** `home-apps` (manager api, game server, site, tunnel connector) and `infrastructure` (RabbitMQ, Redis).
- **Script:** [deploy.sh](deploy.sh), run as root in WSL.

## What runs

| Workload | Image | Where | Address |
| --- | --- | --- | --- |
| Site (Vue, nginx) | `snake-ui` | `home-apps`, release `snake-ui` | https://snake.andradev.io |
| Manager api + manager hub + `/admin` api | `snake-manager` | `home-apps`, release `snake-manager` | https://snake-api.andradev.io |
| Game server (game hub, rooms in memory) | `snake-game-server` | `home-apps`, release `snake-game`, **1 replica** | https://snake-game.andradev.io |
| Cloudflare Tunnel connector (2 replicas) | `cloudflare/cloudflared` | `home-apps`, Deployment `cloudflared` | — |
| RabbitMQ 3.13 (2 Gi volume) | `rabbitmq:3.13-management-alpine` | `infrastructure`, StatefulSet `rabbitmq` | `rabbitmq.infrastructure.svc.cluster.local:5672` |
| Redis 7 (cache, no persistence) | `redis:7-alpine` | `infrastructure`, Deployment `redis` | `redis.infrastructure.svc.cluster.local:6379` |

The three workloads use one chart, [charts/snake-app](charts/snake-app), with a values file each in [values](values).
RabbitMQ and Redis are plain manifests in [manifests](manifests); the script applies them only when no Service of that
name exists yet in `infrastructure`, so an existing broker or cache is never replaced.

**Redis is not needed to play.** The manager api has a Redis cache wired in but nothing reads it. It is deployed because
it was asked for; delete `redis.yaml` and the two Redis lines in `values/manager.yaml` and `deploy.sh` to drop it.

## Cloudflare Tunnel

The connector (`cloudflared`) opens outbound connections to Cloudflare. Cloudflare sends visitors' requests for the three
hosts down those connections to the in-cluster Services. Nothing reaches your router or pfSense (no port forward, no
HAProxy, no origin certificate), which also works when the ISP blocks inbound 80 and 443. WebSockets (the game hub and the
manager hub) work through the tunnel.

**One-time setup in the Cloudflare dashboard** (zone `andradev.io`):

1. Zero Trust > Networks > Tunnels > **Create a tunnel** > type **Cloudflared**, name it `home-lab-snake`.
2. Copy the **token** (the long string after `--token` in the install command). Don't install anything on a machine.
3. On the tunnel's **Public Hostname** tab add three hostnames. Cloudflare creates the DNS records for you:

   | Public hostname | Service type | URL |
   | --- | --- | --- |
   | `snake.andradev.io` | HTTP | `snake-ui.home-apps.svc.cluster.local:8080` |
   | `snake-api.andradev.io` | HTTP | `snake-manager.home-apps.svc.cluster.local:8080` |
   | `snake-game.andradev.io` | HTTP | `snake-game.home-apps.svc.cluster.local:8080` |

   If a record with one of those names already exists, delete it first. The wildcard `*.andradev.io` can stay: the specific
   records win.
4. Store the token in the cluster, or pass it to the script once:

   ```bash
   kubectl --context home-lab-main -n home-apps create secret generic cloudflared-token --from-literal=token='<token>'
   # or: CLOUDFLARE_TUNNEL_TOKEN='<token>' ./deploy/homelab/deploy.sh   (the script creates the Secret)
   ```

TLS is Cloudflare's: visitors get Cloudflare's certificate, and the tunnel is encrypted to the connector. Services inside the
cluster stay plain HTTP. In Zero Trust the tunnel's health shows as **Healthy** while at least one connector is connected.

**Direct path (optional).** `INGRESS=true` also creates Ingresses on the external Traefik (`INGRESS_CLASS`, default
`traefik-external`) with a certificate from `CLUSTER_ISSUER` (default `letsencrypt-production-andradev-io-issuer`). That
path needs inbound 443 to reach the cluster, so it is off by default. With `TUNNEL=false` the script skips the connector.

## Before the first run

1. **Registry login**, in WSL, with a robot account that can push to the project:

   ```bash
   podman login cr.homel4b.dev
   ```

2. **Image pull Secret** in `home-apps` (the script's first step creates the namespace if it is missing):

   ```bash
   kubectl --context home-lab-main create namespace home-apps
   kubectl --context home-lab-main -n home-apps create secret docker-registry cr-homel4b \
     --docker-server=cr.homel4b.dev --docker-username='<robot account>' --docker-password='<token>'
   ```

3. **The tunnel token** (see above).

## Deploy

```bash
REGISTRY=cr.homel4b.dev/<project> ./deploy/homelab/deploy.sh
```

What the script does:

1. Checks the cluster and the registry login, applies the two namespaces, checks the `cr-homel4b` and `cloudflared-token` Secrets.
2. Builds and pushes the three images. Each is tagged `src-<hash>` of the files it is built from; a tag already in the
   registry is not rebuilt and its release keeps its pods. `TAG`, `FORCE_BUILD=1` and `SKIP_BUILD=1` work as in the
   Empower script.
3. Creates, once, the Secrets `rabbitmq-credentials` and `redis-credentials` in `infrastructure` (random passwords), and
   applies RabbitMQ and Redis unless they already exist.
4. Creates the Secret `snake-secrets` in `home-apps`: `api-key` and `admin-key` (random, created once, kept), plus the
   RabbitMQ URL and Redis password copied from `infrastructure`.
5. Installs the three Helm releases and the tunnel connector, and prints the addresses.

## Secrets and the admin page

- **Admin key** for `https://snake.andradev.io/admin`:

  ```bash
  kubectl --context home-lab-main -n home-apps get secret snake-secrets -o go-template='{{index .data "admin-key"}}' | base64 -d
  ```

- **API key** (`api-key`) is the shared key between the game server and the manager, in both directions. It replaces the
  demo key committed in `appsettings.json`.
- Rotate a key by deleting that key from `snake-secrets` and re-running the script (or edit the Secret and restart the pods).
- **Public exposure:** anyone can reach the site, create rooms and join. `/admin` is protected only by the admin key, and
  nothing rate-limits the endpoints. Cloudflare can help without touching the cluster: a WAF rate-limiting rule, or a
  Zero Trust Access policy on `snake-api.andradev.io/admin*` (and on `snake.andradev.io/admin`) limiting it to your email.

## Design notes and limits

- **One game server replica.** Rooms live in the pod's memory, and the tunnel and ingress balance across pods, not rooms,
  so a second replica would split players and break invites. Scaling out needs one address per pod; not done. The Deployment
  uses `Recreate`, so a rollout briefly closes every room (players see the disconnected notice). The manager reaches the
  game server on its in-cluster address (`ServerConfiguration__InternalUrl`) to close a room.
- **Manager registry is in memory** (one replica). A restart forgets rooms until the next game server heartbeat (5 s).
- **RabbitMQ** keeps its queues (durable) on a 2 Gi volume of the cluster's default storage class.
- **Pods run non-root with a read-only root filesystem**; `/tmp` is an `emptyDir`.
- **Not validated against the cluster.** The YAML parses and the script passes `bash -n`, but `helm template`,
  `kubectl apply --dry-run=server` and a real run of the tunnel step have not been done. The image tag
  `cloudflare/cloudflared:2025.8.1` is unchecked; set `TUNNEL_IMAGE` if it does not exist.
- **Switching from the Ingress path.** Releases that already had Ingresses lose them on the next `helm upgrade`
  (`INGRESS=false`), and cert-manager removes their certificates.
