"""주인공(해커) 초상 96×96 v3 — pixelstudio 레시피(W-042, D-130). 7표정, 오른쪽을 봄(D-042), 표정 변화는 작게(D-053).

- 지난판(v2)에서 고친 점: 정수리 평평 → 둥근 머리 윤곽 / 머리끝 1px 톱니 → 큰 덩어리 / 재킷 밋밋 → 큰 디테일(지퍼·소매 주름·팔 주머니).
- 식별점 유지: 턱선 단발(보라 남색), 헤드셋 + 마이크 붐 + 늘어진 케이블, 오버사이즈 남색 봄버 재킷, 청록 눈.
- 원화는 눈으로 보고 참고만 했고 레시피는 열지 않는다. 색은 artkit.PIX 47색만. 광원 왼쪽 위.
[시안] 사용자 확정 전.
"""
import math

from PIL import Image

from ps_char import C, Sprite, new_layer, outline, palette_check, save, grid_blit

N = 96
# 표정: lid=윗눈꺼풀 가림(줄), wide=눈 키움, brow=(눈썹 y 이동, 기울기: +면 안쪽이 올라감), mouth=입 종류, eye_dy=눈 위치
EXPR = {
    "neutral":  dict(lid=2, wide=0, brow=(0, 0), mouth="flat"),
    "joy":      dict(lid=0, wide=0, brow=(-1, 0), mouth="smile", happy=True),
    "sadness":  dict(lid=3, wide=0, brow=(0, 2), mouth="down"),
    "anger":    dict(lid=3, wide=0, brow=(1, -2), mouth="tight"),
    "fear":     dict(lid=0, wide=1, brow=(-1, 2), mouth="open_s"),
    "surprise": dict(lid=0, wide=1, brow=(-2, 0), mouth="o"),
    "disgust":  dict(lid=3, wide=0, brow=(1, -1), mouth="sneer"),
}


