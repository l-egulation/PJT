#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/deployment-common.sh"

: "${ROLLBACK_IMAGE_TAG:?ROLLBACK_IMAGE_TAG must identify a previously verified image set}"
validate_release "$ROLLBACK_IMAGE_TAG"
begin_operation rollback
(( previous_api_replicas > 0 )) || die "API is stopped for maintenance; use an explicit deployment to resume"
restore_annotated_offline_policy
apply_release "$ROLLBACK_IMAGE_TAG"
printf 'Application rollback succeeded: image=%s web-slot=%s; PostgreSQL was not restored\n' "$ROLLBACK_IMAGE_TAG" "$inactive_slot"
