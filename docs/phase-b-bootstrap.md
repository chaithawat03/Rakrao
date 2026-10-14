# Phase B bootstrap execution gate

## Recovered authority

Main is `6d731858d7112004770e0ae50c8bdd472b124900`. Main CI `38534766759` passed backend, frontend and container. Phase A is complete. Phase B is in bootstrap preparation, not live-verified; M4 is not started. Application and deployment workflow behavior have not been changed by this preparation.

Read [bootstrap script and operator instructions](../infra/staging/README.md) and [staging foundation](staging-foundation.md). The executable artifact is `infra/staging/bootstrap-rakrao-dev.sh`. **Codex must never run it against Google Cloud.** The project administrator reviews the concrete plan and executes it in Google Cloud Shell. Do not send Codex tokens, service-account keys, ADC files or private Phone test credentials.

## Known state versus planned state

GitHub repository numeric ID `1401374063`, owner ID `29879036`, and main CI were reconfirmed. The GitHub environments-list API returned zero environments, so `staging` does not currently exist. Creating/enabling it is deferred until successful bootstrap-state verification.

No Google Cloud identity is attached to this Codex task. No live Google Cloud resource inventory was performed. In particular, Cloud SQL count, Firestore mode/location, Artifact Registry, IAM, WIF, Run revisions and Hosting releases are **unknown until Cloud Shell inventory**, not assumed absent. Hosting `rakrao-dev` is the intended reuse candidate from historical inventory.

Official Google/Firebase locations/pricing documentation requests were blocked by managed egress HTTP 403 during preparation. Required documentation domains were saved in the cloud-environment draft. Current Cloud SDK 589.0.0 command definitions were independently downloaded from official `dl.google.com` and checksum-verified for offline CLI validation. This validates command syntax, not live regional availability, prices or project permissions.

## Planned changes for administrator review

| Area | Plan |
|---|---|
| Scope | Only `rakrao-dev`, project number `723651832622`, region `asia-southeast1` |
| Firestore | Reuse compatible `(default)` or create Standard/Native database after current location/pricing review; stop on incompatible existing mode/location/edition |
| Rules/indexes | Deploy repository deny-browser rules and required membership composites; require declared indexes READY, not merely an empty pending list |
| Artifact Registry | Reuse/create one Standard Docker repository `rakrao-staging`; delete only administrator-reviewed `obsolete-` images after 14 days, retain ten latest versions per package |
| Run | Reuse compatible `rakrao-api-staging` without reverting its image; if absent, create an explicitly labelled official Google hello placeholder for the existing-service workflow preflight |
| Run limits | Minimum zero, maximum one, 1 vCPU, 512 MiB, request-based CPU, concurrency 20, timeout 60s, PORT 8080, no Cloud SQL/VPC connector/load balancer |
| Runtime | Dedicated `rakrao-api-runtime`, staging project IDs, default database, attached-identity ADC |
| Hosting | Inventory and reuse `rakrao-dev`; do not create a duplicate site or deploy Hosting/Auth during bootstrap |
| WIF | Pool `rakrao-staging`, provider `github-staging`; immutable repo/owner IDs, main, exact workflow, staging subject and manual-dispatch event |
| GitHub | Leave staging deployment disabled until B3 inventory/permissions review; no workflow dispatch during preparation |

The hello placeholder is not the RAKRAO API and must not be counted as M1–M3 acceptance. Only the later main deployment through WIF replaces it with the SHA-built immutable API digest.

## APIs

The script inventories enabled APIs first. A separate explicitly reviewed API-prerequisite mode enables only missing entries from this list and exits for a new inventory/plan pass:

`serviceusage.googleapis.com`, `cloudresourcemanager.googleapis.com`, `run.googleapis.com`, `firestore.googleapis.com`, `artifactregistry.googleapis.com`, `iam.googleapis.com`, `iamcredentials.googleapis.com`, `sts.googleapis.com`, `firebase.googleapis.com`, `firebasehosting.googleapis.com`, `firebaserules.googleapis.com`, `identitytoolkit.googleapis.com`, `securetoken.googleapis.com`, `logging.googleapis.com`, `monitoring.googleapis.com`.

No Cloud Build is used by the approved GitHub image build. Compute Engine, Secret Manager, Cloud SQL, GKE and networking APIs are not enabled by this script. API activation itself is not evidence that resources are absent or that quotas/billing allow creation.

Cloud SQL must be successfully inventoried as zero before paid-resource creation. If SQL Admin API is disabled, the script stops rather than treating inaccessible inventory as zero. The administrator must resolve the inventory prerequisite explicitly. Optional `sqladmin.googleapis.com` disablement requires a fresh zero-instance check and separate no-dependency review, without `--force`. `sql-component.googleapis.com` is inspected separately and never automatically disabled. Existing databases/secrets/default Compute account and unrelated IAM are not deleted or broadly altered.

## Identities and IAM

Five dedicated accounts, created only on verified absence:

