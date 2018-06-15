Shader "Custom/ProjectiveTextureMapping" {
	Properties{
		_ColorTexArray("Color Texture Array", 2DArray) = "" {}
		_DepthTexArray("Depth Texture Array", 2DArray) = "" {}

		_Count("Number of snapshots to use.", Int) = 1
		_MainCamPos("Main Camera Position", Vector) = (0, 0, 0, 1)
		_ShaderType("The type of material to display [0=default, 1=depth texture, 2=depth projection]", Int) = 1
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
			#pragma target 4.0

			#include "UnityCG.cginc"

			// Can only have a maximum of 16 sampler2D textures
			// Otherwise this error occurs: "maximum ps_4_0 sampler register index (16) exceeded at line 76 (on d3d11)"
			UNITY_DECLARE_TEX2DARRAY(_ColorTexArray);
			UNITY_DECLARE_TEX2DARRAY(_DepthTexArray);
			uniform float4x4 _VPArray[2];
			uniform float4x4 _DVPArray[2];
			uniform float4 _PosArray[2];

			uniform int _Count;
			uniform float4 _MainCamPos;
			uniform int _ShaderType;

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

			// Calculate how much of the texture should be visible given the current viewing angle
			float viewingAngleVisibility(float4x4 mvp, float4 projectorPos, float4 worldPos, float4 normal) {
				float3 projectorDir = normalize(projectorPos - worldPos);
				float3 viewDir = normalize(_MainCamPos - worldPos);
				//float3 viewDir = projectorPos - worldPos;
				//float contribution = clamp(dot(normalize(viewDir), normalize(normal.xyz)), 0, 1);
				float contribution = (dot(viewDir, projectorDir) + 1) / 2;
				return pow(contribution, 10);
				//return contribution != 0;// contribution;
			}

			float4 getProjectorColor(v2f input, int arrayIndex) {
				float4 color = float4(0, 0, 0, 0);

				float4x4 mvp = mul(_VPArray[arrayIndex], UNITY_MATRIX_M);
				float4x4 dmvp = mul(_DVPArray[arrayIndex], UNITY_MATRIX_M);
				float4 projectorPos = _PosArray[arrayIndex];

				// Unproject 3D point to snapshot location
				float4 proj = mul(mvp, input.vertex);
				// Prevent backprojection
				if (proj.z < 0) return color;
				// Apply perspective divide
				proj = ((proj / proj.w) + 1) / 2;
				// Clamp texture; don't allow it to repeat
				if (proj.x < 0 || proj.x > 1 || proj.y < 0 || proj.y > 1) return color;

				float distToEdge = max(abs(proj.x - 0.5), abs(proj.y - 0.5)) * 2;
				//distToEdge = (clamp(distToEdge, 0.8, 1) - 0.8) * 5;
				//float distToEdge = 1 - length(proj.xy - float2(0.5, 0.5));

				// Find contribution for each snapshot based on its viewing angle
				float contribution = _Count >= arrayIndex ? viewingAngleVisibility(mvp, projectorPos, input.worldPos, input.normal) : 0;

				// Get depth value of pixel at unprojected point
				float4 depthProj = mul(dmvp, input.vertex);
				depthProj = ((depthProj / depthProj.w) + 1) / 2;
				//float4 depth = tex2D(depthTexture, depthProj.xy);
				float4 depth = UNITY_SAMPLE_TEX2DARRAY(_DepthTexArray, float3(depthProj.xy / 2, arrayIndex));
				// Prevent shadow artifacts (shadow acne) by setting a bias/offset
				float bias = 0.03f;
				// Only apply texture if pixel is not hidden in snapshot
				// Invert proj.z since depth is from 1-0 instead of 0-1
				if (abs((1 - depthProj.z) - depth.r) < bias) {
					//color = tex2D(colorTexture, proj.xy);
					color = UNITY_SAMPLE_TEX2DARRAY(_ColorTexArray, float3(proj.xy / 2, arrayIndex));
				} else {
					contribution = 0;
				}
				// Change material based on uniform value
				//if (_ShaderType == 1) {
				//	color = depth;
				//} else if (_ShaderType == 2) {
				//	color = float4(1 - depthProj.z, 1 - depthProj.z, 1 - depthProj.z, 1);
				//} else if (_ShaderType == 3) {
				//	color = float4(1 - proj.z, 1 - proj.z, 1 - proj.z, 1);
				//}
				// Store contribution in alpha component
				//color.a = contribution;
				color.a = contribution * (1 - distToEdge);

				return color;
			}

			fixed4 frag(v2f i) : SV_Target {
				float4 c = float4(0, 0, 0, 0);

				float projectorColors[2];

				float totalContribution = 0;
				for (int index = 0; index < _Count; index++) {
					float4 projectorColor = getProjectorColor(i, index);
					projectorColors[index] = projectorColor;
					totalContribution += projectorColor.a;
					c += float4(projectorColor.rgb * projectorColor.a, projectorColor.a);
				}

				// Calculate diffuse lighting
				//float3 lightPos = float3(100, 100, 100);
				//float3 lightDir = normalize(lightPos - i.worldPos.xyz);
				//float3 normal = normalize(i.normal);
				//float diffuse = (dot(lightDir, normal) + 1) / 2;
				//float4 diffuseColor = float4(1, 1, 1, 1) * diffuse;

				// If pixel is not completely colored by a texture, color the rest of it white
				float checkerboard = ((int)((i.worldPos.x + 100.0f) / 0.1) + (int)((i.worldPos.y + 100.0f) / 0.1) + (int)((i.worldPos.z + 100.0f) / 0.1)) % 2;
				float4 defaultColor = float4(checkerboard * i.normal.r, checkerboard * i.normal.g, checkerboard * i.normal.b, 1);

				// Normalize the contribution from all projectors
				if (totalContribution > 0) {
					c /= totalContribution;
				} else {
					c = defaultColor;
				}

				// Reset alpha component
				c.a = 1;

				return c;
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
