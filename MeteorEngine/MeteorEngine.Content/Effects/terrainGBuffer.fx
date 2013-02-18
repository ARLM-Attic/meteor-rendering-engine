
float4x4 World;
float4x4 View;
float4x4 Projection;
float textureScale;
float mapScale;
float clipLevel;

texture Texture, NormalMap;
texture heightMapTexture;

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

struct VertexTerrainInput
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
    float2 TexCoord : TEXCOORD0;
	float3 binormal : BINORMAL0;
    float3 tangent : TANGENT0;
};

struct VertexTerrainOutput
{
    float4 Position : POSITION0;
    float2 TexCoord : TEXCOORD0;
    float3 Depth : TEXCOORD1;
	float3 Normal : TEXCOORD2;
	float4 NewPosition : TEXCOORD3;
    float3 P : TEXCOORD4;
    float3 N : TEXCOORD5;
};

//--- VertexShaders ---//

VertexTerrainOutput VertexShaderTerrain(VertexTerrainInput input)
{
    VertexTerrainOutput output;

	float4x4 wvp = mul(mul(World, View), Projection);

	// First transform the position onto the screen
	output.Position = mul(input.Position, wvp);
	output.NewPosition = input.Position;

	//pass the texture coordinates further
    output.TexCoord = input.TexCoord;

    input.Normal = normalize(mul(input.Normal, World));
	output.Normal = input.Normal;

    output.Depth.x = output.Position.z;// - 100.f; // Subtract to make color more visible
    output.Depth.y = output.Position.w;
	output.Depth.z = output.Position.z;

    output.P = mul(input.Position, World);
	output.N = mul(input.Normal, (float3x3)World);

    return output;
}

//--- PixelShaders ---//

struct PixelShaderOutput1
{
    float4 Color : COLOR0;
    float4 Normal : COLOR1;
    float4 Depth : COLOR2;
};

struct PixelShaderOutput2
{
    float4 Normal : COLOR0;
    float4 Depth : COLOR1;
};

float4 TriplanarMapping(VertexTerrainOutput input, float scale = 1)
{
    // Output the normal, in [0,1] space
    float3 normalFromMap = normalize(mul(input.Normal, View));
    normalFromMap = 0.5f * (normalFromMap + 1.0f);

	float tighten = 0.f;

	float mXY = abs(input.Normal.z) - tighten;
	float mXZ = abs(input.Normal.y) - tighten;
	float mYZ = abs(input.Normal.x) - tighten;

	float total = mXY + mXZ + mYZ;
	mXY /= total;
	mXZ /= total;
	mYZ /= total;

	float4 cXY = tex2D(diffuseSampler, input.NewPosition.xy / textureScale * scale);
	float4 cXZ = tex2D(diffuseSampler, input.NewPosition.xz / textureScale * scale);
	float4 cYZ = tex2D(diffuseSampler, input.NewPosition.yz / textureScale * scale);

	float4 diffuse = cXY * mXY + cXZ * mXZ + cYZ * mYZ;
	return diffuse;
}

float3x3 ComputeTangentFrame(float3 normal, float3 position, float2 texCoord)
{
	float3 dp1 = ddx(position);
	float3 dp2 = ddy(position);
	float2 duv1 = ddx(texCoord);
	float2 duv2 = ddy(texCoord);

	float3x3 M = float3x3(dp1, dp2, cross(dp1, dp2));
	float2x3 inverseM = float2x3( cross( M[1], M[2] ), cross(M[2], M[0]));

	float3 T = mul(float2(duv1.x, duv2.x), inverseM);
	float3 B = mul(float2(duv1.y, duv2.y), inverseM);

	return float3x3(normalize(T), normalize(B), normal);
}

PixelShaderOutput1 PixelTerrainGBuffer(VertexTerrainOutput input)
{
    PixelShaderOutput1 output = (PixelShaderOutput1)1;

	float4 color = TriplanarMapping(input, 4);
	float4 blendedColor = TriplanarMapping(input, 0.3f);

	float depth = pow(input.Depth.x / input.Depth.y, 50);

	// Blend with scaled texture
	output.Color = lerp(color, blendedColor, depth);
	output.Color.a = 1;

    // calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.

	float3x3 tbn = ComputeTangentFrame(input.N, input.P, input.TexCoord);
    float3x3 tangentToWorld;

    tangentToWorld[0] = mul(tbn[0], World);
    tangentToWorld[1] = mul(tbn[1], World);
    tangentToWorld[2] = mul(tbn[2], World);

    // Output the normal, in [0,1] space
    float3 normalFromMap = tex2D(normalMapSampler, input.TexCoord);

	//get normal into world space
    normalFromMap = mul(normalFromMap, tangentToWorld);	
    normalFromMap = normalize(mul(normalFromMap, View));
    output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Terrain doesn't need any specular component
    output.Normal.a = 0;

	// Output Depth
	output.Depth = input.Depth.x / input.Depth.y; 
    return output;
}

PixelShaderOutput2 PixelTerrainSmallGBuffer(VertexTerrainOutput input)
{
    PixelShaderOutput2 output = (PixelShaderOutput2)1;

    // Output the normal, in [0,1] space
    float3 normalFromMap = tex2D(normalMapSampler, input.TexCoord);

    normalFromMap = input.Normal;	
    normalFromMap = normalize(mul(normalFromMap, View));
    output.Normal.rgb = 0.5f * (normalFromMap + 1.0f);

	// Terrain doesn't need any specular component
    output.Normal.a = 0;

	// Output Depth
	output.Depth = input.Depth.x / input.Depth.y; 
    return output;
}

float4 PixelTerrainDiffuse(VertexTerrainOutput input) : COLOR0
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

