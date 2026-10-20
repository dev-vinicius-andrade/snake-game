# Builds, versions and deploys with GitHub Actions

Every commit is built and tested on GitHub. A release, a version tag such as `v1.0.5`, is built and deployed to the home lab
cluster by a runner that lives in the cluster. Nothing about the cluster or the registry is stored in GitHub.

```
commit / pull request ──► CI (GitHub's runners): server tests, site build and tests, deploy script tests

tag v1.0.5 ───────────────► Deploy (runner in the cluster): CI again, build what changed, push, helm upgrade, GitHub release
 ▲
 └── made by hand (git tag) or by the Release workflow (Actions tab > Release > Run workflow)
```

## Versions

Versions are `MAJOR.MINOR.PATCH`, from git tags `vMAJOR.MINOR.PATCH`.

- **Patch** goes up for every ordinary release: `1.0.4` to `1.0.5`. This is the default of the Release workflow.
- **Minor** for something new, **major** for a break: choose it when you run the Release workflow, or tag it yourself.
- Tags that are not of that exact form (`v0.0.1-beta1`, `nightly`) are ignored when the next number is worked out.

What a version gives you:

- A git tag and a GitHub release with generated notes.
- Each image in the registry is tagged `v1.0.5` and `latest`, next to its build tag (`snake-ui:v1.0.5`, `snake-ui:latest` and `snake-ui:src-3f558ee10481`).
  This is the same when you run `deploy.sh` by hand: without a `VERSION` it works out the next one by itself (see below).
- The Deployments carry the label `app.kubernetes.io/version: v1.0.5` (`kubectl -n home-apps get deploy -L app.kubernetes.io/version`).
- Pods run the **source** tag, so a release that did not change an image does not restart it. A release that only touches
  the site does not drop the players from the game server.

**Running `deploy.sh` by hand** needs no version: a run that builds something is a new release, and the script takes the next patch after the
highest `vX.Y.Z` among the git tags and, with podman (which can list a registry's tags), the tags of the registry, so a local deploy never reuses a number; the
first is `v1.0.0`. A run that builds nothing makes no version. `VERSION=1.0.9` (or `v1.0.9`) sets it; a fixed `TAG` does not get one. The script does not
create the git tag: to mark the commit run `git tag -a v1.0.9 -m "Release 1.0.9"` (and push it to have CI release it).

Release from `main`: run **Release** in the Actions tab (it refuses other branches). Or tag by hand; the deploy starts by itself:

```bash
git tag v1.0.5
git push origin v1.0.5
```

**Rollback:** run **Deploy** in the Actions tab with the version to go back to (for example `1.0.4`). It checks out that tag and
deploys it; images that exist are not rebuilt.

## One-time setup

### 1. The runner in the cluster

The runner is [Actions Runner Controller](https://github.com/actions/actions-runner-controller) (the scale set mode), version
`0.15.0`. It starts a pod when a deploy begins and removes it afterwards; each pod has its own Docker daemon (`dind`) to build
the images.

```bash
# the controller
helm install arc oci://ghcr.io/actions/actions-runner-controller-charts/gha-runner-scale-set-controller \
  --version 0.15.0 -n arc-systems --create-namespace

# what the runner may do in the cluster: only home-apps and infrastructure (see deploy/homelab/ci/runner/rbac.yaml)
kubectl apply -f deploy/homelab/ci/runner/rbac.yaml
```

Let the controller register runners in this repository, with a **GitHub App** (recommended) or a token:

- *GitHub App:* create an app (Settings > Developer settings > GitHub Apps) with the repository permissions **Administration:
  read and write** and **Metadata: read**, install it on `snake-game`, then

  ```bash
  kubectl -n arc-runners create secret generic github-runner-auth \
    --from-literal=github_app_id=<app id> \
    --from-literal=github_app_installation_id=<installation id> \
    --from-file=github_app_private_key=<downloaded key>.pem
  ```

- *Token instead:* a classic personal access token with the `repo` scope, then
  `kubectl -n arc-runners create secret generic github-runner-auth --from-literal=github_token=<token>`.

Then start the runner set. Its release name, `snake-deploy`, is the label the workflow asks for:

```bash
helm install snake-deploy oci://ghcr.io/actions/actions-runner-controller-charts/gha-runner-scale-set \
  --version 0.15.0 -n arc-runners -f deploy/homelab/ci/runner/runner-values.yaml
```

`kubectl -n arc-runners get autoscalingrunnerset` shows it, and the repository's Settings > Actions > Runners lists `snake-deploy`.

### 2. In the repository (Settings)

- **Actions > General > Workflow permissions:** *Read and write permissions* (the Release workflow pushes the tag and creates the release).
- **Secrets and variables > Actions > Variables** (optional, these are the defaults):
  `REGISTRY` = `cr.homel4b.dev/home-apps`, `PUBLIC_HOST` = `snake.web.games.andradev.io`.
- No registry password and no kubeconfig are needed: the deploy reads the image pull Secret `cr-homel4b` from the cluster to
  log in, and talks to the cluster as the runner's own service account.

That Secret's robot account must be allowed to **push**, not only pull, to the registry project.

### 3. Old image workflows

`docker-build-and-push-*.yml` also start on `v*` tags, and push images to another registry with the `CODESCOVERY_*` secrets.
They are independent of this setup. If you do not use that registry any more, delete them so a release does not run them too.

## How a deploy works

`deploy/homelab/deploy.sh`, the same script you can run by hand, with `KUBE_CONTEXT=in-cluster` (no kubeconfig: the pod's service
account), `CONTAINER_TOOL=docker` and `VERSION=<version>`. It builds the images whose sources changed, pushes them, tags the
deployed images with the version, and upgrades the three Helm releases. Secrets are never recreated: the script keeps the ones
that exist.

The scripts have tests that run without a cluster, in CI and by hand:

```bash
bash deploy/homelab/ci/deploy.test.sh
bash deploy/homelab/ci/next-version.test.sh
```

## When something goes wrong

- **The deploy job waits and never starts:** no runner is registered. `kubectl -n arc-systems logs deploy/arc-gha-rs-controller`
  usually says why (a wrong secret or missing app permission).
- **`forbidden` from kubectl in the deploy:** the runner's service account lacks a right. See `rbac.yaml`.
- **The push is refused:** the robot account in `cr-homel4b` can only pull.
- **A deploy was interrupted:** run it again; builds and Helm upgrades are safe to repeat.
