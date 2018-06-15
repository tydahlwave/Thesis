Shader "Custom/UseStencil_ProjectiveTextureMapping" {
	Properties{
		_Tex1("Texture", 2D) = "white" {}
		_Tex2("Texture", 2D) = "white" {}
		_Tex3("Texture", 2D) = "white" {}
		_Tex4("Texture", 2D) = "white" {}
		_Tex5("Texture", 2D) = "white" {}
		_Tex6("Texture", 2D) = "white" {}
		_Tex7("Texture", 2D) = "white" {}
		_Tex8("Texture", 2D) = "white" {}
		_Depth1("Depth", 2D) = "white" {}
		_Depth2("Depth", 2D) = "white" {}
		_Depth3("Depth", 2D) = "white" {}
		_Depth4("Depth", 2D) = "white" {}
		_Depth5("Depth", 2D) = "white" {}
		_Depth6("Depth", 2D) = "white" {}
		_Depth7("Depth", 2D) = "white" {}
		_Depth8("Depth", 2D) = "white" {}
		_MVP1("Camera Transform Matrix", 2D) = "" {}
		_MVP2("Camera Transform Matrix", 2D) = "" {}
		_MVP3("Camera Transform Matrix", 2D) = "" {}
		_MVP4("Camera Transform Matrix", 2D) = "" {}
		_MVP5("Camera Transform Matrix", 2D) = "" {}
		_MVP6("Camera Transform Matrix", 2D) = "" {}
		_MVP7("Camera Transform Matrix", 2D) = "" {}
		_MVP8("Camera Transform Matrix", 2D) = "" {}
		_DMVP1("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP2("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP3("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP4("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP5("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP6("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP7("Depth Camera Transform Matrix", 2D) = "" {}
		_DMVP8("Depth Camera Transform Matrix", 2D) = "" {}
		_Pos1("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos2("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos3("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos4("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos5("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos6("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos7("Camera Position", Vector) = (0, 0, 0, 1)
		_Pos8("Camera Position", Vector) = (0, 0, 0, 1)

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
			#pragma target 3.0

			#include "UnityCG.cginc"

			// Can only have a maximum of 16 sampler2D textures
			// Otherwise this error occurs: "maximum ps_4_0 sampler register index (16) exceeded at line 76 (on d3d11)"
			sampler2D _Tex1;
			sampler2D _Tex2;
			sampler2D _Tex3;
			sampler2D _Tex4;
			sampler2D _Tex5;
			sampler2D _Tex6;
			sampler2D _Tex7;
			sampler2D _Tex8;
			sampler2D _Depth1;
			sampler2D _Depth2;
			sampler2D _Depth3;
			sampler2D _Depth4;
			sampler2D _Depth5;
			sampler2D _Depth6;
			sampler2D _Depth7;
			sampler2D _Depth8;
			uniform float4x4 _MVP1;
			uniform float4x4 _MVP2;
			uniform float4x4 _MVP3;
			uniform float4x4 _MVP4;
			uniform float4x4 _MVP5;
			uniform float4x4 _MVP6;
			uniform float4x4 _MVP7;
			uniform float4x4 _MVP8;
			uniform float4x4 _DMVP1;
			uniform float4x4 _DMVP2;
			uniform float4x4 _DMVP3;
			uniform float4x4 _DMVP4;
			uniform float4x4 _DMVP5;
			uniform float4x4 _DMVP6;
			uniform float4x4 _DMVP7;
			uniform float4x4 _DMVP8;
			uniform float4 _Pos1;
			uniform float4 _Pos2;
			uniform float4 _Pos3;
			uniform float4 _Pos4;
			uniform float4 _Pos5;
			uniform float4 _Pos6;
			uniform float4 _Pos7;
			uniform float4 _Pos8;
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
			float viewingAngleVisibility(float4x4 mvp, float4 camPos, float4 worldPos, float4 normal) {
				float3 viewDir = camPos - worldPos;
				//				float3 viewDir = _MainCamPos - worldPos;
				float contribution = clamp(dot(normalize(viewDir), normalize(normal.xyz)), 0, 1);
				return contribution != 0;// contribution;
			}

			float4 snapshotColor(float4x4 mvp, float4x4 dmvp, sampler2D colorTexture, sampler2D depthTexture, v2f i, float4 snapshotPos, int snapshotIndex) {
				float4 color = float4(0, 0, 0, 0);

				// Unproject 3D point to snapshot location
				float4 proj = mul(mvp, i.vertex);
				// Prevent backprojection
				if (proj.z < 0) return color;
				// Apply perspective divide
				proj = (proj / proj.w + 1) / 2;
				// Clamp texture; don't allow it to repeat
				if (proj.x < 0 || proj.x > 1 || proj.y < 0 || proj.y > 1) return color;

				// Find contribution for each snapshot based on its viewing angle
				float contribution = _Count >= snapshotIndex ? viewingAngleVisibility(mvp, snapshotPos, i.worldPos, i.normal) : 0;

				// Get depth value of pixel at unprojected point
				float4 depthProj = mul(dmvp, i.vertex);
				depthProj = (depthProj / depthProj.w + 1) / 2;
				float4 depth = tex2D(depthTexture, depthProj.xy);
				// Prevent shadow artifacts (shadow acne) by setting a bias/offset
				float bias = 0.1f;
				// Only apply texture if pixel is not hidden in snapshot
				// Invert proj.z since depth is from 1-0 instead of 0-1
				//if (depthProj.z - bias < depth.r) {
				if (abs((1 - depthProj.z) - depth.r) < bias) { // TODO: Fix depth comparison. RenderTexture uses different camera intrinsics than actual camera, so proj.z and depth.r are different
					color = tex2D(colorTexture, proj.xy) * contribution;
				}
				else {
					contribution = 0;
				}
				// Change material based on uniform value
				if (_ShaderType == 1) {
					color = depth;
				}
				else if (_ShaderType == 2) {
					color = float4(1 - depthProj.z, 1 - depthProj.z, 1 - depthProj.z, 1);
				}
				else if (_ShaderType == 3) {
					color = float4(1 - proj.z, 1 - proj.z, 1 - proj.z, 1);
				}
				// Store contribution in alpha component
				color.a = contribution;

				return color;
			}

			fixed4 frag(v2f i) : SV_Target{
				float4 c = float4(0, 0, 0, 0);

				float4 c1 = snapshotColor(_MVP1, _DMVP1, _Tex1, _Depth1, i, _Pos1, 1);
				float4 c2 = snapshotColor(_MVP2, _DMVP2, _Tex2, _Depth2, i, _Pos2, 2);
				float4 c3 = snapshotColor(_MVP3, _DMVP3, _Tex3, _Depth3, i, _Pos3, 3);
				float4 c4 = snapshotColor(_MVP4, _DMVP4, _Tex4, _Depth4, i, _Pos4, 4);
				float4 c5 = snapshotColor(_MVP5, _DMVP5, _Tex5, _Depth5, i, _Pos5, 5);
				float4 c6 = snapshotColor(_MVP6, _DMVP6, _Tex6, _Depth6, i, _Pos6, 6);
				float4 c7 = snapshotColor(_MVP7, _DMVP7, _Tex7, _Depth7, i, _Pos7, 7);
				float4 c8 = snapshotColor(_MVP8, _DMVP8, _Tex8, _Depth8, i, _Pos8, 8);
				float totalContribution = c1.a + c2.a + c3.a + c4.a + c5.a + c6.a + c7.a + c8.a;
				c += c1 + c2 + c3 + c4 + c5 + c6 + c7 + c8;

				// If pixel is not completely colored by a texture, color the rest of it white
				float checkerboard = ((int)((i.worldPos.x + 100.0f) / 0.1) + (int)((i.worldPos.y + 100.0f) / 0.1) + (int)((i.worldPos.z + 100.0f) / 0.1)) % 2;
				float4 defaultColor = float4(checkerboard * i.normal.r, checkerboard * i.normal.g, checkerboard * i.normal.b, 1);
				//float4 defaultColor = float4(1, 1, 1, 1);
				c += defaultColor * max((1 - totalContribution), 0);
				// Normalize the contribution from all snapshots
				c /= max(totalContribution, 1);
				// Reset alpha component
				c.a = 1;

				return c;
			}
			ENDCG
		}
	}
	FallBack "Diffuse"
}
