using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PointsUI : MonoBehaviour
{
    [Header("UI References")]
    public Text pointsText;  // Legacy UI Text
    public TextMeshProUGUI pointsTMPText;  // TextMeshPro Text
    
    [Header("UI Settings")]
    public string pointsPrefix = "Points: ";
    public bool hideUIWhenZeroPoints = false;
    
    private int currentDisplayedPoints = -1;
    
    private void Start()
    {
        // Subscribe to points changes
        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged += UpdatePointsDisplay;
            // Update immediately with current points
            UpdatePointsDisplay(PointsManager.Instance.GetPoints());
        }
        else
        {
            // If PointsManager isn't ready yet, try again later
            Invoke(nameof(TrySubscribeAgain), 1f);
        }
    }
    
    private void TrySubscribeAgain()
    {
        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged += UpdatePointsDisplay;
            UpdatePointsDisplay(PointsManager.Instance.GetPoints());
        }
    }
    
    private void UpdatePointsDisplay(int points)
    {
        // Only update if points changed (performance optimization)
        if (currentDisplayedPoints == points) return;
        
        currentDisplayedPoints = points;
        string displayText = pointsPrefix + points.ToString();
        
        // Update Legacy UI Text if available
        if (pointsText != null)
        {
            pointsText.text = displayText;
            
            if (hideUIWhenZeroPoints)
            {
                pointsText.gameObject.SetActive(points > 0);
            }
        }
        
        // Update TextMeshPro Text if available
        if (pointsTMPText != null)
        {
            pointsTMPText.text = displayText;
            
            if (hideUIWhenZeroPoints)
            {
                pointsTMPText.gameObject.SetActive(points > 0);
            }
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.OnPointsChanged -= UpdatePointsDisplay;
        }
    }
    
    /// <summary>
    /// Manual update for testing
    /// </summary>
    [ContextMenu("Update Display")]
    public void ManualUpdateDisplay()
    {
        if (PointsManager.Instance != null)
        {
            UpdatePointsDisplay(PointsManager.Instance.GetPoints());
        }
    }
}