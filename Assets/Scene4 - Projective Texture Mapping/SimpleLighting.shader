Shader "Custom/SimpleLighting" {
	Properties {}
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
				float4 worldPos: VERTEX;
				float4 normal: NORMAL;
			};

			v2f vert(appdata_base v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex); //Equivalent to mul(UNITY_MATRIX_MVP, float4(pos, 1.0))
				o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
				o.normal = mul(UNITY_MATRIX_M, v.normal);
				return o;
			}

			fixed4 frag(v2f i) : SV_Target{
				float3 lightPos = float3(100, 100, 100);
				float3 lightDir = normalize(lightPos - i.worldPos.xyz);
				float3 normal = normalize(i.normal);
				float diffuse = (dot(lightDir, normal) + 1) / 2;
				float4 diffuseColor = float4(1, 1, 1, 1) * diffuse;
				return diffuseColor;
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
