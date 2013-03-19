//-----------------------------------------
// TerrainForwardRender
//-----------------------------------------

#include "terrainConstants.fxh"

// Light and camera properties

float3 CameraPosition;
float3 lightDirection;
float3 lightColor;
float3 ambientTerm;
float lightIntensity;

/// Vertex structs

struct VT_Input
{
    float4 Position : POSITION0;
    float3 Normal : NORMAL0;
};

struct VT_Output
{
    float4 Position : POSITION0;
	float4 Color : COLOR;
    float3 Depth : TEXCOORD1;
	float3 Normal : TEXCOORD2;
	float4 NewPosition : TEXCOORD3;
    float3x3 TangentToWorld	: TEXCOORD4;
};

//--- VertexShaders ---//

VT_Output VertexShaderTerrain(VT_Input input, uniform float yOffset = 0)
{
    VT_Output output;

	input.Position.y += yOffset;
	float4x4 wvp = mul(mul(World, View), Projection);

	// First transform the position onto the screen
	float4 localPosition;
	localPosition.x = input.Position.x % meshSize;
	localPosition.y = input.Position.y;
	localPosition.z = -(int)(input.Position.x / meshSize);
	localPosition.w = 1;

	output.Position = mul(localPosition, wvp);
	output.NewPosition = mul(localPosition, World) / 10.f;

	// Pass the normal and depth
	output.Normal = normalize(mul(input.Normal, World));
    output.Depth.xyz = output.Position.zwz;

	// calculate tangent space to world space matrix using the world space tangent,
    // binormal, and normal as basis vectors.
		
	float3 c1 = cross(input.Normal, float3(0, 0, 1));
	float3 c2 = cross(input.Normal, float3(0, 1, 0));

	// Calculate tangent
	float3 tangent = (distance(c1, 0) > distance(c2, 0)) ? c1 : c2;
	float3 bitangent = cross(input.Normal, tangent);

	output.TangentToWorld[0] = normalize(mul(mul(tangent, World), View));
    output.TangentToWorld[1] = normalize(mul(mul(bitangent, World), View));
    output.TangentToWorld[2] = normalize(mul(mul(input.Normal, World), View));

	output.Color = 1;

    return output;
}

//--- PixelShaders ---//

float3 BlendWeights(float3 normal)
{
	float tighten = 0.4679f; 

	float mXY = saturate(abs(normal.z) - tighten);
	float mXZ = saturate(abs(normal.y) - tighten);
	float mYZ = saturate(abs(normal.x) - tighten);

	float total = mXY + mXZ + mYZ;
	mXY /= total;
	mXZ /= total;
	mYZ /= total;

	return float3(mXY, mXZ, mYZ);
}

float4 TriplanarMapping(VT_Output input, float scale = 1)
{
	float3 m = BlendWeights(input.Normal);

	float4 cXY = tex2D(baseSteepSampler, input.NewPosition.xy / textureScale * scale);
	float4 cXZ = tex2D(baseSampler, input.NewPosition.xz / textureScale * scale);
	float4 cYZ = tex2D(baseSteepSampler, input.NewPosition.zy / textureScale * scale);

	float4 diffuse = cXY * m.x + cXZ * m.y + cYZ * m.z;
	return diffuse;
}

float3 TriplanarNormalMapping(VT_Output input, float scale = 1)
{
	float3 m = BlendWeights(input.Normal);
	
	float3 cXY = tex2D(steepNormalMapSampler, input.NewPosition.xy / textureScale * scale);
	float3 cXZ = float3(0, 0, 1);
	float3 cYZ = tex2D(steepNormalMapSampler, input.NewPosition.zy / textureScale * scale);

	cXY = 2.0f * cXY - 1.0f;
	cYZ = 2.0f * cYZ - 1.0f;

	float3 normal = cXY * m.x + cXZ * m.y + cYZ * m.z;
	normal.xy *= bumpIntensity;
	return normal;
}

