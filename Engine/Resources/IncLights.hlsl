#include "IncHelpers.hlsl"
#include "IncMaterials.hlsl"

#define MAX_LIGHTS_DIRECTIONAL          3
#define MAX_LIGHTS_DIRECTIONAL_CASCADES 3
#define MAX_LIGHTS_POINT                16
#define MAX_LIGHTS_SPOT                 16

#define SHADOW_SAMPLES_HD   16
#define SHADOW_SAMPLES_LD   4

#define MaxSampleCount      16

struct HemisphericLight
{
    float3 AmbientDown;
    float3 AmbientRange;
};
struct DirectionalLight
{
    float3 Diffuse;
    float Pad1;
    float3 Specular;
    float Pad2;
    float3 DirToLight;
    float CastShadow;
    float4 ToCascadeOffsetX;
    float4 ToCascadeOffsetY;
    float4 ToCascadeScale;
    float4x4 ToShadowSpace;
};
struct PointLight
{
    float3 Diffuse;
    float Pad1;
    float3 Specular;
    float Pad2;
    float3 Position;
    float Intensity;
    float Radius;
    float CastShadow;
    float2 PerspectiveValues;
    int MapIndex;
    uint Pad3;
    uint Pad4;
    uint Pad5;
};
struct SpotLight
{
    float3 Diffuse;
    float Pad1;
    float3 Specular;
    float Pad2;
    float3 Position;
    float Angle;
    float3 Direction;
    float Intensity;
    float Radius;
    float CastShadow;
    int MapIndex;
    uint MapCount;
    float4x4 FromLightVP;
};

static float2 poissonDisk[MaxSampleCount] =
{
    float2(0.2770745f, 0.6951455f),
	float2(0.1874257f, -0.02561589f),
	float2(-0.3381929f, 0.8713168f),
	float2(0.5867746f, 0.1087471f),
	float2(-0.3078699f, 0.188545f),
	float2(0.7993396f, 0.4595091f),
	float2(-0.09242552f, 0.5260149f),
	float2(0.3657553f, -0.5329605f),
	float2(-0.3829718f, -0.2476171f),
	float2(-0.01085108f, -0.6966301f),
	float2(0.8404155f, -0.3543923f),
	float2(-0.5186161f, -0.7624033f),
	float2(-0.8135794f, 0.2328489f),
	float2(-0.784665f, -0.2434929f),
	float2(0.9920505f, 0.0855163f),
	float2(-0.687256f, 0.6711345f)
};

static float bias = 0.0001f;
static float poissonFactor = 3500.0f;
static float minShadowFactor = 0.2;

