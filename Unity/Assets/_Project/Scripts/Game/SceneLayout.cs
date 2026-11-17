using System.Collections.Generic;
using UnityEngine;

namespace GameOff.Game
{
    /// <summary>
    /// 맵 배치(Art/Export/scenes128/&lt;맵&gt;/layout.json, D-073)를 읽은 결과(W-010).
    /// 섹터마다 방(L1 껍데기·L2 소품·L3 빛)과 빛 상태(on/off)를 보관한다. 레벨 시작 때 한 번 읽어 배열로 두고, 그리는 쪽은 미리 만든 그림 키만 쓴다
    /// (프레임마다 문자열·List를 만들지 않음, web-optimization §4). 빛을 켜고 끄는 SetLight만 열어 두며 장치와의 연결은 아직 없다(scene-composition §5 제안).
    /// </summary>
    public sealed class SceneLayout
    {
        /// <summary>방 종류 하나(같은 종류의 섹터가 그림 한 벌을 함께 쓴다).</summary>
        public sealed class Room
        {
            public string Name, L1, L2, Dark;
            public Dictionary<string, string> L3 = new Dictionary<string, string>();   // 빛 이름 -> 그림 키
        }

        /// <summary>섹터 하나의 그리기 정보와 빛 상태.</summary>
        public sealed class SectorView
        {
            public int Floor, Col;
            public Room Room;                 // null이면 땅 타일(Tile)
            public string Tile;
            public string[] LightNames;       // 섹터 파일의 순서(그리는 순서)
            public string[] LightKeys;
            public bool[] LightOn;
            public int OnCount;
            /// <summary>방이 있고 켜진 빛이 하나도 없으면 어둠 그림을 덮는다.</summary>
            public bool IsDark => Room != null && OnCount == 0;
        }

        /// <summary>주인공 방 문 같은 L4 그림 하나. XPx는 섹터 왼쪽에서 그림 왼쪽 끝까지(그림 px).</summary>
        public struct Prop
        {
            public string Key;
            public int Floor, Col, XPx;
        }

        public int Floors { get; private set; }
        public int Cols { get; private set; }
        public int SectorPxW { get; private set; }
        public int SectorPxH { get; private set; }
        public int RoofPx { get; private set; }
        public int GroundPx { get; private set; }
        public int FloorYPx { get; private set; }
        public int PxPerLogical { get; private set; }
        public string RoofKey { get; private set; }
        public string EarthKey { get; private set; }
        public Prop[] Props { get; private set; }

        SectorView[,] sectors;
        readonly List<string> allKeys = new List<string>();

        /// <summary>이 배치가 쓰는 그림 키 전체(자동 확인용).</summary>
        public IReadOnlyList<string> AllKeys => allKeys;

        public SectorView Sector(int floor, int col) => sectors[floor, col];

