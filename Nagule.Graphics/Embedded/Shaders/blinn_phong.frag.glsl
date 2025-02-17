#version 410 core

#include <nagule/common.glsl>
#include <nagule/lighting.glsl>
#include <nagule/shadow_mapping.glsl>

#ifdef _HeightTex
#include <nagule/parallax_mapping.glsl>
#endif

properties {
    vec2 Tiling = vec2(1);
    vec2 Offset = vec2(0);
    vec4 Diffuse = vec4(1);
    vec4 Specular = vec4(0);
    float Shininess = 0;

    vec4 Ambient = vec4(0);
    float OcclusionStrength = 1;

    float Threshold = 0.9;
    vec4 Emission = vec4(0);
    float Reflectivity = 0;

    float ParallaxScale = 0.05;
    int ParallaxMinimumLayerCount = 8;
    int ParallaxMaximumLayerCount = 32;
}

uniform sampler2D DiffuseTex;
uniform sampler2D OpacityTex;
uniform sampler2D SpecularTex;
uniform sampler2D RoughnessTex;
uniform sampler2D NormalTex;
uniform sampler2D HeightTex;
uniform sampler2D LightmapTex;
uniform sampler2D AmbientTex;
uniform sampler2D OcclusionTex;
uniform sampler2D EmissiveTex;

in VertexOutput {
    vec2 TexCoord;

    #ifndef LightingMode_Unlit
        vec3 Position;
        #if defined(_NormalTex) || defined(_HeightTex)
            mat3 TBN;
        #else
            vec3 Normal;
        #endif
    #endif

    #if defined(LightingMode_Full) || defined(LightingMode_Local)
        float Depth;
    #endif
} i;

#ifdef RenderMode_Transparent
    #include <nagule/transparency.glsl>
    OUT_ACCUM vec4 Accum;
    OUT_REVEAL float Reveal;
#else
    out vec4 FragColor;
#endif

