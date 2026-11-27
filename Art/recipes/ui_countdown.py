"""카운트다운 패널 그림(W-051) — 추적도 최대 때 장면 뷰 오른쪽 위 안쪽에 뜨는 패널.

- ui_mvp.py 의 ui/panel 과 같은 규격: 1픽셀 = 논리 1px(게임 안에서 4배 확대), 9-slice, 공유 팔레트 PIX, 알파 0/255
- 어두운 바탕(a) + 붉은 1px 테두리(r) + 모서리 꺾쇠(R) + 위쪽 라벨 띠(K, 14px). 라벨 글자는 언어마다 달라 코드가 그린다(game.countdown_label)
- 숫자는 코드가 7-세그먼트로 그린다(그림 없음)
[시안] 사용자 확정 전.
"""
from artkit import PIX, canvas, palette_check, pbox, pfill, pset, save, upscale
from pixfont import draw_text

BAND = 14       # 위쪽 라벨 띠 높이(9-slice 위 테두리, 한글 글꼴 높이까지 들어가게)
BORDER = [4, BAND, 4, 4]


def countdown_panel(w=16, h=26):
    im = canvas(w, h, PIX["a"])
    pbox(im, 0, 0, w - 1, h - 1, "K")                 # 바깥 외곽선
    pbox(im, 1, 1, w - 2, h - 2, "r")                 # 붉은 1px 테두리
    pfill(im, 2, 2, w - 3, BAND - 2, "K")             # 라벨 띠 바탕
    pfill(im, 2, BAND - 1, w - 3, BAND - 1, "r")      # 띠 아래 선
    # 모서리 꺾쇠: 테두리 안쪽으로 3px씩 밝은 붉은색
    for x0, y0, dx, dy in [(1, 1, 1, 1), (w - 2, 1, -1, 1), (1, h - 2, 1, -1), (w - 2, h - 2, -1, -1)]:
        for i in range(3):
            pset(im, x0 + dx * i, y0, "R")
            pset(im, x0, y0 + dy * i, "R")
    return im


def build():
    im = countdown_panel()
    assert not palette_check(im)
    made = [save(im, "ui/countdown_panel.png", {"status": "시안", "style": "pixel", "nine_slice_LTRB": BORDER})]
    # 검수용 미리보기: 68x44 로 늘리고 라벨·숫자 자리를 흉내 낸다(실제 숫자는 코드)
    from PIL import Image
    big = canvas(68, 46, (0, 0, 0, 0))
    W, H = 68, 46
    l, t, r, b = BORDER
    src = im
    def blit(sx0, sy0, sx1, sy1, dx0, dy0, dx1, dy1):
        piece = src.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0), Image.NEAREST)
        big.alpha_composite(piece, (dx0, dy0))
    sw, sh = src.size
    xs = [(0, l, 0, l), (l, sw - r, l, W - r), (sw - r, sw, W - r, W)]
    ys = [(0, t, 0, t), (t, sh - b, t, H - b), (sh - b, sh, H - b, H)]
    for sx0, sx1, dx0, dx1 in xs:
        for sy0, sy1, dy0, dy1 in ys:
            blit(sx0, sy0, sx1, sy1, dx0, dy0, dx1, dy1)
    draw_text(big, (W - 53) // 2, 4, "TRACE MAX", "R")
    for dx in (14, 31 + 14 - 3):
        pfill(big, dx, 19, dx + 11, 38, "r")
    made.append(save(upscale(big, 8), "preview/ui_countdown_x8.png", {"status": "목업 8배"}))
    return made
