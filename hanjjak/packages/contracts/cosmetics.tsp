using TypeSpec.Http;
namespace Hanjjak;

union CosmeticSlot { HEAD: "HEAD", TOP: "TOP", BOTTOM: "BOTTOM", GLOVES: "GLOVES", SHOES: "SHOES", CAPE: "CAPE" }
union RegistrationMode { ONE: "ONE", UNTIL_NEXT_STAR: "UNTIL_NEXT_STAR" }
union DrawCount { ONE: 1, TEN: 10 }
union CosmeticEffectUnit { BASIS_POINTS: "BASIS_POINTS", PERCENTAGE_POINTS: "PERCENTAGE_POINTS", SECONDS: "SECONDS" }
model CosmeticStatEffect { statId: string; value: int32; unit: CosmeticEffectUnit; }
model CosmeticInventoryState { cosmeticId: string; displayName: string | null; imageUrl: string | null; grade: string; slot: CosmeticSlot; registeredQuantity: int32; unregisteredQuantity: int32; reservedQuantity: int32; availableUnregisteredQuantity: int32; cosmeticStar: int32; nextStarThreshold: int32 | null; neededForNextStar: int32 | null; canUpgrade: boolean; upgradeDisabledReason: string | null; }
model CosmeticCollection { contentVersion: string; states: CosmeticInventoryState[]; equipment: Record<string | null>; uniqueRegisteredCount: int32; setStars: Record<int32>; setEffects: Record<CosmeticStatEffect[]>; totalEffects: CosmeticStatEffect[]; }
model CosmeticCatalogItem { cosmeticId: string; displayName: string | null; grade: string; slot: CosmeticSlot; setId: string; imageUrl: string | null; }
model CosmeticCatalogSet { setId: string; displayName: string | null; grade: string; members: Record<string>; effects: Record<CosmeticStatEffect[]>; }
model CosmeticsCatalog { contentVersion: string; cosmetics: CosmeticCatalogItem[]; sets: CosmeticCatalogSet[]; }
model PaymentEstimate { ticketCost: int64; riceCost: int64; executable: boolean; }
model CosmeticMilestone { totalSuccessfulDraws: int64; claimedBoxCount: int64; claimableBoxCount: int64; drawsUntilNextBox: int64; boxItemId: string; ownedBoxQuantity: int64; }
model BannerSummary { bannerId: string; setId: string; displayName: string | null; singleRiceCost: int64; ticketBalance: int64; riceBalance: int64; oneDraw: PaymentEstimate; tenDraw: PaymentEstimate; milestone: CosmeticMilestone; }
model CosmeticDefinitionView { cosmeticId: string; displayName: string | null; imageUrl: string | null; grade: string; slot: CosmeticSlot; setId: string; probabilityNumerator: int32; probabilityDenominator: int64; probabilityDisplay: string; }
model BannerDetail { banner: BannerSummary; gradeProbabilityMillionths: Record<int32>; pool: CosmeticDefinitionView[]; }
model DrawRequest { count: DrawCount; }
model RegistrationRequest { mode: RegistrationMode; }
model EquipmentRequest { cosmeticId: string | null; }
model DrawResult { cosmeticId: string; grade: string; isNew: boolean; }
model DrawResponse { bannerId: string; ticketCost: int64; riceCost: int64; results: DrawResult[]; collection: CosmeticCollection; }
model ClaimRequest { count: int64; }
model MilestoneResult { remainingClaimableCount: int64; }
model OpenBoxRequest { cosmeticId: string; }

@route("/api/v1/cosmetic-gacha/banners") interface CosmeticGacha {
  @get listBanners(): SuccessEnvelope<BannerSummary[]> | ErrorEnvelope;
  @get @route("/{bannerId}") bannerDetail(@path bannerId: string): SuccessEnvelope<BannerDetail> | ErrorEnvelope;
  @post @route("/{bannerId}/draws") draw(@path bannerId: string, @header("Idempotency-Key") key: Uuid, @body request: DrawRequest): SuccessEnvelope<DrawResponse> | ErrorEnvelope;
  @post @route("/{bannerId}/milestone-claims") claim(@path bannerId: string, @header("Idempotency-Key") key: Uuid, @body request: ClaimRequest): SuccessEnvelope<MilestoneResult> | ErrorEnvelope;
}
@route("/api/v1/cosmetic-selector-boxes") interface CosmeticSelectorBoxes {
  @post @route("/{boxItemId}/open") open(@path boxItemId: string, @header("Idempotency-Key") key: Uuid, @body request: OpenBoxRequest): SuccessEnvelope<CosmeticCollection> | ErrorEnvelope;
}
@route("/api/v1/cosmetics") interface Cosmetics {
  @get @route("/catalog") catalog(): SuccessEnvelope<CosmeticsCatalog> | ErrorEnvelope;
  @get @route("/collection") collection(): SuccessEnvelope<CosmeticCollection> | ErrorEnvelope;
  @post @route("/{cosmeticId}/registrations") register(@path cosmeticId: string, @header("Idempotency-Key") key: Uuid, @body request: RegistrationRequest): SuccessEnvelope<CosmeticCollection> | ErrorEnvelope;
  @patch @route("/equipment/{slotId}") equip(@path slotId: CosmeticSlot, @header("Idempotency-Key") key: Uuid, @body request: EquipmentRequest): SuccessEnvelope<CosmeticCollection> | ErrorEnvelope;
}
