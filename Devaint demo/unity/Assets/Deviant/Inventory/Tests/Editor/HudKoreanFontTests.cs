using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Deviant.Inventory.Tests
{
    public sealed class HudKoreanFontTests
    {
        [TestCase("Galmuri11")]
        [TestCase("Galmuri11-Bold")]
        public void StaticPixelFontContainsChapterShopMapAndResultTerms(string face)
        {
            var font = Resources.Load<Font>("PixelInventory/Fonts/" + face);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.fontSize, Is.EqualTo(12), "Galmuri11 must be rasterized at its native 12px grid.");
            const string labels = "팔레트 구매 스탬프 교체 잉크 뽑기 판매 완료 구성 미정 강화 효과 준비 중 사망 처치 수 클리어 청록 황금 빨강 스크롤 확대 축소 도면 슬롯 충전 시계탑 집행자 II + - × →";
            foreach (char glyph in labels)
                if (!char.IsWhiteSpace(glyph)) Assert.That(font.GetCharacterInfo(glyph, out _, font.fontSize), Is.True, "Missing pixel glyph: " + glyph);
        }
        const string LiveText = "검 채찍 레이피어 한손 단검 망치 방패 대검 발사기 폭탄 수리검 가방 3 / 15 잉크 정비 장착 해제 획득 일시정지 닫기 숲 1챕터 · 확정 숲 / 들쥐 행동 확인 힘의 잉크 기본 공격 피해 +20% 10 → 12";
        [Test] public void ShippingAtlasContainsLiveKoreanAndAllDigitsWithoutTTFOrSystemFallback()
        {
            var font = Resources.Load<TMP_FontAsset>("UI/KoreanHUD");
            Assert.That(font, Is.Not.Null); Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(font.sourceFontFile, Is.Null, "Shipping HUD glyphs must work without an imported TTF or OS fallback.");
            Assert.That(font.HasCharacters(LiveText + "0123456789", out uint[] missing, false, false), Is.True,
                "Missing codepoints: " + string.Join(",", missing ?? new uint[0]));
            Assert.That(font.material.mainTexture, Is.SameAs(font.atlasTextures[0]));
            Assert.That(font.material.shader, Is.Not.Null);
            Assert.That(font.material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
        }
        [Test] public void KoreanTextMeshUsesRealGlyphsAndMatchingAtlasMaterial()
        {
            var canvas = new GameObject("Korean HUD probe canvas", typeof(RectTransform), typeof(Canvas));
            var go = new GameObject("Korean HUD glyph probe", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(canvas.transform, false);
            try
            {
                var text = go.AddComponent<TextMeshProUGUI>(); var font = Resources.Load<TMP_FontAsset>("UI/KoreanHUD");
                text.font = font; text.fontSharedMaterial = font.material;
                text.rectTransform.sizeDelta = new Vector2(800, 80); text.fontSize = 24; text.text = "검 가방 3 / 15";
                Canvas.ForceUpdateCanvases(); text.ForceMeshUpdate(true, true);
                Assert.That(text.textInfo.characterCount, Is.EqualTo(text.text.Length));
                foreach (var entry in text.textInfo.characterInfo)
                    if (entry.isVisible)
                    {
                        Assert.That(entry.textElement, Is.Not.Null);
                        Assert.That(entry.textElement.unicode, Is.EqualTo((uint)entry.character), "Replacement glyph rendered for " + entry.character);
                    }
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