        public static SceneLayout Parse(string json)
        {
            var root = MiniJson.Parse(json) as MiniJson.Obj;
            if (root == null) throw new System.FormatException("layout.json 최상위가 객체가 아닙니다.");
            var L = new SceneLayout();
            L.PxPerLogical = root.I("px_per_logical", 4);
            var sp = root.A("sector_px");
            L.SectorPxW = (int)(double)sp[0];
            L.SectorPxH = (int)(double)sp[1];
            L.RoofPx = root.I("roof_px");
            L.GroundPx = root.I("ground_px");
            L.FloorYPx = root.I("floor_y_px");
            L.Floors = root.I("floors");
            L.Cols = root.I("cols");
            var tiles = root.O("tiles");
            L.RoofKey = tiles.S("roof");
            L.EarthKey = tiles.S("earth");
            L.AddKey(L.RoofKey); L.AddKey(L.EarthKey);

            var rooms = new Dictionary<string, Room>();
            var ro = root.O("rooms");
            foreach (string name in ro.Keys)
            {
                var r = ro.O(name);
                var room = new Room { Name = name, L1 = r.S("L1"), L2 = r.S("L2"), Dark = r.S("dark") };
                var l3 = r.O("L3");
                if (l3 != null) foreach (string ln in l3.Keys) room.L3[ln] = l3.S(ln);
                rooms[name] = room;
                L.AddKey(room.L1); L.AddKey(room.L2); L.AddKey(room.Dark);
                foreach (var kv in room.L3) L.AddKey(kv.Value);
            }

            L.sectors = new SectorView[L.Floors, L.Cols];
            foreach (object so in root.A("sectors"))
            {
                var s = (MiniJson.Obj)so;
                int f = s.I("floor"), c = s.I("col");
                if (f < 0 || c < 0 || f >= L.Floors || c >= L.Cols) { Debug.LogError("[SceneLayout] 섹터 위치가 맵 밖입니다: " + f + "," + c); continue; }
                var v = new SectorView { Floor = f, Col = c };
                string rn = s.S("room");
                if (rn == null)
                {
                    v.Tile = s.S("tile");
                    L.AddKey(v.Tile);
                    v.LightNames = new string[0]; v.LightKeys = new string[0]; v.LightOn = new bool[0];
                }
                else
                {
                    if (!rooms.TryGetValue(rn, out v.Room)) { Debug.LogError("[SceneLayout] 알 수 없는 방: " + rn); continue; }
                    var lights = s.O("lights");
                    int n = lights == null ? 0 : lights.Keys.Count;
                    v.LightNames = new string[n]; v.LightKeys = new string[n]; v.LightOn = new bool[n];
                    for (int i = 0; i < n; i++)
                    {
                        string ln = lights.Keys[i];
                        v.LightNames[i] = ln;
                        if (!v.Room.L3.TryGetValue(ln, out v.LightKeys[i])) Debug.LogError("[SceneLayout] 방 " + rn + "에 없는 빛: " + ln);
                        v.LightOn[i] = lights.S(ln) == "on";
                        if (v.LightOn[i]) v.OnCount++;
                    }
                }
                L.sectors[f, c] = v;
            }

            var props = new List<Prop>();
            var objs = root.A("objects");
            if (objs != null)
                foreach (object oo in objs)
                {
                    var o = (MiniJson.Obj)oo;
                    props.Add(new Prop { Key = o.S("sprite"), Floor = o.I("floor"), Col = o.I("col"), XPx = o.I("x_px") });
                    L.AddKey(o.S("sprite"));
                }
            L.Props = props.ToArray();
            return L;
        }

        void AddKey(string k)
        {
            if (!string.IsNullOrEmpty(k) && !allKeys.Contains(k)) allKeys.Add(k);
        }

        // ---------- 빛 상태 ----------

        /// <summary>섹터의 빛 하나를 켜거나 끈다. 섹터·빛 이름이 없으면 false.</summary>
        public bool SetLight(int floor, int col, string name, bool on)
        {
            SectorView v = Find(floor, col);
            if (v == null) return false;
            for (int i = 0; i < v.LightNames.Length; i++)
            {
                if (v.LightNames[i] != name) continue;
                if (v.LightOn[i] != on) { v.LightOn[i] = on; v.OnCount += on ? 1 : -1; }
                return true;
            }
            return false;
        }

        public bool IsLightOn(int floor, int col, string name)
        {
            SectorView v = Find(floor, col);
            if (v == null) return false;
            for (int i = 0; i < v.LightNames.Length; i++)
                if (v.LightNames[i] == name) return v.LightOn[i];
            return false;
        }

        /// <summary>방이 있고 켜진 빛이 없으면 true(어둠 그림이 그려지는 상태).</summary>
        public bool IsDark(int floor, int col)
        {
            SectorView v = Find(floor, col);
            return v != null && v.IsDark;
        }

        SectorView Find(int floor, int col) =>
            floor < 0 || col < 0 || floor >= Floors || col >= Cols ? null : sectors[floor, col];
    }
}
