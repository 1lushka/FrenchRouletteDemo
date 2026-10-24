using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ShieldController : InteractiveObject
{
    [Header("Spec params")]
    public int ShieldSegmentSize = 3;
    public float ShieldMovingTime = 1f;
    public float ShieldTiltAngleMax = 30f;

    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private string targetTag = "Shield";

    [Header("X Clamp (drag, used when no rope layout is available)")]
    [SerializeField] private float xMin = -5f, xMax = 5f;

    [Header("Take / Insert animations")]
    [SerializeField] private float hoverHeight = 1.0f;
    [SerializeField] private float liftDuration = 0.2f;
    [SerializeField] private Ease liftEase = Ease.OutQuad;

    [Header("Drop (external)")]
    [SerializeField] private float externalHideOffsetY = 8f;
    [SerializeField] private float externalDropDuration = 0.45f;
    [SerializeField] private Ease externalDropEase = Ease.OutQuad;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] stackClip;
    [SerializeField] private AudioClip[] takeClip;
    [SerializeField] private AudioClip moveClip;

    [Header("Placement marks")]
    [SerializeField] private int maxPlacementMarks = 6;

    private Transform draggedRoot;
    private Transform draggedVisual;
    private Vector3 visualRestLocalPos;
    private Quaternion visualRestLocalRot;
    private Vector3 tiltPivotLocal;
    private float releaseY;
    private float ghostTargetX;
    private bool hasTargetX;

    private Tween liftTween;
    private Tween moveTween;
    private Tween insertTween;

    private ShieldGhost ghost;
    private AudioSource moveLoop;
    private float lastTrackedX;
    private float shieldWidth;
    private bool layoutReady;

    private readonly Queue<GameObject> placementMarks = new Queue<GameObject>();
    private Transform marksRoot;
    private static Material markMaterial;

    private float _baseY;

    public void HideAbove()
    {
        var tr = transform;
        tr.DOKill();
        tr.position = new Vector3(tr.position.x, _baseY + externalHideOffsetY, tr.position.z);
    }

    public Tween DropFromTop()
    {
        var tr = transform;
        tr.DOKill();
        tr.position = new Vector3(tr.position.x, _baseY + externalHideOffsetY, tr.position.z);
        return tr.DOMoveY(_baseY, externalDropDuration).SetEase(externalDropEase);
    }

    private int appliedSegmentSize = -1;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        ShieldSegmentLayout.Build();
        layoutReady = ShieldSegmentLayout.Count > 0;

        if (layoutReady)
        {
            ShieldSegmentSize = Mathf.Clamp(ShieldSegmentSize, 1, ShieldSegmentLayout.Count);
            shieldWidth = ShieldSegmentSize * ShieldSegmentLayout.Spacing;

            List<Transform> roots = FindShieldRoots();
            foreach (Transform root in roots)
                ResizeShield(root, shieldWidth);

            SnapAllShieldsIntoSlots(roots);
            appliedSegmentSize = ShieldSegmentSize;
        }
        else
        {
            ShieldSegmentSize = Mathf.Max(1, ShieldSegmentSize);
            shieldWidth = ShieldSegmentSize * 0.1f;
            appliedSegmentSize = ShieldSegmentSize;
            Debug.LogWarning("[ShieldController] No rope segments found - shield snapping disabled.");
        }
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;
        _baseY = transform.position.y;
    }

    protected override void Update()
    {
        base.Update();

        if (draggedRoot == null && ghost != null)
        {
            ghost.Dispose();
            ghost = null;
            StopMoveLoop();
            isInteractive = true;
        }

        ApplySegmentSizeIfChanged();

        if (Input.GetMouseButtonDown(0))
            TryPick();

        if (draggedRoot != null)
        {
            if (Input.GetMouseButton(0))
                UpdateDrag();
            else
                ReleaseObject();
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        liftTween?.Kill(); liftTween = null;
        moveTween?.Kill(); moveTween = null;
        insertTween?.Kill(); insertTween = null;

        if (draggedRoot != null)
        {
            ApplyTilt(0f);
            draggedRoot = null;
        }
        hasTargetX = false;

        if (ghost != null)
        {
            ghost.Dispose();
            ghost = null;
        }

        StopMoveLoop();
        isInteractive = true;
    }

    private List<Transform> FindShieldRoots()
    {
        var result = new List<Transform>();

        GameObject[] tagged;
        try { tagged = GameObject.FindGameObjectsWithTag(targetTag); }
        catch (System.ArgumentException)
        {
            Debug.LogWarning($"[ShieldController] Tag '{targetTag}' does not exist.");
            return result;
        }

        foreach (GameObject go in tagged)
        {
            Transform top = FindTaggedRoot(go.transform);
            if (top != null && !result.Contains(top))
                result.Add(top);
        }
        return result;
    }

    private Transform FindTaggedRoot(Transform t)
    {
        Transform top = null;
        while (t != null)
        {
            if (t.CompareTag(targetTag)) top = t;
            t = t.parent;
        }
        return top;
    }

    protected override bool IsPointerOver(RaycastHit hit) => FindTaggedRoot(hit.transform) != null;

    protected override Outline FindHighlightOutline(RaycastHit hit)
    {
        Transform root = FindTaggedRoot(hit.transform);
        return root != null ? root.GetComponentInChildren<Outline>(true) : null;
    }

    private void ResizeShield(Transform root, float targetWidth)
    {
        BoxCollider body = root.GetComponentInChildren<BoxCollider>(true);
        if (body == null) return;

        bool wasEnabled = body.enabled;
        body.enabled = true;
        Physics.SyncTransforms();

        float current = body.bounds.size.x;
        if (current <= 1e-5f || Mathf.Abs(current - targetWidth) <= 1e-4f)
        {
            body.enabled = wasEnabled;
            return;
        }

        Vector3 original = root.localScale;
        float factor = targetWidth / current;

        int axis = -1;
        float bestErr = Mathf.Abs(current - targetWidth);
        for (int a = 0; a < 3; a++)
        {
            if (Mathf.Abs(original[a]) < 1e-6f) continue;
            root.localScale = SetAxis(original, a, original[a] * factor);
            Physics.SyncTransforms();
            float err = Mathf.Abs(body.bounds.size.x - targetWidth);
            if (err < bestErr - 1e-6f)
            {
                axis = a;
                bestErr = err;
                break;
            }
            root.localScale = original;
            Physics.SyncTransforms();
        }

        if (axis < 0)
        {
            Debug.LogWarning($"[ShieldController] Could not resize '{root.name}' to {targetWidth:0.###} (measured {current:0.###}).");
            root.localScale = original;
            body.enabled = wasEnabled;
            return;
        }

        for (int i = 0; i < 6 && bestErr > 1e-4f; i++)
        {
            float measured = body.bounds.size.x;
            if (measured <= 1e-5f) break;
            Vector3 s = root.localScale;
            root.localScale = SetAxis(s, axis, s[axis] * (targetWidth / measured));
            Physics.SyncTransforms();
            bestErr = Mathf.Abs(body.bounds.size.x - targetWidth);
        }

        body.enabled = wasEnabled;

        if (bestErr > targetWidth * 0.01f)
            Debug.LogWarning($"[ShieldController] '{root.name}' width {body.bounds.size.x:0.###} differs from target {targetWidth:0.###}.");
    }

    private void ApplySegmentSizeIfChanged()
    {
        if (!layoutReady || ShieldSegmentSize == appliedSegmentSize || draggedRoot != null) return;

        ShieldSegmentSize = Mathf.Clamp(ShieldSegmentSize, 1, ShieldSegmentLayout.Count);
        appliedSegmentSize = ShieldSegmentSize;
        shieldWidth = ShieldSegmentSize * ShieldSegmentLayout.Spacing;

        List<Transform> roots = FindShieldRoots();
        foreach (Transform root in roots)
            ResizeShield(root, shieldWidth);

        SnapAllShieldsIntoSlots(roots);
    }

    private static Vector3 SetAxis(Vector3 v, int axis, float value)
    {
        v[axis] = value;
        return v;
    }

    private void SnapAllShieldsIntoSlots(List<Transform> roots)
    {
        if (!layoutReady || roots.Count == 0) return;

        roots.Sort((a, b) => a.position.x.CompareTo(b.position.x));

        int size = ShieldSegmentSize;
        int maxFirst = ShieldSegmentLayout.Count - size;
        int nextFree = 0;

        foreach (Transform root in roots)
        {
            if (nextFree > maxFirst) nextFree = 0;
            int first = NearestFirstIndex(root.position.x, size, nextFree);
            Vector3 p = root.position;
            p.x = ShieldSegmentLayout.SlotCenter(first, size);
            root.position = p;
            nextFree = first + size;
        }
    }

    private int NearestFirstIndex(float worldX, int size, int minFirst)
    {
        int maxFirst = ShieldSegmentLayout.Count - size;
        int start = Mathf.Clamp(minFirst, 0, maxFirst);

        int best = start;
        float bestDist = float.MaxValue;
        for (int f = start; f <= maxFirst; f++)
        {
            float d = Mathf.Abs(ShieldSegmentLayout.SlotCenter(f, size) - worldX);
            if (d < bestDist - 1e-7f)
            {
                bestDist = d;
                best = f;
            }
        }
        return best;
    }

    private void TryPick()
    {
        if (!isInteractive) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        Transform root = FindTaggedRoot(hit.transform);
        if (root == null) return;

        BeginDrag(root);
    }

    private void BeginDrag(Transform root)
    {
        isInteractive = false;

        root.DOKill(true);

        releaseY = root.position.y;
        lastTrackedX = root.position.x;
        hasTargetX = false;
        draggedRoot = root;

        BoxCollider body = root.GetComponentInChildren<BoxCollider>(true);
        draggedVisual = body != null ? body.transform : (root.childCount > 0 ? root.GetChild(0) : root);
        visualRestLocalPos = draggedVisual.localPosition;
        visualRestLocalRot = draggedVisual.localRotation;
        tiltPivotLocal = ComputeTiltPivotLocal(root, body);

        ghost = ShieldGhost.Create(root, cam);
        if (ghost != null) ghost.SetWorldX(root.position.x);

        liftTween?.Kill();
        liftTween = root.DOMoveY(releaseY + hoverHeight, liftDuration)
            .SetEase(liftEase)
            .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);

        PlayRandomSoundFrom(takeClip);
        StartMoveLoop();
    }

    private static Vector3 ComputeTiltPivotLocal(Transform root, Collider body)
    {
        if (body == null) return new Vector3(0f, 0.5f, 0f);
        Bounds b = body.bounds;
        Vector3 topCenterWorld = new Vector3(b.center.x, b.max.y, b.center.z);
        return root.InverseTransformPoint(topCenterWorld);
    }

    private void UpdateDrag()
    {
        float mouseWorldX = MouseWorldXOnPlane(draggedRoot.position.z);

        float targetX;
        if (layoutReady)
        {
            ShieldSegmentLayout.GetClampRange(ShieldSegmentSize, out float minCenter, out float maxCenter);
            float clamped = Mathf.Clamp(mouseWorldX, minCenter, maxCenter);
            int first = ShieldSegmentLayout.SnapFirstIndex(clamped, ShieldSegmentSize);
            targetX = ShieldSegmentLayout.SlotCenter(first, ShieldSegmentSize);

            if (ghost != null) ghost.SetSegmentRange(first, ShieldSegmentSize);
        }
        else
        {
            targetX = Mathf.Clamp(mouseWorldX, xMin, xMax);
        }

        if (ghost != null) ghost.SetWorldX(targetX);

        if (!hasTargetX || !Mathf.Approximately(targetX, ghostTargetX))
        {
            hasTargetX = true;
            ghostTargetX = targetX;

            moveTween?.Kill();
            moveTween = draggedRoot.DOMoveX(targetX, Mathf.Max(0.01f, ShieldMovingTime))
                .SetEase(Ease.OutQuad)
                .SetLink(draggedRoot.gameObject, LinkBehaviour.KillOnDestroy);
        }

        float remaining = ghostTargetX - draggedRoot.position.x;
        float spacing = layoutReady ? ShieldSegmentLayout.Spacing : 0.1f;
        float reference = Mathf.Max(0.01f, ShieldSegmentSize * spacing);
        float angle = -Mathf.Sign(remaining) * ShieldTiltAngleMax
                      * Mathf.Clamp01(Mathf.Abs(remaining) / reference);
        ApplyTilt(angle);

        float speed = Time.deltaTime > 0f
            ? Mathf.Abs(draggedRoot.position.x - lastTrackedX) / Time.deltaTime
            : 0f;
        lastTrackedX = draggedRoot.position.x;
        UpdateMoveLoopVolume(speed);
    }

    private float MouseWorldXOnPlane(float planeZ)
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Mathf.Abs(ray.direction.z) < 1e-5f) return draggedRoot.position.x;

        float t = (planeZ - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return draggedRoot.position.x;

        return ray.origin.x + ray.direction.x * t;
    }

    private void ApplyTilt(float angleDegrees)
    {
        if (draggedVisual == null || draggedRoot == null) return;

        draggedVisual.localPosition = visualRestLocalPos;
        draggedVisual.localRotation = visualRestLocalRot;

        if (Mathf.Abs(angleDegrees) < 0.01f) return;

        Quaternion q = Quaternion.AngleAxis(angleDegrees, Vector3.forward);
        Vector3 pivotWorld = draggedRoot.TransformPoint(tiltPivotLocal);
        draggedVisual.position = q * (draggedVisual.position - pivotWorld) + pivotWorld;
        draggedVisual.rotation = q * draggedVisual.rotation;
    }

    private void ReleaseObject()
    {
        if (draggedRoot == null) return;

        Transform root = draggedRoot;
        Vector3 p = root.position;
        float landX = hasTargetX ? ghostTargetX : p.x;
        float markZ = p.z;

        moveTween?.Kill(); moveTween = null;
        liftTween?.Kill(); liftTween = null;

        root.position = new Vector3(landX, p.y, p.z);
        ApplyTilt(0f);

        draggedRoot = null;
        draggedVisual = null;
        hasTargetX = false;
        StopMoveLoop();

        if (ghost != null)
        {
            ghost.Dispose();
            ghost = null;
        }

        bool finished = false;
        void FinishPlacement()
        {
            if (finished) return;
            finished = true;
            insertTween = null;

            isInteractive = true;
            PlayRandomSoundFrom(stackClip);
            SpawnPlacementMark(landX, markZ);
        }

        insertTween?.Kill();
        insertTween = root.DOMoveY(releaseY, liftDuration)
            .SetEase(Ease.InQuad)
            .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(FinishPlacement)
            .OnKill(FinishPlacement);
    }

    private void SpawnPlacementMark(float x, float z)
    {
        if (maxPlacementMarks <= 0) return;
        if (!TryGetMarkPoint(x, z, out Vector3 point, out Vector3 normal)) return;

        if (marksRoot == null)
            marksRoot = new GameObject("ShieldPlacementMarks").transform;

        if (markMaterial == null)
            markMaterial = ShieldGhost.CreateUnlitMaterial(new Color(0.28f, 0.85f, 0.42f, 0.12f));

        GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Collider col = mark.GetComponent<Collider>();
        if (col != null) Destroy(col);

        mark.name = "ShieldPlacementMark";
        mark.transform.SetParent(marksRoot, true);
        mark.transform.position = point + normal * 0.002f;
        mark.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0.5f ? normal : Vector3.up);
        mark.transform.localScale = new Vector3(shieldWidth * 0.6f, 0.001f, 0.22f);

        Renderer r = mark.GetComponent<Renderer>();
        if (r != null)
        {
            r.sharedMaterial = markMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        placementMarks.Enqueue(mark);
        while (placementMarks.Count > maxPlacementMarks)
        {
            GameObject old = placementMarks.Dequeue();
            if (old != null) Destroy(old);
        }
    }

    private bool TryGetMarkPoint(float x, float z, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;

        Vector3 origin = new Vector3(x, releaseY + 0.75f, z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 12f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool tableFound = false;
        Vector3 tablePoint = default, tableNormal = Vector3.up;
        bool tapeFound = false;
        Vector3 tapePoint = default;

        foreach (RaycastHit h in hits)
        {
            if (IsGameplayCollider(h.collider))
            {
                if (!tapeFound && h.collider.GetComponentInParent<TapePiece>() != null)
                {
                    tapeFound = true;
                    tapePoint = h.point;
                }
                continue;
            }

            if (!tableFound && h.point.y < releaseY - 0.05f)
            {
                tableFound = true;
                tablePoint = h.point;
                tableNormal = h.normal;
                break;
            }
        }

        if (tableFound)
        {
            point = tablePoint;
            normal = tableNormal;
            return true;
        }
        if (tapeFound)
        {
            point = tapePoint;
            normal = Vector3.up;
            return true;
        }
        return false;
    }

    private bool IsGameplayCollider(Collider col)
    {
        if (col == null) return false;
        if (col.GetComponentInParent<TapePiece>() != null) return true;
        if (col.GetComponentInParent<Axe>() != null) return true;
        if (FindTaggedRoot(col.transform) != null) return true;
        return false;
    }

    private void StartMoveLoop()
    {
        if (moveClip == null || audioSource == null) return;

        if (moveLoop == null)
        {
            var go = new GameObject("ShieldMoveLoop");
            go.transform.SetParent(transform, false);
            moveLoop = go.AddComponent<AudioSource>();
            moveLoop.playOnAwake = false;
            moveLoop.loop = true;
            moveLoop.spatialBlend = audioSource.spatialBlend;
        }

        moveLoop.clip = moveClip;
        moveLoop.volume = 0f;
        moveLoop.Play();
    }

    private void UpdateMoveLoopVolume(float speed)
    {
        if (moveLoop == null || !moveLoop.isPlaying) return;

        float spacing = layoutReady ? ShieldSegmentLayout.Spacing : 0.1f;
        float referenceSpeed = Mathf.Max(0.01f, ShieldSegmentSize * spacing / Mathf.Max(0.01f, ShieldMovingTime));
        moveLoop.volume = Mathf.Clamp01(speed / referenceSpeed);
    }

    private void StopMoveLoop()
    {
        if (moveLoop != null && moveLoop.isPlaying)
            moveLoop.Stop();
    }

    private void PlayRandomSoundFrom(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0) return;

        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip == null) continue;
            audioSource.PlayOneShot(clip);
            return;
        }
    }
}