// Made with Amplify Shader Editor v1.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Vefects/SH_Vefects_VFX_Easy_Shockwave"
{
	Properties
	{
		[Space(33)][Header(Base Texture)][Space(13)]_BaseTexture("Base Texture", 2D) = "white" {}
		[Space(33)][Header(Noise)][Space(13)]_NoiseTexture("Noise Texture", 2D) = "white" {}
		_Noise01UVS("Noise 01 UV S", Vector) = (1,1,0,0)
		_Noise01UVP("Noise 01 UV P", Vector) = (0.1,0.3,0,0)
		[Normal][Space(33)][Header(Noise Distortion)][Space(13)]_NoiseDistortionTexture("Noise Distortion Texture", 2D) = "white" {}
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
		[Space(33)][Header(AR)][Space(13)]_Cull("Cull", Float) = 0
		_Src("Src", Float) = 5
		_Dst("Dst", Float) = 10
		_ZTest("ZTest", Float) = 2
		_ZWrite("ZWrite", Float) = 0
		_ErosionSmoothness("Erosion Smoothness", Float) = 1
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
		
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#include "UnityCG.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19800
		#pragma surface surf Unlit keepalpha noshadow 
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float4 vertexColor : COLOR;
			float4 uv_texcoord;
			float4 uv2_texcoord2;
			float4 screenPos;
		};

		uniform float _Dst;
		uniform float _Src;
		uniform float _ZWrite;
		uniform float _ZTest;
		uniform float _Cull;
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

		inline half4 LightingUnlit( SurfaceOutput s, half3 lightDir, half atten )
		{
			return half4 ( 0, 0, 0, s.Alpha );
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 panner13 = ( 1.0 * _Time.y * _NoiseDistUVP + ( i.uv_texcoord.xy * _NoiseDistUVS ));
			float2 panner25 = ( 1.0 * _Time.y * _Tile02UVP + ( i.uv_texcoord.xy * _TileUVS ));
			float2 temp_output_28_0 = ( ( ( ( (UnpackNormal( tex2D( _NoiseDistortionTexture, panner13 ) )).xy + -0.5 ) * 2.0 ) * _Noise02Intensity ) + panner25 );
			float chromaticAberration92 = i.uv_texcoord.w;
			float temp_output_36_0 = ( chromaticAberration92 * 0.001 );
			float2 temp_cast_0 = (temp_output_36_0).xx;
			float3 appendResult37 = (float3(tex2D( _BaseTexture, ( temp_output_28_0 + temp_output_36_0 ) ).r , tex2D( _BaseTexture, temp_output_28_0 ).g , tex2D( _BaseTexture, ( temp_output_28_0 - temp_cast_0 ) ).b));
			float desaturation97 = saturate( i.uv2_texcoord2.z );
			float3 desaturateInitialColor39 = appendResult37;
			float desaturateDot39 = dot( desaturateInitialColor39, float3( 0.299, 0.587, 0.114 ));
			float3 desaturateVar39 = lerp( desaturateInitialColor39, desaturateDot39.xxx, desaturation97 );
			float emissiveness95 = i.uv2_texcoord2.y;
			float eros127 = i.uv2_texcoord2.w;
			float2 panner6 = ( 1.0 * _Time.y * _Noise01UVP + ( i.uv_texcoord.xy * _Noise01UVS ));
			float noiseMultiply88 = i.uv2_texcoord2.x;
			float customOffset86 = i.uv_texcoord.z;
			float lerpResult44 = lerp( _CustomOffsetBase , customOffset86 , _CustomOffsetLerp);
			float smoothstepResult116 = smoothstep( eros127 , ( eros127 + _ErosionSmoothness ) , tex2D( _IntensityMask, ( ( tex2D( _NoiseTexture, panner6 ).r * ( _Noise01Intensity * noiseMultiply88 ) ) + ( ( i.uv_texcoord.xy.y * _IntensityMaskUVS ) + lerpResult44 ) ) ).g);
			float temp_output_68_0 = saturate( ( saturate( pow( smoothstepResult116 , _IntensityMaskPower ) ) * _IntensityMaskMultiply ) );
			o.Emission = ( i.vertexColor * float4( ( ( desaturateVar39 * emissiveness95 ) * temp_output_68_0 ) , 0.0 ) ).rgb;
			float2 uv_OpacityMask = i.uv_texcoord * _OpacityMask_ST.xy + _OpacityMask_ST.zw;
			float4 ase_positionSS = float4( i.screenPos.xyz , i.screenPos.w + 1e-7 );
			float4 ase_positionSSNorm = ase_positionSS / ase_positionSS.w;
			ase_positionSSNorm.z = ( UNITY_NEAR_CLIP_VALUE >= 0 ) ? ase_positionSSNorm.z : ase_positionSSNorm.z * 0.5 + 0.5;
			float screenDepth107 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE( _CameraDepthTexture, ase_positionSSNorm.xy ));
			float distanceDepth107 = saturate( ( screenDepth107 - LinearEyeDepth( ase_positionSSNorm.z ) ) / ( _DepthFade ) );
			o.Alpha = saturate( ( saturate( ( saturate( ( saturate( ( temp_output_68_0 * tex2D( _OpacityMask, uv_OpacityMask ).g ) ) * distanceDepth107 ) ) * _OpacityMultiply ) ) * i.vertexColor.a ) );
		}

		ENDCG
	}
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19800
Node;AmplifyShaderEditor.TexCoordVertexDataNode;48;-1152,-1664;Inherit;False;0;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TexCoordVertexDataNode;20;-1152,-1408;Inherit;False;1;4;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.TextureCoordinatesNode;1;-9088,-640;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;8;-8704,-512;Inherit;False;Property;_Noise01UVS;Noise 01 UV S;2;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RegisterLocalVarNode;86;-896,-1664;Inherit;False;customOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;88;-896,-1408;Inherit;False;noiseMultiply;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;7;-8704,-640;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;9;-8192,-512;Inherit;False;Property;_Noise01UVP;Noise 01 UV P;3;0;Create;True;0;0;0;False;0;False;0.1,0.3;-0.07,-0.3;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.RangedFloatNode;17;-7424,-384;Inherit;False;Property;_Noise01Intensity;Noise 01 Intensity;9;0;Create;True;0;0;0;False;0;False;-1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;104;-6144,-256;Inherit;False;Property;_CustomOffsetBase;Custom Offset Base;12;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;47;-5888,-128;Inherit;False;Property;_CustomOffsetLerp;Custom Offset Lerp;11;0;Create;True;0;0;0;False;0;False;0;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;87;-6144,-128;Inherit;False;86;customOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;89;-7424,-256;Inherit;False;88;noiseMultiply;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;6;-8192,-640;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;4;-7808,-896;Inherit;True;Property;_NoiseTexture;Noise Texture;1;0;Create;True;0;0;0;False;3;Space(33);Header(Noise);Space(13);False;7437ec4c7494cc5409d26f1abc0906eb;7437ec4c7494cc5409d26f1abc0906eb;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.TextureCoordinatesNode;42;-6656,-384;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;114;-6400,-256;Inherit;False;Property;_IntensityMaskUVS;Intensity Mask UV S;14;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;3;-7808,-640;Inherit;True;Property;_TextureSample0;Texture Sample 0;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;18;-7168,-384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;43;-6400,-384;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.LerpOp;44;-5888,-256;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;127;-896,-1024;Inherit;False;eros;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;45;-5632,-384;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;15;-7296,-640;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;10;-9088,640;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.Vector2Node;12;-8704,768;Inherit;False;Property;_NoiseDistUVS;Noise Dist UV S;5;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SimpleAddOpNode;46;-5632,-640;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;119;-4608,-384;Inherit;False;Property;_ErosionSmoothness;Erosion Smoothness;27;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;128;-4608,-768;Inherit;False;127;eros;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;11;-8704,640;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;14;-8192,768;Inherit;False;Property;_NoiseDistUVP;Noise Dist UV P;6;0;Create;True;0;0;0;False;0;False;-0.2,-0.1;0.02,-0.1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SamplerNode;49;-5376,-640;Inherit;True;Property;_IntensityMask;Intensity Mask;13;0;Create;True;0;0;0;False;3;Space(33);Header(Intensity Mask);Space(13);False;-1;9b81424c7eafdf54fb47aa88eb4f3ac9;504551a45813a5e4ab030aada700596e;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleAddOpNode;120;-4608,-512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;69;-4224,-768;Inherit;False;Property;_IntensityMaskPower;Intensity Mask Power;16;0;Create;True;0;0;0;False;0;False;1;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;13;-8192,640;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TexturePropertyNode;109;-7808,384;Inherit;True;Property;_NoiseDistortionTexture;Noise Distortion Texture;4;1;[Normal];Create;True;0;0;0;False;3;Space(33);Header(Noise Distortion);Space(13);False;8a635c01852d10342874a696292bbfd0;8a635c01852d10342874a696292bbfd0;True;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SmoothstepOpNode;116;-4608,-640;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;66;-4224,-640;Inherit;True;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;5;-7808,640;Inherit;True;Property;_TextureSample1;Texture Sample 1;0;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;70;-3712,-768;Inherit;False;Property;_IntensityMaskMultiply;Intensity Mask Multiply;17;0;Create;True;0;0;0;False;0;False;1;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;115;-3968,-640;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;110;-7424,640;Inherit;True;True;True;False;True;1;0;FLOAT3;0,0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;26;-6912,1280;Inherit;False;Property;_TileUVS;Tile UV S;7;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode;23;-6912,1024;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;92;-896,-1536;Inherit;False;chromaticAberration;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;67;-3712,-640;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;111;-7168,640;Inherit;True;ConstantBiasScale;-1;;1;63208df05c83e8e49a48ffbdce2e43a0;0;3;3;FLOAT2;0,0;False;1;FLOAT;-0.5;False;2;FLOAT;2;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;22;-6912,768;Inherit;False;Property;_Noise02Intensity;Noise 02 Intensity;10;0;Create;True;0;0;0;False;0;False;1;0.1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;24;-6528,1024;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector2Node;27;-6272,1280;Inherit;False;Property;_Tile02UVP;Tile 02 UV P;8;0;Create;True;0;0;0;False;0;False;-0.2,-0.1;-0.05,-0.2;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.GetLocalVarNode;93;-3456,512;Inherit;False;92;chromaticAberration;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;68;-3456,-640;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;50;-3712,1152;Inherit;True;Property;_OpacityMask;Opacity Mask;20;0;Create;True;0;0;0;False;3;Space(33);Header(Opacity Cutout Mask);Space(13);False;-1;504551a45813a5e4ab030aada700596e;b030380ebcd7b5943b7b1a68c0dc296a;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;16;-6528,640;Inherit;True;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.PannerNode;25;-6016,1024;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;36;-3072,512;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0.001;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;108;-1920,1408;Inherit;False;Property;_DepthFade;Depth Fade;18;0;Create;True;0;0;0;False;3;Space(33);Header(Depth Fade);Space(13);False;0;0.3;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;54;-3200,1152;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;28;-6016,640;Inherit;True;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleAddOpNode;34;-3072,256;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;35;-3072,640;Inherit;False;2;0;FLOAT2;0,0;False;1;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;100;-896,-1152;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DepthFade;107;-1920,1280;Inherit;False;True;True;False;2;1;FLOAT3;0,0,0;False;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;29;-3072,0;Inherit;True;Property;_BaseTexture;Base Texture;0;0;Create;True;0;0;0;False;3;Space(33);Header(Base Texture);Space(13);False;f1e3dfe2def118f4088a81914ed7ab7e;f1e3dfe2def118f4088a81914ed7ab7e;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.SaturateNode;56;-2944,1152;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;33;-2688,512;Inherit;True;Property;_TextureSample4;Texture Sample 4;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;31;-2688,0;Inherit;True;Property;_TextureSample2;Texture Sample 2;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SamplerNode;32;-2688,256;Inherit;True;Property;_TextureSample3;Texture Sample 3;11;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RegisterLocalVarNode;97;-640,-1152;Inherit;False;desaturation;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;105;-1920,1152;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;37;-2304,0;Inherit;True;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;91;-1280,1408;Inherit;False;Property;_OpacityMultiply;Opacity Multiply;19;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;95;-896,-1280;Inherit;False;emissiveness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;98;-2048,256;Inherit;False;97;desaturation;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;106;-1664,1152;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DesaturateOpNode;39;-2048,0;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;57;-1280,1152;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;96;-1792,256;Inherit;False;95;emissiveness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;40;-1792,0;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.VertexColorNode;60;-1152,-512;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;83;-896,1152;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;81;848,-48;Inherit;False;1252;162.95;Lush was here! <3;5;76;78;77;79;80;Lush was here! <3;0,0,0,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;41;-1408,0;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;84;-768,768;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;61;-896,-512;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;FLOAT3;0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.RangedFloatNode;78;1408,0;Inherit;False;Property;_Dst;Dst;23;0;Create;True;0;0;0;True;0;False;10;10;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;77;1152,0;Inherit;False;Property;_Src;Src;22;0;Create;True;0;0;0;True;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;85;-512,768;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;79;1664,0;Inherit;False;Property;_ZWrite;ZWrite;25;0;Create;True;0;0;0;True;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;80;1920,0;Inherit;False;Property;_ZTest;ZTest;24;0;Create;True;0;0;0;True;0;False;2;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;76;896,0;Inherit;False;Property;_Cull;Cull;21;0;Create;True;0;0;0;True;3;Space(33);Header(AR);Space(13);False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;52;-2176,-512;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;51;-1792,-512;Inherit;True;Property;_LUT;LUT;15;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;0,0;Float;False;True;-1;3;ASEMaterialInspector;0;0;Unlit;Vefects/SH_Vefects_VFX_Easy_Shockwave;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;Back;0;True;_ZWrite;0;True;_ZTest;False;0;False;;0;False;;False;0;Custom;0.5;True;False;0;True;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;2;5;True;_Src;10;True;_Dst;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;26;-1;-1;-1;0;False;0;0;True;_Cull;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;16;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;86;0;48;3
WireConnection;88;0;20;1
WireConnection;7;0;1;0
WireConnection;7;1;8;0
WireConnection;6;0;7;0
WireConnection;6;2;9;0
WireConnection;3;0;4;0
WireConnection;3;1;6;0
WireConnection;18;0;17;0
WireConnection;18;1;89;0
WireConnection;43;0;42;2
WireConnection;43;1;114;0
WireConnection;44;0;104;0
WireConnection;44;1;87;0
WireConnection;44;2;47;0
WireConnection;127;0;20;4
WireConnection;45;0;43;0
WireConnection;45;1;44;0
WireConnection;15;0;3;1
WireConnection;15;1;18;0
WireConnection;46;0;15;0
WireConnection;46;1;45;0
WireConnection;11;0;10;0
WireConnection;11;1;12;0
WireConnection;49;1;46;0
WireConnection;120;0;128;0
WireConnection;120;1;119;0
WireConnection;13;0;11;0
WireConnection;13;2;14;0
WireConnection;116;0;49;2
WireConnection;116;1;128;0
WireConnection;116;2;120;0
WireConnection;66;0;116;0
WireConnection;66;1;69;0
WireConnection;5;0;109;0
WireConnection;5;1;13;0
WireConnection;115;0;66;0
WireConnection;110;0;5;0
WireConnection;92;0;48;4
WireConnection;67;0;115;0
WireConnection;67;1;70;0
WireConnection;111;3;110;0
WireConnection;24;0;23;0
WireConnection;24;1;26;0
WireConnection;68;0;67;0
WireConnection;16;0;111;0
WireConnection;16;1;22;0
WireConnection;25;0;24;0
WireConnection;25;2;27;0
WireConnection;36;0;93;0
WireConnection;54;0;68;0
WireConnection;54;1;50;2
WireConnection;28;0;16;0
WireConnection;28;1;25;0
WireConnection;34;0;28;0
WireConnection;34;1;36;0
WireConnection;35;0;28;0
WireConnection;35;1;36;0
WireConnection;100;0;20;3
WireConnection;107;0;108;0
WireConnection;56;0;54;0
WireConnection;33;0;29;0
WireConnection;33;1;35;0
WireConnection;31;0;29;0
WireConnection;31;1;34;0
WireConnection;32;0;29;0
WireConnection;32;1;28;0
WireConnection;97;0;100;0
WireConnection;105;0;56;0
WireConnection;105;1;107;0
WireConnection;37;0;31;1
WireConnection;37;1;32;2
WireConnection;37;2;33;3
WireConnection;95;0;20;2
WireConnection;106;0;105;0
WireConnection;39;0;37;0
WireConnection;39;1;98;0
WireConnection;57;0;106;0
WireConnection;57;1;91;0
WireConnection;40;0;39;0
WireConnection;40;1;96;0
WireConnection;83;0;57;0
WireConnection;41;0;40;0
WireConnection;41;1;68;0
WireConnection;84;0;83;0
WireConnection;84;1;60;4
WireConnection;61;0;60;0
WireConnection;61;1;41;0
WireConnection;85;0;84;0
WireConnection;51;1;52;2
WireConnection;0;2;61;0
WireConnection;0;9;85;0
ASEEND*/
//CHKSM=A376635AAC40B9C9B235BA5CEAF43F939E49EBBE