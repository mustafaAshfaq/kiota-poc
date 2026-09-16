# `openshift/` — Developer Sandbox deploy manifests

Plain Kubernetes/OpenShift YAML, applied by hand. Nothing in CI touches this
folder: GitHub Actions never pushes images to the sandbox registry and never
runs `oc apply` (see `.github/workflows/ci.yml`).

| File | Objects |
| --- | --- |
| `catalog-api.yaml` | Deployment + ClusterIP Service |
| `orders-api.yaml` | Deployment + ClusterIP Service |
| `storefront-api.yaml` | Deployment + ClusterIP Service + **Route** |

The Route on `storefront-api` is the only public surface. Catalog and Orders
are ClusterIP-only and are reached exclusively by the Storefront's Kiota
clients over in-cluster DNS — the cluster mirror of `compose.yaml`, where only
the Storefront publishes a host port.

## Placeholder to substitute

Each Deployment references its image as:

```
image-registry.openshift-image-registry.svc:5000/{username}-dev/<name>:latest
```

`{username}-dev` is the Developer Sandbox namespace (the default context you
get after `oc login`; check with `oc project -q`). Replace it before applying:

```bash
# Linux (GNU sed)
sed -i "s/{username}-dev/$(oc project -q)/g" openshift/*.yaml

# macOS (BSD sed needs an explicit, here empty, backup suffix)
sed -i '' "s/{username}-dev/$(oc project -q)/g" openshift/*.yaml
```

```powershell
# Windows PowerShell
$ns = oc project -q
Get-ChildItem openshift\*.yaml | ForEach-Object {
  (Get-Content $_) -replace '\{username\}-dev', $ns | Set-Content $_
}
```

The registry host must stay fully qualified: a short name such as
`catalog-api:latest` would be resolved against Docker Hub instead of the
in-cluster registry. `imagePullPolicy: Always` means a re-push of `:latest`
plus `oc rollout restart` is enough to ship a new build.

No `namespace:` field is set on any object — they land in your current `oc`
context.

## Deploy

```bash
oc apply -f openshift/
```

`oc apply -f <dir>` only reads `.yaml`/`.yml`/`.json`, so this README is
ignored. Then:

```bash
oc rollout status deployment/catalog-api
oc rollout status deployment/orders-api
oc rollout status deployment/storefront-api

oc get route storefront-api -o jsonpath='{.spec.host}'
```

Open/curl that host over HTTPS (the Route terminates TLS at the router and
redirects plain HTTP). There is no Route for Catalog or Orders by design; to
poke at them, port-forward instead:

```bash
oc port-forward svc/catalog-api 8081:8080
```

## Conventions locked by the spec

- Container port, Service `port` and `targetPort` are all **8080**; the apps
  listen on 8080 so they can run as an arbitrary UID.
- Liveness `GET /health`: delay 10, period 10, timeout 1, failures 3.
- Readiness `GET /ready`: delay 5, period 5, timeout 1, failures 3.
- No `startupProbe`.
- Storefront gets `Catalog__BaseUrl` / `Orders__BaseUrl` — the same keys
  `compose.yaml` sets — pointing at the sibling Services.

## Not this folder's job

- Do **not** `podman kube play` these files. They are OpenShift objects
  (`Route`) applied with `oc`; the local mesh is `compose.yaml` at the repo
  root.
- Building and pushing the three images is a separate manual step (see the
  repo root `README.md`).
