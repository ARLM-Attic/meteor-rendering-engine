//-----------------------------------------
//	TerrainGBuffer
//-----------------------------------------

float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 inverseView;

float textureScale;
float mapScale;
float specPower;
float specIntensity;
float clipLevel;

texture Texture, NormalMap;
texture blendTexture1, blendTexture2;
texture heightMapTexture;

/// Diffuse and normals

sampler diffuseSampler : register(s0) = sampler_state
{
    Texture = <Texture>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler heightSampler : register(s1) = sampler_state
{
    Texture = <heightMapTexture>;
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

/// Blend textures

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
	
	float4 cXY = tex2D(blendSampler1, input.NewPosition.xy / textureScale * scale / 2);
	float4 cXZ = tex2D(diffuseSampler, input.NewPosition.xz / textureScale * scale);
	float4 cYZ = tex2D(blendSampler1, input.NewPosition.zy / textureScale * scale / 2);

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
	
	float3 cXY = tex2D(normalMapSampler, input.NewPosition.xy / textureScale * scale / 2);
	float3 cXZ = float3(0, 0, 1);//tex2D(normalMapSampler, input.NewPosition.xz / textureScale * scale);
	float3 cYZ = tex2D(normalMapSampler, input.NewPosition.zy / textureScale * scale / 2);

	cXY = 2.0f * cXY - 1.0f;
	cYZ = 2.0f * cYZ - 1.0f;

	float3 normal = cXY * mXY + cXZ * mXZ + cYZ * mYZ;
	normal.xy *= 1.8f;
	return normal;
}

PixelShaderOutput1 PixelTerrainGBuffer(VT_Output input)
{
    PixelShaderOutput1 output = (PixelShaderOutput1)1;

	// Determine diffuse texture color
	float4 color = TriplanarMapping(input, 4);
	float4 blendedColor = TriplanarMapping(input, 0.3f);
	float depth = pow(input.Depth.x / input.Depth.y, 50);

	// Blend with scaled texture
	output.Color = lerp(color, blendedColor, depth);
	output.Color.a = 1;

	// Sample normal map color
	float3 normal = TriplanarNormalMapping(input, 4);
	float3 blendedNormal = TriplanarNormalMapping(input, 0.3f);
	normal = lerp(normal, blendedNormal, depth);

	// Output the normal, in [0,1] space
    // Get normal into world space

    float3 normalFromMap = mul(normal, input.TangentToWorld);  
	normalFromMap = normalize(normalFromMap);
	output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Terrain doesn't need any specular component
    output.Normal.a = 0;
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

	float depth = pow(input.Depth.x / input.Depth.y, 50);

	// Blend with scaled texture
	color = lerp(color, blendedColor, depth);
	color.a = 1;
	 
	return color;//float4(0, ClipLevel % 2, 1, 1);
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

