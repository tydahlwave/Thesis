Shader "Custom/StencilShader" {
	SubShader {
		Tags { "RenderType"="Opaque" "Queue"="Geometry-1" "ForceNoShadowCasting"="True"}
		Pass {
			ZWrite Off
			ColorMask 0
			Stencil {
				Ref 2
				Comp always
				Pass replace
			}
		}
	}
	FallBack "Diffuse"
}
