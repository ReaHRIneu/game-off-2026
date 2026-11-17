// W-009: PixelCanvas의 GPU 그리기용 단순 셰이더. 정점 색 x 텍스처, 알파 블렌드.
// TEXCOORD0.z > 0.5 이면 타일 반복(9-slice의 변·가운데), 아니면 한 번만 그린다.
Shader "GameOff/PixelQuad"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Res;   // 그리는 RenderTexture 크기(px)

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float3 l : TEXCOORD0; float2 r1 : TEXCOORD1; float2 r2 : TEXCOORD2; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 l : TEXCOORD0; float2 r1 : TEXCOORD1; float2 r2 : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                // 정점은 RT 픽셀 좌표(왼쪽 위 원점). RT에 그리므로 UV가 위에서 시작하는 플랫폼(D3D 등)은 y를 뒤집는다.
                float cy = 1.0 - 2.0 * v.vertex.y / _Res.y;
                #if UNITY_UV_STARTS_AT_TOP
                cy = -cy;
                #endif
                o.pos = float4(v.vertex.x / _Res.x * 2.0 - 1.0, cy, 0.0, 1.0);
                o.color = v.color; o.l = v.l; o.r1 = v.r1; o.r2 = v.r2;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 l = i.l.xy;
                if (i.l.z > 0.5) l = frac(l);
                float2 uv = i.r1 + l * i.r2;
                return tex2D(_MainTex, uv) * i.color;
            }
            ENDCG
        }
    }
}
