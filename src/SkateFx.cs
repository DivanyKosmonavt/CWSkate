using UnityEngine;
using UnityEngine.Rendering;

namespace CWSkate
{
    // Glowing effects: they brighten what's behind them and light up nothing around (no big coloured circles on the floor).
    internal static class SkateFx
    {
        private static Material sparkMat, trailMat, smokeMat;

        public static Material SparkMaterial => sparkMat != null ? sparkMat : sparkMat = Additive(DotTexture(), "sparks");
        public static Material TrailMaterial => trailMat != null ? trailMat : trailMat = Additive(StripeTexture(), "trail");
        public static Material SmokeMaterial => smokeMat != null ? smokeMat : smokeMat = Additive(DotTexture(), "smoke", additive: false);

        // additive = glows (fire, sparks); otherwise normal see-through (smoke)
        private static Material Additive(Texture2D t, string what, bool additive = true)
        {
            Material m;
            var s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s != null && s.isSupported)
            {
                m = new Material(s);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f); // additive / alpha
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            }
            else m = new Material(Shader.Find("Sprites/Default"));
            m.mainTexture = t;
            m.renderQueue = (int)RenderQueue.Transparent;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            SkatePlugin.Log.LogInfo("FX shader (" + what + "): " + m.shader.name);
            return m;
        }

        // Rocket fire: a bright yellow-orange jet out of the nozzle (emits along the transform's +z).
        // It moves with the nozzle (local space): in world space you flew away from your own fire at rocket speed.
        public static ParticleSystem Flame(Transform at)
        {
            var ps = Emitter(at, "CWSkate_RocketFlame", SparkMaterial, 400, ParticleSystemSimulationSpace.Local);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.55f, 0.1f));
            var shape = ps.shape;
            shape.angle = 9f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.5f), new GradientColorKey(new Color(0.8f, 0.1f, 0.02f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
            return ps;
        }

        // Rocket smoke: grey puffs that grow and hang in the air behind you
        public static ParticleSystem Smoke(Transform at)
        {
            var ps = Emitter(at, "CWSkate_RocketSmoke", SmokeMaterial, 300);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.55f, 0.55f, 0.45f), new Color(0.3f, 0.3f, 0.3f, 0.35f));
            main.gravityModifier = -0.05f; // drifts up a little
            var shape = ps.shape;
            shape.angle = 20f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 4f));
            return ps;
        }

        // An explosion: a fireball, black smoke, flying sparks and a flash of light. power 1 = a fuel canister.
        public static void Explosion(Vector3 at, float power)
        {
            var root = new GameObject("CWSkate_Explosion");
            root.transform.position = at;
            bool low = Perf.Low;

            ParticleSystem Burst(string name, Material mat, int count, Vector2 life, Vector2 spd, Vector2 size, Color a, Color b, float gravity, bool stretch)
            {
                count = Mathf.Max(6, Mathf.RoundToInt(count * Perf.ParticleMul));
                var go = new GameObject(name);
                go.transform.SetParent(root.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.duration = 1f;
                main.loop = false;
                main.playOnAwake = false;
                main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
                main.startSpeed = new ParticleSystem.MinMaxCurve(spd.x * power, spd.y * power);
                main.startSize = new ParticleSystem.MinMaxCurve(size.x * power, size.y * power);
                main.startColor = new ParticleSystem.MinMaxGradient(a, b);
                main.gravityModifier = gravity;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = count + 10;
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f * power;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2f; }
                ps.Play();
                return ps;
            }

            // the fireball: big bright puffs that shrink as they burn out
            var fire = Burst("Fire", SparkMaterial, 70, new Vector2(0.35f, 0.8f), new Vector2(2f, 7f), new Vector2(0.6f, 1.6f),
                new Color(1f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.05f), -0.3f, false);
            var fs = fire.sizeOverLifetime;
            fs.enabled = true;
            fs.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            // black smoke that grows and hangs around
            var smoke = Burst("Smoke", SmokeMaterial, 45, new Vector2(1.8f, 3.2f), new Vector2(1f, 3.5f), new Vector2(0.8f, 1.6f),
                new Color(0.15f, 0.13f, 0.12f, 0.75f), new Color(0.3f, 0.28f, 0.26f, 0.6f), -0.12f, false);
            var ss = smoke.sizeOverLifetime;
            ss.enabled = true;
            ss.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));
            // hot sparks and bits flying everywhere
            Burst("Sparks", SparkMaterial, 90, new Vector2(0.5f, 1.4f), new Vector2(6f, 16f), new Vector2(0.04f, 0.09f),
                new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.1f), 1.2f, true);

            // a flash that lights everything around for a moment (a real light is heavy on old PCs: not in the low-end mode)
            if (low) { Object.Destroy(root, 5f); return; }
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 12f * power;
            light.intensity = 8f;
            root.AddComponent<FlashFade>().Set(light, 0.35f);

            Object.Destroy(root, 5f);
        }

        // "Totem of undying": a burst of golden and green sparks swirling up around a player who was saved
        public static void TotemBurst(Vector3 at)
        {
            var go = new GameObject("CWSkate_TotemBurst");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.2f), new Color(0.4f, 1f, 0.3f));
            main.gravityModifier = -0.15f; // they float up
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(160 * Perf.ParticleMul)), new ParticleSystem.Burst(0.25f, (short)(60 * Perf.ParticleMul)) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(3f); // swirl around the player
            vel.y = new ParticleSystem.MinMaxCurve(1.5f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SparkMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            Object.Destroy(go, 3.5f);
        }

        // The glowing tongue of fire right at the nozzle: a stretched glowing blob that flickers (always visible, unlike particles)
        public static Transform FlameCore(Transform at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "CWSkate_RocketCore";
            var c = go.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
            go.transform.SetParent(at, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.09f);
            go.transform.localScale = new Vector3(0.05f, 0.05f, 0.18f);
            var r = go.GetComponent<MeshRenderer>();
            var m = new Material(SparkMaterial) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            var col = new Color(1f, 0.6f, 0.15f, 1f);
            m.color = col;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            return go.transform;
        }

        private static ParticleSystem Emitter(Transform at, string name, Material mat, int max,
            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World)
        {
            var go = new GameObject(name);
            go.transform.SetParent(at, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = space; // world = the trail stays behind you
            main.maxParticles = max;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = 0.015f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        // a small soft round dot
        private static Texture2D DotTexture()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    px[y * N + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // a stripe that's bright in the middle and soft at the edges (across the trail)
        private static Texture2D StripeTexture()
        {
            const int W = 4, H = 32;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / H * 2f - 1f);
                float a = Mathf.Clamp01(1f - v);
                for (int x = 0; x < W; x++) px[y * W + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }

    // A light that dies out quickly (the explosion's flash).
    internal class FlashFade : MonoBehaviour
    {
        private Light lightSrc;
        private float time, start;

        public void Set(Light l, float t) { lightSrc = l; time = t; start = l.intensity; }

        private void Update()
        {
            if (lightSrc == null) return;
            lightSrc.intensity = Mathf.MoveTowards(lightSrc.intensity, 0f, start / Mathf.Max(0.01f, time) * Time.deltaTime);
            if (lightSrc.intensity <= 0f) { lightSrc.enabled = false; enabled = false; }
        }
    }

    // Sparks flying off the rail behind the board while you grind.
    internal class GrindSparks : MonoBehaviour
    {
        private ParticleSystem ps;
        private bool emitting = true;

        public static GrindSparks Create()
        {
            var go = new GameObject("CWSkate_GrindSparks");
            var s = go.AddComponent<GrindSparks>();
            var ps = s.ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.15f));
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;

            var em = ps.emission;
            em.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; // little streaks, not dots
            r.velocityScale = 0.04f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = SkateFx.SparkMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return s;
        }

        // pos = where the board touches the rail, back = the way the sparks fly (behind you), rate = sparks per second
        public void Set(bool on, Vector3 pos, Vector3 back, float rate)
        {
            if (ps == null) return;
            if (!on && !emitting) return;
            emitting = on;
            var em = ps.emission;
            em.rateOverTime = on ? rate * Perf.ParticleMul : 0f;
            if (!on) return;
            transform.position = pos;
            Vector3 dir = back.sqrMagnitude > 1e-4f ? back.normalized + Vector3.up * 0.6f : Vector3.up;
            transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private void OnDestroy() { }
    }

    // Glowing tracks behind the back wheels, in the colour of your aura rank (rainbow for AURA GOD).
    internal class WheelTrails : MonoBehaviour
    {
        private TrailRenderer[] trails;
        private bool wasOn;
        private float offFor;

        public static WheelTrails Attach(BoardVisual board)
        {
            var w = board.gameObject.AddComponent<WheelTrails>();
            w.trails = new TrailRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("CWSkate_WheelTrail");
                go.transform.SetParent(board.transform, false);
                go.transform.localPosition = new Vector3(i == 0 ? 0.087f : -0.087f, -0.062f, -0.215f); // back wheels, just above the floor
                var t = go.AddComponent<TrailRenderer>();
                t.time = 0.8f;
                t.startWidth = 0.045f;
                t.endWidth = 0f;
                t.minVertexDistance = 0.05f;
                t.numCapVertices = 2;
                t.alignment = LineAlignment.View;
                t.sharedMaterial = SkateFx.TrailMaterial;
                t.shadowCastingMode = ShadowCastingMode.Off;
                t.receiveShadows = false;
                t.emitting = false;
                w.trails[i] = t;
            }
            return w;
        }

        // fire = the Ride to Hell board: a flickering yellow-orange track that burns out red
        public void Tick(bool on, Color c, float dt, bool fire = false)
        {
            if (trails == null) return;
            if (Perf.Low) on = false; // the low-end PC mode has no tracks
            if (!on && !wasOn && offFor > 1f) { offFor += dt; return; } // already off: nothing to update
            if (!on) offFor += dt;
            bool restart = on && !wasOn && offFor > 0.2f; // don't draw a line from where you were before
            if (on) offFor = 0f;
            wasOn = on;
            Color start = new Color(c.r, c.g, c.b, 0.85f), end = new Color(c.r, c.g, c.b, 0f);
            float width = 0.045f;
            if (fire)
            {
                float flicker = Mathf.PerlinNoise(Time.time * 9f, 0.3f);
                start = Color.Lerp(new Color(1f, 0.45f, 0.08f, 0.95f), new Color(1f, 0.85f, 0.3f, 0.95f), flicker);
                end = new Color(0.85f, 0.08f, 0.03f, 0f);
                width = 0.06f + 0.015f * flicker;
            }
            foreach (var t in trails)
            {
                if (t == null) continue;
                if (restart) t.Clear();
                t.emitting = on;
                t.startColor = start;
                t.endColor = end;
                t.startWidth = width;
            }
        }
    }
}
