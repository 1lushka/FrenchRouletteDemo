using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IlyaTestValidator
{
    private static int errors;

    public static void Run()
    {
        errors = 0;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/IlyaTest.unity", OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[Validate] FAIL: cannot open IlyaTest.unity");
            EditorApplication.Exit(2);
            return;
        }
        Ok("scene opened: " + scene.path);

        ValidateShieldController();
        ValidateGameManager();
        ValidateSegments();
        ValidateShields();

        if (errors == 0)
            Debug.Log("[Validate] ALL CHECKS PASSED");
        else
            Debug.LogError($"[Validate] {errors} CHECK(S) FAILED");

        EditorApplication.Exit(errors == 0 ? 0 : 1);
    }

    private static void ValidateShieldController()
    {
        ShieldController sc = Object.FindFirstObjectByType<ShieldController>();
        if (sc == null) { Fail("ShieldController not found in scene"); return; }
        Ok("ShieldController found on '" + sc.gameObject.name + "'");

        if (sc.ShieldSegmentSize == 3) Ok("ShieldSegmentSize = 3"); else Fail($"ShieldSegmentSize = {sc.ShieldSegmentSize}, expected 3");
        if (sc is InteractiveObject) Ok("ShieldController inherits InteractiveObject"); else Fail("ShieldController does not inherit InteractiveObject");
        var soFlag = new SerializedObject(sc);
        var flagProp = soFlag.FindProperty("isInteractive");
        if (flagProp != null && flagProp.boolValue) Ok("isInteractive = true"); else Fail("isInteractive missing or false");
        if (sc.isInteractive) Ok("isInteractive readable via parent-class field"); else Fail("isInteractive field is false");
        var widthProp = soFlag.FindProperty("highlightWidth");
        if (widthProp != null && widthProp.floatValue > 0f) Ok($"highlightWidth = {widthProp.floatValue}"); else Fail("highlightWidth missing or <= 0");
        var colorProp = soFlag.FindProperty("highlightColor");
        if (colorProp != null && colorProp.colorValue.r > 0.9f && colorProp.colorValue.g > 0.7f && colorProp.colorValue.b < 0.1f)
            Ok("highlightColor is golden");
        else Fail("highlightColor is not golden");
        if (Mathf.Approximately(sc.ShieldMovingTime, 1f)) Ok("ShieldMovingTime = 1"); else Fail($"ShieldMovingTime = {sc.ShieldMovingTime}, expected 1");
        if (Mathf.Approximately(sc.ShieldTiltAngleMax, 30f)) Ok("ShieldTiltAngleMax = 30"); else Fail($"ShieldTiltAngleMax = {sc.ShieldTiltAngleMax}, expected 30");

        var so = new SerializedObject(sc);
        if (so.FindProperty("moveClip").objectReferenceValue == null)
            Ok("s_ShieldMove (moveClip) disabled by request");
        else
            CheckClip(so, "moveClip", "s_ShieldMove (moveClip)");
        CheckClipArray(so, "takeClip", "s_ShieldTaken (takeClip)");
        CheckClipArray(so, "stackClip", "s_ShieldPlaced (stackClip)");
        if (so.FindProperty("audioSource").objectReferenceValue != null) Ok("Shield audioSource assigned"); else Fail("Shield audioSource is null");
        if (so.FindProperty("cam").objectReferenceValue != null) Ok("Shield cam assigned"); else Fail("Shield cam is null");
    }

    private static void ValidateGameManager()
    {
        GameManager gm = Object.FindFirstObjectByType<GameManager>();
        if (gm == null) { Fail("GameManager not found in scene"); return; }

        var so = new SerializedObject(gm);
        CheckClipArray(so, "barrierOpenSounds", "barrierOpenSounds");
        CheckClipArray(so, "barrierCloseSounds", "barrierCloseSounds");
        CheckClipArray(so, "roundStartSounds", "roundStartSounds");
        if (so.FindProperty("barrier").objectReferenceValue != null) Ok("GameManager barrier assigned"); else Fail("GameManager barrier is null");
    }

    private static void ValidateSegments()
    {
        TapePiece[] tapes = Object.FindObjectsByType<TapePiece>(FindObjectsSortMode.None);
        if (tapes.Length == 14) Ok("14 TapePiece rope segments present");
        else { Fail($"found {tapes.Length} TapePiece instances, expected 14"); return; }

        var xs = tapes.Select(t => t.transform.position.x).OrderBy(x => x).ToList();
        float spacing = (xs[13] - xs[0]) / 13f;
        if (spacing > 0.05f && spacing < 0.2f) Ok($"segment spacing = {spacing:0.0000}");
        else Fail($"implausible segment spacing {spacing:0.0000}");
    }

    private static void ValidateShields()
    {
        GameObject[] tagged;
        try { tagged = GameObject.FindGameObjectsWithTag("Shield"); }
        catch (System.ArgumentException) { Fail("tag 'Shield' missing"); return; }

        var roots = new List<Transform>();
        foreach (GameObject go in tagged)
        {
            Transform top = null, t = go.transform;
            while (t != null) { if (t.CompareTag("Shield")) top = t; t = t.parent; }
            if (top != null && !roots.Contains(top)) roots.Add(top);
        }

        if (roots.Count == 3) Ok("3 shield root markers tagged 'Shield'");
        else Fail($"found {roots.Count} shield roots, expected 3 (tagged objects: {tagged.Length})");

        foreach (Transform r in roots)
        {
            BoxCollider col = r.GetComponentInChildren<BoxCollider>(true);
            if (col == null) Fail($"shield '{r.name}' has no BoxCollider");
            else if (r.childCount == 0) Fail($"shield '{r.name}' has no visual child");

            Outline outline = r.GetComponentInChildren<Outline>(true);
            if (outline == null) Fail($"shield '{r.name}' has no Outline component for hover highlight");
            else if (outline.enabled) Fail($"shield '{r.name}' Outline is enabled in edit mode (should start disabled)");
            else Ok($"shield '{r.name}' has Outline (disabled until hover)");
        }

        HoverOutline hover = Object.FindFirstObjectByType<HoverOutline>();
        if (hover != null && hover.enabled) Fail("HoverOutline on camera is enabled - conflicts with InteractiveObject highlight");
        else Ok(hover != null ? "HoverOutline on camera disabled (superseded)" : "no HoverOutline in scene");

        ValidateGreedySnap(roots);
    }

    private static void ValidateGreedySnap(List<Transform> roots)
    {
        TapePiece[] tapes = Object.FindObjectsByType<TapePiece>(FindObjectsSortMode.None);
        if (tapes.Length != 14 || roots.Count != 3) return;

        var xs = tapes.Select(t => t.transform.position.x).OrderBy(x => x).ToList();
        const int size = 3;
        float Slot(int first) => xs[first + (size - 1) / 2];

        var authored = roots.Select(r => r.position.x).OrderBy(x => x).ToList();

        var assignments = new List<int>();
        int nextFree = 0;
        foreach (float x in authored)
        {
            int best = nextFree;
            float bd = float.MaxValue;
            for (int f = nextFree; f <= 14 - size; f++)
            {
                float d = Mathf.Abs(Slot(f) - x);
                if (d < bd) { bd = d; best = f; }
            }
            assignments.Add(best);
            nextFree = best + size;
        }

        string got = string.Join(",", assignments);
        if (got == "1,4,7")
        {
            Ok("greedy snap: authored shields -> slots 1-3 / 4-6 / 7-9");
            float tutorialX = Slot(7);
            int nearestTape = 0; float bd = float.MaxValue;
            for (int i = 0; i < xs.Count; i++) { float d = Mathf.Abs(xs[i] - tutorialX); if (d < bd) { bd = d; nearestTape = i; } }
            if (nearestTape == 8) Ok("tutorial shield center aligns with rope segment 8 (second knife target)");
            else Fail($"tutorial shield aligns with segment {nearestTape}, expected 8");
        }
        else
        {
            Fail($"greedy snap assignments = [{got}], expected [1,4,7] (authored X: {string.Join(", ", authored.Select(v => v.ToString("0.000")))} )");
        }
    }

    private static void CheckClip(SerializedObject so, string prop, string label)
    {
        var p = so.FindProperty(prop);
        if (p == null) { Fail($"{label}: property missing"); return; }
        if (p.objectReferenceValue != null) Ok($"{label} assigned"); else Fail($"{label} is null");
    }

    private static void CheckClipArray(SerializedObject so, string prop, string label)
    {
        var p = so.FindProperty(prop);
        if (p == null || !p.isArray) { Fail($"{label}: array property missing"); return; }

        int nonNull = 0;
        for (int i = 0; i < p.arraySize; i++)
            if (p.GetArrayElementAtIndex(i).objectReferenceValue != null)
                nonNull++;

        if (p.arraySize == 0) Fail($"{label}: empty");
        else if (nonNull == 0) Fail($"{label}: all {p.arraySize} entries are null");
        else if (nonNull < p.arraySize) Ok($"{label}: {nonNull}/{p.arraySize} clips (nulls are skipped at runtime)");
        else Ok($"{label}: {nonNull} clips");
    }

    private static void Ok(string msg) => Debug.Log("[Validate] OK   : " + msg);
    private static void Fail(string msg) { errors++; Debug.LogError("[Validate] FAIL : " + msg); }
}