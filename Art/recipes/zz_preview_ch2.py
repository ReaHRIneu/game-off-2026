"""챕터 2 미리보기 — W-039. 절반 크기(그림 크기 확인용, 도트 정밀 아님).

layout_ch2_fac1~3_half.png: layout.json대로 조립(레일 방은 레일 그림을 겹침, 빈 칸은 어두운 단색).
rooms_ch2_sheet.png: 방 12종(L1+L2+L3) 확인 시트(절반 크기). rooms_ch2_quarter.png: 1/4 축소 시트(방 종류 판독 확인용).
rooms_ch2_vs_old.png: 챕터 0·1·2 색 비교(복도·창고 계열).
"""
import json

from PIL import Image

from artkit import EXPORT, PIX, canvas, save

RAIL_ROOMS = {"fc_lift"}
SHEET_ROOMS = ["fc_dock", "fc_press", "fc_laser", "fc_stock", "fc_exit", "fc_control",
               "fc_lift", "fc_corridor", "fc_stair", "fc_watch", "fc_roofpipe"]


def img(key):
    return Image.open(EXPORT / f"{key}.png").convert("RGBA")


def room_comp(rk, bg="e"):
    im = canvas(640, 384, PIX[bg])
    for part in ("L1_shell", "L2_props"):
        im.alpha_composite(img(f"rooms128/{rk}/{part}"))
    for p in sorted((EXPORT / f"rooms128/{rk}").glob("L3_light_*.png")):
        im.alpha_composite(Image.open(p).convert("RGBA"))
    if rk in RAIL_ROOMS:
        im.alpha_composite(img("rooms128/common/rail"))
    return im


def sheet(rooms, cols, cw, chh, name, note):
    rows = (len(rooms) + cols - 1) // cols
    sh = canvas(cols * (cw + 8) + 8, rows * (chh + 8) + 8, (40, 40, 40, 255))
    for i, rk in enumerate(rooms):
        sh.alpha_composite(room_comp(rk).resize((cw, chh), Image.NEAREST), (8 + (i % cols) * (cw + 8), 8 + (i // cols) * (chh + 8)))
    return save(sh, f"preview/{name}.png", {"status": note})


def build():
    made = []
    city = img("backgrounds/city_night_composite")
    rail = img("rooms128/common/rail")
    for key in ("ch2_fac1", "ch2_fac2", "ch2_fac3"):
        L = json.loads((EXPORT / f"scenes128/{key}/layout.json").read_text(encoding="utf-8"))
        sw, sh = L["sector_px"]
        mw, mh = sw * L["cols"], L["roof_px"] + sh * L["floors"] + L["ground_px"]
        im = city.crop((0, 0, mw // 4, mh // 4)).resize((mw, mh), Image.NEAREST)
        for c in range(L["cols"]):
            im.alpha_composite(img(L["tiles"]["roof"]), (c * sw, 0))
            im.alpha_composite(img(L["tiles"]["earth"]).crop((0, 0, sw, L["ground_px"])), (c * sw, mh - L["ground_px"]))
        for f in range(L["floors"]):
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
    made.append(sheet(SHEET_ROOMS, 3, 320, 192, "rooms_ch2_sheet", "미리보기(방 11종, 절반 크기, 레일 방은 레일 포함)"))
    made.append(sheet(SHEET_ROOMS, 4, 160, 96, "rooms_ch2_quarter", "미리보기(방 11종, 1/4 축소 — 방 종류 판독 확인)"))
    made.append(sheet(["corridor", "ot_corridor", "fc_corridor", "basement", "ot_storage", "fc_stock"], 3, 320, 192,
                      "rooms_ch2_vs_old", "미리보기(챕터 0·1·2 색 비교)"))
    return made
