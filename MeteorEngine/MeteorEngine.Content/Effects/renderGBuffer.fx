
float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 WorldInverseTranspose;
float3 CameraPosition;

texture Texture;
texture NormalMap;
texture SpecularMap;
texture EnvironmentMap;

sampler diffuseSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler specularSampler : register(s1) = sampler_state
{
    Texture = <SpecularMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler normalMapSampler : register(s2) = sampler_state
{
    Texture = <NormalMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler environmentMapSampler : register(s3) = sampler_state
{
    Texture = <EnvironmentMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Mirror; 
	AddressV = Mirror; 
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float3 binormal : BINORMAL0;
    float3 tangent : TANGENT0;
    float4 boneIndices : BLENDINDICES0;
    float4 boneWeights : BLENDWEIGHT0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Depth : TEXCOORD1;
    float3x3 TangentToWorld	: TEXCOORD2;
	float3 Reflection : TEXCOORD5;
	float4 NewPosition : TEXCOORD6;
};

struct InstanceInput
{
	float4 vWorld1 : TEXCOORD1;
	float4 vWorld2 : TEXCOORD2;
	float4 vWorld3 : TEXCOORD3;
	float4 vWorld4 : TEXCOORD4;
};

//--- VertexShaders ---//

VertexShaderOutput VertexShaderFunction(VertexShaderInput input, InstanceInput instance)
{
    VertexShaderOutput output;

	float4x4 wvp = mul(mul(World, View), Projection);
	float4x4 WorldInstance = 
		float4x4(instance.vWorld1, instance.vWorld2, instance.vWorld3, instance.vWorld4);

	// First transform by the instance matrix
    float4 worldPosition = mul(input.Position, WorldInstance);
    float4 viewPosition = mul(worldPosition, View);
	output.Position = mul(worldPosition, wvp);
	output.NewPosition = output.Position;

	//pass the texture coordinates further
    output.TexCoord = input.TexCoord;

	//get normal into world space
    input.Normal = normalize(mul(input.Normal, World));
    output.Depth.x = output.Position.z;// - 100.f; // Subtract to make color more visible
    output.Depth.y = output.Position.w;
	output.Depth.z = viewPosition.z;

    // calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.
	output.TangentToWorld[0] = mul(normalize(mul(input.tangent, WorldInstance)), View);
    output.TangentToWorld[1] = mul(normalize(mul(input.binormal, WorldInstance)), View);
    output.TangentToWorld[2] = mul(normalize(mul(input.Normal, WorldInstance)), View);

    // Compute a reflection vector for the environment map.
	float3 ViewDirection = CameraPosition - output.Position;
    output.Reflection = reflect(normalize(ViewDirection), input.Normal);

    return output;
}

#define MaxBones 60
float4x4 bones[MaxBones];

VertexShaderOutput VertexShaderSkinnedAnimation(VertexShaderInput input, InstanceInput instance)
{
    VertexShaderOutput output;

	// Blend between the weighted bone matrices.
	float4x4 skinTransform = 0;
    
	skinTransform += bones[input.boneIndices.x] * input.boneWeights.x;
	skinTransform += bones[input.boneIndices.y] * input.boneWeights.y;
	skinTransform += bones[input.boneIndices.z] * input.boneWeights.z;
	skinTransform += bones[input.boneIndices.w] * input.boneWeights.w;

	input.Normal = mul(input.Normal, skinTransform);
	input.tangent = mul(input.tangent, skinTransform);
	input.binormal = mul(input.binormal, skinTransform);

	// Instancing transformation
	float4x4 wvp = mul(mul(World, View), Projection);
	float4x4 WorldInstance = 
		float4x4(instance.vWorld1, instance.vWorld2, instance.vWorld3, instance.vWorld4);

	float4 worldPosition = mul(mul(input.Position, skinTransform), WorldInstance);
    float4 viewPosition = mul(worldPosition, View);
    output.Position = mul(viewPosition, Projection);
	output.NewPosition = output.Position;

	//pass the texture coordinates further
    output.TexCoord = input.TexCoord;

	//get normal into world space
    input.Normal = normalize(mul(input.Normal, World));
    output.Depth.x = output.Position.z;// - 100.f; // Subtract to make color more visible
    output.Depth.y = output.Position.w;
	output.Depth.z = viewPosition.z;

    // calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.
	output.TangentToWorld[0] = mul(normalize(mul(input.tangent, WorldInstance)), View);
    output.TangentToWorld[1] = mul(normalize(mul(input.binormal, WorldInstance)), View);
    output.TangentToWorld[2] = mul(normalize(mul(input.Normal, WorldInstance)), View);

    // Compute a reflection vector for the environment map.
	float3 ViewDirection = CameraPosition - output.Position;
    output.Reflection = reflect(normalize(ViewDirection), input.Normal);

    return output;
}

//--- PixelShaders ---//

struct PixelShaderOutput1
{
    half4 Color : COLOR0;
    half4 Normal : COLOR1;
    half4 Depth : COLOR2;
	//float Depth2 : DEPTH;
};

struct PixelShaderOutput2
{
    half4 Normal : COLOR0;
    half4 Depth : COLOR1;
};

PixelShaderOutput1 PixelShaderGBuffer(VertexShaderOutput input)
{
    PixelShaderOutput1 output;

	// First check if this pixel is opaque
	//output Color
    output.Color = tex2D(diffuseSampler, input.TexCoord);
	clip(output.Color.a - 0.5);

    // Output the normal, in [0,1] space
    float3 normalFromMap = tex2D(normalMapSampler, input.TexCoord);

    normalFromMap = mul(normalFromMap, input.TangentToWorld);	
    normalFromMap = normalize(normalFromMap);
    output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Output SpecularPower and SpecularIntensity
	float4 specularAttributes = tex2D(specularSampler, input.TexCoord);
    output.Normal.a = specularAttributes.r; //specularIntensity;

	// Output Depth
    //output.Depth = -input.Depth / 2000.f;	//output Depth in linear space, [0..1] 
	output.Depth = input.Depth.x / input.Depth.y;  

	//output.Depth2 = output.Depth;
    return output;
}

PixelShaderOutput2 PixelShaderSmallGBuffer(VertexShaderOutput input)
{
    PixelShaderOutput2 output = (PixelShaderOutput2)1;

	// First check if this pixel is opaque
	float mask = tex2D(diffuseSampler, input.TexCoord).a;
	clip(mask - 0.5);

    // Output the normal, in [0,1] space
    float3 normalFromMap = tex2D(normalMapSampler, input.TexCoord);

    normalFromMap = mul(normalFromMap, input.TangentToWorld);	
    normalFromMap = normalize(normalFromMap);
    output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Output SpecularPower
	// Output SpecularIntensity
	float4 specularAttributes = tex2D(specularSampler, input.TexCoord);
    output.Normal.a = specularAttributes.r; //specularIntensity;

	// Output Depth
    //output.Depth = -input.Depth / 2000.f;	//output Depth in linear space, [0..1] 
	output.Depth = input.Depth.x / input.Depth.y; 
    return output;
}

float4 PixelShaderDiffuseRender(VertexShaderOutput input) : COLOR0
{
	// First check if mask channel is opaque
	float4 diffuse = tex2D(diffuseSampler, input.TexCoord);
	clip(diffuse.a - 0.5);

	float3 envmap = texCUBE(environmentMapSampler, normalize(input.Reflection));

    // Output the normal, in [0,1] space
    float3 normalFromMap = tex2D(normalMapSampler, input.TexCoord);

    normalFromMap = mul(normalFromMap, input.TangentToWorld);	
    normalFromMap = normalize(normalFromMap);

	// Compute a fresnel coefficient from the view vector.
	//float3 ViewDirection = CameraPosition - input.NewPosition;
	//float fresnel = saturate(1 + dot(normalize(ViewDirection), normalFromMap));

	// Just output the diffuse color
	return diffuse;//float4(lerp(diffuse, envmap, pow(fresnel, 2) * 0.7f), 1);
}

/// Very basic shaders ahead

float2 halfPixel;

struct VertexShaderBasicOutput
{
    float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

VertexShaderBasicOutput BasicVS(
	float3 position : POSITION0, float2 texCoord : TEXCOORD0)
{
    VertexShaderBasicOutput output;

	// Just pass these through
    output.Position = float4(position, 1);
	output.TexCoord = texCoord + halfPixel;

    return output;
}

float4 BasicPS(VertexShaderOutput input) : COLOR0
{
    // Simply return the input texture color
	return tex2D(diffuseSampler, input.TexCoord);
}

struct VertexShaderSkyboxData
{
    float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

VertexShaderSkyboxData VertexShaderSkybox(VertexShaderInput input, InstanceInput instance)
{
    VertexShaderSkyboxData output;

	float4x4 WorldInstance = 
		float4x4(instance.vWorld1, instance.vWorld2, instance.vWorld3, instance.vWorld4);

	//handle the position as direction
	float4 hPos = mul(input.Position, WorldInstance);//,0);
	float4x4 wvp = mul(mul(World, View), Projection);
 
	//we should set z and w to the same value, so we will have the skybox at the far plane 
    output.Position = mul(hPos, wvp);//.xyzw;
	output.TexCoord = input.TexCoord;

	return output;
}

float4 PS_White() : COLOR0
{
	return float4(1, 1, 1, 1);
}

/// The following four techniques draw a variation of the GBuffer, 
/// either with two render targets (light pre-pass) or three render 
/// targets (deferred) simultaneously. Techniques used for skinned meshes 
/// simply use a different vertex shader to handle bone tranformations.

technique GBuffer
{
    pass Pass1
    {
	    ZEnable = true;
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderGBuffer();
    }
}

technique GBufferAnimated
{
    pass Pass1
    {
	    ZEnable = true;
        VertexShader = compile vs_3_0 VertexShaderSkinnedAnimation();
        PixelShader = compile ps_3_0 PixelShaderGBuffer();
    }
}

technique SmallGBuffer
{
    pass Pass1
    {
	    ZEnable = true;
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderSmallGBuffer();
    }
}

technique SmallGBufferAnimated
{
    pass Pass1
    {
	    ZEnable = true;
        VertexShader = compile vs_3_0 VertexShaderSkinnedAnimation();
        PixelShader = compile ps_3_0 PixelShaderSmallGBuffer();
    }
}

/// Separately render the diffuse/albedo component to combine, for light pre-pass.

technique DiffuseRender
{
    pass Pass1
    {
		AlphablendEnable = false;
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderDiffuseRender();
    }
}

technique DiffuseRenderAnimated
{
    pass Pass1
    {
		AlphablendEnable = false;
        VertexShader = compile vs_3_0 VertexShaderSkinnedAnimation();
        PixelShader = compile ps_3_0 PixelShaderDiffuseRender();
    }
}

technique Skybox
{
    pass Pass1
    {
		CullMode = None;
		ZENABLE = True;
		ZFUNC = LESSEQUAL;
		ZWRITEENABLE = False;		
		
        VertexShader = compile vs_3_0 VertexShaderSkybox();
        PixelShader = compile ps_3_0 BasicPS();
    }
}

/// Copy a render target straight as is

technique PassThrough
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 BasicVS();
        PixelShader = compile ps_2_0 BasicPS();
    }
}
