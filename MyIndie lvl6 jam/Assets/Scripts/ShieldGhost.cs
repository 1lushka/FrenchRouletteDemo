using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

public class ShieldGhost : MonoBehaviour
{
    private const float WindowWidth = 0.05f;
    private const float WindowHeight = 0.0375f;
    private const float WindowDepthOffset = 0.09f;  
    private const float RowOffset = -0.03f;      

    private static Material s_bodyMaterial;
    private static Material s_frameMaterial;
    private static Mesh s_quadMesh;
    private static TMP_FontAsset s_font;
    private static bool s_fontChecked;

    private Camera cam;
    private readonly List<Window> windows = new List<Window>();
    private Transform windowsRoot; 
    private int first = -1;
    private int size;

    private sealed class Window
    {
        public Transform root;
        public Transform fill;
        public Material fillMaterial;
        public TMP_Text tmpLabel;
        public TextMesh legacyLabel;
        public TapePiece tape;
        public Vector3 worldPos;
        public int lastHealth = -1;
        public int lastMax = -1;
    }


    public static ShieldGhost Create(Transform shieldRoot, Camera cam)
    {
        if (shieldRoot == null) return null;

        var go = new GameObject("ShieldGhost");
        var ghost = go.AddComponent<ShieldGhost>();
        ghost.cam = cam;

        go.transform.SetPositionAndRotation(shieldRoot.position, shieldRoot.rotation);
        go.transform.localScale = shieldRoot.lossyScale;

        for (int i = 0; i < shieldRoot.childCount; i++)
        {
            Transform src = shieldRoot.GetChild(i);
            Transform clone = Instantiate(src, go.transform);
            clone.localPosition = src.localPosition;
            clone.localRotation = src.localRotation;
            clone.localScale = src.localScale;
            Sanitize(clone);
        }

        return ghost;
    }

    private static void Sanitize(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.tag = "Untagged";

            foreach (Collider col in t.GetComponents<Collider>())
                col.enabled = false;

            foreach (Component comp in t.GetComponents<Component>())
            {
                if (comp is Behaviour behaviour)
                {
                    string typeName = behaviour.GetType().Name;
                    if (typeName == "DecalProjector" || typeName == "Outline" || typeName == "HoverOutline")
                        behaviour.enabled = false;
                }
                else if (comp is ParticleSystem ps)
                {
                    ps.Clear(true);
                }
                else if (comp is TrailRenderer trail)
                {
                    trail.Clear();
                }
            }
        }

