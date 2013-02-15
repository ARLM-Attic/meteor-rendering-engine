//-----------------------------------------
//	DepthMap
//-----------------------------------------

//--- Parameters ---

float4x4 World;
float4x4 LightViewProj;
float textureScale;
float mapScale;
float clipLevel;

texture Texture;

//--- Structures ---

struct VertexShaderInput
{
    float4 position : POSITION;
};

struct VertexShaderOutput
{
	float4 position : POSITION;
	float depth : TEXCOORD1;
};

//--- VertexShader ---

VertexShaderOutput DepthMapVS(VertexShaderInput input)
{
	VertexShaderOutput output;

	float4x4 wvp = mul(World, LightViewProj);

	output.position = mul(input.position, wvp);
	output.depth = output.position.z;
	
	return output;
}

//--- PixelShader ---

float4 DepthMapPS (VertexShaderOutput IN) : COLOR0
{	
    return IN.depth;
}

//--- Techniques ---

technique Default
{
    pass P0
    {
		ZEnable = true;
		CullMode = CCW;
        VertexShader = compile vs_3_0 DepthMapVS();
        PixelShader = compile ps_3_0 DepthMapPS();
    }
}
