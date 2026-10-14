#!/usr/bin/env bash
set -euo pipefail
rakrao_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$rakrao_root"
dotnet restore RakRao.sln
dotnet build RakRao.sln --no-restore
npm ci --prefix infra/firebase
infra/firebase/node_modules/.bin/firebase emulators:exec --project demo-rakrao --only auth,firestore \
  'dotnet test RakRao.sln --no-build --no-restore && npm --prefix infra/firebase run test:rules'
