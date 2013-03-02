//-----------------------------------------
//	DirectionalLight
//-----------------------------------------

float4x4 View;
float4x4 Projection;
float4x4 inverseView;
float4x4 invertViewProj;

float2 halfPixel;
float3 camPosition;

// Cascaded shadow map settings

#define NUM_CASCADES 4
#define MAPS_PER_ROW 2
#define MAPS_PER_COL 2

float4x4 lightViewProj[NUM_CASCADES];
float cascadeSplits[NUM_CASCADES];

// Directional light pass

float3 lightDirection;
float3 lightColor;
float3 ambientTerm;
float lightIntensity;

// Apply textures to the lighting

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

float DepthBias = 0.0005f;

/// Poisson disk samples used for shadow filtering

float2 poissonDisk[24] = { 
	float2(0.5713538f, 0.7814451f),
	float2(0.2306823f, 0.6228884f),
	float2(0.1000122f, 0.9680607f),
	float2(0.947788f, 0.2773731f),
	float2(0.2837818f, 0.303393f),
	float2(0.6001099f, 0.4147638f),
	float2(-0.2314563f, 0.5434746f),
	float2(-0.08173513f, 0.0796717f),
	float2(-0.4692954f, 0.8651238f),
	float2(0.2768489f, -0.3682062f),
	float2(-0.5900795f, 0.3607553f),
	float2(-0.1010569f, -0.5284956f),
	float2(-0.4741178f, -0.2713854f),
	float2(0.4067073f, -0.00782522f),
	float2(-0.4603044f, 0.0511527f),
	float2(0.9820454f, -0.1295522f),
	float2(0.8187376f, -0.4105208f),
	float2(-0.8115796f, -0.106716f),
	float2(-0.4698426f, -0.6179109f),
	float2(-0.8402727f, -0.4400948f),
	float2(-0.2302377f, -0.879307f),
	float2(0.2748472f, -0.708988f),
	float2(-0.7874522f, 0.6162704f),
	float2(-0.9310728f, 0.3289311f)
};

#define TOTAL_SAMPLES 20

float3 PoissonDiscFilter(sampler smp, float3 ambient, float2 texCoord, float ourdepth)
{	
	// Get the current depth stored in the shadow map
	float4 samples[TOTAL_SAMPLES]; 

	float shadow = 0;
	float sampleDiscSize = 1.7f;
	float2 pixelSize = shadowMapPixelSize * sampleDiscSize;

	// Sample the texture at various offsets

	[unroll]
	for (int i = 0; i < TOTAL_SAMPLES; i++)
	{
		samples[i] = tex2D(smp, texCoord + poissonDisk[i] * pixelSize).r > ourdepth;
		shadow += samples[i];
	}

	shadow /= (TOTAL_SAMPLES + 1);
	return shadow + ambientTerm;
}

// Blinn D1 (Phong) specular distribution 
float BlinnPhong(float3 normal, float3 view, float3 light, float specPower)
{					
	float3 halfVector = normalize(light + view);
	return pow(saturate(dot(normal, halfVector)), specPower);
}

float4 DirectionalLightPS(VertexShaderOutput input, float4 position) : COLOR0
{
	// Get normal data

	float4 normalData = tex2D(normalSampler, input.TexCoord);
	float3 normal = mul((2.0f * normalData.xyz - 1.0f), inverseView);

	// Get specular data

	float4 specular = tex2D(specularSampler, input.TexCoord);
	float specPower = specular.a * 255;
	float3 specIntensity = specular.rgb;

	float3 lightDir = -normalize(lightDirection);

	// Reflection data

	float3 reflection = normalize(reflect(-lightDir, normal)); 
	float3 directionToCamera = normalize(camPosition - position);

	// Compute the final specular factor
	// Compute diffuse light
	
	float3 halfVector = normalize(lightDir + directionToCamera);
	float ndh = saturate(dot(normal, halfVector));
	float ndl = saturate(dot(normal, lightDir));

	float3 diffuse = ndl * lightColor;

	float specLight = specIntensity * BlinnPhong(normal, directionToCamera, lightDir, specPower);
	//pow(saturate(dot(reflection, directionToCamera)), specPower);

	return float4((ambientTerm + diffuse) * lightIntensity, specLight * lightIntensity);
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

float3 FindShadow(float4 shadowMapPos, float shadowIndex)
{
	// In progress: calculate the bias based on the angle of the surface relative to the light
	//float3 lightDir = -normalize(lightDirection);
	//float bias = dot(lightDir, normal) * 0.005f;

	// Project the shadow map and find the position in it for this pixel
	float2 shadowTexCoord = shadowMapPos.xy / shadowMapPos.w / 2.0f + float2(0.5, 0.5);

	shadowTexCoord.y = 1 - shadowTexCoord.y;
	shadowTexCoord += float2(shadowIndex % MAPS_PER_ROW, floor(shadowIndex / MAPS_PER_ROW));
	shadowTexCoord /= float2(MAPS_PER_ROW, MAPS_PER_COL);

	// Calculate the current pixel depth
	float ourdepth = (shadowMapPos.z / shadowMapPos.w) - DepthBias;  

	// Shadow calculation
	float3 shadow = 
		PoissonDiscFilter(shadowMapSampler, ambientTerm, shadowTexCoord, ourdepth);
	return shadow;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	float depthVal = tex2D(depthSampler, input.TexCoord).r;

	if (depthVal > 0.99999f)
		return float4(1, 1, 1, 0);

	// Convert position to world space
	float4 position = CalculateWorldPosition(input.TexCoord, depthVal);

	// Calculate light color
	float4 lightOutput = DirectionalLightPS(input, position);

	return lightOutput;
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
	float camNear = 0.0025f;
	float camFar = 8.f;
	float linearZ = (2 * camNear) / (camFar + camNear - depthVal * (camFar - camNear));

	// Get the light projection for the first available frustum split	
	float shadowIndex = 0;
	float xt = 1.5f;

	[unroll]
	for (int i = 0; i < NUM_CASCADES; i++)  
		shadowIndex += (linearZ > cascadeSplits[i] * xt);

	// Get normal data for bias adjustment
	float4 normalData = tex2D(normalSampler, input.TexCoord);
	float3 normal = mul((2.0f * normalData.xyz - 1.0f), inverseView);

	// Get shadow map position projected in light view
	float4 shadowMapPos = mul(position, lightViewProj[shadowIndex]);

	// Find the position in the shadow map for this pixel
	float3 shadow = FindShadow(shadowMapPos, shadowIndex);

	// Calculates minimum cascade distance to start blending in shadow
	// from the next cascade for a smoother transition. This reduces the
	// 'pop' or seam visible where the cascades are split.
	float minDistance = cascadeSplits[shadowIndex] * 0.8f * xt;
	
	if (linearZ > minDistance)
	{
		// Get second shadow map position projected in light view
		float4 shadowMapPos2 = mul(position, lightViewProj[shadowIndex + 1]);
		float relDistance = (linearZ - minDistance) / (cascadeSplits[shadowIndex] * xt - minDistance);

		// Get shadow value from next cascade and blend the results
		float3 shadow2 = FindShadow(shadowMapPos2, shadowIndex + 1);

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
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
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