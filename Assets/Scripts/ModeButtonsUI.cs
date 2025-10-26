using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ModeButtonsUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button classicBtn;
    [SerializeField] private TMP_Text classicLabel;
    [SerializeField] private Outline classicOutline;
    [SerializeField] private Button pvpBtn;
    [SerializeField] private TMP_Text pvpLabel;
    [SerializeField] private Outline pvpOutline;

    [Header("Styles")]
    [SerializeField] private Vector2 activeOutlineOffset = new(2f, -2f);
    [SerializeField] private Vector2 normalOutlineOffset = Vector2.zero;
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private Color activeTextColor = new(0.15f, 0.15f, 0.15f);
    [SerializeField] private Color normalTextColor = new(0.15f, 0.15f, 0.15f);
    [SerializeField] private float activeScale = 1.06f;
    [SerializeField] private float normalScale = 1f;

    public void HighlightClassic()
    {
        SetActive(classicBtn, classicLabel, classicOutline, true);
        SetActive(pvpBtn, pvpLabel, pvpOutline, false);
    }

    public void HighlightPvP()
    {
        SetActive(classicBtn, classicLabel, classicOutline, false);
        SetActive(pvpBtn, pvpLabel, pvpOutline, true);
    }

    private void SetActive(Button btn, TMP_Text label, Outline outline, bool active)
    {
        if (label != null)
        {
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            label.color = active ? activeTextColor : normalTextColor;
            label.rectTransform.localScale = Vector3.one * (active ? activeScale : normalScale);
            var mat = label.fontMaterial;
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, active ? 0.25f : 0f);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
        }

        if (outline != null)
        {
            outline.effectColor = outlineColor;
            outline.effectDistance = active ? activeOutlineOffset : normalOutlineOffset;
            outline.enabled = active;
        }

        if (btn != null)
            btn.interactable = !active;
    }
}
