Shader "Custom/ArcGlow"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _GlowColor ("Glow Color", Color) = (1,1,1,1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 0.7
    }
    
    SubShader
    {
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        
        LOD 100
        
        // Enable alpha blending
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            fixed4 _Color;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _Alpha;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate glow based on distance from center
                float dist = abs(i.uv.x - 0.5) * 2.0; // 0 at center, 1 at edges
                
                // Soft edge falloff
                float glow = 1.0 - dist;
                glow = pow(glow, 2.0);
                
                // Combine colors
                fixed4 col = lerp(_Color, _GlowColor, glow * _GlowIntensity);
                col.a = _Alpha * glow;
                
                // Apply vertex color (from LineRenderer)
                col *= i.color;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Sprites/Default"
}
