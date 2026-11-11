using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Platform;

namespace MobileConcept.Core.GameEngine.Maui;

/// <summary>Héberge toute l'interface d'un jeu dans la fenêtre native, au-dessus du contenu de la page MAUI.</summary>
public sealed class NativeGameLayer : IDisposable
{
    private readonly Window window;
    private readonly View content;
    private bool disposed;
#if IOS
    private readonly GameRoot root;
#elif ANDROID
    private readonly GameRoot root;
#endif
    public NativeGameLayer(Window window, View content)
    {
        if (!MainThread.IsMainThread) throw new InvalidOperationException("Thread UI requis.");
        this.window = window; this.content = content;
        var context = window.Handler?.MauiContext ?? throw new InvalidOperationException("Fenêtre non attachée.");
#if IOS
        var host = window.Handler!.PlatformView as UIKit.UIWindow ?? throw new InvalidOperationException("UIWindow absente.");
        root = new GameRoot(content, content.ToPlatform(context)) { Frame = host.Bounds, AutoresizingMask = UIKit.UIViewAutoresizing.FlexibleWidth | UIKit.UIViewAutoresizing.FlexibleHeight };
        host.AddSubview(root); host.BringSubviewToFront(root);
#elif ANDROID
        var activity = window.Handler!.PlatformView as Android.App.Activity ?? throw new InvalidOperationException("Activity absente.");
        var host = activity.Window?.DecorView as Android.Views.ViewGroup ?? throw new InvalidOperationException("DecorView absent.");
        root = new GameRoot(activity, content, content.ToPlatform(context));
        host.AddView(root, new Android.Views.ViewGroup.LayoutParams(-1, -1)); host.BringChildToFront(root);
#else
        throw new PlatformNotSupportedException();
#endif
        window.Destroying += Destroying;
    }
    private void Destroying(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        window.Destroying -= Destroying;
#if IOS
        root.RemoveFromSuperview();
#elif ANDROID
        if (root.Parent is Android.Views.ViewGroup parent) parent.RemoveView(root);
#endif
        content.Handler?.DisconnectHandler(); root.Dispose();
    }
#if IOS
    private sealed class GameRoot : UIKit.UIView
    {
        private readonly View content;
        private readonly UIKit.UIView native;
        public GameRoot(View content, UIKit.UIView native)
        { this.content = content; this.native = native; AddSubview(native); BackgroundColor = UIKit.UIColor.Clear; }
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            double w = Bounds.Width, h = Bounds.Height;
            ((Microsoft.Maui.IView)content).Measure(w, h);
            ((Microsoft.Maui.IView)content).Arrange(new Rect(0, 0, w, h));
            native.Frame = Bounds;
        }
    }
#elif ANDROID
    private sealed class GameRoot : Android.Widget.FrameLayout
    {
        private readonly View content;
        public GameRoot(Android.Content.Context context, View content, Android.Views.View native) : base(context)
        { this.content = content; AddView(native, new LayoutParams(-1, -1)); }
        protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
        {
            base.OnLayout(changed, left, top, right, bottom);
            double density = Resources?.DisplayMetrics?.Density ?? 1;
            ((Microsoft.Maui.IView)content).Measure((right - left) / density, (bottom - top) / density);
            ((Microsoft.Maui.IView)content).Arrange(new Rect(0, 0, (right - left) / density, (bottom - top) / density));
        }
    }
#endif
}
