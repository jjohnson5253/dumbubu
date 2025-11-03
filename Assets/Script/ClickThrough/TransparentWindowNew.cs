using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent (typeof (Camera))]
public class TransparentWindowNew : MonoBehaviour
{
    
    /// <summary>
    /// Raycastで使うマウスイベント情報
    /// </summary>
    private PointerEventData pointerEventData;

    /// <summary>
    /// Raycast 時のレイヤーマスク
    /// </summary>
    public LayerMask hitTestLayerMask;

    [SerializeField]
    private Camera mainCamera;

    private bool clickThrough = true;
    private bool prevClickThrough = false;

    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }
    [DllImport("User32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    [DllImport("User32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    [DllImport("user32.dll")]
    static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", EntryPoint = "SetLayeredWindowAttributes")]
    static extern int SetLayeredWindowAttributes(IntPtr hwnd, int crKey, byte bAlpha, int dwFlags);

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    private static extern int SetWindowPos(IntPtr hwnd, int hwndInsertAfter, int x, int y, int cx, int cy, int uFlags);

    [DllImport("Dwmapi.dll")]
    private static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

    const int GWL_STYLE = -16;
    const uint WS_POPUP = 0x80000000;
    const uint WS_VISIBLE = 0x10000000;
    const int HWND_TOPMOST = -1;
    private const int GWL_EXSTYLE = -0x14;
    private const int WS_EX_TOOLWINDOW = 0x0080;

    int fWidth;
    int fHeight;
    IntPtr hwnd;
    MARGINS margins;

    void Start()
    {
        mainCamera = GetComponent<Camera> ();
        //hitTestLayerMask = ~LayerMask.GetMask("Ignore Raycast");
        pointerEventData = new PointerEventData(EventSystem.current);
        
        clickThrough = true;
        prevClickThrough = false;

        #if !UNITY_EDITOR // You really don't want to enable this in the editor..

        fWidth = Screen.width;
        fHeight = Screen.height;
        margins = new MARGINS() { cxLeftWidth = -1 };
        hwnd = GetActiveWindow();

        
        SetWindowLong (hwnd, -20, ~(((uint)524288) | ((uint)32)));
        //SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, fWidth, fHeight, 32 | 64); //SWP_FRAMECHANGED = 0x0020 (32); //SWP_SHOWWINDOW = 0x0040 (64)
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        SetWindowLong(hwnd, GWL_EXSTYLE, (uint)GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);  
        Application.runInBackground = true;
        #endif
    }

    void Update ()
    {
        // If our mouse is overlapping an object
        //RaycastHit hit = new RaycastHit();
        clickThrough = !HitTestByRaycast();

        if (clickThrough != prevClickThrough) {
            if (clickThrough) {
                Debug.Log("ClickThrough");
                #if !UNITY_EDITOR
                SetWindowLong(hwnd, GWL_STYLE, WS_POPUP | WS_VISIBLE);
                SetWindowLong (hwnd, -20, (uint)524288 | (uint)32);//GWL_EXSTYLE=-20; WS_EX_LAYERED=524288=&h80000, WS_EX_TRANSPARENT=32=0x00000020L
                SetLayeredWindowAttributes (hwnd, 0, 255, 2);// Transparency=51=20%, LWA_ALPHA=2
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, fWidth, fHeight, 32 | 64); //SWP_FRAMECHANGED = 0x0020 (32); //SWP_SHOWWINDOW = 0x0040 (64)
                SetWindowLong(hwnd, GWL_EXSTYLE, (uint)GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);  
                #endif
            } else {
                Debug.Log("Not ClickThrough");
                #if !UNITY_EDITOR
                SetWindowLong (hwnd, -20, ~(((uint)524288) | ((uint)32)));//GWL_EXSTYLE=-20; WS_EX_LAYERED=524288=&h80000, WS_EX_TRANSPARENT=32=0x00000020L
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, fWidth, fHeight, 32 | 64); //SWP_FRAMECHANGED = 0x0020 (32); //SWP_SHOWWINDOW = 0x0040 (64)
                SetWindowLong(hwnd, GWL_EXSTYLE, (uint)GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);  
                #endif
            }
            prevClickThrough = clickThrough;
        }
    }
    
    private bool HitTestByRaycast()
    {
        var position = Input.mousePosition;

        // // uGUIの上か否かを判定
        var raycastResults = new List<RaycastResult>();
        pointerEventData.position = position;
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);
        foreach (var result in raycastResults)
        {
            // レイヤーマスクを考慮（Ignore Raycast 以外ならヒット）
            if (((1 << result.gameObject.layer) & hitTestLayerMask) != 0)
            {
                return true;
            }
        }
        // レイヤーに関わらずヒットさせる場合は下記でよい
        // // uGUIの上と判定されれば、終了
        // if (EventSystem.current.IsPointerOverGameObject())
        // {
        //     onObject = true;
        //     return;
        // }

        if (mainCamera != null && mainCamera.isActiveAndEnabled)
        {
            Ray ray = mainCamera.ScreenPointToRay(position);

            // 3Dオブジェクトの上か否かを判定
            if (Physics.Raycast(ray, out _, 100))
            {
                return true;
            }

            // 2Dオブジェクトの上か判定
            var rayHit2D = Physics2D.GetRayIntersection(ray);
            Debug.DrawRay(ray.origin, ray.direction, Color.blue, 2f, false);
            if (rayHit2D.collider != null)
            {
                if (((1 << rayHit2D.collider.gameObject.layer) & hitTestLayerMask) != 0)
                {
                    return true;
                }
            }
        }
        else
        {
            // カメラが有効でなければメインカメラを取得
            mainCamera = Camera.main;
        }

        return false;
    }
}
