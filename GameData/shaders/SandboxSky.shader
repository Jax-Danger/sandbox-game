Shader "Sandbox/Sky"
{
    // Engine 1.0 (Graphite) sky: drawn by the "Sky" light mode pass; Graphite/Sky.glsl supplies the view direction.
    Properties
    {
        _Zenith("Zenith", Color) = (0.25, 0.45, 0.8, 1.0)
        _Horizon("Horizon", Color) = (0.72, 0.8, 0.88, 1.0)
        _Glow("Glow", Color) = (1.0, 0.48, 0.22, 1.0)
        _SunDir("Sun Direction", Vector) = (0.3, 0.8, 0.2, 0.0)
        _MoonDir("Moon Direction", Vector) = (-0.3, 0.6, 0.4, 0.0)
        _Night("Night", Float) = 0.0
        _GlowAmt("Glow Amount", Float) = 0.0
    }

    Pass
    {
        Tags { "LightMode" = "Sky" }
        GLSLPROGRAM
        Vertex
        {
            #include "Graphite/Sky.glsl"
            void main() { }
        }
        Fragment
        {
            #include "Graphite/Sky.glsl"
            layout(location = 0) out vec4 fragColor;

            uniform vec4 _Zenith;
            uniform vec4 _Horizon;
            uniform vec4 _Glow;
            uniform vec4 _SunDir;
            uniform vec4 _MoonDir;
            uniform float _Night;
            uniform float _GlowAmt;

            float hash(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }

            void main()
            {
                vec3 d = normalize(Graphite_SkyDirection());
                float h = max(d.y, 0.0);
                // Horizon colour == fog colour, so the fogged world and the horizon skirt meet it without a seam.
                vec3 col = mix(_Horizon.rgb, _Zenith.rgb, pow(h, 0.55));
                vec3 s = normalize(_SunDir.xyz);
                float sd = max(dot(d, s), 0.0);
                // Warm glow hugging the horizon around the sun at dawn/dusk: the band grows taller toward the sun (forward
                // scattering), with a wide soft wash, a tighter halo and a hot core, then a crisp limb-darkened sun disc.
                float band = exp(-h * mix(9.0, 3.5, pow(sd, 6.0)));
                col += _Glow.rgb * _GlowAmt * band * (pow(sd, 3.0) * 1.1 + pow(sd, 12.0) * 0.6);
                col += _Glow.rgb * _GlowAmt * band * 0.22;
                float up = smoothstep(-0.06, 0.02, s.y);
                vec3 sunCol = mix(vec3(1.0, 0.72, 0.42), vec3(1.0, 0.97, 0.9), 1.0 - _GlowAmt);
                col += sunCol * (pow(sd, 40.0) * 0.35 + pow(sd, 300.0) * 0.8) * up;
                float disc = smoothstep(0.99935, 0.99965, sd);
                float limb = 0.75 + 0.25 * smoothstep(0.99935, 0.99995, sd);
                col = mix(col, sunCol * 2.2 * limb, disc * up);
                // Tiny dither so the wide gradients don't band on 8-bit output.
                col += (hash(d * 1000.0) - 0.5) / 255.0;
                // Stars: one jittered point per 3D cell on the sphere, faded out near the horizon haze.
                if (_Night > 0.001)
                {
                    float stars = 0.0;
                    for (int k = 0; k < 2; k++)
                    {
                        float sc = k == 0 ? 140.0 : 260.0;
                        vec3 c = floor(d * sc);
                        float r = hash(c + float(k) * 37.0);
                        if (r > (k == 0 ? 0.994 : 0.988))
                        {
                            vec3 j = vec3(hash(c + 1.7), hash(c + 5.3), hash(c + 9.1));
                            vec3 sp = normalize(c + 0.25 + j * 0.5);
                            float dist = length(d * sc - sp * sc);
                            float tw = (r - 0.988) * 60.0;
                            stars += smoothstep(0.32, 0.0, dist) * (k == 0 ? 1.0 : 0.45) * (0.5 + tw);
                        }
                    }
                    col += vec3(0.85, 0.9, 1.0) * stars * _Night * smoothstep(0.02, 0.25, h);
                    // Moon
                    vec3 m = normalize(_MoonDir.xyz);
                    float md = dot(d, m);
                    float disc = smoothstep(0.99955, 0.9997, md);
                    vec3 crater = vec3(0.86, 0.88, 0.92) - 0.12 * hash(floor(d * 900.0));
                    col = mix(col, crater, disc * _Night);
                    col += vec3(0.5, 0.6, 0.8) * pow(max(md, 0.0), 200.0) * 0.25 * _Night;
                }
                fragColor = vec4(col, 1.0);
            }
        }
        ENDGLSL
    }
}