- `rakrao-api-runtime@rakrao-dev.iam.gserviceaccount.com`
- `rakrao-image-builder@rakrao-dev.iam.gserviceaccount.com`
- `rakrao-run-deployer@rakrao-dev.iam.gserviceaccount.com`
- `rakrao-firestore-deployer@rakrao-dev.iam.gserviceaccount.com`
- `rakrao-hosting-deployer@rakrao-dev.iam.gserviceaccount.com`

Runtime uses `roles/datastore.user` conditioned to the default database. Builder gets writer on the one repository; Run deployer gets repository reader, service-scoped `roles/run.developer` and `roles/iam.serviceAccountUser` only on runtime. Firestore deployer gets index/rules administration; Hosting deployer gets Hosting administration. Four deploy accounts receive quota-consumer access and restricted WIF impersonation; runtime receives no WIF trust.

One narrowly defined `rakraoStagingInspector` custom role supplies four read-only metadata permissions needed by Firebase/database CLI inspection without broad Firebase Viewer/Admin. Exact permissions and effective-IAM verification are in the script/README. Dedicated-account user-managed keys, known broad privileges, provider drift and unrelated cleanup policy drift stop execution for review. Inherited/group/conditional effective IAM still requires administrator review; successful bindings alone do not prove least privilege or WIF usability.

No Owner, Editor, IAM Admin, Key Admin, Firebase Authentication Admin or blanket Token Creator is granted. The existing default Compute service account is not modified/deleted. Public invocation is service-scoped; domain authorization remains in the API.

## Location and cost gate

Before any resource apply, review current official sources in Cloud Shell/browser:

- [Firestore locations](https://firebase.google.com/docs/firestore/locations)
- [Firestore pricing](https://firebase.google.com/docs/firestore/pricing)
- [Cloud Run pricing](https://cloud.google.com/run/pricing)
- [Artifact Registry pricing](https://cloud.google.com/artifact-registry/pricing)
- [Firebase Hosting usage/pricing](https://firebase.google.com/docs/hosting/usage-quotas-pricing)

The script also checks the authenticated current Firestore locations list includes `asia-southeast1`. Database location is a long-lived decision. If location, Standard edition pricing or project billing cannot be confirmed, stop; do not use review flags as a substitute for verification.

The expected fixed infrastructure floor is effectively near 0 THB/month **by architecture**, with no always-on server/database or minimum Run instances. It is not a verified price quote or guarantee of a zero bill. Firestore operations/storage/network, Run requests/compute, Hosting traffic/storage, Artifact Registry storage, logs and real SMS can incur usage charges beyond applicable free allowances. No numerical free-quota or THB estimate is asserted until current prices/project quotas are reviewed. Retention requires reviewed obsolete-image tagging; current/rollback digests must never be marked obsolete. No keep-alive traffic or paid scheduler is introduced.

## Safe manual sequence

1. Review the script, plan, region/pricing, project billing and effective IAM. Obtain the prepared branch/artifact in Google Cloud Shell, inspect it, and activate only `rakrao-dev`.
2. Install the pinned tooling from its committed lockfile: `npm ci --prefix infra/firebase` (Node >=22.12). Use current `gcloud`, `jq` and the existing Cloud Shell administrator session; no credentials are supplied as arguments.
3. Run the script with no apply flag for inventory/plan. Follow any precise API/SQL inventory STOP prerequisite before rerunning.
4. Only after review, the administrator may execute the explicitly gated apply command documented in `infra/staging/README.md`. Codex pauses before this step.
5. Provide only the sanitized plan/completion output and nonsecret resource evidence. Never attach ADC/key files, authentication logs, bearer tokens, private Phone test numbers/codes or raw credential-bearing service configurations.

Bootstrap is not a distributed transaction. Errors leave completed supported steps intact; inspect the reported state and rerun after resolving the prerequisite. The script refuses conflicting pre-existing settings rather than resetting resources.

## After user execution: B3–B6

Verify actual database mode/location, rules/indexes, dedicated-account policies, provider condition and narrowly scoped trust, repository policy, Run min/max/network/identity, and existing Hosting site. Only then provide verified public Web app config and final nonsecret GitHub variable values, enable staging and dispatch approved main.

If using GitHub UI: repository **Settings → Environments → New environment → `staging`**, select deployment branch **main**, configure required reviewer(s) where supported, and add environment variables from the verified bootstrap report. Keep `STAGING_DEPLOY_ENABLED=false` until B3 succeeds. Do not configure Phone-test credentials in GitHub variables.

Live acceptance must include controlled Google sign-in, existing fictional Phone test only, M2 uniqueness/audit, M3 atomicity/role isolation/metadata/retry/throttle/Creator tests on dedicated records, browser denial and practical rollback. Record commit, digest, Run revision and Hosting release. Privileged administrator edits remain outside application Creator/audit safeguards. Some multi-member failure-path tests need reviewed fixture setup because M4 is not implemented; never add invitation/member-review endpoints merely to manufacture test data.

Phase B cannot be VERIFIED until these live checks and rollback evidence pass. M4 remains NOT STARTED and is not eligible merely because this script was prepared.