inline float CalcSpotShadowFactor(float3 pPosition, int mapIndex, float4x4 fromLightVP, Texture2DArray<float> shadowMap, uint samples)
{
    if (samples <= 0)
    {
        return 1.0f;
    }

    float4 lightPosition = mul(float4(pPosition, 1), fromLightVP);

    float2 tex = 0.0f;
    tex.x = (+lightPosition.x / lightPosition.w * 0.5f) + 0.5f;
    tex.y = (-lightPosition.y / lightPosition.w * 0.5f) + 0.5f;
    float z = (lightPosition.z / lightPosition.w) - bias;

    float sShadow = 0.0f;

    for (uint i = 0; i < samples; i++)
    {
        float3 stc = float3(tex + poissonDisk[i] / poissonFactor, mapIndex);

        sShadow += shadowMap.SampleCmpLevelZero(SamplerComparisonLessEqual, stc, z);
    }

    return max(minShadowFactor, (sShadow / float(samples)));
}
inline float CalcPointShadowFactor(float3 pPosition, PointLight pointLight, TextureCubeArray<float> shadowMapPoint)
{
    float3 toPixel = pPosition - pointLight.Position;
	
	// Calc PCF depth
    float3 toPixelAbs = abs(toPixel);
    float z = max(toPixelAbs.x, max(toPixelAbs.y, toPixelAbs.z));
    float depth = (pointLight.PerspectiveValues.x * z + pointLight.PerspectiveValues.y) / z;

    return max(minShadowFactor, shadowMapPoint.SampleCmpLevelZero(PCFSampler, float4(toPixel, pointLight.MapIndex), depth));
}
inline float CalcCascadedShadowFactor(float3 position, float4x4 toShadowSpace, float4 toCascadeOffsetX, float4 toCascadeOffsetY, float4 toCascadeScale, Texture2DArray<float> CascadeShadowMapTexture)
{
	// Transform the world position to shadow space
    float4 posShadowSpace = mul(float4(position, 1.0), toShadowSpace);

	// Transform the shadow space position into each cascade position
    float4 posCascadeSpaceX = (toCascadeOffsetX + posShadowSpace.xxxx) * toCascadeScale;
    float4 posCascadeSpaceY = (toCascadeOffsetY + posShadowSpace.yyyy) * toCascadeScale;

	// Check which cascade we are in
    float4 inCascadeX = abs(posCascadeSpaceX) <= 1.0;
    float4 inCascadeY = abs(posCascadeSpaceY) <= 1.0;
    float4 inCascade = inCascadeX * inCascadeY;

	// Prepare a mask for the highest quality cascade the position is in
    float4 bestCascadeMask = inCascade;
    bestCascadeMask.yzw = (1.0 - bestCascadeMask.x) * bestCascadeMask.yzw;
    bestCascadeMask.zw = (1.0 - bestCascadeMask.y) * bestCascadeMask.zw;
    bestCascadeMask.w = (1.0 - bestCascadeMask.z) * bestCascadeMask.w;
    float bestCascade = dot(bestCascadeMask, float4(0.0, 1.0, 2.0, 3.0));
    if (bestCascade >= MAX_LIGHTS_DIRECTIONAL_CASCADES)
    {
		// Fully lit
        return 1.0;
    }

	// Pick the position in the selected cascade
    float3 UVD;
    UVD.x = dot(posCascadeSpaceX, bestCascadeMask);
    UVD.y = dot(posCascadeSpaceY, bestCascadeMask);
    UVD.z = posShadowSpace.z - bias;

	// Convert to shadow map UV values
    UVD.xy = 0.5 * UVD.xy + 0.5;
    UVD.y = 1.0 - UVD.y;

	// Compute the hardware PCF value
    float shadow = CascadeShadowMapTexture.SampleCmpLevelZero(PCFSampler, float3(UVD.xy, bestCascade), UVD.z);
	
	// set the shadow to one (fully lit) for positions with no cascade coverage
    shadow = saturate(shadow + 1.0 - any(bestCascadeMask));
	
    return shadow;
}

inline float CalcFogFactor(float distToEye, float fogStart, float fogRange)
{
    return saturate((distToEye - fogStart) / fogRange);
}
inline float4 ComputeFog(float4 litColor, float distToEye, float fogStart, float fogRange, float4 fogColor)
{
    float fogLerp = saturate((distToEye - fogStart) / fogRange);

    return float4(lerp(litColor.rgb, fogColor.rgb, fogLerp), litColor.a);
}

inline float3 DiffusePass(float3 normal, float3 light, float3 lightDiffuseColor)
{
    return max(0, dot(light, normal)) * lightDiffuseColor;
}

