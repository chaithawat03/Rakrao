#!/usr/bin/env bash
# ADMINISTRATOR ONLY: run in Google Cloud Shell, never from Codex Cloud.
set -euo pipefail
readonly PROJECT_ID=rakrao-dev PROJECT_NUMBER=723651832622 REGION=asia-southeast1
readonly SERVICE=rakrao-api-staging REPOSITORY=rakrao-staging POOL=rakrao-staging PROVIDER=github-staging
readonly INSPECTOR=rakraoStagingInspector
readonly ROOT="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
readonly FIREBASE="$ROOT/infra/firebase/node_modules/.bin/firebase"
readonly RUNTIME="rakrao-api-runtime@$PROJECT_ID.iam.gserviceaccount.com"
readonly PRINCIPAL="principalSet://iam.googleapis.com/projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/$POOL/attribute.repository_id/1401374063"
readonly CONDITION="assertion.repository_id == '1401374063' && assertion.repository_owner_id == '29879036' && assertion.ref == 'refs/heads/main' && assertion.sub == 'repo:chaithawat03/Rakrao:environment:staging' && assertion.workflow_ref == 'chaithawat03/Rakrao/.github/workflows/deploy-staging.yml@refs/heads/main' && assertion.event_name == 'workflow_dispatch'"
readonly MAPPING='google.subject=assertion.sub,attribute.repository_id=assertion.repository_id,attribute.repository_owner_id=assertion.repository_owner_id,attribute.ref=assertion.ref,attribute.workflow_ref=assertion.workflow_ref,attribute.event_name=assertion.event_name'
readonly INSPECT_PERMISSIONS='datastore.databases.getMetadata,firebase.projects.get,resourcemanager.projects.get,serviceusage.services.get'
readonly ACCOUNTS=(rakrao-api-runtime rakrao-image-builder rakrao-run-deployer rakrao-firestore-deployer rakrao-hosting-deployer)
readonly REQUIRED_APIS=(serviceusage.googleapis.com cloudresourcemanager.googleapis.com run.googleapis.com firestore.googleapis.com artifactregistry.googleapis.com iam.googleapis.com iamcredentials.googleapis.com sts.googleapis.com firebase.googleapis.com firebasehosting.googleapis.com firebaserules.googleapis.com identitytoolkit.googleapis.com securetoken.googleapis.com logging.googleapis.com monitoring.googleapis.com)
APPLY=false LOCATION_REVIEW=false IAM_REVIEW=false SHELL_CONFIRM=false DISABLE_SQL=false SQL_REVIEW=false ENABLE_APIS=false
fail() { printf 'STOP: %s\n' "$*" >&2; exit 1; }
usage() {
  cat <<'HELP'
Usage: bash infra/staging/bootstrap-rakrao-dev.sh [--apply]
  [--enable-reviewed-apis] [--reviewed-location-and-pricing] [--reviewed-iam]
  [--confirm-cloud-shell] [--disable-unused-sql-api --reviewed-sql-no-dependencies]
Default: inventory + plan, no writes. Apply requires both review attestations.
--confirm-cloud-shell attests you are the administrator in Google Cloud Shell
when DEVSHELL_PROJECT_ID is unavailable; it is not permission to run in Codex.
Read infra/staging/README.md before using any flags. Never pass credentials.
HELP
}
for arg in "$@"; do
  case "$arg" in
    --apply) APPLY=true;;
    --enable-reviewed-apis) ENABLE_APIS=true;;
    --reviewed-location-and-pricing) LOCATION_REVIEW=true;;
    --reviewed-iam) IAM_REVIEW=true;;
    --confirm-cloud-shell) SHELL_CONFIRM=true;;
    --disable-unused-sql-api) DISABLE_SQL=true;;
    --reviewed-sql-no-dependencies) SQL_REVIEW=true;;
    --help|-h) usage; exit 0;;
    *) fail "Unknown argument: $arg";;
  esac