float4 PixelTerrainForwardRender(VT_Output input) : COLOR0
{
	float4 color = TriplanarMapping(input, 2.f);
	float4 blendedColor = TriplanarMapping(input, 0.3f);
	float4 blendedColor2 = TriplanarMapping(input, 0.22f);

	float depth = pow(abs(input.Depth.x / input.Depth.y), 2 * textureScale);

	// Blend with scaled texture
	blendedColor = lerp(blendedColor, blendedColor2, 0.5f);
	color = lerp(color, blendedColor, depth);
	color.a = 1;

	// Sample normal map color
	float3 normal = TriplanarNormalMapping(input, 2.f);
	float3 blendedNormal = TriplanarNormalMapping(input, 0.3f);
	float3 blendedNormal2 = TriplanarNormalMapping(input, 0.22f);

	blendedNormal = lerp(blendedNormal, blendedNormal2, 0.5f);
	normal = lerp(normal, blendedNormal, depth);

	// Output the normal, in [0,1] space
    // Get normal into world space

    float3 normalFromMap = mul(normal, input.TangentToWorld);  
	normalFromMap = normalize(normalFromMap);
	  
	// Get normal data
	normal = mul(normalFromMap, inverseView);

	// Compute the final specular factor
	// Compute diffuse light

	float3 lightDir = -normalize(lightDirection);
    float ndl = saturate(dot(normal, lightDir));
	float3 diffuse = ambientTerm + ndl * lightColor;

	// Gamma encoding
	color.rgb *= color.rgb;

	float4 finalColor = float4(color.rgb * diffuse * lightIntensity, 1);

	// Add fog based on exponential depth
	float4 fogColor = float4(0.3, 0.5, 0.92, 1);

	float4 outDepth = input.Depth.x / input.Depth.y; 
	finalColor.rgb = lerp(finalColor.rgb, fogColor, pow(abs(outDepth), 1250));

	// Gamma correct inverse
	finalColor.rgb = pow(finalColor.rgb, 1 / 2.f);

    return finalColor;
}

float4 PixelTerrainBasic(VT_Output input) : COLOR0
{
	float4 color = TriplanarMapping(input, 5);
	float4 blendedColor = TriplanarMapping(input, 0.4f);

	float depth = pow(abs(input.Depth.x / input.Depth.y), 50);

	// Blend with scaled texture
	color = lerp(color, blendedColor, depth);
	color.a = 1;

	// Output the normal, in [0,1] space
	float3 normalFromMap = input.Normal;

    //get normal into world space  
	normalFromMap = normalize(mul(normalFromMap, View));

	// Get normal data
	float3 normal = mul(normalFromMap, inverseView);
    
	// Compute the final specular factor
	// Compute diffuse light

	float3 lightDir = -normalize(lightDirection);
    float ndl = saturate(dot(normal, lightDir));
	float3 diffuse = ambientTerm + ndl * lightColor;
	float4 finalColor = float4(color.rgb * diffuse * lightIntensity, 1);

	// Add fog based on exponential depth
	float4 fogColor = float4(0.3, 0.5, 0.92, 1);

	float4 outDepth = input.Depth.x / input.Depth.y;  
	finalColor.rgb = lerp(finalColor.rgb, fogColor, pow(abs(outDepth), 1250));

    return float4(diffuse, 1);
}

float4 PixelTerrainDebug(VT_Output input) : COLOR0
{
	float4 color = input.Color;
    return color;
}

/// The following two techniques draw a variation of the terrain,
/// either with a Shader Model 3 profile or Shader Model 2 profile
/// (for use with the Reach Profile).

technique ForwardRenderTerrain
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;
		AlphaBlendEnable = False;

        VertexShader = compile vs_3_0 VertexShaderTerrain();
        PixelShader = compile ps_3_0 PixelTerrainForwardRender();
    }
}

technique ForwardRenderTerrain_2_0
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;

        VertexShader = compile vs_2_0 VertexShaderTerrain();
        PixelShader = compile ps_2_0 PixelTerrainBasic();
    }
}

/// Simple rendering mode for debug views

technique DebugTerrain
{
    pass Pass1
    {
		CullMode = CCW;
		ZENABLE = True;

        VertexShader = compile vs_2_0 VertexShaderTerrain(0.05f);
        PixelShader = compile ps_2_0 PixelTerrainDebug();
    }
}