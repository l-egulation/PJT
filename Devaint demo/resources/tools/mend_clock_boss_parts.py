"""Fill the approved preview parts' cut edges in a separate authoring output.

The Unity test room bobs every part on its own slight delay, so seams that the single picture hid now
open up: chain remnants baked into the fist tops and spire, chains that stop exactly where the next
part starts, rim tops with no outline, a light arc around the dial hole and flat-cut drips between the
body and the lower ink. Only the parts' copies are changed; the preview originals stay as they are.
Every filled pixel reuses a colour already in the part, so the palette checks still hold.
"""
import numpy as np
from PIL import Image

OUTLINE = (0x17, 0x17, 0x16)
DIAL_CENTRE = (192.5, 247.5)  # dial.png circle; its stones end at radius 51.5


def _rgba(image):
    return np.array(image.convert('RGBA'))


def _is_outline(a):
    return (a[:, :, 3] > 0) & np.all(a[:, :, :3] == OUTLINE, axis=2)


def _put(a, y, x, colour):
    a[y, x, :3] = colour
    a[y, x, 3] = 255


def _drop_specks(a, smallest=5):
    """Remove stray clusters of a few pixels left over from the cut."""
    from scipy import ndimage
    labels, count = ndimage.label(a[:, :, 3] > 0, structure=np.ones((3, 3)))
    for i in range(1, count + 1):
        if (labels == i).sum() < smallest:
            a[labels == i] = 0


def _side_chain(a, centre, top, dx):
    """Clear the splayed remnant under the collar and hang real links there instead: one length of the
    centre chain (edge-on link, then a face link), which carries on behind the fist top."""
    a[top + 4:] = 0
    length = centre[61:95]  # starts on an edge-on link, the same view as the stem under the collar
    for y in range(length.shape[0]):
        for x in np.nonzero(length[y, :, 3] > 0)[0]:
            a[top + y, x + dx] = length[y, x]


def _centre_chain(a, period=23, extra=13):
    """Continue the hanging chain one more link down so the spire covers its end."""
    bottom = np.nonzero(a[:, :, 3].any(axis=1))[0].max() + 1
    a[bottom:bottom + extra] = a[bottom - period:bottom - period + extra]


def _outline_tops(a, top, bottom, left=0, right=None):
    """Give every column whose highest pixel is not outline an outline pixel above it."""
    right = a.shape[1] if right is None else right
    outline = _is_outline(a)
    for x in range(left, right):
        opaque = np.nonzero(a[top:bottom, x, 3] > 0)[0]
        if len(opaque) and not outline[top + opaque[0], x] and top + opaque[0] > 0:
            _put(a, top + opaque[0] - 1, x, OUTLINE)


def _spire(a, cut_bottom=104):
    """Drop the chain bits baked into the spire tip and close the apex with outline."""
    a[:cut_bottom] = 0
    _outline_tops(a, cut_bottom, cut_bottom + 8)


def _dial_hole(a):
    """Paint over the light arc that rings the dial hole just outside the dial stones."""
    outline = _is_outline(a)
    cx, cy = DIAL_CENTRE
    for y in range(200, 243):
        for x in range(int(cx) - 55, int(cx) - 20):
            r = np.hypot(x - cx, y - cy)
            if a[y, x, 3] and not outline[y, x] and 51 <= r < 54 and outline[y - 1:y + 2, x - 1:x + 2].sum() >= 6:
                _put(a, y, x, OUTLINE)


def _underlay(target, source, rows, cols):
    """Fill the target's empty pixels with ink black where the source has pixels in a band, so the two
    still overlap when they move apart. Black only: a copied grey drip would show twice once offset."""
    y0, y1 = rows
    x0, x1 = cols
    band = target[y0:y1, x0:x1]
    fill = (band[:, :, 3] == 0) & (source[y0:y1, x0:x1, 3] > 0)
    band[fill] = (*OUTLINE, 255)


def mend(parts):
    """parts: id -> PIL image (384x416, shared pivot). Returns mended copies of the same ids."""
    a = {k: _rgba(v) for k, v in parts.items()}
    for side in ('chain_left', 'chain_right'):
        _drop_specks(a[side])
    _side_chain(a['chain_left'], a['chain_center'], 242, -100)
    _side_chain(a['chain_right'], a['chain_center'], 242, 100)
    _centre_chain(a['chain_center'])
    for fist in ('fist_left', 'fist_right'):
        _outline_tops(a[fist], 250, 268)
    body, ink = a['body'], a['ink_lower']
    _spire(body)
    _dial_hole(body)
    # Body drips and the lower ink were cut flat at the same line; each now continues 12 px into the other.
    _underlay(body, ink, (300, 316), (130, 255))
    _underlay(ink, body, (290, 302), (138, 247))
    _outline_tops(ink, 288, 302, 138, 247)
    out = {k: Image.fromarray(v) for k, v in a.items()}
    # body_merged (collapse pieces) is body + lower ink in one; rebuild it from the mended pair.
    merged = Image.new('RGBA', parts['body_merged'].size)
    merged.alpha_composite(out['body'])
    merged.alpha_composite(out['ink_lower'])
    out['body_merged'] = merged
    return out