inline float3 SpecularPassPhong(float3 normal, float3 viewer, float3 light, float3 lightSpecularColor, Material k)
{
    float3 R = normalize(-reflect(light, normal));
    
    return (pow(max(0, dot(R, viewer)), k.Shininess)) * lightSpecularColor;
}
inline float3 SpecularPassBlinnPhong(float3 normal, float3 viewer, float3 light, float3 lightSpecularColor, Material k)
{
    float3 R = reflect(viewer, normal);
    
    return pow(max(0, dot(R, -light)), k.Shininess) * lightSpecularColor;
}
inline float3 SpecularPassCookTorrance(float3 normal, float3 viewer, float3 light, float3 lightColor, float3 lightSpecularColor, Material k)
{
    float NdotL = max(0, dot(normal, light));
    float Rs = 0.0;
    if (NdotL > 0)
    {
        float3 H = normalize(light + viewer);
        float NdotH = max(0, dot(normal, H));
        float NdotV = max(0, dot(normal, viewer));
        float VdotH = max(0, dot(light, H));

		// Fresnel reflectance
        float F = pow(1.0 - VdotH, 5.0);
        F *= (1.0 - k.ReflectionAtNormIncidence);
        F += k.ReflectionAtNormIncidence;

		// Microfacet distribution by Beckmann
        float m_squared = k.RoughnessValue * k.RoughnessValue;
        float r1 = 1.0 / (4.0 * m_squared * pow(NdotH, 4.0));
        float r2 = (NdotH * NdotH - 1.0) / (m_squared * NdotH * NdotH);
        float D = r1 * exp(r2);

		// Geometric shadowing
        float two_NdotH = 2.0 * NdotH;
        float g1 = (two_NdotH * NdotV) / VdotH;
        float g2 = (two_NdotH * NdotL) / VdotH;
        float G = min(1.0, min(g1, g2));

        Rs = (F * D * G) / (PI * NdotL * NdotV);
    }
    
    return k.Diffuse.rgb * lightColor * NdotL + lightColor * k.Specular.rgb * NdotL * (k.Shininess + Rs * (1.0 - k.Shininess));
}
inline float3 SpecularPassCookTorrance2(float3 normal, float3 viewer, float3 light, float3 lightSpecularColor, Material k)
{
	// Compute any aliases and intermediary values	
    float3 half_vector = normalize(light + viewer);
    float NdotL = saturate(dot(normal, light));
    float NdotH = saturate(dot(normal, half_vector));
    float NdotV = saturate(dot(normal, viewer));
    float VdotH = saturate(dot(viewer, half_vector));
    float r_sq = k.RoughnessValue * k.RoughnessValue;

	// Evaluate the geometric term
    float geo_numerator = 2.0f * NdotH;
    float geo_denominator = VdotH;
    float geo_b = (geo_numerator * NdotV) / geo_denominator;
    float geo_c = (geo_numerator * NdotL) / geo_denominator;
    float geo = min(1.0f, min(geo_b, geo_c));

	// Now evaluate the roughness term
    float roughness = 0;
    if (ROUGHNESS_LOOK_UP == k.RoughnessMode)
    {
		// texture coordinate is:
        float2 tc = { NdotH, k.RoughnessValue };
        
		// Remap the NdotH value to be 0.0-1.0 instead of -1.0..+1.0
        tc.x += 1.0f;
        tc.x /= 2.0f;

		// look up the coefficient from the texture:
        roughness = CookTorranceTexRoughness.SampleLevel(SamplerLinear, tc, 0).x;
    }

    if (ROUGHNESS_BECKMANN == k.RoughnessMode)
    {
        float roughness_a = 1.0f / (4.0f * r_sq * pow(NdotH, 4));
        float roughness_b = NdotH * NdotH - 1.0f;
        float roughness_c = r_sq * NdotH * NdotH;
        roughness = roughness_a * exp(roughness_b / roughness_c);
    }

    if (ROUGHNESS_GAUSSIAN == k.RoughnessMode)
    {
		// This variable could be exposed as a variable	for the application to control:
        float c = 1.0f;
        float alpha = acos(dot(normal, half_vector));
        roughness = c * exp(-(alpha / r_sq));
    }

	// Next evaluate the Fresnel value
    float fresnel = pow(1.0f - VdotH, 5.0f) * (1.0f - k.ReflectionAtNormIncidence);
    fresnel += k.ReflectionAtNormIncidence;

	// Put all the terms together to compute the specular term in the equation
    float3 Rs_numerator = (fresnel * geo * roughness);
    float Rs_denominator = NdotV * NdotL;
    float3 Rs = Rs_numerator / Rs_denominator;

	// Put all the parts together to generate the final colour
    float3 final = max(0.0f, NdotL) * (lightSpecularColor.rgb * Rs + k.Diffuse.rgb);

	// Return the result
    return final;
}
inline float3 SpecularPass(float3 normal, float3 viewer, float3 light, float3 lightColor, float3 lightSpecularColor, Material k)
{
    if (k.Algorithm == SPECULAR_ALGORITHM_PHONG)
    {
        return SpecularPassPhong(normal, viewer, light, lightSpecularColor, k);
    }
    if (k.Algorithm == SPECULAR_ALGORITHM_BLINNPHONG)
    {
        return SpecularPassBlinnPhong(normal, viewer, light, lightSpecularColor, k);
    }
    if (k.Algorithm == SPECULAR_ALGORITHM_COOKTORRANCE)
    {
        return SpecularPassCookTorrance(normal, viewer, light, lightColor, lightSpecularColor, k);
    }
    
    return 0;
}

