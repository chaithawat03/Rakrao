# Staging foundation: Phase A and Phase B

## Scope and status

Phase A implements the M1–M3 Firestore application migration and prepares CI/CD. Baseline main is `9aacb465231eba9c392d35992ae184162bb6e9b0`; work stays on `architecture/firestore-staging`. No cloud resource is provisioned, no merge is automatic, and M4 (private invitations and reviewed membership) remains unstarted. Live staging verification and WIF/resource bootstrap belong to separately authorized Phase B.

Target: Firebase Hosting + Firebase Authentication + Cloud Run ASP.NET Core 10 + Cloud Firestore Native. PostgreSQL, Cloud SQL, Neon and local Docker/PostgreSQL acceptance are superseded. Existing SQL migration sources/design remain historical evidence. Do not assume any Windows workstation database, uncommitted file or credential exists.

## Cloud-first workflow

Codex Cloud → GitHub branch/PR → GitHub Actions CI → protected manual staging deployment → Google Cloud/Firebase.

`.github/workflows/ci.yml` restores/builds/tests .NET using Auth/Firestore emulators (Java 21), tests deny-browser rules, and runs npm ci, typecheck, lint, formatting, tests and production build. A packaging job builds the container and verifies non-root execution, configurable PORT binding and unauthenticated API rejection without deployment credentials. PR CI has contents-read only, no id-token permission or deployment account. Missing emulators fail integration tests rather than skip or contact production. Use `bash infra/firebase/test-emulators.sh` for equivalent backend/rule acceptance without Docker/PostgreSQL.

`.github/workflows/deploy-staging.yml` is **manual only**, requires main, the `staging` GitHub Environment, a confirmation input and `STAGING_DEPLOY_ENABLED=true`. Leave it disabled in Phase A. WIF provider and GitHub Environment trust/protection must also restrict who may enable/run it. The workflow repeats tests before obtaining deployment credentials, publishes a SHA-tagged API image, resolves its digest, deploys Firestore configuration, waits for index readiness, updates an existing Cloud Run service, and publishes a matching frontend build to an existing Hosting site. It does not bootstrap missing resources. Image building in Actions is packaging, not a Docker workstation/database acceptance requirement.

Staging smoke checks validate API health/readiness and unauthenticated 401 both directly and through Hosting. They do not establish full live Google/Phone sign-in or authenticated M2/M3 acceptance. Phase B must separately perform those flows with controlled accounts, secure fictional Phone-test configuration where needed, and no real SMS in CI.

## Nonsecret GitHub configuration for Phase B

Set these protected `staging` Environment variables after inventory/bootstrap, not fabricated identifiers:

| Variable | Required value |
|---|---|
| `STAGING_DEPLOY_ENABLED` | `true` only after authorized Phase B bootstrap and review |
| `STAGING_GCP_PROJECT_ID` | Explicit Firebase-enabled staging project |
| `STAGING_WIF_PROVIDER` | `projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/POOL/providers/PROVIDER` |
| `STAGING_BUILD_SERVICE_ACCOUNT` | Image-builder account email |
| `STAGING_RUN_DEPLOY_SERVICE_ACCOUNT` | Cloud Run deployer email |
| `STAGING_FIRESTORE_DEPLOY_SERVICE_ACCOUNT` | Rules/index deployer email |
| `STAGING_HOSTING_DEPLOY_SERVICE_ACCOUNT` | Hosting deployer email |
| `STAGING_RUNTIME_SERVICE_ACCOUNT` | Dedicated API runtime account email |
| `STAGING_ARTIFACT_REPOSITORY` | Existing Docker-format Artifact Registry repository in `asia-southeast1` |
| `STAGING_HOSTING_SITE_ID` | Existing Firebase Hosting site ID |
| `STAGING_FIREBASE_WEB_API_KEY` | Public Web app API key; not a service credential |
| `STAGING_FIREBASE_AUTH_DOMAIN` | Public authDomain of the same Firebase project |
| `STAGING_FIREBASE_WEB_APP_ID` | Public Web app appId |

