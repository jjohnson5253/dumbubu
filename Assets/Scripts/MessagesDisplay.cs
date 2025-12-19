using UnityEngine;
using TMPro;
using System.Collections;

public class MessagesDisplay : MonoBehaviour
{
    public static MessagesDisplay Instance { get; private set; }
    
    [Header("References")]
    public TextMeshProUGUI messageText; // Assign in Inspector
    
    [Header("Display Settings")]
    public float messageDisplayTime = 3f;
    public float yOffsetFromDumbubu = 1.5f;
    
    private Coroutine hideCoroutine;
    private static bool hasShownStartMessage = false;
    private GameObject dumbubu;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        dumbubu = GameObject.Find("Dumbubu");
        
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
        
        // Show startup message
        ShowStartupMessage();
    }
    
    private void Update()
    {
        // Position text above Dumbubu every frame
        if (messageText != null && messageText.gameObject.activeSelf && dumbubu != null)
        {
            Vector3 position = dumbubu.transform.position + Vector3.up * yOffsetFromDumbubu;
            messageText.transform.position = Camera.main.WorldToScreenPoint(position);
        }
    }
    
    private void ShowStartupMessage()
    {
        if (hasShownStartMessage) return;
        
        if (dumbubu != null)
        {
            ShowMessage("Click and drag to throw. Right click for menu.");
            hasShownStartMessage = true;
        }
    }
    
    public void ShowGrenadeMessage()
    {
        ShowMessage("Click anywhere to drop. `Esc` to exit.");
    }
    
    public void ShowMessage(string message)
    {
        if (messageText == null)
        {
            Debug.LogWarning("MessageText not assigned!");
            return;
        }
        
        messageText.text = message;
        messageText.gameObject.SetActive(true);
        
        // Hide after delay
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }
        hideCoroutine = StartCoroutine(HideMessageAfterDelay());
    }
    
    private IEnumerator HideMessageAfterDelay()
    {
        yield return new WaitForSeconds(messageDisplayTime);
        
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
        
        hideCoroutine = null;
    }
    
    public static bool IsDisplaying()
    {
        return Instance != null && Instance.messageText != null && Instance.messageText.gameObject.activeSelf;
    }
}
