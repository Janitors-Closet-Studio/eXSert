// Made with Amplify Shader Editor v1.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_VFX_Easy_Shockwave_Distortion"
{
	Properties
	{
		[Space(33)][Header(Base Texture)][Space(13)]_BaseTexture("Base Texture", 2D) = "white" {}
		[Space(33)][Header(Noise)][Space(13)]_NoiseTexture("Noise Texture", 2D) = "white" {}
		_Noise01UVS("Noise 01 UV S", Vector) = (1,1,0,0)
		_Noise01UVP("Noise 01 UV P", Vector) = (0.1,0.3,0,0)
		_DistortionLerp("Distortion Lerp", Float) = 1
		[Normal][Space(33)][Header(Noise Distortion)][Space(13)]_NoiseDistortionTexture("Noise Distortion Texture", 2D) = "bump" {}
		_NoiseDistUVS("Noise Dist UV S", Vector) = (1,1,0,0)
		_NoiseDistUVP("Noise Dist UV P", Vector) = (-0.2,-0.1,0,0)
		_TileUVS("Tile UV S", Vector) = (1,1,0,0)
		_Tile02UVP("Tile 02 UV P", Vector) = (-0.2,-0.1,0,0)
		_Noise01Intensity("Noise 01 Intensity", Float) = -1
		_Noise02Intensity("Noise 02 Intensity", Float) = 1
		_CustomOffsetLerp("Custom Offset Lerp", Float) = 0
		_CustomOffsetBase("Custom Offset Base", Float) = 0
		[Space(33)][Header(Intensity Mask)][Space(13)]_IntensityMask("Intensity Mask", 2D) = "black" {}
		_IntensityMaskUVS("Intensity Mask UV S", Vector) = (1,1,0,0)
		_IntensityMaskPower("Intensity Mask Power", Float) = 1
		_IntensityMaskMultiply("Intensity Mask Multiply", Float) = 1
		[Space(33)][Header(Depth Fade)][Space(13)]_DepthFade("Depth Fade", Float) = 0
		_OpacityMultiply("Opacity Multiply", Float) = 1
		[Space(33)][Header(Opacity Cutout Mask)][Space(13)]_OpacityMask("Opacity Mask", 2D) = "white" {}
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 0
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZWrite("ZWrite", Float) = 0
		_ZTest("ZTest", Float) = 2
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IsEmissive" = "true"  }
		Cull [_Cull]
		ZWrite [_ZWrite]
		ZTest [_ZTest]
		Blend [_Src] [_Dst]
		
		GrabPass{ }
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19800
		#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex);
		#else
		#define ASE_DECLARE_SCREENSPACE_TEXTURE(tex) UNITY_DECLARE_SCREENSPACE_TEXTURE(tex)
		#endif
		#pragma surface surf Unlit keepalpha noshadow 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 screenPos;
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 vertexColor : COLOR;
		};

		uniform float _Dst;
		uniform float _Src;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
		ASE_DECLARE_SCREENSPACE_TEXTURE( _GrabTexture )
		uniform sampler2D _BaseTexture;
		uniform sampler2D _NoiseDistortionTexture;
		uniform float2 _NoiseDistUVP;
		uniform float2 _NoiseDistUVS;
		uniform float _Noise02Intensity;
		uniform float2 _Tile02UVP;
		uniform float2 _TileUVS;
		uniform float _ErosionSmoothness;
		uniform sampler2D _IntensityMask;
		uniform sampler2D _NoiseTexture;
		uniform float2 _Noise01UVP;
		uniform float2 _Noise01UVS;
		uniform float _Noise01Intensity;
		uniform float2 _IntensityMaskUVS;
		uniform float _CustomOffsetBase;
		uniform float _CustomOffsetLerp;
		uniform float _IntensityMaskPower;
		uniform float _IntensityMaskMultiply;
		uniform sampler2D _OpacityMask;
		uniform float4 _OpacityMask_ST;
		UNITY_DECLARE_DEPTH_TEXTURE( _CameraDepthTexture );
		uniform float4 _CameraDepthTexture_TexelSize;
		uniform float _DepthFade;
		uniform float _OpacityMultiply;
		uniform float _DistortionLerp;


		inline float4 ASE_ComputeGrabScreenPos( float4 pos )
		{
			#if UNITY_UV_STARTS_AT_TOP
			float scale = -1.0;
			#else
			float scale = 1.0;
			#endif
			float4 o = pos;
			o.y = pos.w * 0.5f;
			o.y = ( pos.y - o.y ) * _ProjectionParams.x * scale + o.y;
			return o;
		}


		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_grabScreenPos = ASE_ComputeGrabScreenPos( ase_positionSS );
			float4 ase_grabScreenPosNorm = ase_grabScreenPos / ase_grabScreenPos.w;
			float2 appendResult8 = (float2(ase_grabScreenPosNorm.r , ase_grabScreenPosNorm.g));
			float2 panner80 = ( 1.0 * _Time.y * _NoiseDistUVP + ( i.uv_texcoord.xy * _NoiseDistUVS ));
			float2 panner103 = ( 1.0 * _Time.y * _Tile02UVP + ( i.uv_texcoord.xy * _TileUVS ));
			float emissiveness121 = i.uv2_texcoord2.y;
			float eros67 = i.uv2_texcoord2.w;
			float2 panner59 = ( 1.0 * _Time.y * _Noise01UVP + ( i.uv_texcoord.xy * _Noise01UVS ));
			float noiseMultiply51 = i.uv2_texcoord2.x;
			float customOffset50 = i.uv_texcoord.z;
			float lerpResult66 = lerp( _CustomOffsetBase , customOffset50 , _CustomOffsetLerp);
			float smoothstepResult82 = smoothstep( eros67 , ( eros67 + _ErosionSmoothness ) , tex2D( _IntensityMask, ( ( tex2D( _NoiseTexture, panner59 ).r * ( _Noise01Intensity * noiseMultiply51 ) ) + ( ( i.uv_texcoord.xy.y * _IntensityMaskUVS ) + lerpResult66 ) ) ).g);
			float temp_output_100_0 = saturate( ( saturate( pow( smoothstepResult82 , _IntensityMaskPower ) ) * _IntensityMaskMultiply ) );
			float temp_output_130_0 = ( ( tex2D( _BaseTexture, ( ( ( ( (UnpackNormal( tex2D( _NoiseDistortionTexture, panner80 ) )).xy + -0.5 ) * 2.0 ) * _Noise02Intensity ) + panner103 ) ).g * emissiveness121 ) * temp_output_100_0 );
			float2 uv_OpacityMask = i.uv_texcoord * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
			float4 ase_positionSSNorm = ase_positionSS / ase_positionSS.w;
			ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
			float screenDepth111 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy ));
			float distanceDepth111 = saturate( ( screenDepth111 - LinearEyeDepth( ase_positionSSNorm.z ) ) / ( _DepthFade ) );
			float temp_output_129_0 = saturate( ( saturate( temp_output_130_0 ) * saturate( ( saturate( ( saturate( ( temp_output_100_0 * tex2D( _OpacityMask, uv_OpacityMask ).g ) ) * distanceDepth111 ) ) * _OpacityMultiply ) ) ) );
			float2 temp_cast_0 = (saturate( temp_output_129_0 )).xx;
			float distortionIntensity137 = i.uv_texcoord.w;
			float2 lerpResult33 = lerp( float2( 0,0 ) , temp_cast_0 , ( _DistortionLerp * distortionIntensity137 ));
			float4 screenColor9 = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_GrabTexture,( appendResult8 + lerpResult33 ));
			o.Emission = screenColor9.rgb;
			o.Alpha = i.vertexColor.a;
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19800
Node;AmplifyShaderEditor.TexCoordVertexDataNode;46;-3968,-1280;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;47;-3968,-1024;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;48;-11904,-256;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;49;-11520,-128;Inherit;False;Property;_Noise01UVS;Noise 01 UV S;4;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;50;-3712,-1280;Inherit;False;customOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;51;-3712,-1024;Inherit;False;noiseMultiply;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;52;-11520,-256;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;53;-11008,-128;Inherit;False;Property;_Noise01UVP;Noise 01 UV P;6;0;Create;True;0;0;0;False;0;False;0.1,0.3;-0.07,-0.3;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;54;-10240,0;Inherit;False;Property;_Noise01Intensity;Noise 01 Intensity;13;0;Create;True;0;0;0;False;0;False;-1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;55;-8960,128;Inherit;False;Property;_CustomOffsetBase;Custom Offset Base;16;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;56;-8704,256;Inherit;False;Property;_CustomOffsetLerp;Custom Offset Lerp;15;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;57;-8960,256;Inherit;False;50;customOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;58;-10240,128;Inherit;False;51;noiseMultiply;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;59;-11008,-256;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;60;-10624,-512;Inherit;True;Property;_NoiseTexture;Noise Texture;2;0;Create;True;0;0;0;False;3;Space(33);Header(Noise);Space(13);False;7437ec4c7494cc5409d26f1abc0906eb;7437ec4c7494cc5409d26f1abc0906eb;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.TextureCoordinatesNode;61;-9472,0;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;62;-9216,128;Inherit;False;Property;_IntensityMaskUVS;Intensity Mask UV S;18;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;63;-10624,-256;Inherit;True;Property;_TextureSample0;Texture Sample 0;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;64;-9984,0;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;65;-9216,0;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;66;-8704,128;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;67;-3712,-640;Inherit;False;eros;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;68;-8448,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;69;-10112,-256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;72;-8448,-256;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;73;-7424,0;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;24;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;74;-7424,-384;Inherit;False;67;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;70;-11904,1024;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;71;-11520,1152;Inherit;False;Property;_NoiseDistUVS;Noise Dist UV S;9;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;77;-8192,-256;Inherit;True;Property;_IntensityMask;Intensity Mask;17;0;Create;True;0;0;0;False;3;Space(33);Header(Intensity Mask);Space(13);False;-1;None;504551a45813a5e4ab030aada700596e;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode;78;-7424,-128;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;75;-11520,1024;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;76;-11008,1152;Inherit;False;Property;_NoiseDistUVP;Noise Dist UV P;10;0;Create;True;0;0;0;False;0;False;-0.2,-0.1;0.02,-0.1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;79;-7040,-384;Inherit;False;Property;_IntensityMaskPower;Intensity Mask Power;19;0;Create;True;0;0;0;False;0;False;1;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SmoothstepOpNode;82;-7424,-256;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;80;-11008,1024;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;81;-10624,768;Inherit;True;Property;_NoiseDistortionTexture;Noise Distortion Texture;8;1;[Normal];Create;True;0;0;0;False;3;Space(33);Header(Noise Distortion);Space(13);False;8a635c01852d10342874a696292bbfd0;8a635c01852d10342874a696292bbfd0;True;bump;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.PowerNode;83;-7040,-256;Inherit;True;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;84;-10624,1024;Inherit;True;Property;_TextureSample1;Texture Sample 1;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;85;-6528,-384;Inherit;False;Property;_IntensityMaskMultiply;Intensity Mask Multiply;20;0;Create;True;0;0;0;False;0;False;1;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;86;-6784,-256;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;89;-10240,1024;Inherit;True;True;True;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;90;-9728,1664;Inherit;False;Property;_TileUVS;Tile UV S;11;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;91;-9728,1408;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;93;-6528,-256;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;95;-9984,1024;Inherit;True;ConstantBiasScale;-1;;1;63208df05c83e8e49a48ffbdce2e43a0;0;3;3;FLOAT2;0,0;False;1;FLOAT;-0.5;False;2;FLOAT;2;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;96;-9728,1152;Inherit;False;Property;_Noise02Intensity;Noise 02 Intensity;14;0;Create;True;0;0;0;False;0;False;1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;97;-9344,1408;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;98;-9088,1664;Inherit;False;Property;_Tile02UVP;Tile 02 UV P;12;0;Create;True;0;0;0;False;0;False;-0.2,-0.1;-0.05,-0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SaturateNode;100;-6272,-256;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;101;-6528,1536;Inherit;True;Property;_OpacityMask;Opacity Mask;23;0;Create;True;0;0;0;False;3;Space(33);Header(Opacity Cutout Mask);Space(13);False;-1;504551a45813a5e4ab030aada700596e;b030380ebcd7b5943b7b1a68c0dc296a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;102;-9344,1024;Inherit;True;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;103;-8832,1408;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;105;-4736,1792;Inherit;False;Property;_DepthFade;Depth Fade;21;0;Create;True;0;0;0;False;3;Space(33);Header(Depth Fade);Space(13);False;0;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;106;-6016,1536;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;107;-8832,1024;Inherit;True;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DepthFade;111;-4736,1664;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;113;-5760,1536;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;121;-3712,-896;Inherit;False;emissiveness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;112;-5888,384;Inherit;True;Property;_BaseTexture;Base Texture;0;0;Create;True;0;0;0;False;3;Space(33);Header(Base Texture);Space(13);False;f1e3dfe2def118f4088a81914ed7ab7e;f1e3dfe2def118f4088a81914ed7ab7e;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;118;-4736,1536;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;126;-4608,640;Inherit;False;121;emissiveness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;136;-5504,128;Inherit;True;Property;_TextureSample5;Texture Sample 3;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;120;-4096,1792;Inherit;False;Property;_OpacityMultiply;Opacity Multiply;22;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;123;-4480,1536;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;127;-4608,384;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;130;-4224,384;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;125;-4096,1536;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;140;-3840,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;141;-3840,1536;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;137;-3712,-1152;Inherit;False;distortionIntensity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;139;-3840,896;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;44;-1024,512;Inherit;False;Property;_DistortionLerp;Distortion Lerp;7;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;138;-1024,640;Inherit;False;137;distortionIntensity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;129;-2560,512;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;15;-1280,384;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;42;-768,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;31;-1280,128;Inherit;False;Constant;_Vector0;Vector 0;3;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GrabScreenPosition;7;-1664,0;Inherit;False;0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;33;-1024,256;Inherit;False;3;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;8;-1408,0;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.CommentaryNode;45;462,-50;Inherit;False;1253;162.95;Lush was here! <3;5;3;2;4;5;1;Lush was here! <3;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleAddOpNode;32;-1024,0;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector4Node;12;-2176,1280;Inherit;False;Property;_CutoutMaskSelector;Cutout Mask Selector;5;0;Create;True;0;0;0;False;0;False;0,1,0,0;0,1,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;13;-2304,1024;Inherit;True;Property;_CutoutTexture;Cutout Texture;3;0;Create;True;0;0;0;False;3;Space(33);Header(Cutout);Space(13);False;-1;None;504551a45813a5e4ab030aada700596e;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.DotProductOpNode;10;-1920,1024;Inherit;True;2;0;COLOR;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;11;-1664,1024;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScreenColorNode;9;-768,0;Inherit;False;Global;_GrabScreen0;Grab Screen 0;2;0;Create;True;0;0;0;False;0;False;Object;-1;False;False;False;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.VertexColorNode;16;-384,256;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;3;1024,0;Inherit;False;Property;_Dst;Dst;27;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;2;768,0;Inherit;False;Property;_Src;Src;26;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;4;1280,0;Inherit;False;Property;_ZWrite;ZWrite;28;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;5;1537,0;Inherit;False;Property;_ZTest;ZTest;29;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;1;512,0;Inherit;False;Property;_Cull;Cull;25;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;110;-3712,-768;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;117;-3456,-768;Inherit;False;desaturation;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;128;-3968,-128;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;132;-3712,-128;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;24;-1792,608;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;14;-1536,608;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;131;-3424,288;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;133;-3168,288;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;Unlit;Vefects/SH_Vefects_VFX_Easy_Shockwave_Distortion;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;False;0;True;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;2;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;1;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;50;0;46;3
WireConnection;51;0;47;1
WireConnection;52;0;48;0
WireConnection;52;1;49;0
WireConnection;59;0;52;0
WireConnection;59;2;53;0
WireConnection;63;0;60;0
WireConnection;63;1;59;0
WireConnection;64;0;54;0
WireConnection;64;1;58;0
WireConnection;65;0;61;2
WireConnection;65;1;62;0
WireConnection;66;0;55;0
WireConnection;66;1;57;0
WireConnection;66;2;56;0
WireConnection;67;0;47;4
WireConnection;68;0;65;0
WireConnection;68;1;66;0
WireConnection;69;0;63;1
WireConnection;69;1;64;0
WireConnection;72;0;69;0
WireConnection;72;1;68;0
WireConnection;77;1;72;0
WireConnection;78;0;74;0
WireConnection;78;1;73;0
WireConnection;75;0;70;0
WireConnection;75;1;71;0
WireConnection;82;0;77;2
WireConnection;82;1;74;0
WireConnection;82;2;78;0
WireConnection;80;0;75;0
WireConnection;80;2;76;0
WireConnection;83;0;82;0
WireConnection;83;1;79;0
WireConnection;84;0;81;0
WireConnection;84;1;80;0
WireConnection;86;0;83;0
WireConnection;89;0;84;0
WireConnection;93;0;86;0
WireConnection;93;1;85;0
WireConnection;95;3;89;0
WireConnection;97;0;91;0
WireConnection;97;1;90;0
WireConnection;100;0;93;0
WireConnection;102;0;95;0
WireConnection;102;1;96;0
WireConnection;103;0;97;0
WireConnection;103;2;98;0
WireConnection;106;0;100;0
WireConnection;106;1;101;2
WireConnection;107;0;102;0
WireConnection;107;1;103;0
WireConnection;111;0;105;0
WireConnection;113;0;106;0
WireConnection;121;0;47;2
WireConnection;118;0;113;0
WireConnection;118;1;111;0
WireConnection;136;0;112;0
WireConnection;136;1;107;0
WireConnection;123;0;118;0
WireConnection;127;0;136;2
WireConnection;127;1;126;0
WireConnection;130;0;127;0
WireConnection;130;1;100;0
WireConnection;125;0;123;0
WireConnection;125;1;120;0
WireConnection;140;0;130;0
WireConnection;141;0;125;0
WireConnection;137;0;46;4
WireConnection;139;0;140;0
WireConnection;139;1;141;0
WireConnection;129;0;139;0
WireConnection;15;0;129;0
WireConnection;42;0;44;0
WireConnection;42;1;138;0
WireConnection;33;0;31;0
WireConnection;33;1;15;0
WireConnection;33;2;42;0
WireConnection;8;0;7;1
WireConnection;8;1;7;2
WireConnection;32;0;8;0
WireConnection;32;1;33;0
WireConnection;10;0;13;0
WireConnection;10;1;12;0
WireConnection;11;0;10;0
WireConnection;9;0;32;0
WireConnection;110;0;47;3
WireConnection;117;0;110;0
WireConnection;132;0;128;0
WireConnection;132;1;130;0
WireConnection;14;0;24;0
WireConnection;14;1;11;0
WireConnection;131;0;129;0
WireConnection;131;1;128;4
WireConnection;133;0;131;0
WireConnection;0;2;9;0
WireConnection;0;9;16;4
ASEEND*/
//CHKSM=CD035D04D5D095AD7EEF0178CB9B5F83B2DACD43