// Deploys PodBridge as a single Azure Container App: Log Analytics + Application Insights (workspace-based,
// free ingestion up to 5 GB/day) feed both the platform's built-in stdout/stderr log collection and the
// in-process Azure Monitor OpenTelemetry exporter (see Program.cs). All PodBridge configuration - including
// Auth credentials - is mounted as Key-Per-File secrets under /run/secrets, matching the existing, unmodified
// AddKeyPerFile("/run/secrets", optional: true) call in Program.cs and the Docker-secrets convention already
// documented in the README. No application code or Docker image changes are required for this deployment.
targetScope = 'resourceGroup'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Name of the Container App.')
param containerAppName string = 'podbridge'

@description('Name of the Container Apps managed environment.')
param environmentName string = 'podbridge-env'

@description('Name of the Log Analytics workspace backing both platform logs and Application Insights.')
param logAnalyticsWorkspaceName string = 'podbridge-logs'

@description('Name of the Application Insights component.')
param appInsightsName string = 'podbridge-insights'

@description('Fully-qualified container image reference to deploy, exactly as pushed to the registry (e.g. "ghcr.io/mu88/podbridge:0.1.0-chiseled"). Passed explicitly - sourced from the build\'s own image_names output rather than reconstructed here - so promotion to production is a deliberate, auditable choice rather than implicit "latest", and never drifts from the actual registry/repository/tag the build produced.')
param containerImage string

@description('vCPU allocated to the container. Must combine with "memory" into one of Container Apps\' supported (cpu, memory) pairs.')
param cpu string = '0.25'

@description('Memory allocated to the container. Must combine with "cpu" into one of Container Apps\' supported (cpu, memory) pairs.')
param memory string = '0.5Gi'

@description('''
Flattened PodBridge configuration (including Auth credentials), one entry per Key-Per-File secret. Produced by
the deploy workflow from the single PODBRIDGE_CONFIG_JSON GitHub secret. Each item:
- name: Container Apps secret name (must match ^[a-z0-9]([-a-z0-9]*[a-z0-9])?$, e.g. "cfg-podcasts-0-podcastid")
- path: file name mounted under /run/secrets, using the .NET "__" hierarchy delimiter (e.g. "PodBridge__Podcasts__0__PodcastId")
- value: the actual configuration value
''')
@secure()
param secretFilesConfig object

var secretFiles = secretFilesConfig.items

var podcastConfigSecrets = [for file in secretFiles: {
  name: file.name
  value: file.value
}]

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspace.properties.customerId
        sharedKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  // System-assigned identity: not consumed by any resource today (GHCR image is public, Application
  // Insights uses a connection-string secret), but costs nothing and keeps the door open for e.g. a
  // future Key Vault reference instead of Container Apps secrets.
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      secrets: concat(
        podcastConfigSecrets,
        [
          {
            name: 'appinsights-connection-string'
            value: appInsights.properties.ConnectionString
          }
        ]
      )
    }
    template: {
      containers: [
        {
          name: 'podbridge'
          image: containerImage
          resources: {
            cpu: json(cpu)
            memory: memory
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights-connection-string'
            }
          ]
          volumeMounts: [
            {
              volumeName: 'secrets'
              mountPath: '/run/secrets'
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'secrets'
          storageType: 'Secret'
          secrets: [for file in secretFiles: {
            secretRef: file.name
            path: file.path
          }]
        }
      ]
      scale: {
        // Fixed at exactly one replica: PodBridge's background refresh timer isn't designed for concurrent
        // instances, and scale-to-zero would break the timer entirely.
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn
