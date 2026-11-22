"""메인 로비 v3 B2안 (W-027, D-110) — B안 구도를 앞으로 당겨 인물을 크게(화각 f 키움).

- main_lobby_v3_drafts의 원근 함수·부품을 그대로 쓰고 카메라 f만 240→384로 키웠다(소실점·수평선은 B와 같음).
- 방은 왼쪽 옆벽·뒷벽 일부만 보이도록 좁히고, 조력자는 침대 긴 변에 옆으로 걸터앉는다.
- 출력은 B2 이름으로만 저장한다(기존 v3 파일은 덮어쓰지 않음).
[시안] 사용자 선택 전.
"""
import main_lobby_v3_drafts as v3
import main_lobby_drafts as dr
from artkit import EXPORT, canvas, palette_check, upscale

F = 384.0


def make_b2():
    v3.REG.clear(); v3.FOOT.clear(); v3.mons.clear()
    cam = v3.Cam(400, f=F)
    xl, xr = -262, 190
    im = canvas(v3.W, v3.H)
    v3.room(im, cam, xl, xr)
    glass = v3.window(im, cam, -10, 120, 108, 198, seed=23)
    v3.bed(im, cam, -260, -172, 230, v3.ZB - 5, pillow_at=(-252, -182, 340, 380))
    gx, gy = cam.ip(55, 150, v3.ZB)
    dr.relight(im, dr.ell(gx, gy + 30, 160, 110), dr.COOL1)
    hinfo = v3.helper(im, cam, -190, 290, "side")
    pinfo = v3.protagonist(im, cam, 42, 172)
    v3.desk(im, cam, -24, 110)
    v3.keyboard(im, cam, 42)
    v3.mons.append(v3.monitor(im, cam, 10, 62))
    return cam, im, glass, hinfo, pinfo, (xl, xr)


def build():
    out = EXPORT / "preview"
    out.mkdir(parents=True, exist_ok=True)
    cam, scene, glass, hinfo, pinfo, (xl, xr) = make_b2()
    worst = v3.check_converge(cam)
    assert worst < 1e-6, worst
    assert not palette_check(scene)
    withmenu = dr.over(scene, dr.menu_overlay(0))
    assert not palette_check(withmenu)
    back = v3.back_rect(cam, xl, xr)
    marks = {"back": back, "ymarks": [
        (round(hinfo["seat_top_y"]), 0, v3.W), (round(hinfo["ground_y"]), 0, v3.W),
        (round(pinfo["chair_floor_y"]), 0, v3.W)]}
    guide = v3.guide_image(cam, scene, marks, (xl, xr))
    made = []
    for name, im in (("main_lobby_v3_B2.png", scene), ("main_lobby_v3_B2_x3.png", upscale(scene, 3)),
                     ("main_lobby_v3_B2_menu_x3.png", upscale(withmenu, 3)), ("main_lobby_v3_B2_guide.png", guide)):
        im.save(out / name)
        made.append(out / name)
    mx, ma = dr.zone_stats(scene, (20, 14, 180, 104))
    mt = min(m[0][1] for m in v3.mons)
    print(f"  [B2] f={cam.f}, Ch={cam.ch}, 소실점 x={cam.vx} 수평선 y={cam.hy}, 수렴 오차 {worst:.1e}px, 뒷벽 {back}")
    print(f"      모니터 {v3.mons}, 주인공 머리 {pinfo['head_top_y']:.1f}~{pinfo['head_chin_y']:.1f} ({pinfo['head_px']:.1f}px), "
          f"모니터 윗변 y={mt} (얼굴 가림 {'0' if mt > pinfo['head_chin_y'] else '있음'})")
    print(f"      조력자 머리 {hinfo['head_top_y']:.1f}~ ({hinfo['head_px']:.1f}px), 엉덩이 y={hinfo['seat_top_y']:.1f}, 발 접지 y={hinfo['ground_y']:.1f}")
    print(f"      유리 {glass}, 메뉴 영역 최대 {mx:.0f}/평균 {ma:.0f}")
    return made