def base(s=None, body=None):
    s = s or Sprite(N, N)
    # ---- 케이블은 맨 뒤(재킷 앞으로 늘어지는 건 나중에 한 번 더) ----
    # ---- 뒷머리: 둥근 단발 ----
    new_layer(s)
    s.ellipse(15, 8, 77, 66, C["H"])
    s.polygon([(15, 36), (13, 54), (17, 68), (26, 72), (34, 62)], C["H"])           # 왼쪽 옆머리 덩어리(끝 2개)
    s.polygon([(66, 40), (76, 44), (80, 58), (72, 68), (62, 64)], C["H"])            # 오른쪽 뒷머리 덩어리
    s.polygon([(18, 14), (30, 8), (46, 7), (36, 16), (22, 26)], C["U"], only=C["H"])   # 정수리 하이라이트
    s.polygon([(13, 50), (17, 68), (26, 72), (26, 62), (20, 56)], C["J"], only=C["H"])
    s.polygon([(62, 60), (72, 68), (80, 58), (74, 50)], C["J"], only=C["H"])
    outline(s)
    if body:
        body(s)
    else:
        # ---- 재킷(오버사이즈 남색 봄버) ----
        new_layer(s)
        s.polygon([(2, 95), (4, 80), (14, 72), (32, 68), (62, 68), (80, 72), (92, 80), (95, 95)], C["n"])
        s.polygon([(62, 68), (80, 72), (92, 80), (95, 95), (70, 95), (66, 78)], C["N"], only=C["n"])      # 오른쪽 어두운 면
        s.polygon([(4, 80), (14, 72), (30, 69), (24, 78), (12, 86)], C["2"], only=C["n"])                  # 왼쪽 어깨 빛
        s.polygon([(8, 82), (20, 76), (24, 86), (12, 92)], C["n"])
        # 칼라(리브) + 검정 티셔츠
        s.polygon([(33, 68), (63, 68), (66, 78), (58, 94), (50, 94), (44, 80), (40, 94), (30, 94), (28, 78)], C["N"])
        s.polygon([(44, 72), (54, 72), (58, 94), (50, 94)], C["0"])
        s.line(36, 70, 42, 82, C["2"]); s.line(37, 70, 43, 81, C["2"])
        s.line(62, 70, 58, 80, C["N"])
        # 큰 디테일: 어깨 솔기 / 소매 주름 / 팔 주머니 / 지퍼
        s.polygon([(4, 80), (14, 72), (32, 69), (26, 80), (14, 88), (6, 90)], C["2"], only=C["n"])        # 왼쪽 위 밝은 면
        s.polygon([(8, 82), (16, 74), (28, 71), (24, 78), (14, 84)], C["3"], only=C["2"])
        s.line(15, 74, 24, 90, C["N"]); s.line(80, 74, 72, 90, C["N"])                                      # 어깨 솔기
        s.line(44, 82, 38, 95, C["3"]); s.line(45, 82, 39, 95, C["2"])
        s.px(42, 87, C["c"]); s.px(42, 88, C["c"])                                                           # 지퍼 끝 LED
        s.line(10, 90, 20, 87, C["N"]); s.line(9, 94, 22, 90, C["N"])                                       # 소매 주름
        s.polygon([(72, 84), (86, 86), (87, 95), (73, 95)], C["N"])                                         # 팔 주머니
        s.line(72, 85, 86, 87, C["2"]); s.px(80, 89, C["3"])
        for yy in (69, 71, 73):                                                                              # 깃 리브(가로 줄)
            s.line(32, yy, 40, yy + 1, C["n"]); s.line(56, yy + 1, 64, yy, C["n"])
        outline(s)
        # ---- 목 ----
        new_layer(s)
        s.polygon([(40, 58), (56, 58), (58, 70), (44, 74), (38, 70)], C["S"])
        s.polygon([(46, 58), (56, 58), (58, 70), (48, 72)], C["s"], only=C["S"])
        outline(s)
    # ---- 얼굴: 턱이 오른쪽 아래로 좁아짐 ----
    new_layer(s)
    s.polygon([(30, 36), (70, 34), (73, 44), (73, 52), (68, 62), (60, 70), (50, 73), (42, 70), (34, 60), (30, 48)], C["S"])
    s.px(74, 52, C["S"]); s.px(74, 53, C["S"])                                       # 코끝
    s.polygon([(30, 38), (34, 38), (34, 58), (30, 48)], C["s"], only=C["S"])           # 옆머리 그림자
    s.line(70, 56, 63, 66, C["s"]); s.line(70, 57, 64, 67, C["s"]); s.line(62, 69, 54, 72, C["s"])   # 볼·턱 아래 그림자(광원 왼쪽 위)
    s.px(70, 55, C["s"]); s.px(71, 56, C["s"])                                         # 코 그림자
    s.polygon([(38, 52), (42, 52), (44, 56), (38, 58)], C["l"], only=C["S"])           # 볼 하이라이트
    outline(s)
    # ---- 앞머리: 오른쪽으로 쓸린 큰 덩어리 3개(끝이 둥글고 비대칭) + 옆머리 ----
    new_layer(s)
    s.ellipse(16, 9, 77, 36, C["H"])
    s.ellipse(24, 20, 46, 38, C["H"])           # 덩어리 1(크고 낮음)
    s.ellipse(40, 18, 62, 35, C["H"])           # 덩어리 2
    s.polygon([(58, 20), (76, 26), (78, 40), (72, 41), (66, 34)], C["H"])     # 덩어리 3(오른쪽 관자로 쓸림)
    s.polygon([(20, 34), (34, 36), (34, 46), (32, 58), (28, 68), (18, 68), (14, 54)], C["H"])   # 왼쪽 옆머리
    s.polygon([(68, 38), (77, 40), (79, 56), (72, 66), (70, 52)], C["H"])                       # 오른쪽 옆머리
    s.polygon([(18, 14), (30, 8), (46, 7), (38, 15), (24, 26)], C["U"], only=C["H"])
    s.polygon([(32, 20), (44, 21), (40, 28), (30, 30)], C["U"], only=C["H"])                   # 덩어리 1 하이라이트
    s.polygon([(24, 33), (36, 38), (44, 37), (30, 30)], C["J"], only=C["H"])                   # 덩어리 밑 그림자
    s.polygon([(46, 33), (60, 35), (62, 33), (50, 30)], C["J"], only=C["H"])
    s.line(52, 19, 48, 30, C["J"]); s.line(68, 26, 72, 36, C["J"])
    s.polygon([(13, 54), (17, 68), (26, 72), (24, 60)], C["J"], only=C["H"])
    outline(s)
    return s


