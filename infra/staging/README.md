# Administrator bootstrap artifact — B1/B2 only

Do not execute this script from Codex Cloud. The user must first review the plan in
[the Phase B runbook](../../docs/phase-b-bootstrap.md), then execute it manually in
Google Cloud Shell. No Google tokens, downloaded keys, copied ADC files or Firebase
CI tokens belong in Codex, GitHub variables or this repository. This script is not
an install/start script for the Codex environment.

The fixed target is `rakrao-dev` / `723651832622` / `asia-southeast1`. This preparation
has only offline mocked validation; no live resource, WIF, billing, regional pricing
or M1–M3 verification has occurred. Google pricing/location web pages were inaccessible
from the preparation environment. The required current administrator review below
is **unfulfilled**, not implied by the offline SDK syntax checks.

## Cloud Shell prerequisites

Use an administrator's existing Cloud Shell session and authorize its normal CLI
access there. Do not copy credentials or run a service-account-key login. Select
`rakrao-dev` in Cloud Shell and confirm `gcloud config get-value project`. The script
also checks the actual project number. `DEVSHELL_PROJECT_ID=rakrao-dev` is expected;
if Cloud Shell omits that variable, `--confirm-cloud-shell` is an explicit human
attestation of the same execution boundary, not a supported way to run from Codex.

Use Bash, a recent Google Cloud CLI (command definitions checked offline against
SDK 589.0.0), jq, Git, and Node >=22.12 with npm. If Cloud Shell's Node is older,
select Node 22 using its available version manager before continuing. Clone the
reviewed branch into Cloud Shell using ordinary Git access; do not change main or
copy files containing credentials. From repository root:

```bash
node --version
npm ci --prefix infra/firebase
bash infra/staging/bootstrap-rakrao-dev.sh
```

The lockfile pins Firebase CLI 15.11.0. Firebase CLI uses the ordinary Cloud Shell
user authentication available through its SDK/metadata context. If it cannot
resolve that identity, stop and fix Cloud Shell authentication locally; do not
export a token or credential file to another environment. No Firebase Auth
provider configuration is deployed: every bootstrap Firebase write uses exactly
`deploy --only firestore`.

The administrator must have permissions to list the project APIs, Cloud SQL
instances, Firestore databases/locations/indexes, Artifact repositories, Cloud Run
services, Hosting sites, service accounts/keys/custom roles, WIF pools/providers
and relevant IAM policies. Applying additionally requires API enablement (only in
its separate mode), creation of the planned resources/roles, IAM policy updates,
Firestore rules/index deployment and acting as the runtime account. Organization
policies must permit GitHub federation and the service's `allUsers` invoker binding.
The script does not grant its administrator privileges to deployment accounts.

## Review and separate modes

The default is read-only inventory and plan. A failed list/describe (including
permission denied or disabled API) always stops; only a successful complete list
establishes absence. Existing incompatible default Firestore mode, edition or
location stops before resource writes. Deleted/disabled or changed WIF trust,
extra providers, account keys, privileged project bindings, incompatible Run
configuration and unrelated cleanup policies also stop for review.

