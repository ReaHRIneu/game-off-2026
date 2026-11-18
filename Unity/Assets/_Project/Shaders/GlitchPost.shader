// W-013: 글리치 후처리(D-087). PixelCanvas 렌더 텍스처(2560x1440)를 읽어 정수 논리 픽셀(4 RT px) 단위로만 어긋나게 다시 쓴다.
// 요소: 가로 줄 밀림, 팔레트 색 복사(R/C), 스캔라인(a), 2px 점 노이즈(W/C), 블록 깨짐. 번짐·블러·밝기 섬광 없음.
// 색은 C#이 넘기는 팔레트 값(선형)만 덮어쓴다. 원래 색과 섞지 않는다. 모양은 _Tick(30 FPS 단위 정수)으로만 바뀐다.
Shader "GameOff/GlitchPost"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Res;      // (RW, RH, 1/RW, 1/RH)
            float4 _RectA;    // 논리 px (왼쪽, 위, 오른쪽, 아래) 오른쪽·아래는 포함하지 않음. 비어 있으면 right <= left
            float4 _RectB;
            float4 _Shift;    // x 띠 높이(논리 px), y 최대 밀림 폭, z 밀리는 띠 비율, w 치우침(-1..1)
            float4 _Copy;     // x 모드(0 없음 1 R 2 C 3 R+C), y 어긋남 폭(논리 px), z 밀도, w 밝은 픽셀 기준(선형 밝기)
            float4 _Fx;       // x 스캔라인 세기, y 노이즈 밀도, z 블록 크기(논리 px), w 블록 확률
            float _Tick;      // 30 FPS 단위 시드(정수 값)
            float4 _ColR, _ColC, _ColW, _ColA;   // 팔레트 R, C, W, a (선형 색)

            // 정수 인자 3개로 0..1 값을 만드는 직접 만든 해시(부동소수 연산만 사용)
            float H(float a, float b, float c)
            {
                float x = frac(a * 0.7548776 + b * 0.5698403 + c * 0.6180340);
                x = frac(x * x * 57.0 + 13.0 * x + c * 0.131);
                x = frac(x * 91.7 + a * 0.019 + b * 0.023);
                return frac(x * x * 113.0 + x);
            }

            bool Inside(float2 L, float4 r) { return L.x >= r.x && L.x < r.z && L.y >= r.y && L.y < r.w; }

            // RT px(왼쪽 위 원점, 정수) 한 칸을 읽는다. 텍스처 가운데를 직접 읽어 Bilinear·밉맵 영향이 없다.
            float4 Read(float2 px)
            {
                float2 uv = float2((px.x + 0.5) * _Res.z, 1.0 - (px.y + 0.5) * _Res.w);
                return tex2Dlod(_MainTex, float4(uv, 0, 0));
            }

            bool Bright(float4 c) { return dot(c.rgb, float3(0.2126, 0.7152, 0.0722)) > _Copy.w; }

            // 영역 r 안에서 줄 밀림·블록 깨짐을 적용해 읽은 색
            float4 Fetch(float2 px, float4 r, float rid)
            {
                float2 L = floor(px / 4.0);
                float dx = 0.0;
                if (_Shift.y > 0.0)
                {
                    float band = floor((L.y - r.y) / max(_Shift.x, 1.0));
                    if (H(band, _Tick, rid + 1.0) < _Shift.z)
                    {
                        float m = floor(H(band, _Tick, rid + 2.0) * _Shift.y) + 1.0;   // 1..최대 밀림 폭
                        float sg = H(band, _Tick, rid + 3.0) < 0.5 ? -1.0 : 1.0;
                        if (_Shift.w != 0.0 && H(band, _Tick, rid + 4.0) < abs(_Shift.w)) sg = sign(_Shift.w);
                        dx = m * sg;
                    }
                }
                if (_Fx.w > 0.0)
                {
                    float2 cell = floor((L - r.xy) / max(_Fx.z, 1.0));
                    if (H(cell.x, cell.y + 500.0, _Tick + rid) < _Fx.w)
                    {
                        float k = 1.0 + floor(H(cell.x, cell.y + 500.0, _Tick + rid + 9.0) * 2.0);
                        dx += (H(cell.y, cell.x + 700.0, _Tick + rid) < 0.5 ? -1.0 : 1.0) * k * max(_Fx.z, 1.0);
                    }
                }
                float sx = clamp(px.x - dx * 4.0, r.x * 4.0, r.z * 4.0 - 1.0);
                return Read(float2(sx, px.y));
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float2 px = floor(float2(i.uv.x * _Res.x, (1.0 - i.uv.y) * _Res.y));
                float2 L = floor(px / 4.0);
                float4 src = Read(px);
                bool inA = Inside(L, _RectA);
                bool inB = Inside(L, _RectB);
                if (!inA && !inB) return src;
                float4 r = inA ? _RectA : _RectB;
                float rid = inA ? 0.0 : 31.0;

                float4 c = Fetch(px, r, rid);

                // 팔레트 색 복사: 어긋난 자리의 밝은 픽셀을 R/C로 한 번 더 찍는다(2px 디더, 덮어쓰기)
                if (_Copy.x > 0.5 && !Bright(c))
                {
                    float off = _Copy.y * 4.0;
                    float chk = fmod(L.x + L.y, 2.0);
                    bool both = _Copy.x > 2.5;
                    if (chk < 0.5 && H(L.x, L.y, _Tick + 91.0) < _Copy.z)
                    {
                        // 첫 번째 복사: 모드 2는 C, 그 밖에는 R. 왼쪽에서 가져온다
                        if (Bright(Fetch(px - float2(off, 0.0), r, rid))) c = _Copy.x > 1.5 && !both ? _ColC : _ColR;
                    }
                    else if (both && chk > 0.5 && H(L.x, L.y, _Tick + 92.0) < _Copy.z)
                    {
                        // 두 번째 복사(모드 3): 엇갈린 점에 C, 오른쪽에서 가져온다
                        if (Bright(Fetch(px + float2(off, 0.0), r, rid))) c = _ColC;
                    }
                }

                // 스캔라인: 홀수 논리 줄의 디더 점을 어두운 색 a로
                if (_Fx.x > 0.0 && fmod(L.y, 2.0) > 0.5 && fmod(L.x, 2.0) < 0.5 && H(L.x, L.y, 3.0) < _Fx.x * 1.5) c = _ColA;

                // 노이즈: 가로 2칸 점이 W 또는 C로
                if (_Fx.y > 0.0)
                {
                    float cx = floor(L.x / 2.0);
                    if (H(cx, L.y, _Tick + 777.0) < _Fx.y * 0.05) c = H(cx, L.y, _Tick + 778.0) < 0.5 ? _ColW : _ColC;
                }

                c.a = 1.0;
                return c;
            }
            ENDCG
        }
    }
}
