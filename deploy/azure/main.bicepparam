// Production values for main.bicep. No secrets here (section 9a).
// The deploy workflow (.github/workflows/deploy.yml) sets the environment variables below:
// the released image tags, and the Docker Hub pull credentials from GitHub Actions secrets.
// Without DOCKERHUB_USERNAME and DOCKERHUB_PULL_TOKEN set, the file does not compile (on purpose).
using 'main.bicep'

param location = 'northeurope'
param dnsNameLabel = 'edb-fitlife'

param dockerHubUsername = readEnvironmentVariable('DOCKERHUB_USERNAME')
param dockerHubToken = readEnvironmentVariable('DOCKERHUB_PULL_TOKEN')

// The thin slice: one service and the member portal. One image tag per service, never latest.
param serviceContext = 'booking'
param serviceImage = 'edbsport/bookingservice'
param serviceImageTag = readEnvironmentVariable('SERVICE_IMAGE_TAG', '0.1.0')
param uiImage = 'edbsport/memberportal'
param uiImageTag = readEnvironmentVariable('UI_IMAGE_TAG', '0.1.0')
