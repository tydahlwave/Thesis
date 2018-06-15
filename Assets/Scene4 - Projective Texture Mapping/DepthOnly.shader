Shader "Custom/DepthOnly" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
	}
	SubShader{
		Tags { "RenderType" = "Opaque" }
		Cull Off
		ZWrite Off
		ZTest Always

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _CameraDepthTexture;

			struct v2f {
				float4 pos : SV_POSITION;
				float4 scrPos : TEXCOORD1;
			};

			v2f vert(appdata_base v) {
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.scrPos = ComputeScreenPos(o.pos);
				return o;
			}

			struct fout {
				fixed4 color : SV_TARGET;
				//fixed4 depth : SV_TARGET1;
			};

			fout frag(v2f i) {
				//float depthValue = Linear01Depth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.scrPos)).r);
				float depthValue = tex2D(_CameraDepthTexture, i.scrPos.xy).r;
				fixed4 depth = float4(depthValue, depthValue, depthValue, 1);

				fout o;
				o.color = depth;
				return o;
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
