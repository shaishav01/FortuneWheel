using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Fortune wheel with a configurable number of slots.
/// Slot i is centered at i * (360 / slotCount) degrees, measured clockwise from the top.
/// The landed slot is detected from the wheel's final angle relative to the pointer.
/// </summary>
public class FortuneWheel : MonoBehaviour
{
    private readonly ProfilerMarker profilerMarker = new ProfilerMarker("Shaishav");

    public enum StopMode
    {
        Random,
        SpecificSlot
    }

    [Serializable]
    public class SlotEvent : UnityEvent<int> { }

    [Header("Slots")]
    [SerializeField, Range(2, 36)] private int slotCount = 8;
    [SerializeField] private FortuneWheelSlot slotPrefab;
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private Color[] slotColors =
    {
        new Color32(0xE5, 0x39, 0x35, 0xFF),
        new Color32(0xFB, 0x8C, 0x00, 0xFF),
        new Color32(0xFD, 0xD8, 0x35, 0xFF),
        new Color32(0x43, 0xA0, 0x47, 0xFF),
        new Color32(0x1E, 0x88, 0xE5, 0xFF),
        new Color32(0x8E, 0x24, 0xAA, 0xFF)
    };
    [Tooltip("Label distance from the center, as a fraction of the wheel radius.")]
    [SerializeField, Range(0.2f, 0.95f)] private float labelRadius = 0.7f;

