
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
    [SerializeField] private TapePiece[] playerTapes;
    [SerializeField] private TapePiece[] enemyTapes;
    [SerializeField] private Transform playerShieldRoot;
    [SerializeField] private Transform enemyShieldRoot;

    [Header("X Clamp")]
    [SerializeField] private float xMin = -5f;
    [SerializeField] private float xMax = 5f;

    [Header("Take / Insert animations")]
    [SerializeField] private float hoverHeight = 1f;
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
    private float _baseY;

    private bool layoutReady;
    private int appliedSegmentSize = -1;

    private readonly Queue<GameObject> placementMarks = new Queue<GameObject>();

    private Transform marksRoot;
    private static Material markMaterial;

    private List<Transform> playerShields = new List<Transform>();
    private List<Transform> enemyShields = new List<Transform>();

    public void HideAbove()
    {
        Transform tr = transform;
        tr.DOKill();

        tr.position = new Vector3(
            tr.position.x,
            _baseY + externalHideOffsetY,
            tr.position.z
        );
    }

    public Tween DropFromTop()
    {
        Transform tr = transform;
        tr.DOKill();

        tr.position = new Vector3(
            tr.position.x,
            _baseY + externalHideOffsetY,
            tr.position.z
        );

        return tr.DOMoveY(_baseY, externalDropDuration)
            .SetEase(externalDropEase);
    }

    private void Awake()
    {
        if (cam == null)
            cam = Camera.main;

        ShieldSegmentLayout.Build(playerTapes, enemyTapes);

        playerShields = GetDirectChildren(playerShieldRoot);
        enemyShields = GetDirectChildren(enemyShieldRoot);

        layoutReady = ShieldSegmentLayout.PlayerCount > 0;

        if (layoutReady)
        {
            ShieldSegmentSize = Mathf.Clamp(
                ShieldSegmentSize,
                1,
                ShieldSegmentLayout.PlayerCount
            );

            shieldWidth =
                ShieldSegmentSize * ShieldSegmentLayout.PlayerSpacing;

            ResizeShields(
                playerShields,
                shieldWidth
            );

            SnapAllShieldsIntoSlots();

            appliedSegmentSize = ShieldSegmentSize;
        }
        else
        {
            ShieldSegmentSize = Mathf.Max(1, ShieldSegmentSize);
            shieldWidth = ShieldSegmentSize * 0.1f;
            appliedSegmentSize = ShieldSegmentSize;

            Debug.LogWarning(
                "[ShieldController] Player Tapes не назначены."
            );
        }

        if (ShieldSegmentLayout.EnemyCount > 0)
        {
            int enemySize = Mathf.Clamp(
                ShieldSegmentSize,
                1,
                ShieldSegmentLayout.EnemyCount
            );

            float enemyWidth =
                enemySize * ShieldSegmentLayout.EnemySpacing;

            ResizeShields(
                enemyShields,
                enemyWidth
            );
        }

        if (playerShieldRoot == null)
            Debug.LogWarning("[ShieldController] Player Shield Root не назначен.");

        if (enemyShieldRoot == null)
            Debug.LogWarning("[ShieldController] Enemy Shield Root не назначен.");
    }

    private void Start()
    {
        if (cam == null)
            cam = Camera.main;

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

        liftTween?.Kill();
        liftTween = null;

        moveTween?.Kill();
        moveTween = null;

        insertTween?.Kill();
        insertTween = null;

        if (draggedRoot != null)
        {
            ApplyTilt(0f);
            draggedRoot = null;
            draggedVisual = null;
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

    private List<Transform> GetDirectChildren(Transform root)
    {
        List<Transform> result = new List<Transform>();

        if (root == null)
            return result;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child != null)
                result.Add(child);
        }

        return result;
    }

    private Transform GetPlayerShieldRoot(Transform hitTransform)
    {
        if (hitTransform == null || playerShieldRoot == null)
            return null;

        Transform current = hitTransform;

        while (current != null && current.parent != playerShieldRoot)
            current = current.parent;

        if (current != null && current.parent == playerShieldRoot)
            return current;

        return null;
    }

    private bool IsPlayerShield(Transform root)
    {
        return root != null &&
               playerShieldRoot != null &&
               root.parent == playerShieldRoot;
    }

    protected override bool IsPointerOver(RaycastHit hit)
    {
        return IsPlayerShield(
            GetPlayerShieldRoot(hit.transform)
        );
    }

    protected override Outline FindHighlightOutline(RaycastHit hit)
    {
        Transform root = GetPlayerShieldRoot(hit.transform);

        if (!IsPlayerShield(root))
            return null;

        return root.GetComponentInChildren<Outline>(true);
    }

    private void ResizeShields(List<Transform> shields, float targetWidth)
    {
        foreach (Transform shield in shields)
        {
            if (shield != null)
                ResizeShield(shield, targetWidth);
        }
    }

    private void ResizeShield(Transform root, float targetWidth)
    {
        BoxCollider body = root.GetComponentInChildren<BoxCollider>(true);

        if (body == null)
        {
            Debug.LogWarning(
                $"[ShieldController] На щите '{root.name}' нет BoxCollider."
            );
            return;
        }

        bool wasEnabled = body.enabled;
        body.enabled = true;

        Physics.SyncTransforms();

        float current = body.bounds.size.x;

        if (current <= 1e-5f)
        {
            body.enabled = wasEnabled;
            return;
        }

        Vector3 originalScale = root.localScale;

        int bestAxis = -1;
        float bestError = Mathf.Abs(current - targetWidth);

        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(originalScale[axis]) < 1e-6f)
                continue;

            Vector3 testScale = originalScale;
            testScale[axis] *= targetWidth / current;

            root.localScale = testScale;

            Physics.SyncTransforms();

            float error = Mathf.Abs(
                body.bounds.size.x - targetWidth
            );

            if (error < bestError)
            {
                bestError = error;
                bestAxis = axis;
            }

            root.localScale = originalScale;
            Physics.SyncTransforms();
        }

        if (bestAxis < 0)
        {
            body.enabled = wasEnabled;

            Debug.LogWarning(
                $"[ShieldController] Не удалось изменить размер щита '{root.name}'."
            );
            return;
        }

        root.localScale = originalScale;

        for (int i = 0; i < 8; i++)
        {
            Physics.SyncTransforms();

            float measured = body.bounds.size.x;

            if (measured <= 1e-5f)
                break;

            Vector3 scale = root.localScale;

            scale[bestAxis] *= targetWidth / measured;

            root.localScale = scale;
        }

        Physics.SyncTransforms();

        float finalWidth = body.bounds.size.x;

        body.enabled = wasEnabled;

        if (Mathf.Abs(finalWidth - targetWidth) > targetWidth * 0.01f)
        {
            Debug.LogWarning(
                $"[ShieldController] Щит '{root.name}': " +
                $"ширина {finalWidth:0.###}, требуется {targetWidth:0.###}."
            );
        }
    }

    private void ApplySegmentSizeIfChanged()
    {
        if (!layoutReady ||
            ShieldSegmentSize == appliedSegmentSize ||
            draggedRoot != null)
            return;

        ShieldSegmentSize = Mathf.Clamp(
            ShieldSegmentSize,
            1,
            ShieldSegmentLayout.PlayerCount
        );

        appliedSegmentSize = ShieldSegmentSize;

        shieldWidth =
            ShieldSegmentSize * ShieldSegmentLayout.PlayerSpacing;

        ResizeShields(playerShields, shieldWidth);

        SnapAllShieldsIntoSlots();

        if (ShieldSegmentLayout.EnemyCount > 0)
        {
            int enemySize = Mathf.Clamp(
                ShieldSegmentSize,
                1,
                ShieldSegmentLayout.EnemyCount
            );

            float enemyWidth =
                enemySize * ShieldSegmentLayout.EnemySpacing;

            ResizeShields(
                enemyShields,
                enemyWidth
            );
        }
    }

    private void SnapAllShieldsIntoSlots()
    {
        if (!layoutReady || playerShields.Count == 0)
            return;

        int size = ShieldSegmentSize;
        int maxFirst = ShieldSegmentLayout.PlayerCount - size;

        if (playerShields.Count * size > ShieldSegmentLayout.PlayerCount)
        {
            Debug.LogWarning(
                "[ShieldController] Недостаточно сегментов для всех щитов игрока."
            );
            return;
        }

        playerShields.Sort(
            (a, b) => a.position.x.CompareTo(b.position.x)
        );

        int nextFree = 0;

        foreach (Transform root in playerShields)
        {
            if (root == null)
                continue;

            int first = NearestFirstIndex(
                root.position.x,
                size,
                nextFree
            );

            Vector3 position = root.position;

            position.x = ShieldSegmentLayout.SlotCenter(first, size);

            root.position = position;

            nextFree = first + size;

            if (nextFree > maxFirst + size)
                break;
        }
    }

    private int NearestFirstIndex(float worldX, int size, int minFirst)
    {
        int maxFirst = ShieldSegmentLayout.PlayerCount - size;

        int start = Mathf.Clamp(
            minFirst,
            0,
            maxFirst
        );

        int best = start;
        float bestDistance = float.MaxValue;

        for (int first = start; first <= maxFirst; first++)
        {
            float distance = Mathf.Abs(
                ShieldSegmentLayout.SlotCenter(first, size) - worldX
            );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = first;
            }
        }

        return best;
    }

    private void TryPick()
    {
        if (!isInteractive || cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            Transform root = GetPlayerShieldRoot(hit.transform);

            if (!IsPlayerShield(root))
                continue;

            BeginDrag(root);
            return;
        }
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

        draggedVisual = body != null
            ? body.transform
            : (root.childCount > 0 ? root.GetChild(0) : root);

        visualRestLocalPos = draggedVisual.localPosition;
        visualRestLocalRot = draggedVisual.localRotation;

        tiltPivotLocal = ComputeTiltPivotLocal(root, body);

        ghost = ShieldGhost.Create(root, cam);

        if (ghost != null)
            ghost.SetWorldX(root.position.x);

        liftTween?.Kill();

        liftTween = root.DOMoveY(
            releaseY + hoverHeight,
            liftDuration
        )
        .SetEase(liftEase)
        .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);

        PlayRandomSoundFrom(takeClip);
        StartMoveLoop();
    }

    private static Vector3 ComputeTiltPivotLocal(
        Transform root,
        Collider body)
    {
        if (body == null)
            return new Vector3(0f, 0.5f, 0f);

        Bounds bounds = body.bounds;

        Vector3 topCenterWorld = new Vector3(
            bounds.center.x,
            bounds.max.y,
            bounds.center.z
        );

        return root.InverseTransformPoint(topCenterWorld);
    }

    private void UpdateDrag()
    {
        if (draggedRoot == null)
            return;

        float mouseWorldX = MouseWorldXOnPlane(
            draggedRoot.position.z
        );

        float targetX;

        if (layoutReady)
        {
            ShieldSegmentLayout.GetClampRange(
                ShieldSegmentSize,
                out float minCenter,
                out float maxCenter
            );

            float clamped = Mathf.Clamp(
                mouseWorldX,
                minCenter,
                maxCenter
            );

            int first = ShieldSegmentLayout.SnapFirstIndex(
                clamped,
                ShieldSegmentSize
            );

            targetX = ShieldSegmentLayout.SlotCenter(
                first,
                ShieldSegmentSize
            );

            if (ghost != null)
                ghost.SetSegmentRange(first, ShieldSegmentSize);
        }
        else
        {
            targetX = Mathf.Clamp(mouseWorldX, xMin, xMax);
        }

        if (ghost != null)
            ghost.SetWorldX(targetX);

        if (!hasTargetX || !Mathf.Approximately(targetX, ghostTargetX))
        {
            hasTargetX = true;
            ghostTargetX = targetX;

            moveTween?.Kill();

            moveTween = draggedRoot.DOMoveX(
                targetX,
                Mathf.Max(0.01f, ShieldMovingTime)
            )
            .SetEase(Ease.OutQuad)
            .SetLink(draggedRoot.gameObject, LinkBehaviour.KillOnDestroy);
        }

        float remaining = ghostTargetX - draggedRoot.position.x;

        float spacing = layoutReady
            ? ShieldSegmentLayout.PlayerSpacing
            : 0.1f;

        float reference = Mathf.Max(
            0.01f,
            ShieldSegmentSize * spacing
        );

        float angle =
            -Mathf.Sign(remaining) *
            ShieldTiltAngleMax *
            Mathf.Clamp01(Mathf.Abs(remaining) / reference);

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

        if (Mathf.Abs(ray.direction.z) < 1e-5f)
            return draggedRoot.position.x;

        float t = (planeZ - ray.origin.z) / ray.direction.z;

        if (t <= 0f)
            return draggedRoot.position.x;

        return ray.origin.x + ray.direction.x * t;
    }

    private void ApplyTilt(float angleDegrees)
    {
        if (draggedVisual == null || draggedRoot == null)
            return;

        draggedVisual.localPosition = visualRestLocalPos;
        draggedVisual.localRotation = visualRestLocalRot;

        if (Mathf.Abs(angleDegrees) < 0.01f)
            return;

        Quaternion rotation = Quaternion.AngleAxis(
            angleDegrees,
            Vector3.forward
        );

        Vector3 pivotWorld = draggedRoot.TransformPoint(tiltPivotLocal);

        draggedVisual.position =
            rotation * (draggedVisual.position - pivotWorld) + pivotWorld;

        draggedVisual.rotation =
            rotation * draggedVisual.rotation;
    }

    private void ReleaseObject()
    {
        if (draggedRoot == null)
            return;

        Transform root = draggedRoot;

        Vector3 position = root.position;

        float landX = hasTargetX
            ? ghostTargetX
            : position.x;

        float markZ = position.z;

        moveTween?.Kill();
        moveTween = null;

        liftTween?.Kill();
        liftTween = null;

        root.position = new Vector3(
            landX,
            position.y,
            position.z
        );

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
            if (finished)
                return;

            finished = true;
            insertTween = null;

            isInteractive = true;

            PlayRandomSoundFrom(stackClip);
            SpawnPlacementMark(landX, markZ);
        }

        insertTween?.Kill();

        insertTween = root.DOMoveY(
            releaseY,
            liftDuration
        )
        .SetEase(Ease.InQuad)
        .SetLink(root.gameObject, LinkBehaviour.KillOnDestroy)
        .OnComplete(FinishPlacement)
        .OnKill(FinishPlacement);
    }

    private void SpawnPlacementMark(float x, float z)
    {
        if (maxPlacementMarks <= 0)
            return;

        if (!TryGetMarkPoint(
            x,
            z,
            out Vector3 point,
            out Vector3 normal))
            return;

        if (marksRoot == null)
            marksRoot = new GameObject("ShieldPlacementMarks").transform;

        if (markMaterial == null)
        {
            markMaterial = ShieldGhost.CreateUnlitMaterial(
                new Color(0.28f, 0.85f, 0.42f, 0.12f)
            );
        }

        GameObject mark = GameObject.CreatePrimitive(
            PrimitiveType.Cylinder
        );

        Collider collider = mark.GetComponent<Collider>();

        if (collider != null)
            Destroy(collider);

        mark.name = "ShieldPlacementMark";
        mark.transform.SetParent(marksRoot, true);
        mark.transform.position = point + normal * 0.002f;

        mark.transform.rotation = Quaternion.FromToRotation(
            Vector3.up,
            normal.sqrMagnitude > 0.5f ? normal : Vector3.up
        );

        mark.transform.localScale = new Vector3(
            shieldWidth * 0.6f,
            0.001f,
            0.22f
        );

        Renderer renderer = mark.GetComponent<Renderer>();

        if (renderer != null)
        {
            renderer.sharedMaterial = markMaterial;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        placementMarks.Enqueue(mark);

        while (placementMarks.Count > maxPlacementMarks)
        {
            GameObject old = placementMarks.Dequeue();

            if (old != null)
                Destroy(old);
        }
    }

    private bool TryGetMarkPoint(
        float x,
        float z,
        out Vector3 point,
        out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;

        Vector3 origin = new Vector3(
            x,
            releaseY + 0.75f,
            z
        );

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            12f
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        bool tableFound = false;
        Vector3 tablePoint = default;
        Vector3 tableNormal = Vector3.up;

        bool tapeFound = false;
        Vector3 tapePoint = default;

        foreach (RaycastHit hit in hits)
        {
            if (IsGameplayCollider(hit.collider))
            {
                if (!tapeFound &&
                    hit.collider.GetComponentInParent<TapePiece>() != null)
                {
                    tapeFound = true;
                    tapePoint = hit.point;
                }

                continue;
            }

            if (!tableFound && hit.point.y < releaseY - 0.05f)
            {
                tableFound = true;
                tablePoint = hit.point;
                tableNormal = hit.normal;
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

    private bool IsGameplayCollider(Collider collider)
    {
        if (collider == null)
            return false;

        if (collider.GetComponentInParent<TapePiece>() != null)
            return true;

        if (collider.GetComponentInParent<Axe>() != null)
            return true;

        if (GetPlayerShieldRoot(collider.transform) != null)
            return true;

        return false;
    }

    private void StartMoveLoop()
    {
        if (moveClip == null || audioSource == null)
            return;

        if (moveLoop == null)
        {
            GameObject go = new GameObject("ShieldMoveLoop");

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
        if (moveLoop == null || !moveLoop.isPlaying)
            return;

        float spacing = layoutReady
            ? ShieldSegmentLayout.PlayerSpacing
            : 0.1f;

        float referenceSpeed = Mathf.Max(
            0.01f,
            ShieldSegmentSize * spacing /
            Mathf.Max(0.01f, ShieldMovingTime)
        );

        moveLoop.volume = Mathf.Clamp01(
            speed / referenceSpeed
        );
    }

    private void StopMoveLoop()
    {
        if (moveLoop != null && moveLoop.isPlaying)
            moveLoop.Stop();
    }

    private void PlayRandomSoundFrom(AudioClip[] clips)
    {
        if (audioSource == null || clips == null || clips.Length == 0)
            return;

        int start = Random.Range(0, clips.Length);

        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];

            if (clip == null)
                continue;

            audioSource.PlayOneShot(clip);
            return;
        }
    }
}
