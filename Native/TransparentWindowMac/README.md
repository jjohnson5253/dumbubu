# TransparentWindowMac

This native macOS bundle makes Unity's player window and Metal-backed view
non-opaque, keeps the window above normal desktop windows, and toggles native
mouse-event passthrough. It also exposes event-independent cursor coordinates
so Unity can keep hit-testing while the window is click-through.

The checked-in bundle is universal (`arm64` and `x86_64`). To rebuild it after
changing `TransparentWindowMac.mm`, run:

```sh
./Native/TransparentWindowMac/build.sh
```

Building the plugin requires Xcode command-line tools. Normal Unity builds use
the checked-in bundle and do not need Xcode.
