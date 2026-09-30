#import <AppKit/AppKit.h>
#import <QuartzCore/QuartzCore.h>

namespace
{
    __weak NSWindow *sUnityWindow = nil;

    NSWindow *FindUnityWindow()
    {
        if (sUnityWindow != nil && sUnityWindow.isVisible)
        {
            return sUnityWindow;
        }

        NSApplication *application = NSApplication.sharedApplication;
        NSWindow *window = application.mainWindow ?: application.keyWindow;

        if (window == nil)
        {
            for (NSWindow *candidate in application.windows)
            {
                if (candidate.isVisible && candidate.contentView != nil)
                {
                    window = candidate;
                    break;
                }
            }
        }

        sUnityWindow = window;
        return window;
    }

    void MakeViewHierarchyTransparent(NSView *view)
    {
        CALayer *layer = view.layer;
        if (layer != nil)
        {
            layer.opaque = NO;
            layer.backgroundColor = nil;
        }

        for (NSView *subview in view.subviews)
        {
            MakeViewHierarchyTransparent(subview);
        }
    }

    int ConfigureWindow(BOOL clickThrough)
    {
        NSWindow *window = FindUnityWindow();
        if (window == nil)
        {
            return 0;
        }

        window.opaque = NO;
        window.backgroundColor = NSColor.clearColor;
        window.hasShadow = NO;
        window.level = NSFloatingWindowLevel;
        window.ignoresMouseEvents = clickThrough;

        MakeViewHierarchyTransparent(window.contentView);

        // Unity's Fullscreen Window mode creates a dedicated macOS Space. Its
        // background is black, so transparency cannot reveal the desktop there.
        // Screen.fullScreenMode requests the transition; wait for AppKit to finish it.
        if ((window.styleMask & NSWindowStyleMaskFullScreen) != 0)
        {
            return 0;
        }

        window.styleMask = NSWindowStyleMaskBorderless;
        NSScreen *screen = window.screen ?: NSScreen.mainScreen;
        if (screen != nil)
        {
            [window setFrame:screen.frame display:YES animate:NO];
        }

        return 1;
    }

    int RunOnMainThread(BOOL clickThrough)
    {
        __block int result = 0;
        void (^work)(void) = ^{
            // Reapply the complete configuration when click-through changes in
            // case Unity recreated its window or Metal layer in the meantime.
            result = ConfigureWindow(clickThrough);
        };

        if (NSThread.isMainThread)
        {
            work();
        }
        else
        {
            dispatch_sync(dispatch_get_main_queue(), work);
        }

        return result;
    }

    int GetMousePosition(float *normalizedX, float *normalizedY)
    {
        if (normalizedX == nullptr || normalizedY == nullptr)
        {
            return 0;
        }

        __block int result = 0;
        __block float x = 0.0f;
        __block float y = 0.0f;
        void (^work)(void) = ^{
            NSWindow *window = FindUnityWindow();
            if (window == nil)
            {
                return;
            }

            NSView *contentView = window.contentView;
            if (contentView == nil)
            {
                return;
            }

            NSRect bounds = contentView.bounds;
            if (NSWidth(bounds) <= 0.0 || NSHeight(bounds) <= 0.0)
            {
                return;
            }

            NSPoint point = [contentView convertPoint:window.mouseLocationOutsideOfEventStream fromView:nil];
            x = (float)((point.x - NSMinX(bounds)) / NSWidth(bounds));
            y = (float)((point.y - NSMinY(bounds)) / NSHeight(bounds));
            if (contentView.isFlipped)
            {
                y = 1.0f - y;
            }

            result = 1;
        };

        if (NSThread.isMainThread)
        {
            work();
        }
        else
        {
            dispatch_sync(dispatch_get_main_queue(), work);
        }

        *normalizedX = x;
        *normalizedY = y;
        return result;
    }
}

extern "C"
{
    int TransparentWindowMac_Configure(int clickThrough)
    {
        return RunOnMainThread(clickThrough != 0);
    }

    int TransparentWindowMac_SetClickThrough(int clickThrough)
    {
        return RunOnMainThread(clickThrough != 0);
    }

    int TransparentWindowMac_GetMousePosition(float *normalizedX, float *normalizedY)
    {
        return GetMousePosition(normalizedX, normalizedY);
    }
}
