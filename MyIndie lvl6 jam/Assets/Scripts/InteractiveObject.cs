using UnityEngine;

public class InteractiveObject : MonoBehaviour
{
    [Header("Interactive Object")]
    [Tooltip("Можно ли с данным объектом взаимодействовать")]
    public bool isInteractive = true;

    [Tooltip("Ширина контура выделения (QuickOutline, 0..10)")]
    public float highlightWidth = 2f;

    [Tooltip("Цвет выделения контура")]
    public Color highlightColor = new Color(1f, 0.8287672f, 0f, 1f);

    protected Camera highlightCamera;

    private Outline currentOutline;

    protected virtual void Update()
    {
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        Outline target = null;

        if (isInteractive)
        {
            if (highlightCamera == null) highlightCamera = Camera.main;

            if (highlightCamera != null)
            {
                Ray ray = highlightCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 100f) && IsPointerOver(hit))
                    target = FindHighlightOutline(hit);
            }
        }

        if (currentOutline == target) return;

        if (currentOutline != null) currentOutline.enabled = false;
        currentOutline = target;

        if (target != null)
        {
            target.OutlineWidth = highlightWidth;
            target.OutlineColor = highlightColor;
            target.enabled = true;
        }
    }

    protected virtual bool IsPointerOver(RaycastHit hit) =>
        hit.transform != null && hit.transform.IsChildOf(transform);

    protected virtual Outline FindHighlightOutline(RaycastHit hit) =>
        GetComponentInChildren<Outline>(true);

    public void SetInteractive(bool value)
    {
        isInteractive = value;
        if (!value) ClearHighlight();
    }

    private void ClearHighlight()
    {
        if (currentOutline != null) currentOutline.enabled = false;
        currentOutline = null;
    }

    protected virtual void OnDisable()
    {
        ClearHighlight();
    }
}
