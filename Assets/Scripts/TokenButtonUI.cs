using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TokenButtonUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Outline outline;

    [Header("Style")]
    [SerializeField] private Color textColor = new(0.15f, 0.15f, 0.15f);
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private Vector2 activeOutlineOffset = new(2f, -2f);
    [SerializeField] private Vector2 normalOutlineOffset = Vector2.zero;
    [SerializeField] private float activeScale = 1.06f;
    [SerializeField] private float normalScale = 1f;

    public void SetActive(bool active)
    {
        if (label != null)
        {
            label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            label.color = textColor;
            label.rectTransform.localScale = Vector3.one * (active ? activeScale : normalScale);
        }

        if (outline != null)
        {
            outline.effectColor = outlineColor;
            outline.effectDistance = active ? activeOutlineOffset : normalOutlineOffset;
            outline.enabled = active;
        }

        if (button != null)
            button.interactable = !active;
    }
}