done
[[ "${DEVSHELL_PROJECT_ID:-}" == "$PROJECT_ID" || "$SHELL_CONFIRM" == true ]] || fail 'Open Google Cloud Shell with rakrao-dev active. Do not execute from Codex.'
# CLI processes must use the administrator's Cloud Shell session, not copied keys,
# an impersonation override, Firebase CI token, or local emulators.
for name in GOOGLE_APPLICATION_CREDENTIALS CLOUDSDK_AUTH_CREDENTIAL_FILE_OVERRIDE CLOUDSDK_AUTH_ACCESS_TOKEN CLOUDSDK_AUTH_IMPERSONATE_SERVICE_ACCOUNT FIREBASE_TOKEN FIRESTORE_EMULATOR_HOST FIREBASE_AUTH_EMULATOR_HOST; do
  [[ -z "${!name:-}" ]] || fail "Unset $name; use the existing Cloud Shell administrator session."
done
for tool in gcloud jq node; do command -v "$tool" >/dev/null || fail "Install required tool: $tool"; done
[[ -x "$FIREBASE" ]] || fail 'Run npm ci --prefix infra/firebase with Node >=22.12 first (pinned firebase-tools 15.11.0).'
node -e 'const [a,b]=process.versions.node.split(".").map(Number);process.exit(a>22||(a===22&&b>=12)?0:1)' || fail 'Node >=22.12 required.'
cd "$ROOT"
export CLOUDSDK_CORE_DISABLE_PROMPTS=1 FIREBASE_CLI_DISABLE_UPDATE_CHECK=1
[[ "$(gcloud config get-value project 2>/dev/null)" == "$PROJECT_ID" ]] || fail 'Active gcloud project is not rakrao-dev.'
[[ "$(gcloud config get-value auth/impersonate_service_account 2>/dev/null)" =~ ^\(unset\)$|^$ ]] || fail 'Remove gcloud account impersonation configuration in Cloud Shell.'
[[ "$(gcloud projects describe "$PROJECT_ID" --format='value(projectNumber)')" == "$PROJECT_NUMBER" ]] || fail 'Project number mismatch.'
WORK="$(mktemp -d)"
trap 'rm -rf -- "$WORK"' EXIT
# No failed describe/list is interpreted as absence. Full successful lists establish absence.
read_json() {
  local target="$1"; shift
  "$@" > "$WORK/$target.json" || fail "Inventory failed ($target); resolve permissions/API availability, then rerun. No resource absence inferred."
  jq -e 'type == "object" or type == "array"' "$WORK/$target.json" >/dev/null || fail "Invalid JSON inventory: $target"
}
read_array() {
  local target="$1"; shift
  read_json "$target" "$@"
  jq -e 'type == "array"' "$WORK/$target.json" >/dev/null || fail "Expected complete list: $target"
}
gc() { gcloud "$@" --project="$PROJECT_ID"; }
read_array apis gc services list --enabled --format=json
has_api() { jq -e --arg api "$1" 'any(.[]; .config.name == $api)' "$WORK/apis.json" >/dev/null; }
missing=()
for api in "${REQUIRED_APIS[@]}"; do has_api "$api" || missing+=("$api"); done
has_api sqladmin.googleapis.com || fail 'SQL Admin API disabled: zero instances is UNKNOWN. Administrator must inspect Cloud SQL inventory in Console; if inventory requires API activation, separately authorize/enable sqladmin.googleapis.com in Cloud Shell, run gcloud sql instances list --project=rakrao-dev, and rerun. No automatic activation or deletion.'
verify_sql_zero() {
  read_array sql gc sql instances list --format=json
  [[ "$(jq length "$WORK/sql.json")" == 0 ]] || fail 'Cloud SQL instances exist. Stop for separate inventory/cleanup authorization; never delete automatically.'
}
verify_sql_zero
if [[ "$ENABLE_APIS" == true ]]; then
  [[ "$APPLY" != true && "$DISABLE_SQL" != true ]] || fail 'API prerequisite mode is separate from resource apply/SQL disable.'
  [[ "$LOCATION_REVIEW" == true && "$IAM_REVIEW" == true ]] || fail 'API enable mode requires location/pricing and IAM review attestations.'
  printf 'Only these missing architecture APIs will be enabled: %s\n' "${missing[*]:-(none)}"
  if ((${#missing[@]})); then gc services enable "${missing[@]}" --quiet; fi
  printf 'API prerequisites handled. Rerun default inventory/plan; this mode creates no resources.\n'
  exit 0
fi
if ((${#missing[@]})); then
  printf 'Missing architecture APIs: %s\n' "${missing[*]}" >&2
  fail 'Review API list, then run separate --enable-reviewed-apis --reviewed-location-and-pricing --reviewed-iam and rerun plan. Resource absence cannot be established until inventory APIs work.'
fi
read_array databases gc firestore databases list --format=json
jq '[.[] | select(.name | endswith("/databases/(default)"))]' "$WORK/databases.json" > "$WORK/default.json"
DB_EXISTS="$(jq 'length > 0' "$WORK/default.json")"
if [[ "$DB_EXISTS" == true ]]; then
  jq -e --arg region "$REGION" 'length == 1 and .[0].type == "FIRESTORE_NATIVE" and .[0].locationId == $region and (.[0].databaseEdition // "STANDARD") == "STANDARD"' "$WORK/default.json" >/dev/null || fail 'Existing default Firestore mode/location/edition is incompatible; no mutations performed.'
fi
read_array locations gc firestore locations list --format=json
jq -e --arg region "$REGION" 'any(.[]; .locationId == $region)' "$WORK/locations.json" >/dev/null || fail 'Current Firestore location inventory does not include asia-southeast1.'
read_array repositories gc artifacts repositories list --location="$REGION" --format=json
jq --arg name "/repositories/$REPOSITORY" '[.[] | select(.name | endswith($name))]' "$WORK/repositories.json" > "$WORK/repository.json"
REPO_EXISTS="$(jq 'length > 0' "$WORK/repository.json")"
if [[ "$REPO_EXISTS" == true ]]; then
  jq -e 'length == 1 and .[0].format == "DOCKER" and (.[0].mode // "STANDARD_REPOSITORY") == "STANDARD_REPOSITORY" and (.[0].cleanupPolicies // {} | length) == 0' "$WORK/repository.json" >/dev/null || {
    # Exact policy reconciliation occurs below; never overwrite unrelated cleanup policies.
    jq -e 'length == 1 and .[0].format == "DOCKER" and (.[0].mode // "STANDARD_REPOSITORY") == "STANDARD_REPOSITORY"' "$WORK/repository.json" >/dev/null || fail 'Repository format/mode conflict.'
  }
  read_json repository_iam gc artifacts repositories get-iam-policy "$REPOSITORY" --location="$REGION" --format=json
fi
read_array services gc run services list --region="$REGION" --platform=managed --format=json
RUN_EXISTS="$(jq --arg service "$SERVICE" 'any(.[]; .metadata.name == $service)' "$WORK/services.json")"
if [[ "$RUN_EXISTS" == true ]]; then
  read_json run gc run services describe "$SERVICE" --region="$REGION" --format=json
  read_json run_iam gc run services get-iam-policy "$SERVICE" --region="$REGION" --format=json
  # Existing service is never redeployed here. Require an administrator to review
  # its ADC identity, cost/network settings and current rollback image first.
  jq -e --arg sa "$RUNTIME" '
    .spec.template as $t |
    $t.spec.serviceAccountName == $sa and
    ((.metadata.annotations["run.googleapis.com/minScale"] // "0")|tonumber)==0 and
    (($t.metadata.annotations["autoscaling.knative.dev/minScale"] // "0")|tonumber)==0 and
    (.metadata.annotations["run.googleapis.com/maxScale"] == "1" or $t.metadata.annotations["autoscaling.knative.dev/maxScale"] == "1") and
    ($t.metadata.annotations["run.googleapis.com/vpc-access-connector"] // "") == "" and
    ($t.metadata.annotations["run.googleapis.com/network-interfaces"] // "") == "" and
    ($t.metadata.annotations["run.googleapis.com/cloudsql-instances"] // "") == "" and
    ($t.metadata.annotations["run.googleapis.com/cpu-throttling"] // "true") == "true" and
    $t.spec.containerConcurrency == 20 and $t.spec.timeoutSeconds == 60 and
    ($t.spec.containers|length)==1 and
    ($t.spec.containers[0].resources.limits.cpu|tostring|IN("1","1000m")) and
    $t.spec.containers[0].resources.limits.memory == "512Mi" and
    $t.spec.containers[0].ports[0].containerPort == 8080
  ' "$WORK/run.json" >/dev/null || fail 'Existing Run service identity/cost/network settings need separate administrator review; not changing its image or configuration.'

fi
read_json hosting "$FIREBASE" hosting:sites:list --project "$PROJECT_ID" --json --non-interactive
jq -e --arg site "projects/$PROJECT_ID/sites/$PROJECT_ID" --arg suffix "/sites/$PROJECT_ID" '.status == "success" and any(.result.sites[]; .name | endswith($suffix))' "$WORK/hosting.json" >/dev/null || fail 'Expected existing Hosting site rakrao-dev unavailable; do not create a duplicate.'
read_array accounts gc iam service-accounts list --format=json
read_json project_iam gc projects get-iam-policy "$PROJECT_ID" --format=json
for account in "${ACCOUNTS[@]}"; do
  email="$account@$PROJECT_ID.iam.gserviceaccount.com"
  if jq -e --arg email "$email" 'any(.[]; .email == $email)' "$WORK/accounts.json" >/dev/null; then
    jq -e --arg email "$email" 'all(.[] | select(.email == $email); .disabled != true)' "$WORK/accounts.json" >/dev/null || fail "Disabled account: $account"
    read_json "$account-iam" gc iam service-accounts get-iam-policy "$email" --format=json
    read_array "$account-keys" gc iam service-accounts keys list --iam-account="$email" --managed-by=user --format=json
    [[ "$(jq length "$WORK/$account-keys.json")" == 0 ]] || fail "Existing user-managed key on $account; investigate separately. No key deletion attempted."
  fi
done
# Refuse known privileged/project-wide drift on the dedicated identities. This
# does not replace effective IAM review: inherited/groups/deny policies require
# Policy Troubleshooter or administrator inspection (see README).
for account in "${ACCOUNTS[@]}"; do
  email="$account@$PROJECT_ID.iam.gserviceaccount.com"
  jq -e --arg member "serviceAccount:$email" 'all(.bindings[]? | select(any(.members[]?; . == $member)); (.role | IN("roles/owner","roles/editor","roles/iam.securityAdmin","roles/resourcemanager.projectIamAdmin","roles/iam.serviceAccountAdmin","roles/iam.serviceAccountKeyAdmin","roles/iam.serviceAccountTokenCreator","roles/firebase.admin","roles/firebaseauth.admin","roles/run.admin","roles/run.developer","roles/artifactregistry.admin","roles/artifactregistry.writer","roles/artifactregistry.reader","roles/iam.serviceAccountUser")) | not)' "$WORK/project_iam.json" >/dev/null || fail "Privileged or excessively scoped project binding on $account; separate IAM review required."
  if [[ -f "$WORK/$account-iam.json" ]]; then
    jq -e --arg principal "$PRINCIPAL" 'all(.bindings[]? | select(.role == "roles/iam.workloadIdentityUser"); all(.members[]?; . == $principal)) and all(.bindings[]?; .role != "roles/iam.serviceAccountTokenCreator")' "$WORK/$account-iam.json" >/dev/null || fail "Unexpected account impersonation trust on $account."
    if [[ "$account" == rakrao-api-runtime ]]; then
      jq -e 'all(.bindings[]?; .role != "roles/iam.workloadIdentityUser")' "$WORK/$account-iam.json" >/dev/null || fail 'Runtime must not accept GitHub WIF impersonation.'
    fi
  fi
done
read_array roles gc iam roles list --format=json
ROLE_EXISTS="$(jq --arg name "projects/$PROJECT_ID/roles/$INSPECTOR" 'any(.[]; .name == $name)' "$WORK/roles.json")"
if [[ "$ROLE_EXISTS" == true ]]; then
  read_json inspector gc iam roles describe "$INSPECTOR" --format=json
  jq -e --arg permissions "$INSPECT_PERMISSIONS" '(.deleted != true) and (.stage == "GA") and ((.includedPermissions|sort) == ($permissions|split(",")|sort))' "$WORK/inspector.json" >/dev/null || fail 'Inspector role drift; refusing to overwrite.'
fi
read_array pools gc iam workload-identity-pools list --location=global --show-deleted --format=json
POOL_EXISTS="$(jq --arg suffix "/workloadIdentityPools/$POOL" 'any(.[]; .name | endswith($suffix))' "$WORK/pools.json")"
PROVIDER_EXISTS=false
if [[ "$POOL_EXISTS" == true ]]; then
  jq -e --arg suffix "/workloadIdentityPools/$POOL" 'all(.[] | select(.name | endswith($suffix)); .state == "ACTIVE" and .disabled != true)' "$WORK/pools.json" >/dev/null || fail 'Existing pool inactive/deleted/disabled.'
  read_array providers gc iam workload-identity-pools providers list --workload-identity-pool="$POOL" --location=global --show-deleted --format=json
  jq -e --arg suffix "/providers/$PROVIDER" 'all(.[]; .name | endswith($suffix))' "$WORK/providers.json" >/dev/null || fail 'Unexpected provider in pool: trust is shared across providers; refusing broadening.'
  PROVIDER_EXISTS="$(jq 'length > 0' "$WORK/providers.json")"
  if [[ "$PROVIDER_EXISTS" == true ]]; then
    read_json provider gc iam workload-identity-pools providers describe "$PROVIDER" --workload-identity-pool="$POOL" --location=global --format=json
    jq -e --arg condition "$CONDITION" --arg mapping "$MAPPING" '.state == "ACTIVE" and .disabled != true and .oidc.issuerUri == "https://token.actions.githubusercontent.com" and ((.oidc.allowedAudiences // [])|length)==0 and .attributeCondition == $condition and .attributeMapping == ($mapping|split(",")|map(split("=")|{key:.[0],value:.[1]})|from_entries)' "$WORK/provider.json" >/dev/null || fail 'Provider trust drift: refusing to update/overwrite existing trust.'
  fi
fi
# Existing cleanup rules must be exactly ours, or absent. Policies are additive
# only at IAM binding level; cleanup configuration is never blindly overwritten.
if [[ "$REPO_EXISTS" == true ]]; then
  jq --slurpfile wanted "$ROOT/infra/staging/cleanup-policy.json" -e '(.[0].cleanupPolicies // {}) as $existing | ($existing|length)==0 or $existing == ($wanted[0]|map({key:.name,value:({id:.name,action:(.action.type|ascii_upcase)} + (if has("condition") then {condition:(.condition|.tagState |= ascii_upcase)} else {mostRecentVersions:.mostRecentVersions} end))})|from_entries)' "$WORK/repository.json" >/dev/null || fail 'Existing Artifact cleanup policy differs; review without overwriting.'
fi
cat <<PLAN
Plan (no authentication secrets included):
  Project $PROJECT_ID / $PROJECT_NUMBER; region $REGION
  APIs: required APIs already enabled; no automatic enable or unrelated cleanup
  Cloud SQL: zero verified; sql-component enabled=$(has_api sql-component.googleapis.com && echo true || echo false)
  Firestore (default): existing=$DB_EXISTS; Native / STANDARD / $REGION
  Artifact Docker repository: $REPOSITORY; existing=$REPO_EXISTS
  Run service: $SERVICE; existing=$RUN_EXISTS; new service uses Google hello placeholder
  Hosting: reuse $PROJECT_ID; no Hosting/Auth deployment in bootstrap
  Five accounts: ${ACCOUNTS[*]}
  Inspector custom role: four read-only project/database inspection permissions
  Runtime: datastore.user limited by database resource-name condition
  Builder: repository writer; Run deployer: service developer + repository reader + actAs runtime only
  Firestore deployer: project indexAdmin + firebaserules.admin + inspector
  Hosting deployer: project firebasehosting.admin + inspector
  Deployment accounts: serviceUsageConsumer; restricted workloadIdentityUser on each account
  WIF: pool=$POOL provider=$PROVIDER; repository/owner IDs, main, workflow, staging subject, dispatch enforced
  Retention: delete only administrator-marked obsolete- tagged images older than 14 days; keep 10 most recent/package
  Cost: usage charges apply; no always-on resource. Current regional pricing review REQUIRED.
  Optional SQL API disable requested: $DISABLE_SQL (sql-component never automatically disabled)
  Existing IAM policies were read; administrator must review effective inherited/conditional permissions.
PLAN
if [[ "$APPLY" != true ]]; then
  printf 'PLAN ONLY. No writes. Apply requires --apply --reviewed-location-and-pricing --reviewed-iam.\n'
  exit 0
fi
[[ "$LOCATION_REVIEW" == true ]] || fail 'Review current official regional availability, STANDARD Firestore/Run/Hosting/Artifact pricing, billing and long-lived database location first.'
[[ "$IAM_REVIEW" == true ]] || fail 'Review existing effective IAM, org policies and the exact planned bindings first (see README).'
[[ "$DISABLE_SQL" != true || "$SQL_REVIEW" == true ]] || fail 'SQL disable requires explicit no-dependency review.'
# Final pre-write recheck. Cloud state can change; run exclusively and stop on any
# conflict. This is not an atomic transaction; rerun after fixing partial failures.
verify_sql_zero
if [[ "$DB_EXISTS" != true ]]; then gc firestore databases create --database='(default)' --location="$REGION" --type=firestore-native --edition=standard --quiet; fi
if [[ "$REPO_EXISTS" != true ]]; then verify_sql_zero; gc artifacts repositories create "$REPOSITORY" --location="$REGION" --repository-format=docker --description='RAKRAO staging API images' --quiet; fi
for account in "${ACCOUNTS[@]}"; do
  email="$account@$PROJECT_ID.iam.gserviceaccount.com"
  if ! jq -e --arg email "$email" 'any(.[]; .email == $email)' "$WORK/accounts.json" >/dev/null; then
    gc iam service-accounts create "$account" --display-name="$account" --quiet
  fi
done
if [[ "$ROLE_EXISTS" != true ]]; then gc iam roles create "$INSPECTOR" --title='RAKRAO staging metadata inspector' --permissions="$INSPECT_PERMISSIONS" --stage=GA --quiet; fi
if [[ "$POOL_EXISTS" != true ]]; then gc iam workload-identity-pools create "$POOL" --location=global --display-name='RAKRAO staging GitHub' --quiet; fi
if [[ "$PROVIDER_EXISTS" != true ]]; then
  gc iam workload-identity-pools providers create-oidc "$PROVIDER" --workload-identity-pool="$POOL" --location=global --issuer-uri=https://token.actions.githubusercontent.com --attribute-mapping="$MAPPING" --attribute-condition="$CONDITION" --quiet
fi
project_role() { gc projects add-iam-policy-binding "$PROJECT_ID" --member="serviceAccount:$1@$PROJECT_ID.iam.gserviceaccount.com" --role="$2" --condition=None --quiet >/dev/null; }
repo_role() { gc artifacts repositories add-iam-policy-binding "$REPOSITORY" --location="$REGION" --member="serviceAccount:$1@$PROJECT_ID.iam.gserviceaccount.com" --role="$2" --quiet >/dev/null; }
gc projects add-iam-policy-binding "$PROJECT_ID" --member="serviceAccount:$RUNTIME" --role=roles/datastore.user --condition="expression=resource.name == 'projects/$PROJECT_ID/databases/(default)',title=rakrao-staging-default-database" --quiet >/dev/null
repo_role rakrao-image-builder roles/artifactregistry.writer
repo_role rakrao-run-deployer roles/artifactregistry.reader
for account in "${ACCOUNTS[@]:1}"; do
  project_role "$account" roles/serviceusage.serviceUsageConsumer
  gc iam service-accounts add-iam-policy-binding "$account@$PROJECT_ID.iam.gserviceaccount.com" --member="$PRINCIPAL" --role=roles/iam.workloadIdentityUser --quiet >/dev/null
done
for role in roles/datastore.indexAdmin roles/firebaserules.admin "projects/$PROJECT_ID/roles/$INSPECTOR"; do project_role rakrao-firestore-deployer "$role"; done
project_role rakrao-hosting-deployer roles/firebasehosting.admin
project_role rakrao-hosting-deployer "projects/$PROJECT_ID/roles/$INSPECTOR"
gc iam service-accounts add-iam-policy-binding "$RUNTIME" --member="serviceAccount:rakrao-run-deployer@$PROJECT_ID.iam.gserviceaccount.com" --role=roles/iam.serviceAccountUser --quiet >/dev/null
if [[ "$RUN_EXISTS" != true ]]; then
  verify_sql_zero
  gc run deploy "$SERVICE" --image=us-docker.pkg.dev/cloudrun/container/hello --region="$REGION" --platform=managed --service-account="$RUNTIME" --min=0 --max=1 --cpu=1 --memory=512Mi --concurrency=20 --timeout=60 --cpu-throttling --port=8080 --no-allow-unauthenticated --labels=rakrao-purpose=bootstrap-placeholder --set-env-vars="ASPNETCORE_ENVIRONMENT=Staging,Firebase__ProjectId=$PROJECT_ID,Firestore__ProjectId=$PROJECT_ID,Firestore__DatabaseId=(default)" --quiet
fi
gc run services add-iam-policy-binding "$SERVICE" --region="$REGION" --member=allUsers --role=roles/run.invoker --quiet >/dev/null
gc run services add-iam-policy-binding "$SERVICE" --region="$REGION" --member="serviceAccount:rakrao-run-deployer@$PROJECT_ID.iam.gserviceaccount.com" --role=roles/run.developer --quiet >/dev/null
gc artifacts repositories set-cleanup-policies "$REPOSITORY" --location="$REGION" --policy="$ROOT/infra/staging/cleanup-policy.json" --no-dry-run --quiet
"$FIREBASE" deploy --only firestore --project "$PROJECT_ID" --non-interactive
ready=false
for ((attempt=1; attempt<=60; attempt++)); do
  read_array indexes gc firestore indexes composite list --database='(default)' --format=json
  # Require every declared composite, not merely absence of pending indexes.
  if jq -e --slurpfile wanted "$ROOT/firestore.indexes.json" '. as $actual | all(.[]; .state == "READY") and all($wanted[0].indexes[]; . as $w | any($actual[]; (.name | contains("/collectionGroups/" + $w.collectionGroup + "/indexes/")) and .queryScope == $w.queryScope and ([.fields[]|select(.fieldPath != "__name__")|{fieldPath,order}] == $w.fields)))' "$WORK/indexes.json" >/dev/null; then ready=true; break; fi
  sleep 10
done
[[ "$ready" == true ]] || fail 'Required composite indexes not READY after ten minutes; do not enable staging deployment.'
if [[ "$DISABLE_SQL" == true ]]; then
  verify_sql_zero
  gc services disable sqladmin.googleapis.com --quiet
fi
cat <<REPORT
Bootstrap commands completed; live application M1–M3 and WIF authentication NOT verified.
STAGING_DEPLOY_ENABLED=false
STAGING_GCP_PROJECT_ID=$PROJECT_ID
STAGING_WIF_PROVIDER=projects/$PROJECT_NUMBER/locations/global/workloadIdentityPools/$POOL/providers/$PROVIDER
STAGING_BUILD_SERVICE_ACCOUNT=rakrao-image-builder@$PROJECT_ID.iam.gserviceaccount.com
STAGING_RUN_DEPLOY_SERVICE_ACCOUNT=rakrao-run-deployer@$PROJECT_ID.iam.gserviceaccount.com
STAGING_FIRESTORE_DEPLOY_SERVICE_ACCOUNT=rakrao-firestore-deployer@$PROJECT_ID.iam.gserviceaccount.com
STAGING_HOSTING_DEPLOY_SERVICE_ACCOUNT=rakrao-hosting-deployer@$PROJECT_ID.iam.gserviceaccount.com
STAGING_RUNTIME_SERVICE_ACCOUNT=$RUNTIME
STAGING_ARTIFACT_REPOSITORY=$REPOSITORY
STAGING_HOSTING_SITE_ID=$PROJECT_ID
Obtain existing app public config from Firebase Console: STAGING_FIREBASE_WEB_API_KEY, STAGING_FIREBASE_AUTH_DOMAIN, STAGING_FIREBASE_WEB_APP_ID.
Do not supply private Phone test credentials. Keep deployment disabled pending B3 review.
REPORT
