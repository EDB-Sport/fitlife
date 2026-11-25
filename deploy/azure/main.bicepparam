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

// The thin slice: one service and the member portal. One image tag per service, never latest.
param serviceContext = 'booking'
param serviceImage = 'albertarnason/bookingservice'
param serviceImageTag = readEnvironmentVariable('SERVICE_IMAGE_TAG', '0.1.0')
param uiImage = 'albertarnason/memberportal'
param uiImageTag = readEnvironmentVariable('UI_IMAGE_TAG', '0.1.0')
// No UI_IMAGE_TAG, no member portal: deploy.yml sets it only once the portal has a release (#198).
param includeMemberPortal = !empty(readEnvironmentVariable('UI_IMAGE_TAG', ''))