def headset(s):
    # 밴드: 머리 위 둥근 호
    new_layer(s)
    pts = []
    for a in range(200, 300, 4):
        r = math.radians(a)
        pts.append((round(47 + 33 * math.cos(r)), round(36 + 31 * math.sin(r))))
    for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
        s.line(x0, y0, x1, y1, C["3"]); s.line(x0, y0 + 1, x1, y1 + 1, C["3"]); s.line(x0, y0 + 2, x1, y1 + 2, C["2"])
    outline(s)
    # 가까운 쪽 이어컵
    new_layer(s)
    s.ellipse(10, 36, 30, 62, C["2"])
    s.ellipse(12, 38, 28, 60, C["3"])
    s.ellipse(15, 41, 28, 60, C["2"], only=C["3"])
    s.rect(15, 44, 22, 55, C["g"]); s.rect(16, 45, 21, 54, C["T"]); s.rect(16, 45, 18, 50, C["c"])
    s.rect(14, 38, 20, 39, C["W"], only=C["3"])
    outline(s)
    # 마이크 붐(이어컵 아래 → 입 앞) + 케이블
    new_layer(s)
    for (x0, y0, x1, y1) in ((18, 60, 24, 70), (24, 70, 36, 76), (36, 76, 48, 76), (48, 76, 56, 73)):
        s.line(x0, y0, x1, y1, C["3"]); s.line(x0, y0 + 1, x1, y1 + 1, C["2"])
    s.ellipse(54, 69, 61, 76, C["3"]); s.rect(57, 71, 60, 74, C["c"])
    outline(s)
    new_layer(s)
    cx = 17
    for y in range(64, 95):
        x = cx + round(3 * math.sin((y - 64) / 5.0)) + (y - 64) // 6
        s.px(x, y, C["3"]); s.px(x + 1, y, C["2"])
    return s


def eye(s, ex, ey, w, wide, lid, look=1):
    """눈: 위 속눈썹 굵은 선(K) + 흰자 + 청록 홍채 2단 + 하이라이트 1px. lid=위에서 가린 줄 수."""
    top = ey - wide
    rows = 6 + wide
    s.rect(ex - 1, top - 1, ex + w, top, C["K"])                         # 굵은 윗선(2줄)
    s.px(ex - 2, top, C["K"]); s.px(ex + w + 1, top - 1, C["K"])
    for r in range(rows):
        s.rect(ex, top + 1 + r, ex + w - 1, top + 1 + r, C["W"])
    iw = w - 4
    ix = ex + 2 + look
    for r in range(rows):
        col = C["T"] if r < rows - 2 else C["c"]
        s.rect(ix, top + 1 + r, ix + iw - 1, top + 1 + r, col)
    s.rect(ix + 1, top + 2, ix + iw - 2, top + 4, C["v"])                 # 눈동자
    s.px(ix, top + 1, C["W"]); s.px(ix + 1, top + 1, C["W"]) if iw > 4 else None   # 하이라이트
    for r in range(lid):                                                 # 윗눈꺼풀(반쯤 감김)
        s.rect(ex - 1, top + 1 + r, ex + w, top + 1 + r, C["s"] if r == lid - 1 else C["S"])
    if lid:
        s.rect(ex - 1, top + lid + 1, ex + w, top + lid + 1, C["K"]) if False else None
    s.px(ex, top + rows + 1, C["s"]); s.px(ex + w - 1, top + rows + 1, C["s"])


