#include "IncLights.fx"
#include "IncVertexFormats.fx"

cbuffer cbGlobals : register (b0)
{
	uint gMaterialPaletteWidth;
};
cbuffer cbPerFrame : register (b1)
{
	float4x4 gWorld;
	float4x4 gWorldViewProjection;
	float3 gEyePositionWorld;
};
cbuffer cbPerDirLight : register (b2)
{
	DirectionalLight gDirLight;
	float4x4 gLightViewProjection;
	uint gShadows;
}
cbuffer cbPerPointLight : register (b3)
{
	PointLight gPointLight;
}
cbuffer cbPerSpotLight : register (b4)
{
	SpotLight gSpotLight;
}
cbuffer cbCombineLights : register (b5)
{
	float gGlobalAmbient;
	float gFogStart;
	float gFogRange;
	float4 gFogColor;
}

Texture2D gTG1Map : register(t0);
Texture2D gTG2Map : register(t1);
Texture2D gTG3Map : register(t2);
Texture2D gShadowMapStatic : register(t3);
Texture2D gShadowMapDynamic : register(t4);
Texture2D gLightMap : register(t5);
Texture2D gMaterialPalette : register(t6);

struct PSQuadInput
{
    float4 positionHomogeneous : SV_POSITION;
    float2 tex : TEXCOORD0;
};
struct PSLightInput
{
	float4 positionHomogeneous : SV_POSITION;
	float4 positionScreen : TEXCOORD0;
};
struct PSStencilInput
{
	float4 positionHomogeneous : SV_POSITION;
};

PSQuadInput VSQuad(VSVertexPositionTexture input)
{
	PSQuadInput output = (PSQuadInput)0;

    output.positionHomogeneous = mul(float4(input.positionLocal, 1), gWorldViewProjection);
    output.tex = input.tex;
    
    return output;
}
PSLightInput VSLight(VSVertexPosition input)
{
	PSLightInput output = (PSLightInput)0;

    output.positionHomogeneous = mul(float4(input.positionLocal, 1), gWorldViewProjection);
    output.positionScreen = output.positionHomogeneous;
    
    return output;
}
PSStencilInput VSStencil(VSVertexPosition input)
{
	PSStencilInput output = (PSStencilInput)0;

	output.positionHomogeneous = mul(float4(input.positionLocal, 1), gWorldViewProjection);

	return output;
}