inline float3 CalcAmbient(float3 ambientDown, float3 ambientRange, float3 normal)
{
	// Convert from [-1, 1] to [0, 1]
    float up = normal.y * 0.5 + 0.5;

	// Calculate the ambient value
    return ambientDown + up * ambientRange;
}
inline float CalcSphericAttenuation(float intensity, float radius, float distance)
{
    float attenuation = 0.0f;

    float f = distance / radius;
    float denom = max(1.0f - (f * f), 0.0f);
    if (denom > 0.0f)
    {
        float d = distance / (1.0f - (f * f));
        float dn = (d / intensity) + 1.0f;

        attenuation = 1.0f / (dn * dn);
    }

    return attenuation;
}
inline float CalcSpotCone(float3 lightDirection, float spotAngle, float3 L)
{
    float minCos = cos(spotAngle);
    float maxCos = (minCos + 1.0f) * 0.5f;
    float cosAngle = dot(lightDirection, -L);
    return smoothstep(minCos, maxCos, cosAngle);
}

inline float4 ForwardLightEquation(Material k, float3 lAmbient, float lAlbedo, float3 lDiffuse, float4 pDiffuse, float3 lSpecular, float4 pSpecular)
{
    float4 color = pDiffuse * float4(lAmbient * lAlbedo, 1);
    
    float4 emissive = k.Emissive;
    float4 ambient = k.Ambient;
    float4 diffuse = k.Diffuse * float4(lDiffuse, 1) * pDiffuse;
    float4 specular = k.Specular * float4(lSpecular, 1) * pSpecular;

    float4 final = (emissive + ambient + diffuse + specular) * color;
    
    return saturate(final);
}
inline float4 DeferredLightEquation(Material k, float3 lAmbient, float lAlbedo, float3 lDiffuseSpecular, float4 pDiffuse)
{
    float4 color = pDiffuse * float4(lAmbient * lAlbedo, 1);
    
    float4 emissive = k.Emissive;
    float4 ambient = k.Ambient;
    float4 diffuseSpecular = float4(lDiffuseSpecular, 1) * pDiffuse;

    float4 final = (emissive + ambient + diffuseSpecular) * color;
    
    return saturate(final);
}

struct ComputeLightsOutput
{
    float3 diffuse;
    float3 specular;
};

struct ComputeDirectionalLightsInput
{
    DirectionalLight dirLight;
    Material material;
    float3 lod;
    float3 pPosition;
    float3 pNormal;
    float3 ePosition;
    Texture2DArray<float> shadowMap;
};

