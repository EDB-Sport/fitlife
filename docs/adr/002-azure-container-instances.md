# ADR 002: Production on Azure Container Instances, one container group

- **Status:** proposed
- **Date:** 2026-11-23
- **Deciders:** to be confirmed by the group (issue #1)

## Context

The IT-arkitektur course runs the system on-premises with Docker Compose, and its cloud variant (module M15, `foodfactory.bicep`) uses several Azure Container Instances (ACI) container groups in a virtual network behind an Application Gateway. The project description says the solution runs container-based with monitoring, prefers a cloud solution, and needs links to the running functionality in the hand-in. The group pays with Azure for Students credit, which has to last from the first deploy through the exam in January. The Application Gateway has a fixed hourly cost that would use most of that credit (`docs/TECHNICAL_REFERENCE.md` section 9a).

## Decision

Production runs on **Azure Container Instances**, provisioned with **Bicep** in `deploy/azure/`, in **one container group with one public port**. Every container (Caddy, Nginx, the services, the Blazor UIs, MongoDB, RabbitMQ, Vault, Loki, Grafana) runs in that group. Only the entry point has a port on the group's public IP. The containers reach each other on `localhost`, each on its own port, with the addresses taken from configuration. Caddy as a sidecar gives HTTPS with a Let's Encrypt certificate. Data lives on Azure Files shares. Docker Compose stays the environment for laptops and CI.

`deploy.yml` deploys each release from `main` after one member approves it on the `production` environment, logging in to Azure with OpenID Connect (OIDC), so no client secret is stored. That depends on the check below.

## App registration check

OIDC from GitHub Actions needs a federated credential, usually on an app registration in Microsoft Entra ID. An Azure for Students subscription in a school tenant may not allow members to create app registrations (`docs/TECHNICAL_REFERENCE.md` section 9a).

**Result: not yet checked.** Issue #1's agent has no access to the Azure portal or the Azure CLI. A member with the subscription runs the check and writes the result here:

1. In the tenant of the Azure for Students subscription: `az login`, then `az ad app create --display-name fitlife-oidc-check`. If it succeeds, delete it again with `az ad app delete --id <appId>`.
2. If step 1 is refused, check the tenant setting: `az rest --method get --url https://graph.microsoft.com/v1.0/policies/authorizationPolicy --query defaultUserRolePermissions.allowedToCreateApps`.
3. If app registrations aren't allowed, try a **user-assigned managed identity** with a federated credential for the repo's `production` environment. It's an Azure resource in our own subscription, not a tenant object, and `azure/login` accepts it with the same OIDC settings. The role assignment on the resource group needs the Owner role on the subscription.

| Outcome | What `deploy.yml` does |
|---|---|
| App registration allowed | OIDC with an app registration, as planned |
| Not allowed, managed identity works | OIDC with the user-assigned managed identity; no secret stored |
| Neither works | One member runs the same `az deployment group create` from their machine, and CI only runs the smoke test. Recorded here as a deviation |

## Alternatives considered

- **Docker Compose on-premises, as in the course.** No cloud cost. Rejected: the project description prefers a cloud solution, and the hand-in needs public links that work until the exam.
- **The course's cloud variant: several container groups in a virtual network behind an Application Gateway.** Closer to a real production setup, with path-based routing and room for several instances. Rejected: the gateway's fixed hourly cost would use most of the Azure for Students credit before the January exam. The report describes it as the next step for scaling and resilience.
- **Azure Container Apps or AKS.** Autoscaling and managed ingress. Rejected: not taught in the course, so harder to explain line by line at the exam, and more to set up.

## Consequences

- Easier: the whole environment is rebuilt from the repo with one command, reviewed like code, and removed with `az group delete` after the exam. ACI bills per second while the containers run, and there is no gateway cost.
- Harder: one container group means no autoscaling and one failure domain. A group has CPU and memory limits per region, so the .NET containers get small requests, and a second group is needed if the total doesn't fit. Containers share `localhost`, so every .NET container needs its own port (`ASPNETCORE_HTTP_PORTS`), unlike compose. ACI gives no certificate, so the Caddy sidecar is needed for HTTPS.
- Must do: run the app registration check above before `deploy.yml` is built, and write the result here. Set a budget alert on the subscription, keep the group running from hand-in (18 Dec) through the exam, and delete the resource group after the exam.
