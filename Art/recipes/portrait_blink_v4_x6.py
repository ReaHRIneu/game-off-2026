"""초상 눈 깜빡임 v4 — 닫힌 눈 프레임 14+14장(W-050 §1-1, D-052·D-053). 64x64.

- 원본 표정 그림(`portrait_hacker_v4_x6`·`portrait_runner_v4_x6`가 만든 그림)은 바꾸지 않는다.
  같은 레시피 함수로 표정 그림을 메모리에서 다시 만들고, 눈 부분(상자 안의 눈 색 픽셀)만
  피부색으로 덮은 뒤 아래 눈꺼풀 자리에 속눈썹 선을 1px 긋는다. 눈 밖은 한 픽셀도 바꾸지 않는다.
- 확대·축소 없음. 알파 0/255, 새 색 없음(그림 안의 색만).
- 출력: portraits/hacker_v4_x6_<표정>_blink.png, portraits/runner_v4_x6_<표정>_blink.png
[시안] 사용자 확정 전.
"""
from pathlib import Path

from artkit import save
import portrait_hacker_v4_x6 as H
import portrait_runner_v4_x6 as Rn

REPO = Path(__file__).resolve().parents[2]
EXPRS = H.EXPRS

# 눈 색(눈 상자 안에서 피부색으로 덮을 색) — 그림에서 읽은 값
H_EYE = {
    (11, 8, 10), (98, 35, 54), (6, 55, 83), (195, 191, 223), (148, 66, 72), (251, 252, 250),
    (39, 123, 155), (22, 92, 122), (129, 211, 240), (85, 166, 196), (15, 12, 17),
}
R_EYE = {
    (253, 252, 251), (32, 66, 70), (145, 162, 198), (55, 104, 106), (119, 178, 168), (84, 133, 127),
    (138, 204, 188), (180, 186, 206), (115, 131, 166), (28, 25, 25), (24, 22, 20),
}
# 눈 상자: (x0, y0, x1, y1, 속눈썹도 덮기, 줄 y, 덮을 추가 칸). 뒤집은 뒤 좌표, 양끝 포함.
# 표정별 y0 덮어쓰기(눈썹이 속눈썹과 같은 색이라 눈썹을 지우지 않게)는 Y0_BY_EXPR.
H_BOXES = [
    (32, 24, 40, 31, True, 29, []),
    (43, 26, 46, 29, False, 28, []),
]
R_BOXES = [
    (29, 23, 34, 30, True, 28, []),
    (28, 25, 28, 29, False, 28, []),
    (40, 25, 45, 30, False, 28, [(42, 25), (43, 25), (44, 25)]),
]
Y0_BY_EXPR = {"runner": {"anger": 24, "disgust": 24, "surprise": 22}}   # 첫 눈 상자의 y0
EXTRA_BY_EXPR = {("runner", "surprise"): [(41, 24), (42, 24), (43, 24), (41, 25), (42, 25), (43, 25)]}  # 둘째(가까운) 눈 상자에 더 덮을 속눈썹 칸


def blink(im, boxes, eye_colors, lash_color, skin, line, name="", expr=""):
    """상자 안 눈 색 픽셀을 피부색으로 덮고, 눈이 있던 열마다 같은 줄 높이에 속눈썹 선을 1px 긋는다."""
    out = im.copy()
    for i, (x0, y0, x1, y1, with_lash, ly, extra) in enumerate(boxes):
        if i == 0:
            y0 = Y0_BY_EXPR.get(name, {}).get(expr, y0)
        cols = set()
        cells = []
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                c = im.getpixel((x, y))[:3]
                if c in eye_colors and (with_lash or c != lash_color):
                    cells.append((x, y))
                    cols.add(x)
        if name == "runner" and i == 2:
            extra = extra + EXTRA_BY_EXPR.get((name, expr), [])
        cells += [c for c in extra if im.getpixel(c)[:3] == lash_color]
        for c in cells:
            out.putpixel(c, skin + (255,))
        for x in cols:
            out.putpixel((x, ly), line + (255,))
    return out


def faces(mod, expr_edit_src):
    from PIL import Image
    base = Image.open(expr_edit_src).convert("RGBA")
    res = []
    for e in EXPRS:
        im = mod.edit(e, base.copy())
        res.append(mod.cut_bg(im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)))
    return res


def build():
    from PIL import Image
    made = []
    prev = REPO / "Art" / "Export" / "preview"
    prev.mkdir(parents=True, exist_ok=True)
    rows = []
    for name, mod, eyes, boxes, skin, line in (
        ("hacker", H, H_EYE, H_BOXES, H.SK, H.LASH),
        ("runner", Rn, R_EYE, R_BOXES, Rn.SK, Rn.LASH),
    ):
        opens = faces(mod, mod.SRC)
        pairs = []
        for e, op in zip(EXPRS, opens):
            bl = blink(op, boxes, eyes, line, skin, line, name, e)
            assert bl.size == (64, 64)
            assert all(a in (0, 255) for a in bl.getchannel("A").getdata())
            assert set(p[:3] for p in bl.getdata() if p[3]) <= set(p[:3] for p in op.getdata() if p[3]), "새 색"
            made.append(save(bl, f"portraits/{name}_v4_x6_{e}_blink.png", {"status": "본그림(눈 깜빡임)"}))
            pairs.append((op, bl))
        rows.append(pairs)
    # 검수판: 눈 주변 12배, 위=원본 아래=깜빡임
    for name, pairs, crop in (("hacker", rows[0], (26, 18, 50, 36)), ("runner", rows[1], (22, 18, 50, 36))):
        w, h = crop[2] - crop[0], crop[3] - crop[1]
        s = 10
        sheet = Image.new("RGBA", (w * s * 7, h * s * 2), (60, 70, 90, 255))
        for i, (op, bl) in enumerate(pairs):
            sheet.alpha_composite(op.crop(crop).resize((w * s, h * s), Image.NEAREST), (i * w * s, 0))
            sheet.alpha_composite(bl.crop(crop).resize((w * s, h * s), Image.NEAREST), (i * w * s, h * s))
        sheet.save(prev / f"w050_{name}_blink.png")
    return made