inline ComputeLightsOutput ComputeDirectionalLightLOD1(ComputeDirectionalLightsInput input)
{
    float3 L = normalize(input.dirLight.DirToLight);
    float3 V = normalize(input.ePosition - input.pPosition);

    float cShadowFactor = 1;
    [flatten]
    if (input.dirLight.CastShadow == 1)
    {
        cShadowFactor = CalcCascadedShadowFactor(
            input.pPosition,
            input.dirLight.ToShadowSpace,
            input.dirLight.ToCascadeOffsetX,
            input.dirLight.ToCascadeOffsetY,
            input.dirLight.ToCascadeScale,
            input.shadowMap);
    }
    
    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.dirLight.Diffuse) * cShadowFactor;
    output.specular = SpecularPass(input.pNormal, V, L, input.dirLight.Diffuse, input.dirLight.Specular, input.material) * cShadowFactor;

    return output;
}
inline ComputeLightsOutput ComputeDirectionalLightLOD2(ComputeDirectionalLightsInput input)
{
    float3 L = normalize(input.dirLight.DirToLight);

    float cShadowFactor = 1;
    [flatten]
    if (input.dirLight.CastShadow == 1)
    {
        cShadowFactor = CalcCascadedShadowFactor(
            input.pPosition,
            input.dirLight.ToShadowSpace,
            input.dirLight.ToCascadeOffsetX,
            input.dirLight.ToCascadeOffsetY,
            input.dirLight.ToCascadeScale,
            input.shadowMap);
    }

    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.dirLight.Diffuse) * cShadowFactor;
    output.specular = 0;

    return output;
}
inline ComputeLightsOutput ComputeDirectionalLightLOD3(ComputeDirectionalLightsInput input)
{
    float3 L = normalize(input.dirLight.DirToLight);

    float cShadowFactor = 1;
    [flatten]
    if (input.dirLight.CastShadow == 1)
    {
        cShadowFactor = CalcCascadedShadowFactor(
            input.pPosition,
            input.dirLight.ToShadowSpace,
            input.dirLight.ToCascadeOffsetX,
            input.dirLight.ToCascadeOffsetY,
            input.dirLight.ToCascadeScale,
            input.shadowMap);
    }

    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.dirLight.Diffuse) * cShadowFactor;
    output.specular = 0;

    return output;
}
inline ComputeLightsOutput ComputeDirectionalLight(ComputeDirectionalLightsInput input)
{
    float distToEye = length(input.ePosition - input.pPosition);

    if (distToEye < input.lod.x)
    {
        return ComputeDirectionalLightLOD1(input);
    }
    else if (distToEye < input.lod.y)
    {
        return ComputeDirectionalLightLOD2(input);
    }
    else
    {
        return ComputeDirectionalLightLOD3(input);
    }
}

struct ComputeSpotLightsInput
{
    SpotLight spotLight;
    Material material;
    float3 lod;
    float3 pPosition;
    float3 pNormal;
    float3 ePosition;
    Texture2DArray<float> shadowMap;
};

inline ComputeLightsOutput ComputeSpotLightLOD1(ComputeSpotLightsInput input, float dist)
{
    float3 L = input.spotLight.Position - input.pPosition;
    float D = length(L);
    L /= D;
    float3 V = normalize(input.ePosition - input.pPosition);

    float cShadowFactor = 1;
    [flatten]
    if (input.spotLight.CastShadow == 1 && input.spotLight.MapIndex >= 0)
    {
        cShadowFactor = CalcSpotShadowFactor(input.pPosition, input.spotLight.MapIndex, input.spotLight.FromLightVP, input.shadowMap, SHADOW_SAMPLES_HD);
    }

    float attenuation = CalcSphericAttenuation(input.spotLight.Intensity, input.spotLight.Radius, D);
    attenuation *= CalcSpotCone(input.spotLight.Direction, input.spotLight.Angle, L);

    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.spotLight.Diffuse) * attenuation * cShadowFactor;
    output.specular = SpecularPass(input.pNormal, V, L, input.spotLight.Diffuse, input.spotLight.Specular, input.material) * dist * attenuation * cShadowFactor;

    return output;
}
inline ComputeLightsOutput ComputeSpotLightLOD2(ComputeSpotLightsInput input)
{
    float3 L = input.spotLight.Position - input.pPosition;
    float D = length(L);
    L /= D;

    float cShadowFactor = 1;
    [flatten]
    if (input.spotLight.CastShadow == 1 && input.spotLight.MapIndex >= 0)
    {
        cShadowFactor = CalcSpotShadowFactor(input.pPosition, input.spotLight.MapIndex, input.spotLight.FromLightVP, input.shadowMap, SHADOW_SAMPLES_LD);
    }

    float attenuation = CalcSphericAttenuation(input.spotLight.Intensity, input.spotLight.Radius, D);
    attenuation *= CalcSpotCone(input.spotLight.Direction, input.spotLight.Angle, L);

    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.spotLight.Diffuse) * attenuation * cShadowFactor;
    output.specular = 0;

    return output;
}
inline ComputeLightsOutput ComputeSpotLight(ComputeSpotLightsInput input)
{
    float distToEye = length(input.ePosition - input.pPosition);

    if (distToEye < input.lod.x)
    {
        return ComputeSpotLightLOD1(input, 1.0f - (max(1.0f, distToEye / input.lod.x)));
    }
    else if (distToEye < input.lod.z)
    {
        return ComputeSpotLightLOD2(input);
    }
    else
    {
        ComputeLightsOutput output;
        output.diffuse = 0;
        output.specular = 0;
        return output;
    }
}