float4 PSDirectionalLight(PSQuadInput input) : SV_TARGET
{
    float4 tg2 = gTG2Map.SampleLevel(SamplerPoint, input.tex, 0);
    float4 tg3 = gTG3Map.SampleLevel(SamplerPoint, input.tex, 0);

	float3 normal = tg2.xyz;
	float doLighting = tg2.a;
	float3 position = tg3.xyz;
	float materialIndex = tg3.w;

	if(doLighting == 0)
	{
		Material k = GetMaterialData(gMaterialPalette, materialIndex, gMaterialPaletteWidth);

		float4 lightPosition = mul(float4(position, 1), gLightViewProjection);

		float4 diffuse = 0;
		float4 specular = 0;

		ComputeDirectionalLight(
			gDirLight, 
			k.Shininess,
			position,
			normal,
			gEyePositionWorld,
			lightPosition,
			gShadows,
			gShadowMapStatic,
			gShadowMapDynamic,
			diffuse,
			specular);

		diffuse = k.Diffuse * diffuse;
		specular = k.Specular * specular;

		return diffuse + specular;
	}
	else
	{
		return 0;
	}
};
float4 PSPointLight(PSLightInput input) : SV_TARGET
{
	//Get texture coordinates
	float4 lPosition = input.positionScreen;
	lPosition.xy /= lPosition.w;
	float2 tex = 0.5f * (float2(lPosition.x, -lPosition.y) + 1);

    float4 tg2 = gTG2Map.SampleLevel(SamplerPoint, tex, 0);
    float4 tg3 = gTG3Map.SampleLevel(SamplerPoint, tex, 0);

	float3 normal = tg2.xyz;
	float doLighting = tg2.a;
	float3 position = tg3.xyz;
	float materialIndex = tg3.w;

	if(doLighting == 0)
	{
		Material k = GetMaterialData(gMaterialPalette, materialIndex, gMaterialPaletteWidth);

		float4 diffuse = 0;
		float4 specular = 0;

		ComputePointLight(
			gPointLight, 
			k.Shininess,
			position,
			normal,
			gEyePositionWorld,
			diffuse,
			specular);

		diffuse = k.Diffuse * diffuse;
		specular = k.Specular * specular;

		return diffuse + specular;
	}
	else
	{
		return 0;
	}
};
float4 PSSpotLight(PSLightInput input) : SV_TARGET
{
	//Get texture coordinates
	float4 lPosition = input.positionScreen;
	lPosition.xy /= lPosition.w;
	float2 tex = 0.5f * (float2(lPosition.x, -lPosition.y) + 1);

    float4 tg2 = gTG2Map.SampleLevel(SamplerPoint, tex, 0);
    float4 tg3 = gTG3Map.SampleLevel(SamplerPoint, tex, 0);
	
	float3 normal = tg2.xyz;
	float doLighting = tg2.a;
	float3 position = tg3.xyz;
	float materialIndex = tg3.w;

	if(doLighting == 0)
	{
		Material k = GetMaterialData(gMaterialPalette, materialIndex, gMaterialPaletteWidth);

		float4 diffuse = 0;
		float4 specular = 0;

		ComputeSpotLight(
			gSpotLight, 
			k.Shininess,
			position,
			normal,
			gEyePositionWorld,
			diffuse,
			specular);

		diffuse = k.Diffuse * diffuse;
		specular = k.Specular * specular;

		return diffuse + specular;
	}
	else
	{
		return 0;
	}
};
float4 PSCombineLights(PSQuadInput input) : SV_TARGET
{
    float4 tg1 = gTG1Map.SampleLevel(SamplerPoint, input.tex, 0);
    float4 tg2 = gTG2Map.SampleLevel(SamplerPoint, input.tex, 0);
    float4 tg3 = gTG3Map.SampleLevel(SamplerPoint, input.tex, 0);
	float4 lmap = gLightMap.Sample(SamplerPoint, input.tex);

	float doLighting = tg2.w;
	if(doLighting == 0)
	{
		float4 color = tg1;
		float3 position = tg3.xyz;
		float materialIndex = tg3.w;
		float4 light = lmap;

		Material k = GetMaterialData(gMaterialPalette, materialIndex, gMaterialPaletteWidth);

		float4 emissive = k.Emissive;
		float4 ambient = k.Ambient * gGlobalAmbient;

		color = (emissive + ambient + light) * (color);

		if(gFogRange > 0)
		{
			float distToEye = length(gEyePositionWorld - position);

			color = ComputeFog(color, distToEye, gFogStart, gFogRange, gFogColor);
		}

		return saturate(color);
	}
	else
	{
		return tg1;
	}
};

technique11 DeferredDirectionalLight
{
    pass P0
    {
		SetVertexShader(CompileShader(vs_5_0, VSQuad()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_5_0, PSDirectionalLight()));
    }
}
technique11 DeferredPointStencil
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_5_0, VSStencil()));
		SetGeometryShader(NULL);
		SetPixelShader(NULL);
	}
}
technique11 DeferredPointLight
{
    pass P0
    {
		SetVertexShader(CompileShader(vs_5_0, VSLight()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_5_0, PSPointLight()));
	}
}
technique11 DeferredSpotStencil
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_5_0, VSStencil()));
		SetGeometryShader(NULL);
		SetPixelShader(NULL);
	}
}
technique11 DeferredSpotLight
{
    pass P0
    {
		SetVertexShader(CompileShader(vs_5_0, VSLight()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_5_0, PSSpotLight()));
	}
}
technique11 DeferredCombineLights
{
    pass P0
    {
		SetVertexShader(CompileShader(vs_5_0, VSQuad()));
		SetGeometryShader(NULL);
		SetPixelShader(CompileShader(ps_5_0, PSCombineLights()));
    }
}
