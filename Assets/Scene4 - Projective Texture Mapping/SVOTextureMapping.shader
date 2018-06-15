Shader "Custom/SVOTextureMapping" {
	Properties{
		_DepthTex("Depth Texture", 2D) = "" {}
	}
	SubShader{
		Tags{ "RenderType" = "Opaque" }
		LOD 200

		Stencil{
			Ref 2
			Comp equal
			Pass keep
		}

		Pass{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			// Supposedly allows for nicer lighting, but also allows up to 224 const registers rather than only 32
			//#pragma target 3.0
			#pragma target 5.0

			#include "UnityCG.cginc"

			struct v2f {
				float4 pos : SV_POSITION;
				float4 vertex: VERTEX;
				float4 worldPos: VERTEX1;
				float4 normal: NORMAL;
			};

			struct SVONode {
				int children[8];
				float3 pos;
				uint3 color;
				uint pixelCount;
			};

			StructuredBuffer<SVONode> octree;
			uniform sampler2D _DepthTex;
			uniform float4x4 _InverseVP;
			uniform float4x4 _DVP;

			v2f vert(appdata_base v) {
				v2f o;
				o.vertex = v.vertex;
				o.pos = UnityObjectToClipPos(v.vertex); //Equivalent to mul(UNITY_MATRIX_MVP, float4(pos, 1.0))
				o.worldPos = mul(UNITY_MATRIX_M, v.vertex);
				o.normal = mul(UNITY_MATRIX_M, v.normal);
				return o;
			}

			float3 getSVOColor(uint2 pixelPos) {
				// Calculate 3D position of pixel
				//float4 ndc = float4((pixelPos.x / (float)1280) * 2 - 1, (pixelPos.y / (float)720) * 2 - 1, 1-depth, 1);
				//float4 worldPos = mul(_InverseDVP, ndc);
				//worldPos /= worldPos.w;
				// Use depth value of 0 to unproject point onto the near plane
				float4 ndc = float4((pixelPos.x / (float)1280) * 2 - 1, (pixelPos.y / (float)720) * 2 - 1, 0, 1);
				float4 worldPos = mul(_InverseVP, ndc);
				worldPos /= worldPos.w;

				// Project into depth map to get depth value
				worldPos.w = 1;
				float4 ndc_depth = mul(_DVP, worldPos);
				ndc_depth /= ndc_depth.w;
				//uint2 depthPixelPos = uint2(ndc_depth.x * 1280, ndc_depth.y * 720);
				float depthValue = tex2D(_DepthTex, float2((ndc_depth.x + 1) / 2.0, (ndc_depth.y + 1) / 2.0)).r;

				// Calculate the real position using depth value
				ndc.z = 1 - depthValue;
				float4 realWorldPos = mul(_InverseVP, ndc);
				realWorldPos /= realWorldPos.w;

				// Traverse octree
				int oldNodeIndex = 0;
				SVONode oldNode = octree[oldNodeIndex];
				int childIndex = (realWorldPos.z >= oldNode.pos.z) * 4 + (realWorldPos.y >= oldNode.pos.y) * 2 + (realWorldPos.x >= oldNode.pos.x);

				// Continue traversing until at lowest level of octree
				while (oldNode.children[childIndex] > 0) {
					oldNodeIndex = oldNode.children[childIndex];
					oldNode = octree[oldNodeIndex];
					childIndex = (realWorldPos.z >= oldNode.pos.z) * 4 + (realWorldPos.y >= oldNode.pos.y) * 2 + (realWorldPos.x >= oldNode.pos.x);
				}

				return float3(oldNode.color.r, oldNode.color.g, oldNode.color.b) / 255.0;
			}

			fixed4 frag(v2f i) : SV_Target{
				float4 c = float4(0, 0, 0, 0);

				uint2 pixelPos = uint2(i.pos.x, i.pos.y);
				float3 SVOColor = getSVOColor(pixelPos);

				return float4(SVOColor.r, SVOColor.g, SVOColor.b, 1);
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
