// A monthly cost budget with e-mail alerts on the subscription (section 9a, issue #10).
// Run once by a person with rights on the subscription, not by the deploy workflow:
//   az deployment sub create --location switzerlandnorth --template-file deploy/azure/budget.bicep \
//     --parameters contactEmails='["someone@example.com"]'
targetScope = 'subscription'

@description('Budget name.')
param budgetName string = 'fitlife-monthly'

@description('Monthly budget in the subscription\'s billing currency.')
@minValue(1)
param amount int = 30

@description('Who gets the alert e-mails. Passed on the command line, so no address is committed.')
@minLength(1)
param contactEmails string[]

@description('First day of the budget period. Defaults to the first of the current month.')
param startDate string = utcNow('yyyy-MM-01')

resource budget 'Microsoft.Consumption/budgets@2026-06-01' = {
  name: budgetName
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
    }
    notifications: {
      actualOver50Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: contactEmails
      }
      actualOver80Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: contactEmails
      }
      forecastOver100Percent: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: contactEmails
      }
    }
  }
}
