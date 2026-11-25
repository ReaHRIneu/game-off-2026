"""주인공(해커) 초상 96×96 v2 — 손으로 찍은 도트 격자 버전 (W-040 갈래 A, R2).

- 그림은 전부 문자 격자(한 글자 = 한 픽셀)다: 기본 격자 `grids/a_hacker_base.txt`(96×96) + 표정·부속 조각 `grids/a_hacker_expr.txt`.
  도형 함수(draw_poly·draw_ellipse·draw_line·pfill)와 자동 외곽선·자동 명암은 쓰지 않는다. 이 레시피가 하는 일은
  격자를 읽어 겹쳐 붙이는 것뿐이다(위치 이동·완성된 격자 위에 조각 덮기).
- 원화(2026-11-25)는 눈으로 보고 형태·색·식별점만 참고했다. 이 레시피는 원화를 열지 않는다. 원화의 그러데이션·가는 선·잔주름은
  옮기지 않고 이 게임의 도트 화풍(또렷한 색 덩어리, 명암 3단, 디더 없음)으로 찍었다.
- 식별점: ① 턱선 단발 ② 헤드셋 + 마이크 붐 + 늘어진 케이블(회색 테이프 띠) ③ 오버사이즈 남색 재킷 어깨선 ④ 청록 눈·LED ⑤ 반쯤 감긴 눈
- 방향: 화면 오른쪽을 본다(D-042). 표정 변화는 작게(D-053).
[시안] 사용자 확정 전. 기존 portrait_hacker.py 출력은 덮어쓰지 않고 hacker_v2_<표정>.png 로 저장.
"""
from artkit import PIX, contact_sheet, palette_check, save, upscale
from gridkit import GRIDS, grid_image, load_rows

N = 96

# 표정 → 덮을 조각 이름들(순서대로)
EXPRESSIONS = {
    "neutral":  ["eyeL4", "eyeR4", "browL_flat", "browR_flat", "mouth_flat"],
    "joy":      ["eyeL3", "eyeR3", "browL_flat", "browR_flat", "mouth_smile"],
    "sadness":  ["eyeL5", "eyeR5", "browL_inup", "browR_inup", "mouth_down"],
    "anger":    ["eyeL5", "eyeR5", "browL_indown", "browR_indown", "mouth_tight"],
    "fear":     ["eyeLw", "eyeRw", "browL_inup", "browR_inup", "mouth_open"],
    "surprise": ["eyeLw", "eyeRw", "browL_raised", "browR_raised", "mouth_o"],
    "disgust":  ["eyeL5", "eyeR4", "browL_indown", "browR_flat", "mouth_sneer"],
}
ALWAYS = ["nose", "mic", "cable"]          # 모든 표정에 붙는 부속 조각


def load_blocks(name):
    """`@이름 x y` + 격자 행들 → {이름: (x, y, 행 목록)}."""
    blocks, cur = {}, None
    for ln in (GRIDS / name).read_text(encoding="utf-8").splitlines():
        if ln.startswith("@"):
            nm, x, y = ln.split()
            nm = nm[1:]
            cur = (int(x), int(y), [])
            blocks[nm] = cur
        elif ln.strip() and not ln.lstrip().startswith("#") and cur is not None:
            cur[2].append(ln.replace(" ", "").replace("|", ""))
    for nm, (x, y, rows) in blocks.items():
        assert len({len(r) for r in rows}) == 1, (nm, [len(r) for r in rows])
    return blocks


def stamp(im, block):
    """조각을 위치만 맞춰 덮는다(`.` 칸은 건너뜀)."""
    x0, y0, rows = block
    px = im.load()
    for dy, r in enumerate(rows):
        for dx, ch in enumerate(r):
            if ch != ".":
                px[x0 + dx, y0 + dy] = PIX[ch]


def portrait(name, base=None, blocks=None, expressions=None):
    base = base or grid_image(load_rows("a_hacker_base.txt"), N)
    blocks = blocks or load_blocks("a_hacker_expr.txt")
    im = base.copy()
    for nm in (expressions or EXPRESSIONS)[name] + ALWAYS:
        stamp(im, blocks[nm])
    return im


def build():
    made, ims = [], {}
    base = grid_image(load_rows("a_hacker_base.txt"), N)
    blocks = load_blocks("a_hacker_expr.txt")
    for name in EXPRESSIONS:
        im = portrait(name, base, blocks)
        bad = palette_check(im)
        assert not bad, (name, bad)
        ims[name] = im
        made.append(save(im, f"portraits/hacker_v2_{name}.png",
                         {"status": "시안", "style": "pixel", "size": 96, "expression": name,
                          "note": "W-040 R2 손으로 찍은 도트 격자(원화 기준)"}))
    made.append(contact_sheet(made, 192, "preview/hacker_v2_expressions_sheet.png", cols=4))
    made.append(save(upscale(ims["neutral"], 4), "preview/hacker_v2_neutral_x4.png", {"status": "미리보기"}))
    return made