struct ComputePointLightsInput
{
    PointLight pointLight;
    Material material;
    float3 lod;
    float3 pPosition;
    float3 pNormal;
    float3 ePosition;
    TextureCubeArray<float> shadowMapPoint;
};

inline ComputeLightsOutput ComputePointLightLOD1(ComputePointLightsInput input, float dist)
{
    float3 L = input.pointLight.Position - input.pPosition;
    float D = length(L);
    L /= D;
    float3 V = normalize(input.ePosition - input.pPosition);

    float cShadowFactor = 1;
    [flatten]
    if (input.pointLight.CastShadow == 1 && input.pointLight.MapIndex >= 0)
    {
        cShadowFactor = CalcPointShadowFactor(input.pPosition, input.pointLight, input.shadowMapPoint);
    }

    float attenuation = CalcSphericAttenuation(input.pointLight.Intensity, input.pointLight.Radius, D);

    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.pointLight.Diffuse) * attenuation * cShadowFactor;
    output.specular = SpecularPass(input.pNormal, V, L, input.pointLight.Diffuse, input.pointLight.Specular, input.material) * dist * attenuation * cShadowFactor;

    return output;
}
inline ComputeLightsOutput ComputePointLightLOD2(ComputePointLightsInput input)
{
    float3 L = input.pointLight.Position - input.pPosition;
    float D = length(L);
    L /= D;

    float cShadowFactor = 1;
    [flatten]
    if (input.pointLight.CastShadow == 1 && input.pointLight.MapIndex >= 0)
    {
        cShadowFactor = CalcPointShadowFactor(input.pPosition, input.pointLight, input.shadowMapPoint);
    }

    float attenuation = CalcSphericAttenuation(input.pointLight.Intensity, input.pointLight.Radius, D);

    ComputeLightsOutput output;

    output.diffuse = DiffusePass(input.pNormal, L, input.pointLight.Diffuse) * attenuation * cShadowFactor;
    output.specular = 0;

    return output;
}
inline ComputeLightsOutput ComputePointLight(ComputePointLightsInput input)
{
    float distToEye = length(input.ePosition - input.pPosition);

    if (distToEye < input.lod.x)
    {
        return ComputePointLightLOD1(input, 1.0f - (max(1.0f, distToEye / input.lod.x)));
    }
    else if (distToEye < input.lod.z)
    {
        return ComputePointLightLOD2(input);
    }
    else
    {
        ComputeLightsOutput output;
        output.diffuse = 0;
        output.specular = 0;
        return output;
    }
}

struct ComputeLightsInput
{
    Material material;
    HemisphericLight hemiLight;
    DirectionalLight dirLights[MAX_LIGHTS_DIRECTIONAL];
    PointLight pointLights[MAX_LIGHTS_POINT];
    SpotLight spotLights[MAX_LIGHTS_SPOT];
    uint dirLightsCount;
    uint pointLightsCount;
    uint spotLightsCount;
    float albedo;
    float3 lod;
    float fogStart;
    float fogRange;
    float4 fogColor;
    float3 pPosition;
    float3 pNormal;
    float4 pColorDiffuse;
    float4 pColorSpecular;
    float3 ePosition;
    Texture2DArray<float> shadowMapDir;
    Texture2DArray<float> shadowMapSpot;
    TextureCubeArray<float> shadowMapPoint;
};

