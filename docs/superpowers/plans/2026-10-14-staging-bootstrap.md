# Phase B staging bootstrap preparation

Authority: user Phase B brief, with explicit Cloud Shell execution gate. Main baseline `6d731858d7112004770e0ae50c8bdd472b124900`; Phase A complete and CI38534766759 passed. M4 remains unstarted.

## Scope

Prepare and independently review `infra/staging/bootstrap-rakrao-dev.sh` and a Cloud Shell runbook. Never execute it against Google Cloud from Codex. Inventory live state and perform live acceptance only after the user executes the reviewed script and supplies safe output. Do not enable GitHub staging deployment before bootstrap verification.

## Preparation tasks

1. Recover baseline, inspect required repository artifacts and available authentication/networking; verify current region/pricing evidence where accessible. Read-only GitHub Environment inspection only.
2. Implement a fail-closed, rerunnable Cloud Shell script targeting only project rakrao-dev/723651832622 and asia-southeast1, with dry-run default, explicit apply/cost/location gates, pre-mutation inventory, five least-privilege identities, narrow GitHub OIDC trust, default Firestore, one Docker repository with economical retention and scale-to-zero Cloud Run preflight service. Reuse Hosting; deploy only rules/indexes, never Auth providers. Do not delete existing data/resources or keys. SQL API cleanup is optional and conditional on verified zero instances/no dependencies, never forced.
3. Test safety behavior with mocked cloud CLI fixtures; independently review permissions, destructive/repeat behavior and guards. Report exact planned resources/costs and pending manual actions. Stop at execution gate. No application/workflow behavior changes absent a demonstrated blocker.

## Deferred B3–B6

Verify user-supplied bootstrap output and actual cloud state; then configure protected GitHub staging variables, explicitly enable and dispatch deployment of approved main, perform live controlled Auth/M2/M3 tests with dedicated records and preserve digest/revision/Hosting rollback evidence. Never request tokens, ADC files or private phone-test credentials. These stages remain pending until the manual boundary is satisfied.
