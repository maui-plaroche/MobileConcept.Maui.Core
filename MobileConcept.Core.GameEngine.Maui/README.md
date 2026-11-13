# Hôte MAUI du moteur 2.5D

**Statut :** validé — version 0.1.0, .NET 10 / MAUI 10.0.20 / SkiaSharp 3.119.1.

Voir [le guide du moteur](../MobileConcept.Core.GameEngine/README.md) pour l'architecture, les mesures, les budgets mémoire, le cycle de vie et les overlays UIWindow/DecorView.

Intégration : référencer ce projet et `MobileConcept.Core`, puis appeler `UseMobileConcept()` et `UseMobileConceptGameEngine()` (qui enregistre SkiaSharp, le service d'effets, la surface Metal sur iOS et la surface Vulkan sur Android). Les règles métier et les assets du jeu appartiennent à l'application consommatrice.

| Fichier | Rôle |
|---|---|
| `GameCanvasView.cs` | Vue de jeu. Mode processeur (raster borné et couche pleine résolution `IOverlayScene`) ou mode carte graphique (`gpu: true` : scène enregistrée en `SKPicture` sur le fil d'interface, relue en pleine résolution). `Start()`, `Stop()`, `MaximumFramesPerSecond`, `FrameDiagnostics`, `PresentedFrames` (images réellement affichées), `AndroidGpu` / `RenderingApi`. |
| `DisplayFrameSource.cs` | Images déclenchées par l'écran : Choreographer sur Android, CADisplayLink sur iOS (plage 60 à `MaximumFramesPerSecond`). Minuteur à 1/60 s en repli sur les autres plateformes. |
| `MetalSurface.cs` | iOS : composant MAUI et son handler qui hébergent `SKMetalView` (dessin à la demande, pas de boucle propre). |
| `VulkanSurface.cs`, `Vulkan/` | Android : surface Skia sur Vulkan (essai, option `GameCanvasView.AndroidGpu`) — appels natifs `libvulkan.so`, swapchain, fil de rendu, repli OpenGL. |
| `FrameDiagnosticsLog.cs` | Relevé de cadence et de temps de dessin, une ligne `[FrameStats]` toutes les 5 s, tampons fixes. |
| `NativeGameLayer.cs` | Héberge tout le jeu dans UIWindow (iOS) ou le DecorView (Android). |
| `NativeWindowEffects.cs` | Effets décoratifs (particules) au-dessus de la fenêtre, implémentation de `IWindowEffects`. |
| `AppBuilderExtensions.cs` | `UseMobileConceptGameEngine()`. |

Sur iPhone, dépasser 60 images par seconde demande la clé `CADisableMinimumFrameDurationOnPhone` dans l'`Info.plist` de l'application.
