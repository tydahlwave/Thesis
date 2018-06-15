Shader "Custom/InpaintGenerateMask" {
	Properties {
		_Src ("Source texture (RGB)", 2D) = "white" {}
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		Pass{
			CGPROGRAM
			#include "UnityCG.cginc"
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0 // Use shader model 3.0

			sampler2D _Src;
			//sampler2D _Mask;
			sampler2D _Inpaint;

			struct v2f {
				float4 pos : SV_POSITION;
				//float4 vertex: VERTEX;
				//float4 worldPos: VERTEX1;
				//float4 normal: NORMAL;
			};

			v2f vert(appdata_base v) {
				v2f o;
				//o.vertex = v.vertex;
				o.pos = UnityObjectToClipPos(v.vertex); //Equivalent to mul(UNITY_MATRIX_MVP, float4(pos, 1.0))
														//o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
														//o.normal = mul(UNITY_MATRIX_M, v.normal);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target{
				float4 srcColor = tex2D(_Src, float2(i.pos.x / 600.0f, i.pos.y / 400.0f));
				//float4 maskColor = tex2D(_Mask, depthProj.xy);
				float4 inpaintColor = tex2D(_Inpaint, i.pos.xy);
				if (srcColor.r == 0 && srcColor.g == 1 && srcColor.b == 0) {
					return fixed4(1, 1, 1, 1);
				} else {
					return fixed4(0, 0, 0, 1);
				}
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
