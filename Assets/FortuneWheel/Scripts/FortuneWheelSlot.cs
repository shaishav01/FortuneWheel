using UnityEngine;
using Unity.Profiling;
/// <summary>
/// One wedge of the fortune wheel. The wedge is a radial-filled circle sprite, so a single
/// sprite works for any slot count. The label sits on the wedge's center line.
/// </summary>
public class FortuneWheelSlot : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image segment;
    [SerializeField] private UnityEngine.UI.Text label;

    private readonly ProfilerMarker profilerMarker = new ProfilerMarker("FortuneWheelSlot");

    public int Index { get; private set; }

    /// <param name="index">Slot index (0 = slot at angle 0, increasing clockwise).</param>
    /// <param name="slotAngle">Angle covered by one slot, in degrees.</param>
    /// <param name="labelDistance">Label distance from the wheel center, in local units.</param>
    public void Setup(int index, float slotAngle, Color color, float labelDistance)
    {
        profilerMarker.Begin();
        Index = index;
        gameObject.name = "Slot_" + index;

        // Slot root points along its center line (clockwise = negative Z in UI space).
        RectTransform rt = (RectTransform)transform;
        rt.localRotation = Quaternion.Euler(0f, 0f, -index * slotAngle);

        if (segment != null)
        {
            segment.type = UnityEngine.UI.Image.Type.Filled;
            segment.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            segment.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            segment.fillClockwise = true;
            segment.fillAmount = slotAngle / 360f;
            segment.color = color;
            // Fill starts at the top and runs clockwise; shift back by half a slot to center it.
            segment.rectTransform.localRotation = Quaternion.Euler(0f, 0f, slotAngle * 0.5f);
        }

        if (label != null)
        {
            label.text = index.ToString();
            label.rectTransform.anchoredPosition = new Vector2(0f, labelDistance);
        }
        profilerMarker.End();
    }
}
