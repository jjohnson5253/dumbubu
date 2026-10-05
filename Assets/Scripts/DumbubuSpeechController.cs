using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dumbubu.ChatGPT;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DumbubuSpeechController : MonoBehaviour
{
    private const float SpeechInterval = 30f;
    private ChatGptClient client;
    private Transform pet;
    private Renderer petRenderer;
    private Rigidbody2D petBody;
    private string skill;
    private string selectedClient;
    private readonly Queue<string> recentLines = new Queue<string>();
    private CancellationTokenSource operation;
    private Button connectButton, accountButton, speechButton, testButton;
    private TMP_Text titleLabel, connectLabel, accountLabel, speechLabel, statusLabel, bubbleText;
    private GameObject bubbleCanvas;
    private RectTransform bubble;
    private bool busy, speaking, muted, shuttingDown, speechPaused, pendingBubble;
    private float nextSpeech, visibleUntil, bubbleDuration;
    private int failures;

    public void Initialize(MenuDisplay menu, Transform dumbubu)
    {
        pet = dumbubu;
        petRenderer = pet.GetComponentInChildren<Renderer>();
        petBody = pet.GetComponent<Rigidbody2D>();
        muted = PlayerPrefs.GetInt("DumbubuSpeechMuted", 0) == 1;
        CreateMenuControls(menu);
        CreateBubble();
        try
        {
            skill = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "ChatGPT", "SKILL.md"));
            if (string.IsNullOrWhiteSpace(skill)) throw new IOException();
            client = new ChatGptClient(Path.Combine(Application.persistentDataPath, "ChatGPT"));
            selectedClient = client.Registrations.ActiveClientId;
            statusLabel.text = client.Connected ? "Connected. Dumbubu speaks every 30 seconds." : "Connect to hear Dumbubu's thoughts. Uses your ChatGPT plan.";
        }
        catch (Exception)
        {
            statusLabel.text = "ChatGPT setup could not be loaded. Check the skill file and local account storage.";
        }
        nextSpeech = Time.unscaledTime + (client != null && client.Connected ? 1f : SpeechInterval);
        RefreshControls();
    }

    private void CreateMenuControls(MenuDisplay menu)
    {
        var panel = menu.menuPanel.GetComponent<RectTransform>();
        // Make room below the existing controls without changing their relative layout.
        foreach (RectTransform child in panel) child.anchoredPosition += Vector2.up * 165f;
        panel.sizeDelta += Vector2.up * 330f;
        titleLabel = Label(panel, "Dumbubu's thoughts", new Vector2(0, -24), new Vector2(310, 28), 19);
        connectButton = MakeButton(panel, "Continue with ChatGPT", -60, out connectLabel);
        accountButton = MakeButton(panel, "Account: new ChatGPT account", -99, out accountLabel);
        speechButton = MakeButton(panel, "Speech: on", -138, out speechLabel);
        TMP_Text unused;
        testButton = MakeButton(panel, "Test speech now", -177, out unused);
        var usageButton = MakeButton(panel, "ChatGPT Usage settings", -216, out unused);
        statusLabel = Label(panel, "", new Vector2(0, -278), new Vector2(310, 76), 14);
        connectButton.onClick.AddListener(() => { if (busy) operation?.Cancel(); else _ = ChangeConnectionAsync(); });
        accountButton.onClick.AddListener(ChooseNextAccount);
        speechButton.onClick.AddListener(ToggleSpeech);
        testButton.onClick.AddListener(() =>
        {
            speechPaused = false;
            failures = 0;
            MenuDisplay.HideMenu();
            _ = SpeakAsync();
        });
        usageButton.onClick.AddListener(() => Application.OpenURL("https://chatgpt.com/settings/usage"));
    }

    private Button MakeButton(RectTransform parent, string text, float y, out TMP_Text label)
    {
        var obj = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        obj.layer = parent.gameObject.layer;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(310, 33);
        rect.anchoredPosition = new Vector2(0, y);
        obj.GetComponent<Image>().color = new Color(0.13f, 0.17f, 0.16f, 1f);
        var button = obj.GetComponent<Button>();
        button.targetGraphic = obj.GetComponent<Image>();
        label = Label(rect, text, Vector2.zero, rect.sizeDelta - new Vector2(12, 4), 16);
        return button;
    }

    private TMP_Text Label(RectTransform parent, string text, Vector2 position, Vector2 size, float fontSize)
    {
        var obj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        obj.layer = parent.gameObject.layer;
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.richText = false;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
        return label;
    }

    private void CreateBubble()
    {
        bubbleCanvas = new GameObject("Dumbubu speech", typeof(Canvas), typeof(CanvasScaler));
        DontDestroyOnLoad(bubbleCanvas);
        var canvas = bubbleCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = bubbleCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var obj = new GameObject("Speech bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        bubble = obj.GetComponent<RectTransform>();
        bubble.SetParent(bubbleCanvas.transform, false);
        bubble.sizeDelta = new Vector2(330, 86);
        bubble.pivot = new Vector2(0.5f, 0);
        var background = obj.GetComponent<Image>();
        background.color = new Color(0.08f, 0.10f, 0.13f, 0.96f);
        background.raycastTarget = false;
        bubbleText = Label(bubble, "", Vector2.zero, new Vector2(302, 66), 19);
        bubbleCanvas.SetActive(false);
    }

    private void Update()
    {
        if (client == null || pet == null) return;
        bool obstructed = MenuDisplay.IsDisplaying() || MessagesDisplay.IsDisplaying() || busy;
        // Start the reading timer only when the player can actually see the bubble.
        if (pendingBubble && !obstructed && !muted)
        {
            visibleUntil = Time.unscaledTime + bubbleDuration;
            pendingBubble = false;
        }
        if (obstructed && Time.unscaledTime < visibleUntil) visibleUntil += Time.unscaledDeltaTime;
        bool visible = Time.unscaledTime < visibleUntil && !muted && !obstructed;
        bubbleCanvas.SetActive(visible);
        if (visible && Camera.main != null)
        {
            Vector3 head = petRenderer != null ? new Vector3(pet.position.x, petRenderer.bounds.max.y, pet.position.z) : pet.position + Vector3.up * 1.5f;
            Vector3 screen = Camera.main.WorldToScreenPoint(head);
            float scale = bubbleCanvas.GetComponent<Canvas>().scaleFactor;
            float halfWidth = bubble.rect.width * scale * 0.5f, height = bubble.rect.height * scale;
            screen.x = Mathf.Clamp(screen.x, halfWidth + 8, Mathf.Max(halfWidth + 8, Screen.width - halfWidth - 8));
            screen.y = Mathf.Clamp(screen.y + 14, 8, Mathf.Max(8, Screen.height - height - 8));
            bubble.position = screen;
        }
        if (client.Connected && !muted && !speechPaused && !busy && !speaking && Time.unscaledTime >= nextSpeech &&
            !MenuDisplay.IsDisplaying() && !MessagesDisplay.IsDisplaying()) _ = SpeakAsync();
    }

    private async Task SpeakAsync()
    {
        speaking = true;
        statusLabel.text = "Connected. Waiting for Dumbubu's first words…";
        RefreshControls();
        operation = new CancellationTokenSource();
        var current = operation;
        float startedAt = Time.unscaledTime;
        // Snapshot only game state on Unity's main thread. No screen contents or ChatGPT history are read.
        string context = "Local time: " + DateTime.Now.ToString("HH:mm") + ".\nDumbubu is " +
            (petBody != null && petBody.velocity.magnitude > 2f ? "bouncing around" : "hanging out") + ".";
        if (PointsManager.Instance != null) context += "\nBump points: " + PointsManager.Instance.GetPoints() + ".";
        try
        {
            string line = await client.SpeakAsync(skill, context, recentLines.ToArray(), current.Token);
            if (shuttingDown || current.IsCancellationRequested || muted || busy) return;
            ShowBubble(line);
            recentLines.Enqueue(line);
            while (recentLines.Count > 5) recentLines.Dequeue();
            failures = 0;
            statusLabel.text = "Connected. Dumbubu speaks every 30 seconds.";
        }
        catch (OperationCanceledException) { if (!shuttingDown && !current.IsCancellationRequested) SpeechFailed("ChatGPT timed out. Trying again shortly."); }
        catch (ChatGptException error)
        {
            if (!shuttingDown)
            {
                speechPaused = error.PausesSpeech;
                SpeechFailed(error.Message);
            }
        }
        catch (Exception) { if (!shuttingDown) SpeechFailed("Dumbubu couldn't connect. Trying again shortly."); }
        finally
        {
            speaking = false;
            current.Dispose();
            if (operation == current) operation = null;
            if (!shuttingDown)
            {
                nextSpeech = failures == 0 ? Mathf.Max(Time.unscaledTime, startedAt + SpeechInterval)
                    : Time.unscaledTime + Mathf.Min(300f, SpeechInterval * Mathf.Pow(2, Mathf.Min(failures, 4)));
                RefreshControls();
            }
        }
    }

    private void ShowBubble(string text, float duration = 10f)
    {
        bubbleText.text = text;
        bubbleText.rectTransform.sizeDelta = new Vector2(302, Mathf.Max(50, bubbleText.GetPreferredValues(text, 302, 0).y));
        float height = bubbleText.rectTransform.sizeDelta.y + 24;
        bubble.sizeDelta = new Vector2(330, height);
        bubbleText.rectTransform.anchoredPosition = Vector2.zero;
        bubbleDuration = duration;
        pendingBubble = true;
    }

    private void SpeechFailed(string message)
    {
        failures++;
        statusLabel.text = message;
        // The mapped message contains no provider body, account identifier, or credentials.
        Debug.Log("Dumbubu speech: " + message);
        ShowBubble(speechPaused ? "ChatGPT speech paused. Check ChatGPT Usage settings in my menu."
            : "ChatGPT speech unavailable. Open my menu for details.", 15f);
    }

    private async Task ChangeConnectionAsync()
    {
        if (client == null || busy) return;
        operation?.Cancel();
        busy = true;
        visibleUntil = 0;
        pendingBubble = false;
        var current = new CancellationTokenSource();
        operation = current;
        RefreshControls();
        try
        {
            if (client.Connected && selectedClient == client.Registrations.ActiveClientId)
            {
                statusLabel.text = "Disconnecting ChatGPT…";
                bool revoked = await client.DisconnectAsync(current.Token);
                if (!shuttingDown) statusLabel.text = revoked ? "ChatGPT disconnected." : "Disconnected locally. Remote disconnect wasn't confirmed; remove Dumbubu in ChatGPT Settings.";
            }
            else
            {
                statusLabel.text = "Finish signing in in your browser. You have 3 minutes.";
                await client.SignInAsync(selectedClient, Application.OpenURL, current.Token);
                if (!shuttingDown)
                {
                    selectedClient = client.Registrations.ActiveClientId;
                    recentLines.Clear();
                    failures = 0;
                    speechPaused = false;
                    nextSpeech = Time.unscaledTime + 1f;
                    statusLabel.text = "Connected. Close this menu to hear Dumbubu, or Test speech now.";
                    ShowBubble("ChatGPT connected. Getting my thoughts ready…");
                }
            }
        }
        catch (OperationCanceledException) { if (!shuttingDown) statusLabel.text = "Sign-in canceled or timed out. You can try again."; }
        catch (ChatGptException error) { if (!shuttingDown) statusLabel.text = error.Message; }
        catch (Exception) { if (!shuttingDown) statusLabel.text = "Couldn't connect ChatGPT. Check your connection and try again."; }
        finally
        {
            busy = false;
            current.Dispose();
            if (operation == current) operation = null;
            if (!shuttingDown) RefreshControls();
        }
    }

    private void ChooseNextAccount()
    {
        var accounts = client.Registrations.Accounts;
        int index = accounts.FindIndex(a => a.ClientId == selectedClient);
        selectedClient = index + 1 < accounts.Count ? accounts[index + 1].ClientId : null;
        RefreshControls();
    }

    private void ToggleSpeech()
    {
        muted = !muted;
        PlayerPrefs.SetInt("DumbubuSpeechMuted", muted ? 1 : 0);
        PlayerPrefs.Save();
        visibleUntil = 0;
        pendingBubble = false;
        if (muted && speaking && !busy) operation?.Cancel();
        nextSpeech = Time.unscaledTime + SpeechInterval;
        RefreshControls();
    }

    private void RefreshControls()
    {
        connectButton.interactable = client != null;
        accountButton.interactable = client != null && !busy;
        speechButton.interactable = client != null && !busy;
        testButton.interactable = client != null && client.Connected && !busy && !speaking && !muted;
        connectLabel.text = busy ? "Cancel" : client != null && client.Connected && selectedClient == client.Registrations.ActiveClientId ? "Disconnect ChatGPT" : "Continue with ChatGPT";
        speechLabel.text = muted ? "Speech: off" : speechPaused ? "Speech: paused · see status" : "Speech: on · every 30 seconds";
        var selected = client?.Registrations.Accounts.Find(a => a.ClientId == selectedClient);
        int activeIndex = client == null ? -1 : client.Registrations.Accounts.FindIndex(a => a.ClientId == client.Registrations.ActiveClientId);
        titleLabel.text = client != null && client.Connected ? "Thoughts · active account " + (activeIndex + 1) : "Dumbubu's thoughts";
        int index = selected == null ? -1 : client.Registrations.Accounts.IndexOf(selected);
        accountLabel.text = selected == null ? "Account: add ChatGPT account" : "Account " + (index + 1) + ": " + (selected.Email ?? "ChatGPT") + " · next";
        accountLabel.enableAutoSizing = true;
        accountLabel.fontSizeMin = 11;
        accountLabel.fontSizeMax = 16;
    }

    private void OnDestroy()
    {
        shuttingDown = true;
        operation?.Cancel();
        client?.Dispose();
        if (bubbleCanvas != null) Destroy(bubbleCanvas);
    }
}
