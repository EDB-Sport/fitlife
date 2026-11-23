// FitLife production on Azure Container Instances (docs/CSHARP_SETUP_TODO.md section 9a, issue #10).
// The thin slice: Caddy (HTTPS) -> Nginx -> one service and the member portal, in one container group.
// Deploy: see deploy/azure/README.md. Values for production are in main.bicepparam.
targetScope = 'resourceGroup'

metadata description = 'FitLife production: one container group with Caddy, Nginx, one service and the member portal.'

@description('Azure region for every resource.')
param location string = resourceGroup().location

@description('DNS label of the public IP. The public URL becomes https://<label>.<region>.azurecontainer.io.')
@minLength(3)
@maxLength(63)
param dnsNameLabel string

@description('Docker Hub user that owns the pull token.')
@minLength(1)
param dockerHubUsername string

@description('Docker Hub access token with read-only scope, used to pull every image (course rule #10).')
@secure()
@minLength(1)
param dockerHubToken string

@description('Bounded context of the one service in the thin slice. Nginx routes /api/<context>/ to it.')
@allowed([
  'booking'
  'membership'
  'identity'
])
param serviceContext string

@description('Docker Hub repository of that service, without a tag, e.g. edbsport/bookingservice.')
param serviceImage string

@description('Released version of the service image, X.Y.Z (section 9). Never latest.')
@minLength(5)
param serviceImageTag string

@description('Docker Hub repository of the member portal (Blazor UI), without a tag.')
param uiImage string

@description('Released version of the member portal image, X.Y.Z. Never latest.')
@minLength(5)
param uiImageTag string

@description('Health endpoint of the service, for its liveness and readiness probes (section 7).')
param serviceHealthPath string = '/health'

@description('Health endpoint of the member portal, for its liveness and readiness probes (section 7).')
param uiHealthPath string = '/health'

@description('Nginx image. The same version as the gateway in deploy/compose/services.yml.')
param nginxImage string = 'nginx:1.30.7-alpine'

@description('Caddy image, the HTTPS sidecar in front of Nginx.')
param caddyImage string = 'caddy:2.11.9-alpine'

// Caddy keeps its Let's Encrypt certificate here, so a restart doesn't request a new one.
// MongoDB, Loki and Grafana get their shares here as the slice grows.
var caddyShareName = 'caddy-data'

// Globally unique, and the same on every deploy to this resource group.
var storageAccountName = 'fitlife${uniqueString(resourceGroup().id)}'

module storage 'modules/storage.bicep' = {
  params: {
    location: location
    storageAccountName: storageAccountName
    shareNames: [
      caddyShareName
    ]
  }
}

module containers 'modules/containers.bicep' = {
  // The shares must exist before the group mounts them. The name is passed as a known value,
  // not as storage's output, so what-if can predict the container group (what-if-short-circuiting).
  dependsOn: [
    storage
  ]
  params: {
    location: location
    dnsNameLabel: dnsNameLabel
    storageAccountName: storageAccountName
    caddyShareName: caddyShareName
    dockerHubUsername: dockerHubUsername
    dockerHubToken: dockerHubToken
    serviceContext: serviceContext
    serviceImage: '${serviceImage}:${serviceImageTag}'
    serviceHealthPath: serviceHealthPath
    uiImage: '${uiImage}:${uiImageTag}'
    uiHealthPath: uiHealthPath
    nginxImage: nginxImage
    caddyImage: caddyImage
  }
}

@description('The public HTTPS address of the gateway. The hand-in links start here.')
output publicUrl string = containers.outputs.publicUrl

@description('Where the thin-slice service is reached through the gateway, e.g. /api/booking. Used by the smoke test.')
output serviceBasePath string = '/api/${serviceContext}'