No service-account JSON key, Firebase CI token or raw Google access/refresh token is stored. The auth action creates temporary WIF external-account ADC configuration; those generated files are ignored and excluded from image build context. Do not confuse this temporary keyless configuration with a downloaded service-account private key.

Current workflow expects the accounts in the staging project and the default Firestore database. Inventory existing `rakrao-dev` Firebase app/project and OAuth/domain dependencies before choosing staging resources; Phase A does not rename or mutate them.

## WIF trust bootstrap requirements

Google Cloud IAM, Security Token Service and IAM Credentials APIs must be available. Create a global Workload Identity Pool and OIDC provider with issuer `https://token.actions.githubusercontent.com`. Map subject, repository_id, repository_owner_id, ref, environment/subject, event and workflow claims.

Verified GitHub repository identity: `chaithawat03/Rakrao`, repository ID `1401374063`, owner ID `29879036`. Bind trust to these immutable numeric IDs, `refs/heads/main`, the approved deploy workflow and protected `staging` Environment; exclude pull-request contexts, forks and other workflows. Example provider condition skeleton (adapt to actual mapped token claims):

```text
assertion.repository_id == '1401374063' &&
assertion.repository_owner_id == '29879036' &&
assertion.ref == 'refs/heads/main' &&
assertion.sub == 'repo:chaithawat03/Rakrao:environment:staging' &&
assertion.workflow_ref == 'chaithawat03/Rakrao/.github/workflows/deploy-staging.yml@refs/heads/main' &&
assertion.event_name == 'workflow_dispatch'
```

Grant the restricted external principal `roles/iam.workloadIdentityUser` on each deployment account, not project-wide impersonation of arbitrary accounts. Routine impersonation does not require a blanket Token Creator grant. Deployment jobs request `contents: read` and `id-token: write`; actions are commit-SHA pinned. Protect main/required checks and restrict staging environment branches/reviewers. Keep IAM/WIF bootstrap separate from normal deployers.

## IAM plan

These are minimum intended predefined roles/scopes to validate during Phase B; avoid Owner/Editor and broad Firebase Admin.

| Identity | Intended roles/scope |
|---|---|
| API runtime | `roles/datastore.user` on selected database; consider a custom no-delete role where feasible; no deploy/IAM admin |
| Image builder | `roles/artifactregistry.writer` on the one image repository |
| Run deployer | `roles/run.developer` for the existing service, image repository reader, and `roles/iam.serviceAccountUser` on only the runtime account |
| Firestore config deployer | `roles/datastore.indexAdmin` and `roles/firebaserules.admin`; required database/project inspection permissions |
| Hosting deployer | `roles/firebasehosting.admin`; Firebase CLI needs `firebase.projects.get` and site inspection permissions |
| Cloud Run service agent | Standard managed Cloud Run service-agent role; repository reader additionally if cross-project images are selected later |

Add `roles/serviceusage.serviceUsageConsumer` when required by the actual CLI/quota flow. Confirm effective `firebase.projects.get`, `datastore.databases.get` and Cloud Run update permissions. Do not grant Authentication Admin merely to verify Firebase ID tokens. No Secret Manager access is required by the shipped emulator/keyless persistence configuration. New secret needs require their own inventory and scoped grant.

Bootstrap administrators temporarily need permissions to enable APIs, create/configure accounts and WIF, create the database/repository/service/site, and set IAM. They are separate from deployment/runtime identities. Configure public invocation once (normally service-level `roles/run.invoker` for `allUsers`, subject to organization policy). Routine deployment deliberately has no IAM-policy mutation flags.

## Firestore provisioning and enforcement

Phase B inventories existing databases and chooses Native mode `(default)` in the approved location (planning `asia-southeast1`). Location is a deliberate provisioning decision. Deploy `firestore.rules` and the two membership composite indexes in `firestore.indexes.json`; wait for actual index readiness and verify real query permissions. Emulator execution does not prove production index readiness or IAM.

