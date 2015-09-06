#include "IncLights.fx"
#include "IncVertexFormats.fx"

#define PC_FIRE 1
#define PC_RAIN 2

#define PT_EMITTER 0
#define PT_FLARE 1

cbuffer cbPerFrame : register (b0)
{
	float gMaximumAge;
	float gEmitterAge;
	float gTotalTime;
	float gElapsedTime;
	float3 gAccelerationWorld;
	float3 gEyePositionWorld;
	float4x4 gWorld;
	float4x4 gWorldViewProjection;
	uint gTextureCount;
	float gFogStart;
	float gFogRange;
	float4 gFogColor;
};
cbuffer cbFixed : register (b1)
{
	float2 gQuadTexC[4] = 
	{
		float2(0.0f, 1.0f),
		float2(0.0f, 0.0f),
		float2(1.0f, 1.0f),
		float2(1.0f, 0.0f)
	};
};

Texture2DArray gTextureArray;
Texture1D gTextureRandom;

float3 RandomVector3(float offset)
{
	//Use game time plus offset to sample random texture.
	float u = (gTotalTime + offset);
	
	return gTextureRandom.SampleLevel(SamplerLinear, u, 0).xyz;
}

VSVertexParticle VSStreamOut(VSVertexParticle input)
{
    return input;
}

[maxvertexcount(2)]
void GSStreamOutFire(point VSVertexParticle input[1], inout PointStream<VSVertexParticle> ptStream)
{
	input[0].age += gElapsedTime;
	
	if(input[0].type == PT_EMITTER)
	{
		if(input[0].age > gEmitterAge)
		{
			float3 vRandom = normalize(RandomVector3(0.0f));
			vRandom.x *= 0.33f;
			vRandom.z *= 0.33f;

			VSVertexParticle p;
			p.positionWorld = input[0].positionWorld;
			p.velocityWorld = vRandom * input[0].sizeWorld.x;
			p.sizeWorld = input[0].sizeWorld;
			p.color = input[0].color;
			p.age = 0.0f;
			p.type = PT_FLARE;

			ptStream.Append(p);
			
			input[0].age = 0.0f;
		}
		
		ptStream.Append(input[0]);
	}
	else
	{
		if(input[0].age <= gMaximumAge)
		{
			ptStream.Append(input[0]);
		}
	}
}
[maxvertexcount(50)]
void GSStreamOutSmoke(point VSVertexParticle input[1], inout PointStream<VSVertexParticle> ptStream)
{
	input[0].age += gElapsedTime;
	
	if(input[0].type == PT_EMITTER)
	{
		if(input[0].age > gEmitterAge)
		{
			for(int i = 0; i < 49; ++i)
			{
				float3 vRandom = normalize(RandomVector3((float)i / 49.0f));
				vRandom.x *= 0.5f;
				vRandom.z *= 0.5f;

				VSVertexParticle p;
				p.positionWorld = input[0].positionWorld;
				p.velocityWorld = vRandom * input[0].sizeWorld.x * 0.5f;
				p.sizeWorld = input[0].sizeWorld;
				p.color = input[0].color;
				p.age = 0.0f;
				p.type = PT_FLARE;

				ptStream.Append(p);
			}
			
			input[0].age = 0.0f;
		}
		
		ptStream.Append(input[0]);
	}
	else
	{
		if(input[0].age <= gMaximumAge)
		{
			ptStream.Append(input[0]);
		}
	}
}
[maxvertexcount(6)]
void GSStreamOutRain(point VSVertexParticle input[1], inout PointStream<VSVertexParticle> ptStream)
{
	input[0].age += gElapsedTime;
	
	if(input[0].type == PT_EMITTER)
	{
		if(input[0].age > gEmitterAge)
		{
			for(int i = 0; i < 5; ++i)
			{
				float3 vRandom = 35.0f * RandomVector3((float)i / 5.0f);
				vRandom.y = (gEyePositionWorld.y - (gAccelerationWorld.y * gMaximumAge)) * 0.5f;

				VSVertexParticle p;
				p.positionWorld = gEyePositionWorld.xyz + vRandom;
				p.velocityWorld = gAccelerationWorld;
				p.color = input[0].color;
				p.sizeWorld = input[0].sizeWorld;
				p.age = 0.0f;
				p.type = PT_FLARE;

				ptStream.Append(p);
			}

			input[0].age = 0.0f;
		}

		ptStream.Append(input[0]);
	}
	else
	{
		if(input[0].age <= gMaximumAge)
		{
			ptStream.Append(input[0]);
		}
	}
}

