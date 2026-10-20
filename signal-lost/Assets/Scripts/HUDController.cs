using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Reference to the Health Bar Slider")]
    public Slider healthSlider;

    [Tooltip("Reference to the Scrap Count TextMeshPro object")]
    public TextMeshProUGUI scrapText;

    [Tooltip("Reference to the Objective TextMeshPro object")]
    public TextMeshProUGUI objectiveText;

    [Tooltip("Reference to the Scanner UI Element (e.g., central reticle/compass)")]
    public Image scannerIndicator;

    [Header("Debug / Default Values")]
    [SerializeField] private int currentScrap = 0;

    void Start()
    {
        // Initialize default UI display
        UpdateScrapUI(currentScrap);
        SetObjective("Explore the area and scan for scrap.");
    }

    /// <summary>
    /// Updates the health bar slider max value and current health value.
    /// Call this from the Player Health script.
    /// </summary>
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    /// <summary>
    /// Updates the displayed scrap count text.
    /// Call this when scrap is picked up or spent.
    /// </summary>
    public void UpdateScrapUI(int amount)
    {
        currentScrap = amount;
        if (scrapText != null)
        {
            scrapText.text = $"Scrap: {currentScrap}";
        }
    }

    /// <summary>
    /// Updates the main objective tracker text on screen.
    /// </summary>
    public void SetObjective(string newObjective)
    {
        if (objectiveText != null)
        {
            objectiveText.text = string.IsNullOrWhiteSpace(newObjective) ? "" : $"Objective: {newObjective}";
        }
    }

    /// <summary>
    /// Updates scanner reticle color or opacity based on signal proximity.
    /// </summary>
    public void SetScannerSignalStrength(float normalizedStrength)
    {
        if (scannerIndicator != null)
        {
            // Adjust opacity/color to visually show signal strength
            Color color = scannerIndicator.color;
            color.a = Mathf.Clamp01(normalizedStrength);
            scannerIndicator.color = color;
        }
    }
}
