"""layout.json대로 챕터 0 맵을 조립한 미리보기(엔진 구현 확인 기준). 절반 크기로 저장(그림 크기 확인용, 도트 정밀 아님)."""
import json

from PIL import Image

from artkit import EXPORT, PIX, canvas, save


def img(key):
    return Image.open(EXPORT / f"{key}.png").convert("RGBA")


def build():
    made = []
    city = img("backgrounds/city_night_composite")
    for key in ("ch0_home1", "ch0_home2"):
        L = json.loads((EXPORT / f"scenes128/{key}/layout.json").read_text(encoding="utf-8"))
        sw, sh = L["sector_px"]
        mw, mh = sw * L["cols"], L["roof_px"] + sh * L["floors"] + L["ground_px"]
        im = city.crop((0, 0, mw // 4, mh // 4)).resize((mw, mh), Image.NEAREST)          # L0(엔진은 시차 스크롤)
        for c in range(L["cols"]):
            im.alpha_composite(img(L["tiles"]["roof"]), (c * sw, 0))
            im.alpha_composite(img(L["tiles"]["earth"]).crop((0, 0, sw, L["ground_px"])), (c * sw, mh - L["ground_px"]))
        for s in L["sectors"]:
            x, y = s["col"] * sw, L["roof_px"] + s["floor"] * sh
            if s["room"] is None:
                im.alpha_composite(img(s["tile"]), (x, y)); continue
            r = L["rooms"][s["room"]]
            im.alpha_composite(img(r["L1"]), (x, y)); im.alpha_composite(img(r["L2"]), (x, y))
            on = [k for k, v in s["lights"].items() if v == "on"]
            for k in on:
                im.alpha_composite(img(r["L3"][k]), (x, y))
            if not on:
                im.alpha_composite(img(r["dark"]), (x, y))
        for o in L["objects"]:
            spr = img(o["sprite"])
            im.alpha_composite(spr, (o["col"] * sw + o["x_px"], L["roof_px"] + o["floor"] * sh + L["floor_y_px"] - spr.height))
        made.append(save(im.resize((mw // 2, mh // 2), Image.NEAREST), f"preview/layout_{key}_half.png", {"status": "미리보기(절반 크기)"}))
    return made