The API owns data access. Browser reads/writes remain denied even with Firebase Authentication. Server SDKs bypass Security Rules and use IAM. Supported mutations atomically enforce UID/grant uniqueness, business+audit writes, rolling-hour throttle and last-Creator protection through the authoritative Family guard. The guard version is separate from metadata integer ETags.

Audit is create-only through application architecture. Privileged administrators can update/delete documents or corrupt Creator invariant records outside that boundary. The previous PostgreSQL append-only/deferred triggers are not reproduced at the database layer. Future membership-state changes must use the same Creator guard; M4 is not implemented here.

## Cloud Run and Hosting

Container: `Dockerfile`, .NET/ASP.NET Core 10, non-root runtime, `0.0.0.0:$PORT`. Staging service `rakrao-api-staging`, region `asia-southeast1`, request-based CPU allocation, minimum zero, initial maximum one, bounded concurrency/timeouts. Runtime environment: `ASPNETCORE_ENVIRONMENT=Staging`, `Firebase__ProjectId`, `Firestore__ProjectId`, `Firestore__DatabaseId=(default)`. Runtime ADC comes from the attached account. No PostgreSQL connection string, Cloud SQL attachment, VPC connector or service-account key.

`/health` is liveness. `/health/ready` performs a three-second bounded Firestore point read with a thirty-second on-demand cache, no writes or background polling. Cloud Run startup/liveness should use liveness; do not schedule keep-alive readiness traffic that prevents scale-to-zero.

Hosting publishes `apps/web/dist`; `/api/**` rewrites to this Cloud Run service before SPA fallback. Frontend uses relative `/api` and public Firebase Web configuration. Add authorized Auth domains/provider settings separately. Do not expose direct Firestore data access or cache authenticated API responses publicly. The prepared workflow deploys Hosting only, without silently updating Authentication providers. Hosting revision pinning is not enabled; its additional Run permissions/tag cleanup require a separate reviewed change.

Managed cloud container validation may need a temporary BuildKit CA mount:

```bash
docker build --secret id=proxy_ca,src=/etc/ssl/certs/ca-certificates.crt -t rakrao-phase-a:local .
```

The Dockerfile supports an optional combined trust-bundle secret for restore. It does not bake session certificates/credentials into the image or disable TLS verification. Keep managed Docker proxy/credential configuration unchanged. This validation is optional for a developer workstation.

## Cutover, rollback and cost

Inventory any authoritative SQL data; no Windows import is assumed. A separate import must preserve UUIDs, UTC timestamps, versions, references, grant/audit history, active keys/guards and retry/throttle semantics. Validate before write freeze/cutover and keep a recoverable export. Do not recreate databases or assume rollback of an image reverses new Firestore writes.

Keep previous API digest/revision and Hosting release together. Prefer backward-compatible backend changes before frontend release. If Hosting deploy fails after API deploy, maintain backward compatibility and explicitly restore the prior API revision/Hosting release where required; this workflow is not a multi-service atomic deploy. Phase B must practice rollback and define data restore/retention before real use.

Near-zero fixed cost is the objective. Billing/Blaze is needed for Cloud Run integration. Avoid Cloud SQL, minimum instances, VPC connectors, load balancers and unnecessary scheduled jobs. Bound Firestore reads/writes, logs and artifacts; retain only needed images/Hosting releases. Real Phone Authentication, storage, network traffic, logs and operations can incur variable charges. Budget alerts notify; they are not spending caps. No fixed-cost cloud resource is newly provisioned in Phase A.

## Phase B exit evidence

Before calling live staging verified: confirm IAM/WIF denial for unauthorized contexts, real composite indexes, runtime ADC/project binding, deny-browser rules, direct/Hosting token rejection, controlled Google/Phone sign-in, concurrent User provisioning, family atomicity, current role isolation, metadata conflicts, retries/throttle and Creator protection. Check container revision, Hosting release and their commit/digest mapping, operational limits and restore/rollback evidence. Phase A CI green means migration code acceptance, not completed Phase B.