void main()
{
    vec2 tiledCoord = i.TexCoord;

    #ifdef _Tiling
        tiledCoord *= Tiling;
    #endif

    #ifdef _Offset
        tiledCoord += Offset;
    #endif

    vec3 viewDir;
    float parallaxShadow = 1;

    #if (!defined(LightingMode_Unlit) && defined(_Specular)) || defined(_HeightTex)
        viewDir = normalize(CameraPosition - i.Position);
    #endif

    #ifdef _HeightTex
        tiledCoord = ParallaxOcclusionMapping(
            HeightTex, tiledCoord, viewDir * i.TBN,
            ParallaxScale, ParallaxMinimumLayerCount, ParallaxMaximumLayerCount);

        #ifdef _EnableParallaxEdgeClip
            if (tiledCoord.x > 1.0 || tiledCoord.y > 1.0 || tiledCoord.x < 0.0 || tiledCoord.y < 0.0) {
                discard;
            }
        #endif
    #endif

    vec3 position;
    vec3 normal;

    vec3 diffuse;
    vec4 diffuseColor;

    vec3 specular;
    vec4 specularColor;

    #ifdef _DiffuseTex
        diffuseColor = Diffuse * texture(DiffuseTex, tiledCoord);
    #else
        diffuseColor = Diffuse;
    #endif

    #ifdef _OpacityTex
        diffuseColor.a *= texture(OpacityTex, tiledCoord).r;
    #endif

    #ifdef _Threshold
        if (diffuseColor.a < Threshold) {
            discard;
        }
    #endif

    #ifndef LightingMode_Unlit
        position = i.Position;

        #if defined(_NormalTex)
            normal = texture(NormalTex, tiledCoord).rgb;
            normal = normal * 2.0 - 1.0;
            normal = normalize(i.TBN * normal);
        #elif defined(_HeightTex)
            normal = i.TBN[2];
        #else
            normal = i.Normal;
        #endif

        #ifdef _LightmapTex
            diffuse = texture(LightmapTex, i.TexCoord).rgb;
        #else
            diffuse = vec3(0);
        #endif 
    #endif

    #if !defined(LightingMode_Unlit) && defined(_Specular)
        specular = vec3(0);
        specularColor = Specular;
        
        #ifdef _SpecularTex
            specularColor *= texture(SpecularTex, tiledCoord);
        #endif

        #ifdef _RoughnessTex
            specularColor *= 1 - texture(RoughnessTex, tiledCoord);
        #endif
    #endif

    Light light;
    vec3 ambientLightAccum = vec3(0);

    #if defined(LightingMode_Full) || defined(LightingMode_Global)
    {
        for (int n = 0; n < GlobalLightCount; n++) {
            FetchGlobalLight(GlobalLightIndices[n], light);
            int category = light.Category;
            vec3 lightColor = light.Color.rgb * light.Color.a;

            if (category == LIGHT_DIRECTIONAL) {
                vec3 lightDir = light.Direction;
                float diff = max(0.8 * dot(normal, lightDir) + 0.2, 0.0);
                diffuse += diff * lightColor;

                #ifdef _Specular
                {
                    vec3 divisor = normalize(viewDir + lightDir);
                    float spec = pow(max(dot(divisor, normal), 0.0), Shininess);
                    specular += spec * lightColor;
                }
                #endif

                #if defined(_HeightTex) && defined(_EnableParallaxShadow)
                    parallaxShadow *= ParallaxSoftShadowMultiplier(
                        HeightTex, tiledCoord, lightDir * i.TBN,
                        ParallaxScale, ParallaxMinimumLayerCount, ParallaxMaximumLayerCount);
                #endif
            }
            else if (category == LIGHT_AMBIENT) {
                ambientLightAccum += lightColor;
            }
        }
    }
    #endif

    #ifndef LightingMode_Unlit
    {
        #if defined(_HeightTex) && defined(_EnableParallaxShadow)
            diffuse *= pow(parallaxShadow, 4.0);
        #endif

        float ao;

        #ifdef _OcclusionTex
            ao = texture(OcclusionTex, tiledCoord).r;
        #else
            ao = 1;
        #endif

        #ifdef _OcclusionStrength
            ao = 1.0 + OcclusionStrength * (ao - 1.0);
        #endif

        #if defined(LightingMode_Full) || defined(LightingMode_Global)
            diffuse += ao * ambientLightAccum;
        #endif

        #ifdef _Ambient
            #ifdef _AmbientTex
            {
                vec4 ambientColor = ao * Ambient * texture(AmbientTex, tiledCoord);
                diffuse += ambientColor.a * ambientColor.rgb;
            }
            #else
                diffuse += ao * Ambient.a * Ambient.rgb;
            #endif
        #endif
    }
    #endif

    #if defined(LightingMode_Full) || defined(LightingMode_Local)
    {
        int clusterIndex = GetClusterIndex(gl_FragCoord.xy, i.Depth);
        int lightCount = FetchLightCount(clusterIndex);

        for (int n = 0; n < lightCount; n++) {
            FetchLightFromCluster(clusterIndex, n, light);
            int category = light.Category;

            vec3 lightDir = light.Position - position;
            float distance = length(lightDir);
            lightDir /= distance;

            float diff = max(dot(normal, lightDir), 0.0);
            float spec;

            #ifdef _Specular
            {
                vec3 divisor = normalize(viewDir + lightDir);
                spec = pow(max(dot(divisor, normal), 0.0), Shininess);
            }
            #endif

            if (category == LIGHT_SPOT) {
                float theta = dot(lightDir, light.Direction);
                float epsilon = light.InnerConeAngle - light.OuterConeAngle;
                float intensity = clamp((theta - light.OuterConeAngle) / epsilon, 0.0, 1.0);
                diff *= intensity;

                #ifdef _Specular
                    spec *= intensity;
                #endif
            }

            vec3 lightColor = light.Color.rgb * light.Color.a;
            float attenuation = CalculateLightAttenuation(light.Range, distance);
            diffuse += diff * attenuation * lightColor;

            #ifdef _Specular
                specular += spec * attenuation * lightColor;
            #endif
        }
    }
    #endif

    vec3 color;
    vec4 emissionColor;

    #ifndef LightingMode_Unlit
        color = diffuse * diffuseColor.rgb;
        #if defined(_Specular)
            color += specular * specularColor.rgb;
        #endif
    #else
        color = diffuseColor.rgb;
    #endif

    #ifdef _Emission
        #ifdef _EmissiveTex
            emissionColor = Emission * texture(EmissiveTex, tiledCoord);
            color += emissionColor.a * emissionColor.rgb;
        #else
            color += Emission.a * Emission.rgb;
        #endif
    #elif defined(_EmissiveTex)
        emissionColor = texture(EmissiveTex, tiledCoord);
        color += emissionColor.a * emissionColor.rgb;
    #endif

    #ifdef RenderMode_Transparent
    {
        float alpha = diffuseColor.a;
        Reveal = GetTransparencyWeight(vec4(color, alpha)) * alpha;
        Accum = vec4(color * Reveal, alpha);
    }
    #else
        FragColor = vec4(color, diffuseColor.a);
    #endif
}