// Production values for main.bicep. No secrets here (section 9a).
// The deploy workflow (.github/workflows/deploy.yml) sets the environment variables below:
// the released image tags, and the Docker Hub pull credentials from GitHub Actions secrets.
// Without DOCKERHUB_USERNAME, DOCKERHUB_PULL_TOKEN and the secrets below set, the file does not compile (on purpose).
using 'main.bicep'

param location = 'switzerlandnorth'
param dnsNameLabel = 'edb-fitlife'

param dockerHubUsername = readEnvironmentVariable('DOCKERHUB_USERNAME')
param dockerHubToken = readEnvironmentVariable('DOCKERHUB_PULL_TOKEN')

// Secrets, from GitHub Actions secrets of the same name (deploy.yml). The names match deploy/compose/.env.
param mongoRootPassword = readEnvironmentVariable('MONGO_ROOT_PASSWORD')
param bookingDbPassword = readEnvironmentVariable('BOOKING_DB_PASSWORD')
param membershipDbPassword = readEnvironmentVariable('MEMBERSHIP_DB_PASSWORD')
param identityDbPassword = readEnvironmentVariable('IDENTITY_DB_PASSWORD')
param notificationDbPassword = readEnvironmentVariable('NOTIFICATION_DB_PASSWORD')
param rabbitmqPassword = readEnvironmentVariable('RABBITMQ_PASSWORD')
param vaultRootToken = readEnvironmentVariable('VAULT_DEV_ROOT_TOKEN')
param jwtSigningKey = readEnvironmentVariable('JWT_SIGNING_KEY')
// The demo accounts' passwords (#371): GitHub secrets of the environment production, not the values of .env.example.
param demoMedlemPassword = readEnvironmentVariable('DEMO_MEDLEM_PASSWORD')
param demoTraenerPassword = readEnvironmentVariable('DEMO_TRAENER_PASSWORD')
param demoAdminPassword = readEnvironmentVariable('DEMO_ADMIN_PASSWORD')

// The services in the group. One image tag per service, never latest: deploy.yml sets each from the newest
// <prefix>/v* release tag (tools/ci/deploy-versions.sh). A further service is one more entry. NotificationService
// (#261) has its own parameters below, as it is left out while it has no release.
param services = [
  {
    context: 'booking'
    image: 'albertarnason/bookingservice'
    imageTag: readEnvironmentVariable('BOOKING_IMAGE_TAG', '0.1.0')
  }
  {
    context: 'membership'
    image: 'albertarnason/membershipservice'
    imageTag: readEnvironmentVariable('MEMBERSHIP_IMAGE_TAG', '0.1.0')
  }
  {
    context: 'identity'
    image: 'albertarnason/identityservice'
    imageTag: readEnvironmentVariable('IDENTITY_IMAGE_TAG', '0.1.0')
  }
]

param uiImage = 'albertarnason/memberportal'
param uiImageTag = readEnvironmentVariable('UI_IMAGE_TAG', '0.1.0')
// No UI_IMAGE_TAG, no member portal: deploy.yml sets it only once the portal has a release (#198).
param includeMemberPortal = !empty(readEnvironmentVariable('UI_IMAGE_TAG', ''))
param notificationImage = 'albertarnason/notificationservice'
param notificationImageTag = readEnvironmentVariable('NOTIFICATION_IMAGE_TAG', '0.1.0')
// Likewise: no NOTIFICATION_IMAGE_TAG, no NotificationService.
param includeNotificationService = !empty(readEnvironmentVariable('NOTIFICATION_IMAGE_TAG', ''))
