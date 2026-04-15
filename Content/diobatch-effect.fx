#if OPENGL
	#define SV_POSITION POSITION
	#define VS_SHADERMODEL vs_3_0
	#define PS_SHADERMODEL ps_3_0
#else
	#define VS_SHADERMODEL vs_4_0_level_9_1
	#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 Projection;


sampler2D Sampler0 : register(s0);
sampler2D Sampler1 : register(s1);
sampler2D Sampler2 : register(s2);
sampler2D Sampler3 : register(s3);
sampler2D Sampler4 : register(s4);
sampler2D Sampler5 : register(s5);
sampler2D Sampler6 : register(s6);
sampler2D Sampler7 : register(s7);

// ─── Structs ───────────────────────────────────
struct VSInput {
    float4 Position     : POSITION0;
    float4 ClipRect     : TEXCOORD0;   // x, y, w, h
    float2 ClipParams   : TEXCOORD1;   // x = rounding, y = rotation
    float4 ColorA       : COLOR0;      // Start Color / Solid Color / Tint
    float4 ColorB       : COLOR1;      // End Color
    float4 GradientData : TEXCOORD2;   // xy = Start/Center/UV, zw = End/Edge/TexIndex
    float  PaintType    : TEXCOORD3;   // 0=Solid, 1=Linear, 2=Radial, 3=Texture
};

struct VSOutput {
    float4 Position     : SV_POSITION;
    float4 ClipRect     : TEXCOORD0;
    float2 ClipParams   : TEXCOORD1;
    float4 ColorA       : COLOR0;
    float4 ColorB       : COLOR1;
    float4 GradientData : TEXCOORD2;
    float  PaintType    : TEXCOORD3;
    float2 ScreenPos    : TEXCOORD4;   // Pre-projection XY
};

// ─── Vertex Shader ─────────────────────────────
VSOutput VS(VSInput input) {
    VSOutput output;
    output.Position     = mul(input.Position, Projection);
    output.ClipRect     = input.ClipRect;
    output.ClipParams   = input.ClipParams;
    output.ColorA       = input.ColorA;
    output.ColorB       = input.ColorB;
    output.GradientData = input.GradientData;
    output.PaintType    = input.PaintType;
    output.ScreenPos    = input.Position.xy;
    return output;
}

// ─── Math Helpers ──────────────────────────────
float2 Rotate(float2 p, float2 pivot, float angle) {
    float s, c;
    sincos(angle, s, c);
    p -= pivot;
    p  = float2(p.x * c - p.y * s,
                p.x * s + p.y * c);
    return p + pivot;
}

float RoundedRectSDF(float2 p, float2 center, float2 halfSize, float radius) {
    float2 q = abs(p - center) - halfSize + radius;
    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
}

// ─── Pixel Shader ─────────────────────────────
float4 PS(VSOutput input) : SV_TARGET {
    float2 pixelPos = input.ScreenPos;
    float4 finalColor = input.ColorA;

    // ─── 1. RESOLVE PAINT TYPE ─────────────────────────────────
    [branch]
    if (input.PaintType == 1.0) {
        // Linear Gradient
        float2 start = input.GradientData.xy;
        float2 end = input.GradientData.zw;
        float2 dir = end - start;
        float sqLen = dot(dir, dir);
        float t = saturate(dot(pixelPos - start, dir) / max(sqLen, 0.0001));
        finalColor = lerp(input.ColorA, input.ColorB, t);
    } 
    else if (input.PaintType == 2.0) {
        // Radial Gradient
        float2 center = input.GradientData.xy;
        float2 edge = input.GradientData.zw;
        float radius = distance(center, edge);
        float d = distance(pixelPos, center);
        float t = saturate(d / max(radius, 0.0001));
        finalColor = lerp(input.ColorA, input.ColorB, t);
    } 
    else if (input.PaintType == 3.0) {
        // Texture
        float2 uv = input.GradientData.xy;
        int texIndex = (int)(input.GradientData.z + 0.1);
        float4 texColor = float4(1, 1, 1, 1);
        
        if (texIndex == 0) texColor = tex2D(Sampler0, uv);
        else if (texIndex == 1) texColor = tex2D(Sampler1, uv);
        else if (texIndex == 2) texColor = tex2D(Sampler2, uv);
        else if (texIndex == 3) texColor = tex2D(Sampler3, uv);
        else if (texIndex == 4) texColor = tex2D(Sampler4, uv);
        else if (texIndex == 5) texColor = tex2D(Sampler5, uv);
        else if (texIndex == 6) texColor = tex2D(Sampler6, uv);
        else if (texIndex == 7) texColor = tex2D(Sampler7, uv);

        finalColor = texColor * input.ColorA; // ColorA acts as the Tint
    }

    // ─── 2. APPLY CLIPPING & ROUNDING ──────────────────────────
    float2 clipWH = input.ClipRect.zw;
    [branch]
    if (clipWH.x > 0.0 && clipWH.y > 0.0) {
        float2 clipXY = input.ClipRect.xy;
        float radius = input.ClipParams.x;
        float rotation = input.ClipParams.y;

        float2 center = clipXY + clipWH * 0.5;
        float2 halfSize = clipWH * 0.5;
        
        float2 p = pixelPos;
        [branch]
        if (rotation != 0.0)
            p = Rotate(p, center, -rotation);
            
        float d = RoundedRectSDF(p, center, halfSize, radius);
        float alphaMask = 1.0 - smoothstep(-0.5, 0.5, d);
        clip(alphaMask - 0.001);
        finalColor *= alphaMask;
    }

    return finalColor;
}

// ─── Techniques ────────────────────────────────
technique Primitive {
    pass P0 {
        VertexShader = compile VS_SHADERMODEL VS();
        PixelShader  = compile PS_SHADERMODEL PS();
    }
}