inline float4 ComputeLightsLOD1(ComputeLightsInput input)
{
    float3 lDiffuse = 0;
    float3 lSpecular = 0;

    float3 V = normalize(input.ePosition - input.pPosition);

    float3 lAmbient = CalcAmbient(input.hemiLight.AmbientDown.rgb, input.hemiLight.AmbientRange.rgb, input.pNormal);

    uint i = 0;

    for (i = 0; i < input.dirLightsCount; i++)
    {
        float3 L = normalize(input.dirLights[i].DirToLight);

        float cShadowFactor = 1;
        [flatten]
        if (input.dirLights[i].CastShadow == 1)
        {
            cShadowFactor = CalcCascadedShadowFactor(
                input.pPosition,
                input.dirLights[i].ToShadowSpace,
                input.dirLights[i].ToCascadeOffsetX,
                input.dirLights[i].ToCascadeOffsetY,
                input.dirLights[i].ToCascadeScale,
                input.shadowMapDir);
        }

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.dirLights[i].Diffuse);
        float3 cSpecular = SpecularPass(input.pNormal, V, L, input.dirLights[i].Diffuse, input.dirLights[i].Specular, input.material);

        lDiffuse += (cDiffuse * cShadowFactor);
        lSpecular += (cSpecular * cShadowFactor);
    }

    for (i = 0; i < input.spotLightsCount; i++)
    {
        float3 P = input.spotLights[i].Position - input.pPosition;
        float D = length(P);
        float3 L = P / D;

        float cShadowFactor = 1;
		[flatten]
        if (input.spotLights[i].CastShadow == 1 && input.spotLights[i].MapIndex >= 0)
        {
            cShadowFactor = CalcSpotShadowFactor(input.pPosition, input.spotLights[i].MapIndex, input.spotLights[i].FromLightVP, input.shadowMapSpot, SHADOW_SAMPLES_HD);
        }

        float attenuation = CalcSphericAttenuation(input.spotLights[i].Intensity, input.spotLights[i].Radius, D);
        attenuation *= CalcSpotCone(input.spotLights[i].Direction, input.spotLights[i].Angle, L);

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.spotLights[i].Diffuse);
        float3 cSpecular = SpecularPass(input.pNormal, V, L, input.spotLights[i].Diffuse, input.spotLights[i].Specular, input.material);
        
        lDiffuse += (cDiffuse * cShadowFactor * attenuation);
        lSpecular += (cSpecular * cShadowFactor * attenuation);
    }

    for (i = 0; i < input.pointLightsCount; i++)
    {
        float3 P = input.pointLights[i].Position - input.pPosition;
        float D = length(P);
        float3 L = P / D;

        float cShadowFactor = 1;
        [flatten]
        if (input.pointLights[i].CastShadow == 1 && input.pointLights[i].MapIndex >= 0)
        {
            cShadowFactor = CalcPointShadowFactor(input.pPosition, input.pointLights[i], input.shadowMapPoint);
        }

        float attenuation = CalcSphericAttenuation(input.pointLights[i].Intensity, input.pointLights[i].Radius, D);

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.pointLights[i].Diffuse);
        float3 cSpecular = SpecularPass(input.pNormal, V, L, input.pointLights[i].Diffuse, input.pointLights[i].Specular, input.material);

        lDiffuse += (cDiffuse * cShadowFactor * attenuation);
        lSpecular += (cSpecular * cShadowFactor * attenuation);
    }

    return ForwardLightEquation(input.material, lAmbient, input.albedo, lDiffuse, input.pColorDiffuse, lSpecular, input.pColorSpecular);
}
inline float4 ComputeLightsLOD2(ComputeLightsInput input)
{
    float3 lDiffuse = 0;

    float3 lAmbient = CalcAmbient(input.hemiLight.AmbientDown.rgb, input.hemiLight.AmbientRange.rgb, input.pNormal);

    uint i = 0;

    for (i = 0; i < input.dirLightsCount; i++)
    {
        float3 L = normalize(input.dirLights[i].DirToLight);

        float cShadowFactor = 1;
        [flatten]
        if (input.dirLights[i].CastShadow == 1)
        {
            cShadowFactor = CalcCascadedShadowFactor(
                input.pPosition,
                input.dirLights[i].ToShadowSpace,
                input.dirLights[i].ToCascadeOffsetX,
                input.dirLights[i].ToCascadeOffsetY,
                input.dirLights[i].ToCascadeScale,
                input.shadowMapDir);
        }

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.dirLights[i].Diffuse);

        lDiffuse += (cDiffuse * cShadowFactor);
    }

    for (i = 0; i < input.spotLightsCount; i++)
    {
        float3 P = input.spotLights[i].Position - input.pPosition;
        float D = length(P);
        float3 L = P / D;

        float cShadowFactor = 1;
		[flatten]
        if (input.spotLights[i].CastShadow == 1 && input.spotLights[i].MapIndex >= 0)
        {
            cShadowFactor = CalcSpotShadowFactor(input.pPosition, input.spotLights[i].MapIndex, input.spotLights[i].FromLightVP, input.shadowMapSpot, SHADOW_SAMPLES_LD);
        }

        float attenuation = CalcSphericAttenuation(input.spotLights[i].Intensity, input.spotLights[i].Radius, D);
        attenuation *= CalcSpotCone(input.spotLights[i].Direction, input.spotLights[i].Angle, L);

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.spotLights[i].Diffuse);

        lDiffuse += (cDiffuse * cShadowFactor * attenuation);
    }

    for (i = 0; i < input.pointLightsCount; i++)
    {
        float3 P = input.pointLights[i].Position - input.pPosition;
        float D = length(P);
        float3 L = P / D;

        float cShadowFactor = 1;
        [flatten]
        if (input.pointLights[i].CastShadow == 1 && input.pointLights[i].MapIndex >= 0)
        {
            cShadowFactor = CalcPointShadowFactor(input.pPosition, input.pointLights[i], input.shadowMapPoint);
        }

        float attenuation = CalcSphericAttenuation(input.pointLights[i].Intensity, input.pointLights[i].Radius, D);

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.pointLights[i].Diffuse);

        lDiffuse += (cDiffuse * cShadowFactor * attenuation);
    }

    return ForwardLightEquation(input.material, lAmbient, input.albedo, lDiffuse, input.pColorDiffuse, 0, 0);
}
inline float4 ComputeLightsLOD3(ComputeLightsInput input)
{
    float3 lDiffuse = 0;

    float3 lAmbient = CalcAmbient(input.hemiLight.AmbientDown.rgb, input.hemiLight.AmbientRange.rgb, input.pNormal);

    uint i = 0;

    for (i = 0; i < input.dirLightsCount; i++)
    {
        float3 L = normalize(input.dirLights[i].DirToLight);

        float cShadowFactor = 1;
        [flatten]
        if (input.dirLights[i].CastShadow == 1)
        {
            cShadowFactor = CalcCascadedShadowFactor(
                input.pPosition,
                input.dirLights[i].ToShadowSpace,
                input.dirLights[i].ToCascadeOffsetX,
                input.dirLights[i].ToCascadeOffsetY,
                input.dirLights[i].ToCascadeScale,
                input.shadowMapDir);
        }

        float3 cDiffuse = DiffusePass(input.pNormal, L, input.dirLights[i].Diffuse);

        lDiffuse += (cDiffuse * cShadowFactor);
    }

    return ForwardLightEquation(input.material, lAmbient, input.albedo, lDiffuse, input.pColorDiffuse, 0, 0);
}
inline float4 ComputeLights(ComputeLightsInput input)
{
    float distToEye = length(input.ePosition - input.pPosition);

    float fog = 0;
    if (input.fogRange > 0)
    {
        fog = CalcFogFactor(distToEye, input.fogStart, input.fogRange);
    }

    if (fog >= 1)
    {
        return input.fogColor;
    }
    else
    {
        float4 color = 0;
        if (distToEye < input.lod.x)
        {
            color = ComputeLightsLOD1(input);
        }
        else if (distToEye < input.lod.y)
        {
            color = ComputeLightsLOD2(input);
        }
        else
        {
            color = ComputeLightsLOD3(input);
        }

        return float4(lerp(color.rgb, input.fogColor.rgb, fog), color.a);
    }
}