`--reviewed-location-and-pricing` attests that the administrator has checked the
current official [Firestore locations](https://cloud.google.com/firestore/docs/locations),
[Firestore pricing](https://cloud.google.com/firestore/pricing),
[Cloud Run pricing](https://cloud.google.com/run/pricing),
[Artifact Registry pricing](https://cloud.google.com/artifact-registry/pricing) and
[Firebase pricing](https://firebase.google.com/pricing), including Standard Native
Firestore in Singapore, Hosting/Cloud Run regional integration, applicable free
quotas, project billing and the database's long-lived location. It also attests
that existing Firestore features such as PITR, backups and TTL, if present, have
been reviewed for charges. The script independently checks current Firestore
`locations list` contains `asia-southeast1`; a region listing alone does not verify
pricing, quota or edition compatibility. Stop if availability/pricing is unclear.

`--reviewed-iam` attests that the administrator reviewed all proposed bindings and
existing **effective** permissions. `get-iam-policy` returns direct resource
bindings, not a complete effective-permission calculation. Inspect project,
folder/organization inheritance, groups, conditional bindings, IAM deny policies
and relevant service-account/resource policies in Console IAM/Policy Troubleshooter.
For example, Cloud Shell's `gcloud projects get-iam-policy rakrao-dev --format=json`
and `gcloud iam service-accounts get-iam-policy EMAIL --project=rakrao-dev` show
bindings but do not prove a permission works. Use Policy Troubleshooter against
the exact account, permission and resource for unresolved access, and validate
actual WIF deploy behavior in B3–B5. Do not share raw broad IAM inventories publicly.
No script flag claims this external review was performed automatically.

If required APIs are missing, the plan identifies their exact names. After review,
this separate explicit mode enables only missing APIs in the constant architecture
allowlist and **exits**; it does not create resources:

```bash
bash infra/staging/bootstrap-rakrao-dev.sh \
  --enable-reviewed-apis --reviewed-location-and-pricing --reviewed-iam
bash infra/staging/bootstrap-rakrao-dev.sh
```

There is no Cloud Build, Compute, Secret Manager or Cloud SQL API in that allowlist.
API prerequisite mode cannot be combined with `--apply` or SQL disable. API enablement
can enable Google's required service dependencies; inspect the API inventory again.

A successful Cloud SQL instance inventory of zero is required for both modes and
is rechecked before paid resource creation. If SQL Admin is disabled, zero is
**unknown**. The script stops. The administrator must first inspect the Cloud SQL
Console; if inventory requires activation, separately authorize/enable only
`sqladmin.googleapis.com` in Cloud Shell, run
`gcloud sql instances list --project=rakrao-dev`, and rerun the script. Activation
is not evidence of zero and is never performed implicitly. Existing SQL instances
require a separate reviewed cleanup decision, never automatic deletion.

After a successful plan and all reviews, the user may execute:

```bash
bash infra/staging/bootstrap-rakrao-dev.sh \
  --apply --reviewed-location-and-pricing --reviewed-iam
```

Optional `--disable-unused-sql-api --reviewed-sql-no-dependencies` disables only
`sqladmin.googleapis.com` after another successful zero-instance check and an
explicit no-dependency attestation. No `--force` or dependent-service disable is
used. `sql-component.googleapis.com` is inventoried separately and left unchanged;
review its consumers separately. SQL cleanup is best deferred until the final
successful bootstrap because subsequent reruns intentionally cannot assume zero
when the inventory API is disabled. No other API cleanup is automated.

## Scope and reruns

Five dedicated identities use the user-specified IDs. Runtime receives
`roles/datastore.user` under a condition limiting `resource.name` to the default
database; this still grants delete permissions and is not audit immutability.
Existing direct runtime `roles/datastore.user` bindings must have exactly the approved
database expression and condition title; unconditional or different bindings stop
without removing any IAM. Adding a narrow grant would not cancel broader access.
A custom runtime role is deferred until real permission telemetry justifies one.
The four deployment accounts get Service Usage Consumer and narrowly bound WIF
impersonation. Builder gets repository writer; Run deployer gets repository reader,
service-level Run Developer, and Service Account User on the runtime account only.
Firestore deployer gets index admin and rules admin at project scope, because
Firebase rules releases and the deployment APIs use project scope. Hosting deployer
gets Hosting Admin. These two accounts receive a four-permission metadata inspector
custom role (`datastore.databases.getMetadata`, `firebase.projects.get`,
`resourcemanager.projects.get`, `serviceusage.services.get`). The custom role avoids
broad Firebase viewer or datastore data-reading roles just for CLI inspection.

WIF uses numeric repository `1401374063`, owner `29879036`, main ref, the exact
`deploy-staging.yml` workflow, staging subject and workflow_dispatch event. The
restricted repository principalSet is bound on the four deployment accounts only.
The script refuses extra providers in the pool and exact provider-condition/mapping
drift, including allowed audiences. It never rewrites existing trust to make it
match. No Owner, Editor, key admin, Authentication Admin or Token Creator is granted.

For a missing service, bootstrap creates the official Google Cloud Run hello
container at `us-docker.pkg.dev/cloudrun/container/hello`, labeled
`rakrao-purpose=bootstrap-placeholder`, solely to satisfy the deployment workflow's
existing-service preflight. It runs with min 0, max 1, one CPU, 512 MiB, request-based
CPU, concurrency 20, timeout 60 and port 8080, attached to the dedicated runtime
identity. This is **not** the application and proves no M1–M3 acceptance. The source
image is Google's public mutable demo tag; record its resolved revision/digest in
B3, then replace it with the reviewed main image digest via the staging workflow.
An existing matching service is reused without deploying/replacing its image.
Existing incompatible identity/cost/network configuration stops for separate review.
Public invocation is configured on this one service; application authorization
remains the application's responsibility after real deployment.

The script reuses Hosting site `rakrao-dev`, deploys only Firestore rules/indexes,
and waits up to ten minutes for every declared composite index to be READY. It
does not deploy Hosting, modify Auth providers, configure GitHub, dispatch workflows,
perform live user tests or start M4. The final output supplies nonsecret GitHub
variables with `STAGING_DEPLOY_ENABLED=false`; obtain the existing Firebase Web
app's public API key/authDomain/appId from Console. Do not supply Phone test secrets.

Creation occurs only for verified missing named resources. IAM additions are
idempotent and preserve unrelated bindings. Reruns still reapply desired bindings,
Firestore rules/indexes and the exact cleanup policy; they do not duplicate named
resources or revert an application's deployed image. Stop after any error, inspect
partial state and rerun the plan after resolving it. There is no automatic rollback
of resource creation or data deletion. Execute exclusively: inventory and mutation
are not atomic and an administrator must prevent concurrent infrastructure changes.

## Economical retention without deleting rollback images

The tracked policy keeps the ten newest versions of each package and deletes only
versions older than 14 days that an administrator explicitly tagged `obsolete-*`.
Ordinary SHA tags, untagged digests and current/rollback images are not eligible
unless someone explicitly marks them obsolete. Existing unrelated cleanup policies
are refused instead of overwritten. Never mark a digest used by any traffic-serving
revision, retained rollback revision or paired Hosting release as obsolete. Artifact
cleanup is asynchronous; age is image age, not time since applying the obsolete tag.

At least monthly (or after a high-volume deployment batch), inventory all revisions,
traffic assignments, image digests and paired Hosting rollback releases. Preserve
current and agreed rollback digests, then mark only reviewed obsolete versions with
`obsolete-*` tags. Remove any such tag if an image becomes a rollback candidate;
do not rely solely on the most-recent-ten exception. Review cleanup logs and storage.
This explicit review trades some operator work/storage for avoiding silent deletion
of an older known-good rollback image. Storage is not a hard cap: without the review,
SHA-tagged images accumulate. No budget alert is a spending cap and zero monthly
cost is not guaranteed.

## Offline validation

From repository root, with Python 3, Bash, Node and jq installed:

```bash
bash -n infra/staging/bootstrap-rakrao-dev.sh
python3 infra/staging/test_bootstrap.py
```

The harness copies only bootstrap/configuration files into a temporary directory
and replaces both cloud CLIs with local fixture programs. It records every write,
checks rejected preflights made none, validates the WIF condition and verifies a
second apply does not create duplicate resources or redeploy the Run image. It
requires no cloud credentials or network. Passing mocks validates control flow,
not effective IAM, Google service behavior, region/pricing, quota or live deployment.