        if (s_bodyMaterial == null)
            s_bodyMaterial = CreateUnlitMaterial(new Color(0.45f, 0.85f, 1f, 0.35f));

        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = s_bodyMaterial;
            r.sharedMaterials = mats;
        }
    }

    public void SetWorldX(float x)
    {
        Vector3 p = transform.position;
        if (!Mathf.Approximately(p.x, x))
            transform.position = new Vector3(x, p.y, p.z);

        foreach (Window w in windows)
            if (w.root != null && w.root.gameObject.activeSelf)
                w.root.position = w.worldPos;
    }

    public void SetSegmentRange(int firstIndex, int count)
    {
        if (firstIndex == first && count == size) return;
        first = firstIndex;
        size = count;

        IReadOnlyList<TapePiece> segments = ShieldSegmentLayout.Segments;

        while (windows.Count < count)
            windows.Add(CreateWindow());

        for (int i = 0; i < windows.Count; i++)
        {
            Window w = windows[i];
            bool active = i < count;
            w.root.gameObject.SetActive(active);
            if (!active) continue;

            int segmentIndex = firstIndex + i;
            if (segmentIndex < 0 || segmentIndex >= segments.Count || segments[segmentIndex] == null)
                continue;

            TapePiece tape = segments[segmentIndex];
            w.tape = tape;

            Vector3 sp = tape.transform.position;
            float y = sp.y + RowOffset;
            w.worldPos = new Vector3(sp.x, y, sp.z - WindowDepthOffset);
            w.root.position = w.worldPos;
            w.lastHealth = -1; // force refresh
        }
    }


    private void LateUpdate()
    {
        Vector3 camPos = cam != null ? cam.transform.position : transform.position + Vector3.back;

        foreach (Window w in windows)
        {
            if (w.root == null || !w.root.gameObject.activeSelf || w.tape == null) continue;

            // face the camera
            Vector3 toCam = camPos - w.worldPos;
            if (toCam.sqrMagnitude > 1e-6f)
                w.root.rotation = Quaternion.LookRotation(toCam);

            // health display
            int hp = w.tape.Health;
            int max = w.tape.MaxHealth;
            if (hp == w.lastHealth && max == w.lastMax) continue;

            w.lastHealth = hp;
            w.lastMax = max;

            float ratio = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;

            Vector3 fillScale = w.fill.localScale;
            fillScale.x = WindowWidth * 0.96f * ratio;
            w.fill.localScale = fillScale;
            SetMaterialColor(w.fillMaterial, HealthColor(ratio));

            string label = hp.ToString();
            if (w.tmpLabel != null) w.tmpLabel.text = label;
            else if (w.legacyLabel != null) w.legacyLabel.text = label;
        }
    }

    private static Color HealthColor(float ratio)
    {
        if (ratio > 0.66f) return new Color(0.25f, 0.9f, 0.35f, 0.85f);
        if (ratio > 0.33f) return new Color(1f, 0.8f, 0.1f, 0.85f);
        return new Color(0.95f, 0.2f, 0.12f, 0.85f);
    }


    private Transform WindowsRoot()
    {
        if (windowsRoot == null)
        {
            var go = new GameObject("ShieldHealthWindows");
            windowsRoot = go.transform;
        }
        return windowsRoot;
    }

    //это тестовые ui для отображения хп сегментов
    private Window CreateWindow()
    {
        var w = new Window();

        var rootGo = new GameObject("HealthWindow");
        rootGo.transform.SetParent(WindowsRoot(), false);
        w.root = rootGo.transform;

        GameObject frameGo = new GameObject("Frame");
        frameGo.transform.SetParent(w.root, false);
        frameGo.transform.localPosition = new Vector3(-WindowWidth * 0.5f, 0f, 0f);
        frameGo.transform.localScale = new Vector3(WindowWidth, WindowHeight, 1f);
        var frameFilter = frameGo.AddComponent<MeshFilter>();
        frameFilter.sharedMesh = QuadMesh();
        var frameRenderer = frameGo.AddComponent<MeshRenderer>();
        frameRenderer.sharedMaterial = FrameMaterial();
        frameRenderer.shadowCastingMode = ShadowCastingMode.Off;
        frameRenderer.receiveShadows = false;

        GameObject fillGo = new GameObject("Fill");
        fillGo.transform.SetParent(w.root, false);
        fillGo.transform.localPosition = new Vector3(-WindowWidth * 0.5f, 0f, 0.001f);
        fillGo.transform.localScale = new Vector3(WindowWidth * 0.96f, WindowHeight * 0.78f, 1f);
        var fillFilter = fillGo.AddComponent<MeshFilter>();
        fillFilter.sharedMesh = QuadMesh();
        var fillRenderer = fillGo.AddComponent<MeshRenderer>();
        w.fillMaterial = CreateUnlitMaterial(HealthColor(1f));
        fillRenderer.sharedMaterial = w.fillMaterial;
        fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
        fillRenderer.receiveShadows = false;
        w.fill = fillGo.transform;

        // health number label
        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(w.root, false);

        TMP_FontAsset font = DefaultFont();
        if (font != null)
        {
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.font = font;
            tmp.text = "0";
            tmp.enableAutoSizing = false;
            tmp.fontSize = 0.15f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.3f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            tmp.rectTransform.sizeDelta = new Vector2(0.35f, 0.15f);
            tmp.rectTransform.localPosition = new Vector3(0f, 0f, 0.002f);
            tmp.rectTransform.localRotation = Quaternion.identity;
            w.tmpLabel = tmp;
        }
        else
        {
            var tm = labelGo.AddComponent<TextMesh>();
            Font legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = legacyFont;
            tm.text = "0";
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = 0.04f;
            tm.fontSize = 48;
            tm.color = Color.white;

            var labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer == null) labelRenderer = labelGo.AddComponent<MeshRenderer>();
            if (legacyFont != null) labelRenderer.sharedMaterial = legacyFont.material;
            labelRenderer.shadowCastingMode = ShadowCastingMode.Off;
            labelRenderer.receiveShadows = false;
            w.legacyLabel = tm;
        }

        labelGo.transform.localPosition = new Vector3(0f, 0.09f, 0.002f);
        return w;
    }


    public static Material CreateUnlitMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        SetMaterialColor(mat, color);

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        return mat;
    }

    public static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    private static Material FrameMaterial()
    {
        if (s_frameMaterial == null)
            s_frameMaterial = CreateUnlitMaterial(new Color(0f, 0f, 0f, 0.55f));
        return s_frameMaterial;
    }

    private static Mesh QuadMesh()
    {
        if (s_quadMesh != null) return s_quadMesh;

        var mesh = new Mesh { name = "ShieldGhostQuad" };
        mesh.vertices = new[]
        {
            new Vector3(0f, -0.5f, 0f),
            new Vector3(1f, -0.5f, 0f),
            new Vector3(1f, 0.5f, 0f),
            new Vector3(0f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        s_quadMesh = mesh;
        return mesh;
    }

    private static TMP_FontAsset DefaultFont()
    {
        if (!s_fontChecked)
        {
            s_fontChecked = true;
            try { s_font = TMP_Settings.defaultFontAsset; }
            catch { s_font = null; }
        }
        return s_font;
    }


    public void Dispose()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        foreach (Window w in windows)
            if (w.fillMaterial != null)
                Destroy(w.fillMaterial);
        windows.Clear();

        if (windowsRoot != null) Destroy(windowsRoot.gameObject);
    }
}
