#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/deployment-common.sh"

: "${IMAGE_TAG:?IMAGE_TAG is required}"
validate_release "$IMAGE_TAG"
begin_operation backfill
[[ "$previous_api_image" == "docker.io/hanjjak/game-api:$IMAGE_TAG" ]] || die "game-api deployment is not serving IMAGE_TAG; deploy the release before retrying backfill"
[[ "${POSTGRES_DB:-$configured_db}" == "$configured_db" ]] || die "POSTGRES_DB differs from bootstrap; migrate database configuration separately"
[[ "${POSTGRES_USER:-$configured_user}" == "$configured_user" ]] || die "POSTGRES_USER differs from bootstrap; migrate database configuration separately"
"${KUBECTL[@]}" rollout status statefulset/postgres --timeout=300s
job_suffix="$(date -u +%Y%m%d%H%M%S)-$$"
backfill_job="backfill-$job_suffix"
render_manifest backfill-job.yaml --image-tag "$IMAGE_TAG" --job-name "$backfill_job" \
  | "${KUBECTL[@]}" create -f - >/dev/null
wait_completion "job/$backfill_job" 900
release_changed=0
printf 'Progression backfill retry succeeded: image=%s\n' "$IMAGE_TAG"
