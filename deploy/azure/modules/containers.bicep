// The container group: every container shares one localhost, and only Caddy has a public port (section 9a).
//
//   internet --443--> caddy --:80--> nginx --:5001..--> service
//                                          --:5101----> member portal

@description('Azure region.')
param location string

@description('DNS label of the public IP: <label>.<region>.azurecontainer.io.')
param dnsNameLabel string

@description('Storage account that holds the file shares.')
param storageAccountName string

@description('File share for Caddy\'s certificates.')
param caddyShareName string

@description('Docker Hub user that owns the pull token.')
param dockerHubUsername string

@description('Docker Hub access token with read-only scope.')
@secure()
param dockerHubToken string

@description('Bounded context of the thin-slice service: booking, membership or identity.')
param serviceContext string

@description('Service image with its tag, e.g. edbsport/bookingservice:0.1.0.')
param serviceImage string

@description('Health endpoint of the service.')
param serviceHealthPath string

@description('Member portal image with its tag.')
param uiImage string

@description('Health endpoint of the member portal.')
param uiHealthPath string

@description('Nginx image with its tag.')
param nginxImage string

@description('Caddy image with its tag.')
param caddyImage string

// Containers in a group share localhost, so each one listens on its own port.
// Compose reaches the same apps as http://<name>:8080 instead; the Nginx template takes either (course rule #13).
var gatewayPort = 80 // `listen 80` in deploy/compose/nginx/default.conf.template
var caddyHttpPort = 8081 // Caddy's plain-HTTP listener, moved off 80 because Nginx has it. Not public.
var servicePorts = {
  booking: 5001
  membership: 5002
  identity: 5003
}
var uiPort = 5101

var fqdn = '${dnsNameLabel}.${location}.azurecontainer.io'

// Small requests: the region's limit is per container group, and the stack grows (section 9a).
var smallContainer = {
  requests: {
    cpu: json('0.25')
    memoryInGB: json('0.25')
  }
}
var dotnetContainer = {
  requests: {
    cpu: json('0.25')
    memoryInGB: json('0.5')
  }
}

@description('A probe on an HTTP health endpoint inside the group. A failing liveness probe restarts the container.')
func healthProbe(path string, port int, initialDelaySeconds int) object => {
  httpGet: {
    path: path
    port: port
    scheme: 'http'
  }
  initialDelaySeconds: initialDelaySeconds
  periodSeconds: 10
  timeoutSeconds: 5
  failureThreshold: 3
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2025-06-01' existing = {
  name: storageAccountName
}

resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2025-09-01' = {
  name: 'fitlife'
  location: location
  properties: {
    osType: 'Linux'
    sku: 'Standard'
    restartPolicy: 'Always'
    imageRegistryCredentials: [
      {
        server: 'index.docker.io'
        username: dockerHubUsername
        password: dockerHubToken
      }
    ]
    ipAddress: {
      type: 'Public'
      dnsNameLabel: dnsNameLabel
      ports: [
        {
          port: 443
          protocol: 'TCP'
        }
      ]
    }
    containers: [
      {
        name: 'caddy'
        properties: {
          image: caddyImage
          resources: smallContainer
          ports: [
            {
              port: 443
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
            // Read by deploy/azure/caddy/Caddyfile.
            {
              name: 'SITE_ADDRESS'
              value: fqdn
            }
            {
              name: 'GATEWAY_PORT'
              value: string(gatewayPort)
            }
            {
              name: 'CADDY_HTTP_PORT'
              value: string(caddyHttpPort)
            }
          ]
          volumeMounts: [
            {
              name: 'caddyfile'
              mountPath: '/etc/caddy' // the image's default command reads /etc/caddy/Caddyfile
              readOnly: true
            }
            {
              name: 'caddy-data'
              mountPath: '/data'
            }
          ]
        }
      }
      {
        name: 'nginx'
        properties: {
          image: nginxImage
          resources: smallContainer
          ports: [
            {
              port: gatewayPort
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
            // Rendered into the shared template by the image's envsubst at start, like in compose.
            // A route whose app isn't in the group yet answers 502.
            {
              name: 'NGINX_RESOLVER'
              value: '168.63.129.16' // Azure's DNS. The upstreams are IP addresses, so it is rarely asked.
            }
            {
              name: 'BOOKING_URL'
              value: 'http://127.0.0.1:${servicePorts.booking}'
            }
            {
              name: 'MEMBERSHIP_URL'
              value: 'http://127.0.0.1:${servicePorts.membership}'
            }
            {
              name: 'IDENTITY_URL'
              value: 'http://127.0.0.1:${servicePorts.identity}'
            }
            {
              name: 'MEMBER_PORTAL_URL'
              value: 'http://127.0.0.1:${uiPort}'
            }
            {
              // The template's /grafana/ route (compose: monitoring.yml). Grafana isn't in the group,
              // so nothing listens on this port and the route answers 502.
              name: 'GRAFANA_URL'
              value: 'http://127.0.0.1:3000'
            }
          ]
          volumeMounts: [
            {
              name: 'nginx-templates'
              mountPath: '/etc/nginx/templates'
              readOnly: true
            }
          ]
          livenessProbe: healthProbe('/health', gatewayPort, 10)
          readinessProbe: healthProbe('/health', gatewayPort, 5)
        }
      }
      {
        name: '${serviceContext}-svc'
        properties: {
          image: serviceImage
          resources: dotnetContainer
          ports: [
            {
              port: servicePorts[serviceContext]
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: string(servicePorts[serviceContext])
            }
          ]
          livenessProbe: healthProbe(serviceHealthPath, servicePorts[serviceContext], 30)
          readinessProbe: healthProbe(serviceHealthPath, servicePorts[serviceContext], 10)
        }
      }
      {
        name: 'member-portal'
        properties: {
          image: uiImage
          resources: dotnetContainer
          ports: [
            {
              port: uiPort
              protocol: 'TCP'
            }
          ]
          environmentVariables: [
            {
              name: 'ASPNETCORE_HTTP_PORTS'
              value: string(uiPort)
            }
          ]
          livenessProbe: healthProbe(uiHealthPath, uiPort, 30)
          readinessProbe: healthProbe(uiHealthPath, uiPort, 10)
        }
      }
    ]
    volumes: [
      {
        // One source for the gateway config: the compose template, mounted as a file.
        name: 'nginx-templates'
        secret: {
          'default.conf.template': loadFileAsBase64('../../compose/nginx/default.conf.template')
        }
      }
      {
        name: 'caddyfile'
        secret: {
          Caddyfile: loadFileAsBase64('../caddy/Caddyfile')
        }
      }
      {
        name: 'caddy-data'
        azureFile: {
          shareName: caddyShareName
          storageAccountName: storageAccount.name
          storageAccountKey: storageAccount.listKeys().keys[0].value
        }
      }
    ]
  }
}

@description('The public HTTPS address of the gateway.')
output publicUrl string = 'https://${containerGroup.properties.ipAddress.?fqdn ?? fqdn}'
