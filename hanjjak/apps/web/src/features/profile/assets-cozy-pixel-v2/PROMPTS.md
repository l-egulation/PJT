# Cozy Pixel v2 image-generation prompts

All generated images used the built-in image generation tool. The supplied UI captures were style and composition references only. Assets from earlier profile directories were not used as references or inputs.

## Shared prompt

> Production-ready cozy 16-bit pixel-art game UI asset. Use crisp stepped pixel clusters, warm ivory paper, oatmeal beige, dark cocoa outlines, coral interaction accents, cyan progress accents, and restrained golden highlights. Keep UI centers calm enough for HTML text. Do not bake text, labels, numbers, watermarks, medieval metal, ornate gold trim, or unrelated scene objects into the asset. When requested, the area outside the isolated asset must be genuine transparent alpha with no halo.

## Asset-specific prompt set

| Asset | Final prompt delta |
| --- | --- |
| `modal-paper-frame-9slice.png` | Wide blank warm-ivory paper panel with irregular 1–4 pixel-looking torn notches, subtle beige fibers, intact corners, transparent exterior, and no shadow. |
| `paper-texture-tile.png` | Quiet square handmade-paper surface with sparse fibers and faint mottling, no border or focal stain; seamless on all four edges. |
| `primary-button-9slice.png` | Friendly coral-red action button with a lighter top plane, darker coral lower edge, and thin cocoa outline; no label. |
| `close-icon-32.png` | One bold dark-cocoa X made from two clean stepped diagonal strokes, no surrounding button. |
| `sprout-icon-32.png` | Small asymmetrical two-leaf olive sprout with a short curved stem and strong 32 px silhouette. |
| `section-check-icon-32.png` | Coral outline square checkbox with a confident coral check slightly overlapping the upper-right. |
| `accent-rays-coral-v2.png` | Exactly two detached coral-red pixel brush strokes matching the supplied crop, tightly framed on transparent alpha. |
| `accent-rays-yellow-v2.png` | Exactly two detached warm yellow-orange pixel brush strokes matching the supplied crop, tightly framed on transparent alpha. |
| `profile-info-icons-v3.png` | Fixed 5 x 1 transparent sprite sheet: solid person bust, outlined envelope, two-leaf sprout, solid five-point star, bright golden coin containing one uppercase S. Each icon fills 78-84% of its cell. |
| `bookmark-tab-red.png` | Single blank 3:1 horizontal side bookmark in muted coral/dusty rose, with a shallow inward V-notch at the page-side edge, cocoa stepped outline, restrained fabric-paper grain, and genuine transparent exterior. |
| `bookmark-tab-blue.png` | Exact color variant of the red bookmark in eye-comforting dusty blue around `#6F9EAA`; preserve silhouette, crop, texture, outline, blank center, and transparent alpha. A final background-extraction pass removed the generated black matte without altering the tab. |
| `edit-pencil-icon.png` | One short diagonal wooden pencil in muted coral red, warm tan, and cocoa brown; bold 16-bit silhouette, crisp stepped clusters, tightly framed on genuine transparent alpha, readable at 24–30 CSS pixels, with no button, text, scenery, or emoji rendering. |

## Deterministic source-derived assets

- `leaf-watermark.png` was derived from the new v2 sprout icon, recolored to muted paper beige, and reduced to 28% alpha.

Generated checkerboard mattes, enclosed black matte pixels, and unwanted glow were removed during alpha validation. The paper tile was assembled as mirrored quadrants so opposite edges match exactly.

The v2 two-stroke accents and `profile-info-icons-v3.png` were generated with the built-in image tool from the supplied crops, then normalized onto transparent 32 x 32 accent canvases and five equal 128 x 128 icon cells. The superseded three-ray accents and padded icon sheets were removed after their source references reached zero.

The bookmark pair was generated as isolated production UI assets with the built-in image tool. The red asset established the shared silhouette; the blue asset used that image as its edit input so both states keep the same proportions and pixel detail.
