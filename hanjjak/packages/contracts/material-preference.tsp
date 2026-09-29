using TypeSpec.Http;
namespace Hanjjak;

union MaterialType {
  POTATO: "POTATO",
  SWEET_POTATO: "SWEET_POTATO",
  CORN: "CORN"
}

model MaterialDropRates {
  POTATO: int32;
  SWEET_POTATO: int32;
  CORN: int32;
}

model MaterialOption {
  materialType: MaterialType;
  displayName: string;
  dropRates: MaterialDropRates;
}

model MaterialPreferenceState {
  selected: boolean;
  primaryMaterialType: MaterialType | null;
  options: MaterialOption[];
}

model MaterialPreferenceSelection {
  selected: boolean;
  primaryMaterialType: MaterialType;
  dropRates: MaterialDropRates;
}

model SelectMaterialPreferenceRequest {
  materialType: MaterialType;
}

@route("/api/v1/material-preference")
interface MaterialPreference {
  @get get(): SuccessEnvelope<MaterialPreferenceState> | ErrorEnvelope;
  @post select(@header("Idempotency-Key") idempotencyKey: Uuid, @body request: SelectMaterialPreferenceRequest): SuccessEnvelope<MaterialPreferenceSelection> | ErrorEnvelope;
}
