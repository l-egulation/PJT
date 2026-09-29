# Cozy Pixel v2 profile assets

This directory is the only asset source for the new cozy-pixel profile modal.
Do not import or copy profile UI assets from earlier asset directories.

## Asset register

| Asset | Native size | Alpha | Usage notes |
| --- | ---: | --- | --- |
| `modal-paper-frame-9slice.png` | 1417 x 858 | yes | Main paper modal. Start 9-slice trials at 64 px per side and keep the center filled. |
| `paper-texture-tile.png` | 1254 x 1254 | no | Low-contrast paper surface for small floating detail surfaces. The main modal center comes from the paper frame itself. |
| `primary-button-9slice.png` | 1893 x 432 | yes | Coral primary action. Start at 112 px per side. |
| `close-icon-32.png` | 32 x 32 | yes | Modal close mark. |
| `sprout-icon-32.png` | 32 x 32 | yes | Character title marker. |
| `section-check-icon-32.png` | 32 x 32 | yes | Checked section marker. |
| `accent-rays-coral-v2.png` | 32 x 32 | yes | Two-stroke coral section-title emphasis matched to the approved capture. |
| `accent-rays-yellow-v2.png` | 32 x 32 | yes | Two-stroke golden section-title emphasis matched to the approved capture. |
| `profile-info-icons-v3.png` | 640 x 128 | yes | 5 x 1 sprite: person, mail, sprout, star, S rice coin. Each icon fills a 128 x 128 cell tightly. |
| `bookmark-tab-red.png` | 2159 x 728 | yes | Active left-side page bookmark. Keep the blank center available for HTML text and scale the full silhouette as one image. |
| `bookmark-tab-blue.png` | 2157 x 729 | yes | Inactive left-side page bookmark. Muted dusty blue variant with the same visual weight as the active tab. |
| `edit-pencil-icon.png` | 1254 x 1254 | yes | Coral pixel pencil used by the inline nickname edit control. Render at 26 px with pixelated scaling. |
| `Jua-Regular.ttf` | font | n/a | Bundled rounded Korean display/body font for the profile modal. |
| `OFL-Jua.txt` | license | n/a | SIL Open Font License text for the bundled Jua font. |
| `leaf-watermark.png` | 160 x 160 | yes | Low-opacity paper watermark. |

## Rendering rules

- Keep pixel edges crisp (`image-rendering: pixelated`) when the asset is scaled.
- Preserve transparent outer pixels; do not flatten the image onto a matte color.
- Render copy, values, tables, separators, and layout in HTML/CSS rather than baking them into raster assets.
- Use the red and blue bookmark assets as complete button backgrounds; do not recreate their notch, outline, or paper/fabric grain with CSS shapes.
- Treat the listed 9-slice values as integration starting points and verify them at the final component size.
