Shader "Custom/CheckeredNormals" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"

			struct v2f {
				float4 pos : SV_POSITION;
				float4 vertex: VERTEX;
				float4 worldPos: VERTEX1;
				float4 normal: NORMAL;
			};

			v2f vert(appdata_base v) {
				v2f o;
				o.vertex = v.vertex;
				o.pos = UnityObjectToClipPos(v.vertex); //Equivalent to mul(UNITY_MATRIX_MVP, float4(pos, 1.0))
				o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
				o.normal = mul(UNITY_MATRIX_M, v.normal);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target {
				float checkerboard = ((int)((i.worldPos.x + 100.0f) / 0.1) + (int)((i.worldPos.y + 100.0f) / 0.1) + (int)((i.worldPos.z + 100.0f) / 0.1)) % 2;
				float4 defaultColor = float4(checkerboard * i.normal.r, checkerboard * i.normal.g, checkerboard * i.normal.b, 1);
				return defaultColor;
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