GSParticleSolid VSDrawSolid(VSVertexParticle input)
{
	float t = input.age;
	float opacity = 1.0f - smoothstep(0.0f, 1.0f, t / 1.0f);
	
	float3 pos = 0.5f * t * t * gAccelerationWorld + t * input.velocityWorld + input.positionWorld;

	GSParticleSolid output;
	output.positionWorld = pos;
	output.color = float4(input.color.rgb, opacity);
	output.sizeWorld = input.sizeWorld;
	output.type  = input.type;
	
	return output;
}
GSParticleLine VSDrawLine(VSVertexParticle input)
{
	float t = input.age;

	float3 pos = 0.5f * t * t * gAccelerationWorld + t * input.velocityWorld + input.positionWorld;

	GSParticleLine output;
	output.positionWorld = pos;
	output.color = input.color;
	output.type  = input.type;
	
	return output;
}

[maxvertexcount(4)]
void GSDrawSolid(point GSParticleSolid input[1], uint primID : SV_PrimitiveID, inout TriangleStream<PSParticleSolid> triStream)
{
	if(input[0].type != PT_EMITTER)
	{
		float3 look  = normalize(gEyePositionWorld.xyz - input[0].positionWorld);
		float3 right = normalize(cross(float3(0, 1, 0), look));
		float3 up    = cross(look, right);

		float halfWidth  = 0.5f * input[0].sizeWorld.x;
		float halfHeight = 0.5f * input[0].sizeWorld.y;
	
		float4 v[4];
		v[0] = float4(input[0].positionWorld + halfWidth * right - halfHeight * up, 1.0f);
		v[1] = float4(input[0].positionWorld + halfWidth * right + halfHeight * up, 1.0f);
		v[2] = float4(input[0].positionWorld - halfWidth * right - halfHeight * up, 1.0f);
		v[3] = float4(input[0].positionWorld - halfWidth * right + halfHeight * up, 1.0f);
		
		PSParticleSolid output;
		[unroll]
		for(int i = 0; i < 4; ++i)
		{
			v[i].y += halfHeight;

			output.positionHomogeneous = mul(v[i], gWorldViewProjection);
			output.positionWorld = mul(v[i], gWorld).xyz;
			output.tex = gQuadTexC[i];
			output.color = input[0].color;
			output.primitiveID = primID;
			
			triStream.Append(output);
		}
	}
}
[maxvertexcount(2)]
void GSDrawLine(point GSParticleLine input[1], uint primID : SV_PrimitiveID, inout LineStream<PSParticleLine> lineStream)
{
	if( input[0].type != PT_EMITTER )
	{
		float3 p0 = input[0].positionWorld;
		float3 p1 = input[0].positionWorld + 0.07f * gAccelerationWorld;
		
		PSParticleLine v0;
		v0.positionHomogeneous = mul(float4(p0, 1.0f), gWorldViewProjection);
		v0.positionWorld = mul(float4(p0, 1), gWorld).xyz;
		v0.color = input[0].color;
		v0.tex = float2(0.0f, 0.0f);
		v0.primitiveID = primID;
		lineStream.Append(v0);
		
		PSParticleLine v1;
		v1.positionHomogeneous = mul(float4(p1, 1.0f), gWorldViewProjection);
		v1.positionWorld = mul(float4(p1, 1), gWorld).xyz;
		v1.color = input[0].color;
		v1.tex  = float2(1.0f, 1.0f);
		v1.primitiveID = primID;
		lineStream.Append(v1);
	}
}

float4 PSDrawSolid(PSParticleSolid input) : SV_TARGET
{
	float3 uvw = float3(input.tex, input.primitiveID % gTextureCount);

	float4 litColor = gTextureArray.Sample(SamplerLinear, uvw) * input.color;

	if(gFogRange > 0)
	{
		float3 toEyeWorld = gEyePositionWorld - input.positionWorld;
		float distToEye = length(toEyeWorld);

		float4 fog = ComputeFog(litColor, distToEye, gFogStart, gFogRange, gFogColor);

		litColor = float4(fog.rgb, litColor.a);
	}

	return litColor;
}
float4 PSDrawLine(PSParticleLine input) : SV_TARGET
{
	float3 uvw = float3(input.tex, input.primitiveID % gTextureCount);

	float4 litColor = gTextureArray.Sample(SamplerLinear, uvw);

	if(gFogRange > 0)
	{
		float3 toEyeWorld = gEyePositionWorld - input.positionWorld;
		float distToEye = length(toEyeWorld);

		float4 fog = ComputeFog(litColor, distToEye, gFogStart, gFogRange, gFogColor);

		litColor = float4(fog.rgb, litColor.a);
	}

	return litColor;
}

