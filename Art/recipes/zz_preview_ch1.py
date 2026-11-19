"""챕터 1 미리보기 — W-015. 절반 크기(그림 크기 확인용, 도트 정밀 아님).

layout_ch1_old1_half.png / layout_ch1_old2_half.png: layout.json대로 조립(레일 방은 레일 그림을 겹침).
rooms_ch1_sheet.png: 방 11종(L1+L2+L3) 확인 시트.
"""
import json

from PIL import Image

from artkit import EXPORT, PIX, canvas, save

RAIL_ROOMS = {"ot_endroom", "ot_vacant", "ot_roofexit"}   # 03-01 §2-1·§2-2의 [레일] 방
SHEET_ROOMS = ["ot_rental", "ot_endroom", "ot_shop", "ot_vacant", "ot_roofhall", "ot_roofexit",
               "ot_corridor", "ot_stairwell", "ot_lobby", "ot_storage", "ot_lasercorr"]


def img(key):
    return Image.open(EXPORT / f"{key}.png").convert("RGBA")


def room_comp(rk, lights, bg="e"):
    r = json.loads((EXPORT / "scenes128/ch1_old1/layout.json").read_text(encoding="utf-8"))["rooms"].get(rk)
    if r is None:
        r = json.loads((EXPORT / "scenes128/ch1_old2/layout.json").read_text(encoding="utf-8"))["rooms"][rk]
    im = canvas(640, 384, PIX[bg])
    im.alpha_composite(img(r["L1"])); im.alpha_composite(img(r["L2"]))
    for k in sorted(r["L3"]):
        im.alpha_composite(img(r["L3"][k]))
    return im


def build():
    made = []
    city = img("backgrounds/city_night_composite")
    rail = img("rooms128/common/rail")
    for key in ("ch1_old1", "ch1_old2"):
        L = json.loads((EXPORT / f"scenes128/{key}/layout.json").read_text(encoding="utf-8"))
        sw, sh = L["sector_px"]
        mw, mh = sw * L["cols"], L["roof_px"] + sh * L["floors"] + L["ground_px"]
        im = city.crop((0, 0, mw // 4, mh // 4)).resize((mw, mh), Image.NEAREST)
        for c in range(L["cols"]):
            im.alpha_composite(img(L["tiles"]["roof"]), (c * sw, 0))
            im.alpha_composite(img(L["tiles"]["earth"]).crop((0, 0, sw, L["ground_px"])), (c * sw, mh - L["ground_px"]))
        for f in range(L["floors"]):                              # 빈 칸은 어두운 바깥 단색(엔진과 같음)
            for c in range(L["cols"]):
                im.paste(PIX["K"], (c * sw, L["roof_px"] + f * sh, c * sw + sw, L["roof_px"] + f * sh + sh))
        for s in L["sectors"]:
            x, y = s["col"] * sw, L["roof_px"] + s["floor"] * sh
            r = L["rooms"][s["room"]]
            im.alpha_composite(img(r["L1"]), (x, y)); im.alpha_composite(img(r["L2"]), (x, y))
            on = [k for k, v in s["lights"].items() if v == "on"]
            for k in on:
                im.alpha_composite(img(r["L3"][k]), (x, y))
            if not on:
                im.alpha_composite(img(r["dark"]), (x, y))
            if s["room"] in RAIL_ROOMS:
                im.alpha_composite(rail, (x, y))
        made.append(save(im.resize((mw // 2, mh // 2), Image.NEAREST), f"preview/layout_{key}_half.png", {"status": "미리보기(절반 크기)"}))

    cols = 3
    cw, chh = 320, 192
    rows = (len(SHEET_ROOMS) + cols - 1) // cols
    sheet = canvas(cols * (cw + 8) + 8, rows * (chh + 8) + 8, (40, 40, 40, 255))
    for i, rk in enumerate(SHEET_ROOMS):
        c = room_comp(rk, None)
        if rk in RAIL_ROOMS:
            c.alpha_composite(rail)
        sheet.alpha_composite(c.resize((cw, chh), Image.NEAREST), (8 + (i % cols) * (cw + 8), 8 + (i // cols) * (chh + 8)))
    made.append(save(sheet, "preview/rooms_ch1_sheet.png", {"status": "미리보기(방 11종, 절반 크기, 레일 방은 레일 포함)"}))
    return made
