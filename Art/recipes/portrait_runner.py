"""조력자(러너) 초상 96×96 — 손으로 찍은 도트 격자 버전 (W-040 갈래 A, R2).

- 그림은 전부 문자 격자(한 글자 = 한 픽셀): 기본 격자 `grids/a_runner_base.txt`(96×96) + 표정·부속 조각 `grids/a_runner_expr.txt`.
  도형 함수·자동 외곽선·자동 명암은 쓰지 않는다. 이 레시피는 격자를 읽어 위치만 맞춰 겹쳐 붙인다.
- 원화(2026-11-25)는 눈으로 보고 형태·색·식별점만 참고했다(원화 파일을 열지 않음). 그러데이션·가는 선·잔주름은 옮기지 않고
  이 게임의 도트 화풍(또렷한 색 덩어리, 명암 3단, 디더 없음)으로 찍었다.
- 식별점: ① 갈색 울프컷 + 바보털 + 머리끝 주황(다발 끝마다 들쭉날쭉) ② 주황 윈드브레이커(높은 칼라·어깨 노랑 줄)
  ⑤ 한쪽 이어피스. ③ 허리 가방·④ 카키 바지는 가슴 위 흉상이라 초상에는 나오지 않는다.
- [제안] 방향: 해커와 반대로 화면 왼쪽을 본다. 한쪽 작은 묶음은 왼쪽 뒤에 넣고, 머리핀은 노랑 1개, 원화의 동그라미 장식·별 표식은 뺐다.
  표정 7종은 원화 8칸 중 neutral=1행1, joy=1행2, sadness=1행3, anger=2행1, fear=3행1, surprise=3행2, disgust=3행3에 대응.
[시안] 사용자 확정 전. 출력은 runner_<표정>.png.
"""
from artkit import contact_sheet, palette_check, save, upscale
from gridkit import grid_image, load_rows
from portrait_hacker_v2 import load_blocks, stamp

N = 96
ALWAYS = ["nose", "earpiece", "hairpin", "pony"]
# 눈썹 → 눈 → 입 순서로 덮는다(눈 윗선이 눈썹 끝 위에 오도록)
EXPRESSIONS = {
    "neutral":  ["browL_flat", "browR_flat", "eyeL1", "eyeR1", "mouth_smirk"],
    "joy":      ["browL_up", "browR_up", "eyeLs", "eyeRs", "mouth_grin"],
    "sadness":  ["browL_inup", "browR_inup", "eyeL2", "eyeR2", "mouth_frown", "tear", "tearL"],
    "anger":    ["browL_indown", "browR_indown", "eyeL3", "eyeR3", "mouth_grit"],
    "fear":     ["browL_inup2", "browR_inup2", "eyeLw", "eyeRw", "mouth_gasp", "sweat"],
    "surprise": ["browL_raised", "browR_raised", "eyeLw", "eyeRw", "mouth_shout"],
    "disgust":  ["browL_indown", "browR_flat", "eyeL3", "eyeR3", "mouth_sneer", "wrinkle"],
}


def portrait(name, base=None, blocks=None):
    base = base or grid_image(load_rows("a_runner_base.txt"), N)
    blocks = blocks or load_blocks("a_runner_expr.txt")
    im = base.copy()
    for nm in EXPRESSIONS[name] + ALWAYS:
        stamp(im, blocks[nm])
    return im


def build():
    made, ims = [], {}
    base = grid_image(load_rows("a_runner_base.txt"), N)
    blocks = load_blocks("a_runner_expr.txt")
    for name in EXPRESSIONS:
        im = portrait(name, base, blocks)
        bad = palette_check(im)
        assert not bad, (name, bad)
        ims[name] = im
        made.append(save(im, f"portraits/runner_{name}.png",
                         {"status": "시안", "style": "pixel", "size": 96, "expression": name,
                          "note": "W-040 R2 손으로 찍은 도트 격자 러너 초상(왼쪽을 봄)"}))
    made.append(contact_sheet(made, 192, "preview/runner_expressions_sheet.png", cols=4))
    made.append(save(upscale(ims["neutral"], 4), "preview/runner_neutral_x4.png", {"status": "미리보기"}))
    return made
