
#define NUM_CASCADES 4

float4x4 View;
float4x4 Projection;
float4x4 lightProjection[NUM_CASCADES];
float4x4 lightViewProj[NUM_CASCADES];
float4x4 inverseView;
float4x4 invertViewProj;

float2 halfPixel;
float3 camPosition;
float cascadeSplits[NUM_CASCADES];

float3 lightDirection;
float3 lightColor;
float3 ambientTerm;
float lightIntensity;

texture depthMap;
texture normalMap;
texture shadowMap;
texture specularMap;

const float shadowMapSize;
const float2 shadowMapPixelSize;

sampler normalSampler : register(s1) = sampler_state
{
	Texture = <normalMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler specularSampler : register(s2) = sampler_state
{
    Texture = <specularMap>;
	Filter = MIN_MAG_MIP_LINEAR;
	AddressU = Wrap;
	AddressV = Wrap;
};

sampler depthSampler : register(s4) = sampler_state
{
	Filter = MIN_MAG_MIP_POINT;
	AddressU = Clamp;
	AddressV = Clamp;
	Texture = <depthMap>;
};

sampler shadowMapSampler = sampler_state
{
	Filter = MIN_MAG_MIP_POINT;
	AddressU = Clamp;
	AddressV = Clamp;
	Texture = <shadowMap>;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : POSITION0;
	float2 TexCoord : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    output.Position = input.Position;
	output.TexCoord = input.TexCoord + halfPixel;

    return output;
}

float DepthBias = 0.002f;

float2 poissonDisk[4] = {
	float2( -0.94201624, -0.39906216 ),
	float2( 0.94558609, -0.76890725 ),
	float2( -0.094184101, -0.92938870 ),
	float2( 0.34495938, 0.29387760 )
};

// Linear filter with 4 samples
// Source by XNA Info
// http://www.xnainfo.com/content.php?content=36

float3 LinearFilter4Samples(sampler smp, float3 ambient, float2 texCoord, float ourdepth)
{	
	// Get the current depth stored in the shadow map
	float4x4 samples = (float4x4)0; 
	float4 newSamples;

	[unroll]
	for (int i = 0; i < 4; i++)
	{
		samples[i].x = tex2D(smp, texCoord + float2(i%2,     i/2) * shadowMapPixelSize).r > ourdepth;
		samples[i].y = tex2D(smp, texCoord + float2(i%2 + 1, i/2) * shadowMapPixelSize).r > ourdepth;
		samples[i].z = tex2D(smp, texCoord + float2(i%2,     i/2 + 1) * shadowMapPixelSize).r > ourdepth;
		samples[i].w = tex2D(smp, texCoord + float2(i%2 + 1, i/2 + 1) * shadowMapPixelSize).r > ourdepth;

		newSamples[i] = dot(samples[i], 0.25f);  
	}

	// Determine the lerp amounts           
	float2 lerps;
	lerps.x = frac(texCoord.x * (shadowMapSize * 2));
	lerps.y = frac(texCoord.y * (shadowMapSize * 2));

	// lerp between the shadow values to calculate our light amount

	//float shadow = lerp(lerp(samples[0].x, samples[0].y, lerps.x), 
	//	lerp(samples[0].z, samples[0].w, lerps.x ), lerps.y); 

	float shadow = lerp(lerp(newSamples.x, newSamples.y, lerps.x), 
		lerp(newSamples.z, newSamples.w, lerps.x ), lerps.y); 	

	return shadow + ambientTerm;
}

float4 DirectionalLightPS(VertexShaderOutput input, float4 position) : COLOR0
{
	// Get normal data

	float4 normalData = tex2D(normalSampler, input.TexCoord);
	float3 normal = mul((2.0f * normalData.xyz - 1.0f), inverseView);

	// Get specular data

	float specPower = 12;//normalData.a * 255;
	float3 specIntensity = normalData.a;

	float3 lightDir = -normalize(lightDirection);

	// Reflection data

	//float selfShadow = saturate(dot(lightDir, normal));
	float3 reflection = normalize(reflect(-lightDir, normal)); 
	float3 directionToCamera = normalize(camPosition - position);

	// Compute the final specular factor
	// Compute diffuse light
	
	float ndl = saturate(dot(normal, lightDir));
	ndl = ambientTerm + (ndl * (1 - ambientTerm));
	float3 diffuse = ndl * lightColor;

	float specLight = specIntensity * 
		pow(saturate(dot(directionToCamera, reflection)), specPower);

	return float4(diffuse * lightIntensity, specLight * lightIntensity);
}

float4 CalculateWorldPosition(float2 texCoord, float depthVal)
{
	// Convert position to world space
	float4 position;

	position.xy = texCoord.x * 2.0f - 1.0f;
	position.y = -(texCoord.y * 2.0f - 1.0f);
	position.z = depthVal;
	position.w = 1.0f;

	position = mul(position, invertViewProj);
	position /= position.w;

	return position;
}

float3 FindShadow(float4 shadowMapPos, float shadowIndex, float3 normal)
{
	// In progress: calculate the bias based on the angle of the surface relative to the light
	//float3 lightDir = -normalize(lightDirection);
	//float bias = lerp(0.0002, 0.2, dot(lightDir, normal));

	// Project the shadow map and find the position in it for this pixel
	float2 shadowTexCoord = shadowMapPos.xy / shadowMapPos.w / 2.0f + float2(0.5, 0.5);

	shadowTexCoord.x /= 2.f;
	shadowTexCoord.x += (shadowIndex % 2) / 2.f;
	shadowTexCoord.y = 1 - shadowTexCoord.y;
	shadowTexCoord.y /= 2.f;
	shadowTexCoord.y += floor(shadowIndex / 2) / 2.f;

	// Calculate the current pixel depth
	float ourdepth = (shadowMapPos.z / shadowMapPos.w) - DepthBias;  

	// Shadow calculation
	return LinearFilter4Samples(shadowMapSampler, ambientTerm, shadowTexCoord, ourdepth);
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	float depthVal = tex2D(depthSampler, input.TexCoord).r;
	clip (depthVal > 0.99999f);
	//	return float4(0.5, 0.5, 0.5, 0.15);

	float4 position = CalculateWorldPosition(input.TexCoord, depthVal);

	return DirectionalLightPS(input, position);
}

float4 PixelShaderShadowed(VertexShaderOutput input) : COLOR0
{	
	float depthVal = tex2D(depthSampler, input.TexCoord).r;

	if (depthVal > 0.99999f)
		return float4(1, 1, 1, 0);

	// Convert position to world space
	float4 position = CalculateWorldPosition(input.TexCoord, depthVal);

	// Calculate light color
	float4 lightOutput = DirectionalLightPS(input, position);

	// Get linear depth space from viewport distance
	float camNear = 0.001f;
	float camFar = 1.f;
	float linearZ = (2 * camNear) / (camFar +  camNear - depthVal * (camFar - camNear));

	// Get the light projection for the first available frustum split	
	float shadowIndex = 0;

	[unroll]
	for (int i = 0; i < NUM_CASCADES; i++)  
		shadowIndex += (linearZ > cascadeSplits[i]);

	// Get normal data for bias adjustment
	float4 normalData = tex2D(normalSampler, input.TexCoord);
	float3 normal = mul((2.0f * normalData.xyz - 1.0f), inverseView);

	// Get shadow map position projected in light view
	float4 shadowMapPos = mul(position, lightViewProj[shadowIndex]);

	// Find the position in the shadow map for this pixel
	float3 shadow = FindShadow(shadowMapPos, shadowIndex, normal);

	// Calculates minimum cascade distance to start blending in shadow
	// from the next cascade for a smoother transition. This reduces the
	// 'pop' or seam visible where the cascades are split.
	float minDistance = cascadeSplits[shadowIndex] * 0.8f;
	
	if (linearZ > minDistance)
	{
		// Get second shadow map position projected in light view
		float4 shadowMapPos2 = mul(position, lightViewProj[shadowIndex + 1]);
		float relDistance = (linearZ - minDistance) / (cascadeSplits[shadowIndex] - minDistance);

		// Get shadow value from next cascade and blend the results
		float3 shadow2 = FindShadow(shadowMapPos2, shadowIndex + 1, normal);
		shadow = lerp(shadow, shadow2, relDistance);
	}

	lightOutput.rgb *= shadow;
	return lightOutput;
}

half4 PixelShift(VertexShaderOutput input) : COLOR0
{	
	float2 stippleOffset;
	stippleOffset.x = 0;
	stippleOffset.y = frac(input.TexCoord.y * 360);
	float step = 0;

	// Shift the odd row pixels left
	float horizontalStep = 1.0 / 1280.0f;
	input.TexCoord.x += horizontalStep * round(stippleOffset.y);

	float4 position = (float4)0;

	return DirectionalLightPS(input, position);
}

technique NoShadow
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}

technique Shadowed
{
    pass Pass1
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderShadowed();
    }
}