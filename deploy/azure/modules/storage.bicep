// Storage account with Azure Files shares, mounted as volumes in the container group (section 9a).

@description('Azure region.')
param location string

@description('Storage account name: globally unique, 3-24 lowercase letters and digits.')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Names of the file shares to create.')
param shareNames string[]

@description('Size limit of each share in GiB.')
param shareQuotaGiB int = 1

resource storageAccount 'Microsoft.Storage/storageAccounts@2025-06-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    // Container Instances mounts Azure Files with the account key, so shared-key access stays on.
    allowSharedKeyAccess: true
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2025-06-01' = {
  parent: storageAccount
  name: 'default'
}

resource shares 'Microsoft.Storage/storageAccounts/fileServices/shares@2025-06-01' = [
  for shareName in shareNames: {
    parent: fileService
    name: shareName
    properties: {
      shareQuota: shareQuotaGiB
    }
  }
]

@description('Name of the storage account. The container group reads its key itself, so no key leaves this module.')
output storageAccountName string = storageAccount.name
