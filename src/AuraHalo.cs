using UnityEngine;
using UnityEngine.Rendering;

namespace CWSkate
{
    // Soft glowing disc around the rider that always faces the camera. It only brightens what's behind it
    // and doesn't light the surroundings. Your own halo is hidden in first person (it would be inside your head).
    internal class AuraHalo : MonoBehaviour
    {
        private const int LocalHiddenLayer = 29;
        private static Material sharedMat;
        private static Texture2D tex;
        private static bool additive;

        private Material mat;
        private MeshRenderer rend;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private int hasBaseColor = -1; // -1 = not checked yet
        private float strength;

        public static AuraHalo Create(bool localPlayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "CWSkate_AuraHalo";
            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            if (localPlayer) go.layer = LocalHiddenLayer;
            go.transform.localScale = Vector3.one * 1.7f;
            var h = go.AddComponent<AuraHalo>();
            h.rend = go.GetComponent<MeshRenderer>();
            h.rend.shadowCastingMode = ShadowCastingMode.Off;
            h.rend.receiveShadows = false;
            h.mat = new Material(SharedMaterial());
            h.rend.sharedMaterial = h.mat;
            h.rend.enabled = false;
            return h;
        }

        private static Material SharedMaterial()
        {
            if (sharedMat != null) return sharedMat;
            var t = Texture();
            var s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s != null && s.isSupported)
            {
                var m = new Material(s);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 2f); // additive
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
                m.mainTexture = t;
                sharedMat = m;
                additive = true;
            }
            else
            {
                var m = new Material(Shader.Find("Sprites/Default"));
                m.mainTexture = t;
                m.renderQueue = (int)RenderQueue.Transparent;
                sharedMat = m;
            }
            sharedMat.hideFlags = HideFlags.DontUnloadUnusedAsset;
            SkatePlugin.Log.LogInfo("Aura halo shader: " + sharedMat.shader.name);
            return sharedMat;
        }

        // round soft glow, brightest in the middle
        private static Texture2D Texture()
        {
            if (tex != null) return tex;
            const int N = 64;
            tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a); // smooth edge
                    px[y * N + x] = new Color(1f, 1f, 1f, a * a);
                }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public void Set(Vector3 pos, Color color, float target, float dt)
        {
            strength = Mathf.MoveTowards(strength, target, dt * 2f);
            rend.enabled = strength > 0.01f;
            if (!rend.enabled) return;
            transform.position = pos;
            var cam = MainCamera.instance != null ? MainCamera.instance.Cam : Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.06f;
            transform.localScale = Vector3.one * (1.4f + strength * 0.8f) * pulse;
            color.a = Mathf.Clamp01(strength) * (additive ? 0.8f : 0.45f);
            mat.color = color;
            if (hasBaseColor < 0) hasBaseColor = mat.HasProperty(BaseColorId) ? 1 : 0;
            if (hasBaseColor == 1) mat.SetColor(BaseColorId, color);
        }

        public void Hide()
        {
            strength = 0f;
            if (rend) rend.enabled = false;
        }

        private void OnDestroy()
        {
            if (mat) Destroy(mat);
        }
    }
}
