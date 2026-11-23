Shader "NightChase/Stars"
{
 SubShader
 {
  Tags { "Queue"="Transparent-50" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZWrite Off ZTest LEqual Blend One One
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float4 _Tint;
   float _Twinkle, _Speed;
   struct Input {float4 vertex:POSITION; float2 uv:TEXCOORD0;};
   struct Output {float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float glow:TEXCOORD1;};
   Output vert(Input v){
    Output o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
    // [УГЛУБЛЕНИЕ] Городская засветка гасит низкие звезды.
    o.glow=smoothstep(.08,.45,v.vertex.y)*(1-_Twinkle*.5+_Twinkle*.5*sin(_Time.y*_Speed+v.vertex.x*42+v.vertex.z*71)); return o;
   }
   float4 frag(Output i):SV_Target {float d=saturate(1-length(i.uv-.5)*2);return _Tint*d*d*i.glow;}
   ENDHLSL
  }
 }
}
