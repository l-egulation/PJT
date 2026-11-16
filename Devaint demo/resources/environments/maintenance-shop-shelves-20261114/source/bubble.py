"""장인 말풍선: 예전(11-13 UI 테마 적용 전) MaintenanceBubbleView 그대로.
흰 바탕, 먹 외곽선 계단 모서리, 갈무리11 굵게 12px, 줄 15px, 너비 184, 이름표는 먹 바탕에 흰 이름.
위치는 장인 발밑 + (0.5칸, 3.75칸) 머리점 위 10px, 꼬리는 머리점.
"""
import math
from PIL import Image, ImageDraw, ImageFont
import mock as m

GAL = m.FONTS / 'Inventory/Resources/PixelInventory/Fonts/Galmuri11-Bold.ttf'
FONT = ImageFont.truetype(str(GAL), 12)
FILL = (255, 255, 255, 255)
BODY_W, LINE = 184, 15


def width(t):
    return ImageDraw.Draw(Image.new('L', (1, 1))).textlength(t, font=FONT)


def wrap(text):
    lines = []
    for para in text.split(chr(10)):
        line = ''
        for word in para.split(' '):
            nxt = (line + ' ' + word) if line else word
            if width(nxt) <= BODY_W: line = nxt; continue
            if line: lines.append(line)
            line = ''
            for ch in word:
                if line and width(line + ch) > BODY_W: lines.append(line); line = ''
                line += ch
        if line: lines.append(line)
    return lines


def size(text):
    lines = wrap(text)
    return math.ceil(max(width(l) for l in lines)) + 20, len(lines) * LINE + 18


def show(c, text, speaker, head, canvas_w, top=53, sketch=True):
    ink = (36, 35, 29, 255) if sketch else (31, 27, 25, 255)
    w, h = size(text); hx, hy = head
    x = round(min(max(hx - w / 2, 8), canvas_w - 8 - w)); y = round(max(top, hy - h - 10))
    tail = round(min(max(hx - x, 12), w - 12))
    lay = Image.new('RGBA', c.size, (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    def box(bx, by, bw, bh, col):
        if bw > 0 and bh > 0: d.rectangle((x + bx, y + by, x + bx + bw - 1, y + by + bh - 1), fill=col)
    box(2, 0, w - 4, h, ink); box(0, 2, w, h - 4, ink); box(1, 1, w - 2, h - 2, ink); box(3, 2, w - 6, h - 4, FILL); box(2, 3, w - 4, h - 6, FILL)
    for r in range(7): box(tail - (14 - 2 * r) // 2, h - 2 + r, 14 - 2 * r, 1, ink)
    for r in range(5): box(tail - (10 - 2 * r) // 2, h - 2 + r, 10 - 2 * r, 1, FILL)
    tag = math.ceil(width(speaker)) + 12
    box(6, -14, tag, 16, ink); box(5, -13, tag + 2, 14, ink)
    c.alpha_composite(lay); d = ImageDraw.Draw(c)
    d.text((x + 12, y - 12), speaker, font=FONT, fill=FILL)
    for i, line in enumerate(wrap(text)): d.text((x + 10, y + 9 + i * LINE), line, font=FONT, fill=ink)
    return x, y, w, h
