// FitLife production on Azure Container Instances (docs/CSHARP_SETUP_TODO.md section 9a, issues #10 and #23).
// One container group: Caddy (HTTPS) -> Nginx -> the services and the member portal, with MongoDB, RabbitMQ
// and Vault next to them. Loki and Grafana join with #6, the other services and UIs as they are released.
// Deploy: see deploy/azure/README.md. Values for production are in main.bicepparam.
targetScope = 'resourceGroup'

metadata description = 'FitLife production: one container group with Caddy, Nginx, the services and UIs, MongoDB, RabbitMQ and Vault.'

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

@description('A service of the group.')
type service = {
  @description('Its bounded context. Nginx routes /api/<context>/ to it.')
  context: 'booking' | 'membership' | 'identity'

  @description('Its Docker Hub repository, without a tag, e.g. albertarnason/bookingservice.')
  image: string

  @description('Its released version, X.Y.Z (section 9). Never latest.')
  @minLength(5)
  imageTag: string
}

@description('The services in the container group besides NotificationService, one container each. A further service is one more entry (and its port and settings in modules/containers.bicep).')
param services service[]

@description('Whether the member portal is in the container group. False until it has a release (no image to pull yet); then Nginx answers 502 on its routes.')
param includeMemberPortal bool

@description('Docker Hub repository of the member portal (Blazor UI), without a tag.')
param uiImage string

@description('Released version of the member portal image, X.Y.Z. Never latest. Unused while includeMemberPortal is false.')
@minLength(5)
param uiImageTag string

@description('Whether NotificationService is in the container group. False until it has a release (no image to pull yet).')
param includeNotificationService bool

@description('Docker Hub repository of NotificationService, without a tag.')
param notificationImage string

@description('Released version of the NotificationService image, X.Y.Z. Never latest. Unused while includeNotificationService is false.')
@minLength(5)
param notificationImageTag string

@description('Health endpoint of the member portal, for its liveness and readiness probes (section 7).')
param uiHealthPath string = '/health'

@description('Nginx image. The same version as the gateway in deploy/compose/services.yml.')
param nginxImage string = 'nginx:1.30.7-alpine'

@description('Caddy image, the HTTPS sidecar in front of Nginx.')
param caddyImage string = 'caddy:2.11.9-alpine'

@description('MongoDB image. The same version as deploy/compose/infrastructure.yml.')
param mongoImage string = 'mongo:8.0.38'

@description('RabbitMQ image. The same version as deploy/compose/infrastructure.yml.')
param rabbitmqImage string = 'rabbitmq:4.4.1-management'

@description('Vault image (dev mode). The same version as deploy/compose/infrastructure.yml.')
param vaultImage string = 'hashicorp/vault:2.2.2'

@description('MongoDB admin user name.')
param mongoRootUsername string = 'root'

@description('RabbitMQ user that the services connect as.')
param rabbitmqUsername string = 'fitlife'

// Secrets: the deploy workflow passes them from GitHub Actions secrets (main.bicepparam), and the
// containers get them as secureValue, which the portal doesn't show. The names match deploy/compose/.env.

@description('MongoDB admin password. Used only by the init script that creates the service users, and for debugging.')
@secure()
@minLength(1)
param mongoRootPassword string

@description('Password of the MongoDB user booking-svc, which can reach only the booking database.')
@secure()
@minLength(1)
param bookingDbPassword string

@description('Password of the MongoDB user membership-svc, which can reach only the membership database.')
@secure()
@minLength(1)
param membershipDbPassword string

@description('Password of the MongoDB user identity-svc, which can reach only the identity database.')
@secure()
@minLength(1)
param identityDbPassword string

@description('Password of the MongoDB user notification-svc, which can reach only the notification database.')
@secure()
@minLength(1)
param notificationDbPassword string

@description('RabbitMQ password of rabbitmqUsername.')
@secure()
@minLength(1)
param rabbitmqPassword string

@description('Root token of the dev-mode Vault. The services read their secrets from Vault with it (#17).')
@secure()
@minLength(1)
param vaultRootToken string

@description('Key that signs and checks the JWTs (HMAC-SHA256), at least 32 characters. Seeded into Vault.')
@secure()
@minLength(32)
param jwtSigningKey string

// The demo accounts' passwords (#237). Made-up demo data for the public URL, so not the values of deploy/compose/.env.example,
// and not in Vault: Identity gets them as settings, as in compose.

@description('Password of the demo account with the role Medlem, which the smoke test logs in with.')
@secure()
@minLength(1)
param demoMedlemPassword string

@description('Password of the demo account with the role Træner.')
@secure()
@minLength(1)
param demoTraenerPassword string

@description('Password of the demo account with the role Admin.')
@secure()
@minLength(1)
param demoAdminPassword string

// Caddy keeps its Let's Encrypt certificate here, so a restart doesn't request a new one.
var caddyShareName = 'caddy-data'
// MongoDB's data directory. Loki and Grafana get their shares here with #6.
var mongoShareName = 'mongodb-data'

// Globally unique, and the same on every deploy to this resource group.
var storageAccountName = 'fitlife${uniqueString(resourceGroup().id)}'

module storage 'modules/storage.bicep' = {
  params: {
    location: location
    storageAccountName: storageAccountName
    shareNames: [
      caddyShareName
      mongoShareName
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
    mongoShareName: mongoShareName
    dockerHubUsername: dockerHubUsername
    dockerHubToken: dockerHubToken
    services: map(services, s => {
      context: s.context
      image: '${s.image}:${s.imageTag}'
    })
    includeMemberPortal: includeMemberPortal
    uiImage: '${uiImage}:${uiImageTag}'
    uiHealthPath: uiHealthPath
    includeNotificationService: includeNotificationService
    notificationImage: '${notificationImage}:${notificationImageTag}'
    nginxImage: nginxImage
    caddyImage: caddyImage
    mongoImage: mongoImage
    rabbitmqImage: rabbitmqImage
    vaultImage: vaultImage
    mongoRootUsername: mongoRootUsername
    rabbitmqUsername: rabbitmqUsername
    mongoRootPassword: mongoRootPassword
    bookingDbPassword: bookingDbPassword
    membershipDbPassword: membershipDbPassword
    identityDbPassword: identityDbPassword
    notificationDbPassword: notificationDbPassword
    rabbitmqPassword: rabbitmqPassword
    vaultRootToken: vaultRootToken
    jwtSigningKey: jwtSigningKey
    demoMedlemPassword: demoMedlemPassword
    demoTraenerPassword: demoTraenerPassword
    demoAdminPassword: demoAdminPassword
  }
}

@description('The public HTTPS address of the gateway. The hand-in links start here.')
output publicUrl string = containers.outputs.publicUrl