GBufferPSOutput PSDeferredDrawSolid(PSParticleSolid input)
{
	GBufferPSOutput output = (GBufferPSOutput)0;

	float3 uvw = float3(input.tex, input.primitiveID % gTextureCount);
	float4 color = gTextureArray.Sample(SamplerLinear, uvw) * input.color;
	
	output.color = color;
	output.normal = 0;
	output.depth.xyz = input.positionHomogeneous.xyz;
	output.depth.w = input.positionHomogeneous.z / input.positionHomogeneous.w;
	output.shadow = 0;
	
	return output;
}
GBufferPSOutput PSDeferredDrawLine(PSParticleLine input)
{
	GBufferPSOutput output = (GBufferPSOutput)0;

	float3 uvw = float3(input.tex, input.primitiveID % gTextureCount);
	float4 color = gTextureArray.Sample(SamplerLinear, uvw) * input.color;

	output.color = color;
	output.normal = 0;
	output.depth.xyz = input.positionHomogeneous.xyz;
	output.depth.w = input.positionHomogeneous.z / input.positionHomogeneous.w;
	output.shadow = 0;
	
	return output;
}

GeometryShader gsStreamOutFire = ConstructGSWithSO(CompileShader(gs_5_0, GSStreamOutFire()), "POSITION.xyz; COLOR.rgba; VELOCITY.xyz; SIZE.xy; AGE.x; TYPE.x");
GeometryShader gsStreamOutSmoke = ConstructGSWithSO(CompileShader(gs_5_0, GSStreamOutSmoke()), "POSITION.xyz; COLOR.rgba; VELOCITY.xyz; SIZE.xy; AGE.x; TYPE.x");
GeometryShader gsStreamOutRain = ConstructGSWithSO(CompileShader(gs_5_0, GSStreamOutRain()), "POSITION.xyz; COLOR.rgba; VELOCITY.xyz; SIZE.xy; AGE.x; TYPE.x");

technique11 FireStreamOut
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSStreamOut()));
        SetGeometryShader(gsStreamOutFire);
        SetPixelShader(NULL);

        SetDepthStencilState(StencilDisableDepth, 0);
    }
}
technique11 SmokeStreamOut
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSStreamOut()));
        SetGeometryShader(gsStreamOutSmoke);
        SetPixelShader(NULL);

        SetDepthStencilState(StencilDisableDepth, 0);
    }
}
technique11 RainStreamOut
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSStreamOut()));
        SetGeometryShader(gsStreamOutRain);
        SetPixelShader(NULL);

        SetDepthStencilState(StencilDisableDepth, 0);
    }
}

technique11 SolidDraw
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSDrawSolid()));
        SetGeometryShader(CompileShader(gs_5_0, GSDrawSolid()));
        SetPixelShader(CompileShader(ps_5_0, PSDrawSolid()));

        SetDepthStencilState(StencilEnableDepth, 0);
    }
}
technique11 LineDraw
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSDrawLine()));
        SetGeometryShader(CompileShader(gs_5_0, GSDrawLine()));
        SetPixelShader(CompileShader(ps_5_0, PSDrawLine()));

        SetDepthStencilState(StencilEnableDepth, 0);
    }
}

technique11 DeferredSolidDraw
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSDrawSolid()));
        SetGeometryShader(CompileShader(gs_5_0, GSDrawSolid()));
        SetPixelShader(CompileShader(ps_5_0, PSDeferredDrawSolid()));

        SetDepthStencilState(StencilEnableDepth, 0);
    }
}
technique11 DeferredLineDraw
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, VSDrawLine()));
        SetGeometryShader(CompileShader(gs_5_0, GSDrawLine()));
        SetPixelShader(CompileShader(ps_5_0, PSDeferredDrawLine()));

        SetDepthStencilState(StencilEnableDepth, 0);
    }
}