    [Header("Rotation")]
    [Tooltip("Transform that rotates. Usually the parent of the slot container.")]
    [SerializeField] private RectTransform wheel;
    [Tooltip("Pointer angle in degrees, clockwise from the top (0 = pointer at the top).")]
    [SerializeField] private float pointerAngle = 0f;
    [SerializeField] private bool spinClockwise = true;
    [SerializeField, Min(0.1f)] private float spinDuration = 4f;
    [SerializeField, Min(0)] private int minFullRotations = 4;
    [SerializeField, Min(0)] private int maxFullRotations = 6;
    [Tooltip("Normalized progress (0-1) over normalized time (0-1). Default is ease-out cubic.")]
    [SerializeField] private AnimationCurve spinCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 3f, 3f),
        new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("Land at a random point inside the slot instead of its exact center.")]
    [SerializeField] private bool randomOffsetInSlot = true;
    [Tooltip("Keeps the landing point away from slot edges (fraction of half a slot).")]
    [SerializeField, Range(0f, 0.95f)] private float edgePadding = 0.3f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Stop Mode")]
    [SerializeField] private StopMode stopMode = StopMode.Random;
    [Tooltip("Used when Stop Mode is Specific Slot.")]
    [SerializeField, Min(0)] private int targetSlotIndex = 0;

    [Header("UI (optional)")]
    [SerializeField] private UnityEngine.UI.Button spinButton;
    [SerializeField] private UnityEngine.UI.Text resultLabel;

    [Header("Events")]
    public UnityEvent onSpinStarted;
    public SlotEvent onSpinCompleted;

    /// <summary>Raised with the detected slot index when the wheel stops.</summary>
    public event Action<int> SpinCompleted;

    [SerializeField, HideInInspector] private List<FortuneWheelSlot> slots = new List<FortuneWheelSlot>();

    private float currentAngle;   // UI Z rotation (counter-clockwise positive)
    private Coroutine spinRoutine;

    public bool IsSpinning => spinRoutine != null;
    public int SlotCount => slotCount;
    public int LastResultIndex { get; private set; } = -1;
    public float SlotAngle => 360f / slotCount;

    public StopMode Mode
    {
        get => stopMode;
        set => stopMode = value;
    }

    public int TargetSlotIndex
    {
        get => targetSlotIndex;
        set => targetSlotIndex = Mathf.Clamp(value, 0, slotCount - 1);
    }

    private void Awake()
    {
        if (wheel == null) wheel = (RectTransform)transform;
        currentAngle = wheel.localEulerAngles.z;

        if (slots.Count != slotCount || slots.Exists(s => s == null))
            RebuildSlots();
    }

    private void OnEnable()
    {
        if (spinButton != null) spinButton.onClick.AddListener(Spin);
    }

    private void OnDisable()
    {
        if (spinButton != null) spinButton.onClick.RemoveListener(Spin);
        if (spinRoutine != null)
        {
            StopCoroutine(spinRoutine);
            spinRoutine = null;
            SetButtonInteractable(true);
        }
    }

    /// <summary>Changes the slot count at runtime and rebuilds the wheel.</summary>
    public void SetSlotCount(int count)
    {
        if (IsSpinning) return;
        slotCount = Mathf.Max(2, count);
        targetSlotIndex = Mathf.Clamp(targetSlotIndex, 0, slotCount - 1);
        RebuildSlots();
    }

    /// <summary>Spins using the configured Stop Mode.</summary>
    public void Spin()
    {
        int target = stopMode == StopMode.SpecificSlot
            ? Mathf.Clamp(targetSlotIndex, 0, slotCount - 1)
            : UnityEngine.Random.Range(0, slotCount);
        SpinTo(target);
    }

    /// <summary>Spins and stops on the given slot, regardless of Stop Mode.</summary>
    /// 
    public void SpinTo(int slotIndex)
    {
        if (IsSpinning || wheel == null) return;
        profilerMarker.Begin();
        slotIndex = Mathf.Clamp(slotIndex, 0, slotCount - 1);
        spinRoutine = StartCoroutine(SpinRoutine(slotIndex));
        profilerMarker.End();
    }

    /// <summary>Slot currently under the pointer, computed from the wheel angle.</summary>
    public int GetSlotIndexAtPointer()
    {
        // Slot i sits at (angle - i * slotAngle) CCW from the top, so the slot under a
        // pointer at P (CW from top) satisfies angle - i * slotAngle = -P.
        float relative = Mathf.Repeat(currentAngle + pointerAngle, 360f);
        int index = Mathf.FloorToInt(relative / SlotAngle + 0.5f);
        return index % slotCount;
    }

    [ContextMenu("Rebuild Slots")]
    public void RebuildSlots()
    {
        if (slotPrefab == null || slotContainer == null)
        {
            Debug.LogWarning("[FortuneWheel] Assign Slot Prefab and Slot Container.", this);
            return;
        }

        slots.RemoveAll(s => s == null);

        while (slots.Count < slotCount)
            slots.Add(Instantiate(slotPrefab, slotContainer, false));

        for (int i = slots.Count - 1; i >= slotCount; i--) //what this logic do? why? 
        // This loop removes any extra slots that are beyond the desired slot count.
        // It starts from the last slot and goes backwards to avoid index issues while removing elements.
        {
            DestroySafe(slots[i].gameObject);
            slots.RemoveAt(i);
        }

        float slotAngle = SlotAngle;
        float radius = Mathf.Min(slotContainer.rect.width, slotContainer.rect.height) * 0.5f;
        float labelDistance = radius * labelRadius;

        for (int i = 0; i < slotCount; i++)
        {
            Color color = slotColors != null && slotColors.Length > 0
                ? slotColors[GetColorIndex(i)]
                : Color.white;
            slots[i].Setup(i, slotAngle, color, labelDistance);
        }
    }

    private IEnumerator SpinRoutine(int target)
    {
        SetButtonInteractable(false);
        if (resultLabel != null) resultLabel.text = "Spinning...";
        onSpinStarted?.Invoke();

        float slotAngle = SlotAngle;
        // Explain offset calculation
        // If randomOffsetInSlot is true, we calculate a random offset within the slot to make the landing point less predictable.
        // The offset is scaled by slotAngle * 0.5 and reduced by edgePadding to avoid landing too close to the slot edges.
        //What will be the oddset value for slot target 5?
        float offset = randomOffsetInSlot
            ? UnityEngine.Random.Range(-1f, 1f) * slotAngle * 0.5f * (1f - edgePadding)
            : 0f;

        // Wheel angle (CCW) that puts the target's landing point under the pointer.
        float finalAngle = target * slotAngle + offset - pointerAngle;
        int rotations = UnityEngine.Random.Range(
            Mathf.Min(minFullRotations, maxFullRotations),
            Mathf.Max(minFullRotations, maxFullRotations) + 1);

        float startAngle = currentAngle;
        float travel = spinClockwise
            ? -(rotations * 360f + Mathf.Repeat(startAngle - finalAngle, 360f))
            : rotations * 360f + Mathf.Repeat(finalAngle - startAngle, 360f);

        float elapsed = 0f;
        while (elapsed < spinDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / spinDuration);
            float progress = spinCurve != null && spinCurve.length > 1
                ? spinCurve.Evaluate(t)
                : 1f - (1f - t) * (1f - t) * (1f - t);
            ApplyAngle(startAngle + travel * progress);
            yield return null;
        }

        // Snap exactly to the end and keep the angle bounded.
        ApplyAngle(Mathf.Repeat(startAngle + travel, 360f));
        spinRoutine = null;

        LastResultIndex = GetSlotIndexAtPointer();
        if (LastResultIndex != target)
            Debug.LogWarning($"[FortuneWheel] Detected slot {LastResultIndex} but targeted {target}.", this);

        if (resultLabel != null) resultLabel.text = "Slot " + LastResultIndex;
        SetButtonInteractable(true);

        onSpinCompleted?.Invoke(LastResultIndex);
        SpinCompleted?.Invoke(LastResultIndex);
    }

    private void ApplyAngle(float angle)
    {
        currentAngle = angle;
        wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private int GetColorIndex(int slotIndex)
    {
        int colorCount = slotColors.Length;
        int index = slotIndex % colorCount;
        // Avoid the first and last wedge sharing a color when the count doesn't divide evenly.
        if (slotIndex == slotCount - 1 && colorCount > 1 && index == 0)
            index = 1;
        return index;
    }

    private void SetButtonInteractable(bool value)
    {
        if (spinButton != null) spinButton.interactable = value;
    }

    private static void DestroySafe(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        targetSlotIndex = Mathf.Clamp(targetSlotIndex, 0, slotCount - 1);

        // Live preview when Slot Count changes in the Inspector (scene or prefab mode).
        if (Application.isPlaying || slots.Count == slotCount) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
            RebuildSlots();
        };
    }
#endif
}
