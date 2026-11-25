"""챕터 2 의뢰 2-1~2-3의 공장 방 배치(layout.json) — W-039, 03-01 §3-1~§3-3 맵 표(D-068·D-126).

방 키는 rooms128/fc_*. 형식은 zz_layout_ch1 출력과 같다(rooms, sectors, tiles 공용).
빈 칸은 섹터 목록에서 뺀다(엔진이 어두운 바깥 배경 단색으로 그린다). 레일 방은 fc_lift(rooms128/common/rail 재사용).
rooms128/ 그림이 먼저 만들어져 있어야 한다(room128_ch2rooms → room128_ch2zcolor 먼저).
"""
import json

from artkit import EXPORT
from zz_layout_ch1 import room_def

# 층 0이 맨 위. None = 빈 칸
MAPS = {
    # 2-1 FACTORY A (03-01 §3-1): 2F · 1F x 4열
    "ch2_fac1": [
        ["fc_dock", "fc_press", "fc_corridor", None],                           # 2F: 하역장(시작) · 프레스 라인[시계] · 복도(계단 2F->1F) · (빈 칸)
        [None, "fc_stock", "fc_laser", "fc_exit"],                              # 1F: (빈 칸) · 창고(단말) · 레이저 라인[시계] · 출하장(출구)
    ],
    # 2-2 FACTORY B (03-01 §3-2): 2F · 1F x 5열
    "ch2_fac2": [
        ["fc_dock", "fc_press", "fc_press", "fc_watch", "fc_stair"],            # 2F: 하역장(시작) · 프레스 A · 프레스 B · 감시 통로(카메라) · 계단실(계단 2F->1F)
        ["fc_exit", "fc_corridor", "fc_laser", "fc_stock", "fc_stair"],         # 1F: 출하장(출구) · 복도 · 레이저 라인(중계기 뒤) · 창고(단말) · 계단실(도착)
    ],
    # 2-3 FACTORY C (03-01 §3-3): 3F · 2F · 1F x 4열
    "ch2_fac3": [
        ["fc_control", None, "fc_roofpipe", None],                              # 3F: 관제실(출구) · (빈 칸) · 옥상 배관(계단 도착) · (빈 칸)
        [None, "fc_lift", "fc_press", "fc_stair"],                              # 2F: (빈 칸) · 화물 승강 칸[레일] · 프레스 라인[시계] · 계단실(계단 2F->3F)
        ["fc_dock", "fc_laser", "fc_stock", "fc_stair"],                        # 1F: 하역장(시작) · 레이저 라인[시계](중계기) · 창고(단말) · 계단실(계단 1F->2F)
    ],
}


def build():
    base = json.loads((EXPORT / "scenes128" / "ch0_home1" / "layout.json").read_text(encoding="utf-8"))
    made = []
    for key, rows in MAPS.items():
        used, sectors = {}, []
        for f, row in enumerate(rows):
            for c, rk in enumerate(row):
                if rk is None:
                    continue
                if rk not in used:
                    used[rk], _ = room_def(rk)
                sectors.append({"floor": f, "col": c, "room": rk, "lights": {l: "on" for l in sorted(used[rk]["L3"])}})
        layout = {
            "map": key, "status": "시안(W-039, 공장 방)",
            "decisions": base["decisions"] + ["D-068", "D-126"],
            "px_per_logical": base["px_per_logical"], "sector_px": base["sector_px"],
            "floor_y_px": base["floor_y_px"], "ceil_y_px": base["ceil_y_px"],
            "roof_px": base["roof_px"], "ground_px": base["ground_px"],
            "floors": len(rows), "cols": len(rows[0]),
            "tiles": base["tiles"], "rooms": used, "sectors": sectors,
            "rail": "rooms128/common/rail",
            "light_states": base["light_states"],
        }
        path = EXPORT / "scenes128" / key / "layout.json"
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(layout, ensure_ascii=False, indent=1), encoding="utf-8")
        made.append(path)
    return made
