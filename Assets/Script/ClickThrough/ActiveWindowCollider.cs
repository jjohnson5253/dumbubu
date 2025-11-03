using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public class ActiveWindowCollider : MonoBehaviour
{
    
    /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary>
    /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>
    public static IDictionary<IntPtr, string> GetOpenWindows()
    {
        IntPtr shellWindow = GetShellWindow();
        Dictionary<IntPtr, string> windows = new Dictionary<IntPtr, string>();

        EnumWindows(delegate(IntPtr hWnd, int lParam)
        {
            if (hWnd == shellWindow) return true;
            if (!IsWindowVisible(hWnd)) return true;

            int length = GetWindowTextLength(hWnd);
            if (length == 0) return true;

            StringBuilder builder = new StringBuilder(length);
            GetWindowText(hWnd, builder, length + 1);

            windows[hWnd] = builder.ToString();
            return true;

        }, 0);

        return windows;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();
    
    [DllImport("User32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool IsIconic(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetWindowRect(HandleRef hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    [Serializable]
    public struct RECT
    {
        public int Left;        // x position of upper-left corner
        public int Top;         // y position of upper-left corner
        public int Right;       // x position of lower-right corner
        public int Bottom;      // y position of lower-right corner
    }
    
    Rectangle myRect = new Rectangle();
    

    const int GWL_STYLE = -16;
    const uint WS_MAXIMIZE = 0x01000000;
    const uint WS_SIZEBOX = 0x00040000;
    
    static bool IsSizeboxWindow(IntPtr hWnd)
    {
        int style = GetWindowLong(hWnd, GWL_STYLE);
        return (style & WS_SIZEBOX) == WS_SIZEBOX;
    }
    
    static bool IsMaximizeWindow(IntPtr hWnd)
    {
        int style = GetWindowLong(hWnd, GWL_STYLE);
        return (style & WS_MAXIMIZE) == WS_MAXIMIZE;
    }

    void UpdateCurrentOpenedWindows()
    {
        activeWindowRects.Clear();
        foreach(KeyValuePair<IntPtr, string> window in GetOpenWindows())
        {
            if (!IsIconic(window.Key))
            {
                if(IsSizeboxWindow(window.Key) && !IsMaximizeWindow(window.Key))
                {
                    IntPtr handle = window.Key;
                    string title = window.Value;
                    //Debug.Log("Window handle: " + handle + " Window title: " + title);

                    RECT rect;
                    GetWindowRect(new HandleRef(this, handle), out rect);
                    myRect = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
                    activeWindowRects.Add(rect);
                    //Debug.Log("Window rect: " + myRect);
                }
            }
        }
    }
    
    public int windowRigidLayer = 0;
    public int maxWindowColliderCount = 10;
    [SerializeField]
    public List<RECT> activeWindowRects = new List<RECT>();
    [SerializeField]
    public List<BoxCollider2D> boxColliders = new List<BoxCollider2D>();
    
    void CreateBoxColliders()
    {
        for (int i = 0; i < maxWindowColliderCount; i++)
        {
            GameObject boxObject = new GameObject("WindowCollider");
            boxObject.transform.position = new Vector3(0, -100, 0);
            BoxCollider2D boxCollider = boxObject.AddComponent<BoxCollider2D>();
            boxObject.layer = windowRigidLayer;

            Rigidbody2D rb = boxObject.AddComponent<Rigidbody2D>();
            rb.isKinematic = true;

            boxColliders.Add(boxCollider);
        }
    }
    
    void UpdateBoxColliders()
    {
        int count = Math.Min(activeWindowRects.Count, maxWindowColliderCount);
        for (int i = 0; i < count; i++)
        {
            RECT rect = activeWindowRects[i];
            BoxCollider2D boxCollider = boxColliders[i];

            Vector2 pixelSize = new Vector2(rect.Right - rect.Left, rect.Bottom - rect.Top);
            Vector2 screenPosition = new Vector2(rect.Left + pixelSize.x / 2, rect.Top + pixelSize.y / 2);

            Vector3 worldPosition = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, Screen.height - screenPosition.y, 0));
            worldPosition.z = 0; // Set z to 0 since we are working in 2D

            Vector3 worldSize = Camera.main.ScreenToWorldPoint(new Vector3(pixelSize.x, pixelSize.y, 0)) - Camera.main.ScreenToWorldPoint(Vector3.zero);

            boxCollider.size = new Vector2(worldSize.x, worldSize.y);
            boxCollider.attachedRigidbody.MovePosition(worldPosition);
        }
        
        // Reset the size and position of unused colliders
        for (int i = count; i < maxWindowColliderCount; i++)
        {
            BoxCollider2D boxCollider = boxColliders[i];
            boxCollider.size = Vector2.one;
            boxCollider.attachedRigidbody.MovePosition(new Vector3(0, -15, 0));
        }
    }

    // Start is called before the first frame update
    void Start()
    {
//#if !UNITY_EDITOR
        UpdateCurrentOpenedWindows();
        CreateBoxColliders();
//#endif
    }

    // Update is called once per frame
    void Update()
    {
        UpdateCurrentOpenedWindows();
        UpdateBoxColliders();
    }
}