def face(s, name):
    e = EXPR[name]
    new_layer(s)
    ey = 44
    wide = e["wide"]
    NX, NW, FX, FW = 39, 12, 58, 11
    if e.get("happy"):
        for ex0, wd in ((NX, 11), (FX, 10)):                              # 웃는 눈: 폭 9~10px 호
            for i in range(wd):
                y = ey + 2 - round(2.4 * math.sin(math.pi * i / (wd - 1)))
                s.px(ex0 + i, y + 2, C["K"]); s.px(ex0 + i, y + 3, C["K"]) if i in (0, wd - 1) else None
            s.rect(ex0 + 2, ey + 5, ex0 + wd - 3, ey + 5, C["c"])
    else:
        eye(s, NX, ey, NW, wide, e["lid"])
        eye(s, FX, ey, FW, wide, e["lid"], look=0)
    dy, tilt = e["brow"]
    by = 40 + dy
    for i in range(NW):          # 가까운 눈썹: 오른쪽 끝이 안쪽
        s.px(NX + i, by - round(tilt * i / (NW - 1.0)), C["K"])
    for j in range(FW):          # 먼 눈썹: 왼쪽 끝이 안쪽
        s.px(FX + j, by - round(tilt * (FW - 1 - j) / (FW - 1.0)), C["K"])
    # 코·입
    m = e["mouth"]
    mx, my = 56, 63
    if m == "flat":
        s.rect(mx, my, mx + 6, my, C["m"]); s.px(mx - 1, my, C["s"])
    elif m == "smile":
        grid_blit(s, mx - 2, my - 1, ["K.....K", ".KKKKK.", "..WWW.."])
        s.px(mx, my, C["m"])
    elif m == "down":
        s.line(mx, my + 1, mx + 6, my + 1, C["m"]); s.px(mx - 1, my + 2, C["m"]); s.px(mx + 7, my + 2, C["m"])
    elif m == "tight":
        s.rect(mx, my, mx + 6, my, C["K"]); s.px(mx + 2, my + 1, C["m"])
    elif m == "open_s":
        s.rect(mx, my - 1, mx + 5, my + 2, C["K"]); s.rect(mx + 1, my, mx + 4, my + 1, C["m"])
    elif m == "o":
        s.rect(mx + 1, my - 1, mx + 4, my + 3, C["K"]); s.rect(mx + 2, my, mx + 3, my + 2, C["m"])
    elif m == "sneer":
        s.line(mx, my + 1, mx + 6, my, C["K"]); s.px(mx + 6, my - 1, C["K"]); s.px(mx + 3, my + 1, C["W"])
    if name == "fear":
        s.px(37, 46, C["c"]); s.px(37, 47, C["c"]); s.px(37, 48, C["W"])
    return s


def portrait(name):
    s = base()
    headset(s)
    face(s, name)
    return s


def build():
    made, ims = [], []
    for name in EXPR:
        s = portrait(name)
        im = s.composite()
        bad = palette_check(im)
        assert not bad, (name, bad)
        assert all(a in (0, 255) for a in im.getchannel("A").getdata()), name
        ims.append(im)
        made.append(save(im, f"portraits/hacker_v3_{name}.png",
                         {"status": "시안", "style": "pixel", "size": 96, "expression": name,
                          "note": "W-042 pixelstudio 레시피(D-130)"}))
        if name == "neutral":
            s.save_silhouette("Art/Export/preview/w042_hacker_silhouette.png", scale=5)
    board = Image.new("RGBA", (96 * 4 * 4, 96 * 2 * 4), (200, 200, 200, 255))
    for k, im in enumerate(ims):
        big = im.resize((384, 384), Image.NEAREST)
        board.alpha_composite(big, ((k % 4) * 384, (k // 4) * 384))
    board.save("Art/Export/preview/w042_hacker_board.png")
    ims[0].resize((576, 576), Image.NEAREST).save("Art/Export/preview/w042_hacker_neutral_x6.png")
    return made
