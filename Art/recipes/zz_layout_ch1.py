"""챕터 1 의뢰 1-1~1-4의 구시가지 방 배치(layout.json) — W-015, 03-01 §2-1·§2-2 맵 표(D-091).

방 키는 rooms128/ot_*. 형식은 zz_layout_ch0·zz_layout_ch1_tmp 출력과 같다(rooms, sectors, tiles 공용).
빈 칸은 섹터 목록에서 뺀다(엔진이 어두운 바깥 배경 단색으로 그린다).
rooms128/ 그림이 먼저 만들어져 있어야 한다(room128_ch1rooms·room128_ch1recolor 먼저).
"""
import json

from artkit import EXPORT

# 층 0이 맨 위. None = 빈 칸
MAPS = {
    # 1-1 OLD TOWN A·B (W-031, 03-01 §2-7, D-119): 3F · 2F · 1F x 5열
    "ch1_old1": [
        ["ot_rental", "ot_endroom", None, "ot_corridor", "ot_stairwell"],      # 3F: A 옥탑 셋방 · A 끝 방[레일] · (빈 칸) · B 복도 · B 계단실(단말, 계단 3F->2F)
        ["ot_corridor", "ot_stairwell", None, "ot_corridor", "ot_stairwell"],   # 2F: A 복도(도착) · A 계단실(계단 2F->3F) · (빈 칸) · B 복도(계단 2F->1F) · B 계단실(도착)
        ["ot_shop", "ot_storage", None, "ot_lobby", "ot_storage"],              # 1F: A 상점(시작, 계단 1F->2F) · A 창고 · (빈 칸) · B 로비(출구) · B 창고
    ],
    # 1-2 OLD TOWN C: 3F · 2F · 1F x 5열
    "ch1_old2": [
        [None, "ot_roofexit", None, "ot_roofhall", "ot_stairwell"],            # 3F: (빈 칸) · 옥상 출구 방[레일] · (빈 칸) · 옥상 복도 · 계단실
        [None, None, "ot_corridor", "ot_lasercorr", "ot_stairwell"],           # 2F: (빈 칸) · (빈 칸) · 복도 · 레이저 복도 · 계단실
        ["ot_shop", "ot_vacant", None, "ot_storage", None],                    # 1F: 상점(시작) · 빈 점포[레일] · (빈 칸) · 창고(단말) · (빈 칸)
    ],
    # 1-3 OLD TOWN D (W-031, 03-01 §2-7, D-119): 3F · 2F · 1F x 5열. 새 방 그림 없이 기존 구시가지 방 재사용
    "ch1_old3": [
        ["ot_rental", "ot_corridor", None, None, None],                         # 3F: 셋방(단말) · 복도 · (빈 칸) x3
        ["ot_shop", "ot_vacant", "ot_lasercorr", "ot_stairwell", None],        # 2F: 상점(시작) · 빈 점포[레일] · 레이저 복도 · 계단실(계단 2F->1F, W-033) · (빈 칸)
        [None, None, "ot_storage", "ot_lobby", "ot_shop"],                      # 1F: (빈 칸) x2 · 창고 · 로비(출구) · 상점
    ],
    # TEST_2-0 시간 위상 시험 맵(W-035): 1F x 5열. 새 방 그림 없이 기존 구시가지 방 재사용
    "ch2_test": [
        ["ot_corridor", "ot_storage", "ot_vacant", "ot_corridor", "ot_lobby"],  # 시작 복도(중계기) · 프레스 방 A[시계] · 프레스 방 B[시계] · 복도 · 로비(출구)
    ],
    # 1-4(새) OLD TOWN F (W-036, 03-01 §2-8, D-125): 3F · 2F · 1F x 5열. 새 방 그림 없이 기존 구시가지 방 재사용
    "ch1_old5": [
        [None, None, None, "ot_roofexit", None],                                # 3F: (빈 칸) x3 · 옥탑(출구) · (빈 칸 — B 끝 방이 올라올 자리)
        ["ot_rental", "ot_endroom", None, "ot_corridor", "ot_endroom"],         # 2F: 셋방(시작) · A 끝 방[레일] · (빈 칸 — 골목 위) · B 복도(해치) · B 끝 방[레일]
        ["ot_shop", "ot_storage", None, "ot_lobby", "ot_storage"],              # 1F: 상점 · 창고 · (빈 칸) · B 로비(해치 도착) · B 창고(단말, 계단 1F->2F)
    ],
    # 1-5(새) OLD TOWN G (W-036, 03-01 §2-8, D-125): 4F · 3F · 2F · 1F x 4열. 새 방 그림 없이 기존 구시가지 방 재사용
    "ch1_old6": [
        [None, "ot_roofhall", None, "ot_roofexit"],                             # 4F: (빈 칸) · 옥상 복도(도착) · (빈 칸) · 옥상 출구 방[레일](출구)
        ["ot_rental", "ot_stairwell", "ot_corridor", "ot_stairwell"],           # 3F: 셋방 · 계단실(계단 3F->4F) · 감시 복도[레일](카메라) · 계단실(도착)
        ["ot_shop", "ot_corridor", "ot_lasercorr", "ot_stairwell"],             # 2F: 상점(시작) · 복도(해치) · 레이저 복도(중계기) · 계단실(계단 2F->3F)
        [None, "ot_storage", None, None],                                       # 1F: (빈 칸) · 창고(단말, 계단 1F->2F) · (빈 칸) x2
    ],
    # 1-4 OLD TOWN E (W-031, 03-01 §2-7, D-119): 4F · 3F · 2F · 1F x 4열
    "ch1_old4": [
        ["ot_roofexit", None, "ot_roofhall", None],                             # 4F: 옥상 출구 방[레일](출구) · (빈 칸) · 옥상 복도(도착) · (빈 칸)
        [None, "ot_rental", "ot_corridor", "ot_stairwell"],                     # 3F: (빈 칸) · 셋방 · 복도(셔터, 계단 3F->4F) · 계단실(도착)
        ["ot_rental", "ot_corridor", "ot_lasercorr", "ot_stairwell"],           # 2F: 셋방(시작) · 복도 · 레이저 복도(중계기) · 계단실(카메라, 계단 2F->3F)
        [None, "ot_vacant", "ot_storage", None],                                # 1F: (빈 칸) · 빈 점포[레일] · 창고(단말) · (빈 칸)
    ],
}


def room_def(rk):
    man = json.loads((EXPORT / "manifest.json").read_text(encoding="utf-8"))
    lights = sorted(p.stem[len("L3_light_"):] for p in (EXPORT / f"rooms128/{rk}").glob("L3_light_*.png"))
    return {
        "L1": f"rooms128/{rk}/L1_shell", "L2": f"rooms128/{rk}/L2_props",
        "L3": {l: f"rooms128/{rk}/L3_light_{l}" for l in lights},
        "dark": f"rooms128/{rk}/L3_dark",
        "windows": man.get(f"rooms128/{rk}/L1_shell.png", {}).get("windows", []),
    }, lights


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
            "map": key, "status": "시안(W-015, 구시가지 방)",
            "decisions": base["decisions"] + ["D-084", "D-091", "D-100"],
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
