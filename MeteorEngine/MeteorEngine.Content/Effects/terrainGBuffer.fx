//-----------------------------------------
//	TerrainGBuffer
//-----------------------------------------

float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 inverseView;

/// Visual texture features
float textureScale;
float mapScale;
float specPower;
float specIntensity;
float bumpIntensity;

/// Debug features
float clipLevel;

/// Base textures

texture Texture, steepTexture;
sampler baseSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler baseSteepSampler : register(s1) = sampler_state
{
    Texture = <steepTexture>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

texture heightMapTexture;
sampler heightSampler : register(s2) = sampler_state
{
    Texture = <heightMapTexture>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

/// Normal map textures

texture NormalMap, steepNormalMap;
sampler normalMapSampler : register(s3) = sampler_state
{
    Texture = <NormalMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler steepNormalMapSampler : register(s4) = sampler_state
{
    Texture = <steepNormalMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

/// Blend textures

texture blendTexture1;
sampler blendSampler1 = sampler_state
{
    Texture = <blendTexture1>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

/// Vertex structs

struct VT_Input
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
    float3 binormal : BINORMAL0;
    float3 tangent : TANGENT0;
};

struct VT_Output
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Depth : TEXCOORD1;
	float3 Normal : TEXCOORD2;
	float4 NewPosition : TEXCOORD3;
	float3 N : TEXCOORD4;
    float3x3 TangentToWorld	: TEXCOORD5;
};

//--- VertexShaders ---//

VT_Output VertexShaderTerrain(VT_Input input)
{
    VT_Output output;

	float4x4 wvp = mul(mul(World, View), Projection);

	// First transform the position onto the screen
	output.Position = mul(input.Position, wvp);
	output.NewPosition = input.Position;

	//pass the texture coordinates further
    output.TexCoord = input.TexCoord;

	output.Normal = normalize(mul(input.Normal, World));
	output.N = mul(input.Normal, World);

    output.Depth.x = output.Position.z;
    output.Depth.y = output.Position.w;
	output.Depth.z = output.Position.z;

	// calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.
	output.TangentToWorld[0] = mul(normalize(mul(input.tangent, World)), View);
    output.TangentToWorld[1] = mul(normalize(mul(input.binormal, World)), View);
    output.TangentToWorld[2] = mul(normalize(mul(input.Normal, World)), View);

    return output;
}

VT_Output VertexTerrainDebug(VT_Input input)
{
    VT_Output output;

	input.Position.y += 0.005f;
	float4x4 wvp = mul(mul(World, View), Projection);

	// First transform the position onto the screen
	output.Position = mul(input.Position, wvp);
	output.NewPosition = input.Position;

	//pass the texture coordinates further
    output.TexCoord = input.TexCoord;

	output.Normal = normalize(mul(input.Normal, World));
	output.N = mul(input.Normal, World);

    output.Depth.x = output.Position.z;
    output.Depth.y = output.Position.w;
	output.Depth.z = output.Position.z;

	// calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.
	output.TangentToWorld[0] = normalize(mul(mul(input.tangent, World), View));
    output.TangentToWorld[1] = normalize(mul(mul(input.binormal, World), View));
    output.TangentToWorld[2] = normalize(mul(mul(input.Normal, World), View));

    return output;
}

//--- PixelShaders ---//

struct PixelShaderOutput1
{
    float4 Normal : COLOR0;
    float4 Depth : COLOR1;
    float4 Color : COLOR2;
	float4 Specular : COLOR3;
};

struct PixelShaderOutput2
{
    float4 Normal : COLOR0;
    float4 Depth : COLOR1;
};

float4 TriplanarMapping(VT_Output input, float scale = 1)
{
	float tighten = 0.3679f; 

	float mXY = saturate(abs(input.Normal.z) - tighten);
	float mXZ = saturate(abs(input.Normal.y) - tighten);
	float mYZ = saturate(abs(input.Normal.x) - tighten);

	float total = mXY + mXZ + mYZ;
	mXY /= total;
	mXZ /= total;
	mYZ /= total;
	
	float4 cXY = tex2D(baseSteepSampler, input.NewPosition.xy / textureScale * scale / 2);
	float4 cXZ = tex2D(baseSampler, input.NewPosition.xz / textureScale * scale);
	float4 cYZ = tex2D(baseSteepSampler, input.NewPosition.zy / textureScale * scale / 2);

	float4 diffuse = cXY * mXY + cXZ * mXZ + cYZ * mYZ;
	return diffuse;
}

float3 TriplanarNormalMapping(VT_Output input, float scale = 1)
{
	float tighten = 0.3679f; 

	float mXY = saturate(abs(input.Normal.z) - tighten);
	float mXZ = saturate(abs(input.Normal.y) - tighten);
	float mYZ = saturate(abs(input.Normal.x) - tighten);

	float total = mXY + mXZ + mYZ;
	mXY /= total;
	mXZ /= total;
	mYZ /= total;
	
	float3 cXY = tex2D(steepNormalMapSampler, input.NewPosition.xy / textureScale * scale / 2);
	float3 cXZ = float3(0, 0, 1);
	float3 cYZ = tex2D(steepNormalMapSampler, input.NewPosition.zy / textureScale * scale / 2);

	cXY = 2.0f * cXY - 1.0f;
	cYZ = 2.0f * cYZ - 1.0f;

	float3 normal = cXY * mXY + cXZ * mXZ + cYZ * mYZ;
	normal.xy *= bumpIntensity;
	return normal;
}

PixelShaderOutput1 PixelTerrainGBuffer(VT_Output input)
{
    PixelShaderOutput1 output = (PixelShaderOutput1)1;

	// Determine diffuse texture color
	float4 color = TriplanarMapping(input, 5);
	float4 blendedColor = TriplanarMapping(input, 0.4f);
	float blendDepth = pow(input.Depth.x / input.Depth.y, 35);

	// Blend with scaled texture
	output.Color = lerp(color, blendedColor, blendDepth);
	output.Color.a = 1;

	// Sample normal map color
	float3 normal = TriplanarNormalMapping(input, 5);
	float3 blendedNormal = TriplanarNormalMapping(input, 0.4f);
	normal = lerp(normal, blendedNormal, blendDepth);

	// Output the normal, in [0,1] space
    // Get normal into world space

    float3 normalFromMap = mul(normal, input.TangentToWorld);  
	normalFromMap = normalize(normalFromMap);
	output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Terrain doesn't need any specular component
    output.Normal.a = 1;

	float3 specularIntensity = specIntensity;
	output.Specular = float4(specularIntensity, specPower);

	// Output Depth
	output.Depth = input.Depth.x / input.Depth.y; 
    return output;
}

PixelShaderOutput2 PixelTerrainSmallGBuffer(VT_Output input)
{
    PixelShaderOutput2 output = (PixelShaderOutput2)1;

    // Output the normal, in [0,1] space
    float3 normalFromMap = tex2D(normalMapSampler, input.TexCoord);

    normalFromMap = mul(normalFromMap, input.Normal);	
    normalFromMap = normalize(mul(normalFromMap, View));
    output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Terrain doesn't need any specular component
    output.Normal.a = 0;

	// Output Depth
	output.Depth = input.Depth.x / input.Depth.y; 
    return output;
}

float4 PixelTerrainDiffuse(VT_Output input) : COLOR0
{
	float3 h = input.NewPosition.y;
	float4 color = TriplanarMapping(input, 4);
	float4 blendedColor = TriplanarMapping(input, 0.3f);

	float depth = pow(abs(input.Depth.x / input.Depth.y), 50);

	// Blend with scaled texture
	color = lerp(color, blendedColor, depth);
	color.a = 1;
	 
	return color;//float4(0, ClipLevel % 2, 1, 1);
}

PixelShaderOutput1 PixelTerrainDebug(VT_Output input) : COLOR0
{
    PixelShaderOutput1 output = (PixelShaderOutput1)1;

	float3 color = float3(1, 1, 1);
	output.Color.rgb = color;
	output.Color.a = 1;

	output.Normal.rgb = float3(0.5, 0.5, 1);
	output.Normal.a = 0;

	// Output Depth and Specular
	output.Depth = input.Depth.x / input.Depth.y; 
	output.Specular = 0;

    return output;
}

/// The following four techniques draw a variation of the GBuffer, 
/// either with two render targets (light pre-pass) or three render 
/// targets (deferred) simultaneously.

technique GBufferTerrain
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;

        VertexShader = compile vs_3_0 VertexShaderTerrain();
        PixelShader = compile ps_3_0 PixelTerrainGBuffer();
    }
}

technique SmallGBufferTerrain
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;

        VertexShader = compile vs_3_0 VertexShaderTerrain();
        PixelShader = compile ps_3_0 PixelTerrainSmallGBuffer();
    }
}

/// Separately render the diffuse/albedo component to combine, for light pre-pass.

technique DiffuseRenderTerrain
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;

        VertexShader = compile vs_3_0 VertexShaderTerrain();
        PixelShader = compile ps_3_0 PixelTerrainDiffuse();
    }
}

/// Simple rendering mode for debug views

technique DebugTerrain
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;

        VertexShader = compile vs_2_0 VertexTerrainDebug();
        PixelShader = compile ps_2_0 PixelTerrainDebug();
    }
}
