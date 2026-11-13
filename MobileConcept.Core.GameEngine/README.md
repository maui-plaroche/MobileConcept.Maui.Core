# MobileConcept.Core.GameEngine — moteur 2.5D

**Statut :** validé — version 0.1.0, .NET 10 / MAUI 10.0.20 / SkiaSharp 3.119.1. Boucle calée sur l'écran et rendu par la carte graphique (OpenGL sur Android, Metal sur iOS) depuis le 11 novembre 2026 ; Vulkan sur Android en option et 90 Hz sur Android depuis le 12 novembre 2026.

Moteur maison en C# construit depuis zéro. SkiaSharp est la bibliothèque de dessin ; MAUI est l'hôte mobile. Aucun concept Sokoban dans ces modules.

## Modules

- `MobileConcept.Core.GameEngine` : .NET pur + SkiaSharp. Contrat de scène (`IGameScene`, `IOverlayScene`), projection isométrique, horloge à pas fixe (`FixedStepClock`), animations d'atlas (`SpriteAnimation`), easing, particules (`ParticleSystem`), cache de textures borné (`TextureCache`), gestes de balayage (`SwipeGesture`).
- `MobileConcept.Core.GameEngine.Maui` : hôte MAUI. `GameCanvasView` (mode processeur ou carte graphique), `DisplayFrameSource` (images déclenchées par l'écran), `MetalSurface` (Metal sur iOS), `FrameDiagnosticsLog` (mesures), `NativeGameLayer` et `NativeWindowEffects` (jeu et effets dans UIWindow iOS / DecorView Android). Référence le Core existant.
- `MobileConcept.Core.GameEngine.Tests` : tests indépendants d'un téléphone.

Les applications habituelles continuent à utiliser `MobileConcept.Core` seul. Une application de jeu référence le module MAUI et appelle :

```csharp
builder.UseMauiApp<App>()
       .UseMobileConcept()
       .UseMobileConceptGameEngine();
```

Créer une `IGameScene`, l'assigner à `GameCanvasView.Scene`, puis appeler `Start()` quand la page est visible et active, `Stop()` lorsqu'elle disparaît ou est désactivée. La vue ne possède pas la scène : le propriétaire libère la vue et la scène. Aucune allocation de texture, aucun chargement disque dans `Render`.

```csharp
// Carte graphique en pleine résolution ; jusqu'à 120 images par seconde sur les écrans qui le permettent (iPhone ProMotion).
var view = new GameCanvasView(gpu: true) { Scene = scene, MaximumFramesPerSecond = 120 };
```

Sur iPhone, dépasser 60 images par seconde demande aussi la clé `CADisableMinimumFrameDurationOnPhone` (booléen vrai) dans l'`Info.plist` de l'application.

```csharp
// Sur le thread UI ; fenêtre de la page concernée, pas une fenêtre globale.
effects.Play(page.Window, EffectPreset.Victory, new Vector2(.5f, .3f));
// L'overlay est automatiquement retiré après la dernière particule ou à la désactivation.
effects.Clear(page.Window);
```

## Règles 2.5D

1. Monde logique XY en cases, Z en hauteur ; projection `(x-y)*tileWidth/2, (x+y)*tileHeight/2-z*elevation`. Défaut : 96×48, élévation 48.
2. La caméra transforme ensuite les unités de dessin en coordonnées MAUI. Inverser caméra puis projection pour le picking.
3. Dessiner sol et ombres, puis objets triés par profondeur des pieds et identifiant stable, puis effets locaux, HUD MAUI, overlays natifs temporaires.
4. La grille métier décide immédiatement ; le rendu interpole vers l'état confirmé. Les animations n'écrivent jamais les règles.
5. Images déclenchées par l'écran (`DisplayFrameSource` : Choreographer sur Android, CADisplayLink sur iOS jusqu'à 120 i/s) et pas variable égal au temps écoulé entre deux images (borné à 0,1 s). Le minuteur à 1/60 s d'origine se décalait de l'écran (6 à 19 % d'images à zéro ou deux pas) ; il ne reste qu'en repli. `FixedStepClock` reste disponible pour des règles à pas fixe.
6. Règles, gestes, `Update` et `Render` sur le fil d'interface. En mode carte graphique, `Render` est enregistré dans une `SKPicture` sur ce fil ; seule la relecture se fait sur le fil de dessin (fil OpenGL sur Android). Préparer les fichiers avant d'afficher la scène ; ne jamais toucher aux vues ni au canvas de la vue depuis un autre fil. Une scène peut préparer des images hors écran sur un fil de fond (exemple : les bandes du décor de la carte de Sokoban World) à condition d'utiliser sa propre surface et ses propres pinceaux sur ce fil, et de ne publier l'image terminée que sur le fil d'interface.
7. Sprites RGBA transparents, pivot aux pieds et rectangles d'atlas explicites. Éviter les atlas monolithiques : fichiers par personnage, action et direction. Le cache détient les bitmaps ; une scène ne doit pas conserver une texture après Clear().
8. L'animation de marche peut boucler ; poussée et victoire se terminent. Les ombres sont des objets distincts. Les caisses ne sont jamais dessinées dans le sprite du personnage.

## Mémoire Android

Les limites sont des garde-fous configurables de départ, pas une garantie universelle d'absence d'OOM.

- Texture : côté maximum 1024 px, RGBA8888, fichier encodé maximum 4 Mio, budget 24 Mio par cache avec réserve de deux fois la taille de la texture entrante.
- `TextureCache` lit le header par SKCodec puis refuse les dimensions avant `TryAllocPixels`. Il ne décode pas un atlas géant pour le réduire ensuite. Préparer une variante basse résolution hors runtime si l'asset est trop grand.
- Un seul monde actif ; décharger son cache avant le suivant. Ni `SKBitmap.Copy`, ni `SKImage.FromBitmap` répété, ni encodage PNG dans la boucle de rendu.
- Mode processeur : `GameCanvasView` réduit la taille du canvas AVANT l'allocation native (par défaut 1024 px sur le grand côté et 786432 pixels, réglables par `MaxRasterDimension` et `MaxRasterPixels`), puis agrandit la vue par le compositeur natif. `IgnorePixelScaling` seul ne suffit pas à limiter le bitmap Android.
- Mode carte graphique : pas de bitmap plein écran côté processeur ; les images de la scène deviennent des textures de la carte graphique, envoyées une fois puis gardées en cache. Mesuré sur Pixel 7a (Sokoban World) : mémoire totale ≈ 405 Mo contre ≈ 330 Mo en mode processeur, stable sur 8 allers-retours carte ↔ jeu.
- Overlay : un seul par fenêtre, remplace le précédent ; même surface bornée. Retrait à la fin, après 4 secondes maximum, et sur Deactivated/Stopped/Destroying.
- Particules : tableau préalloué, capacité fixe (256 par défaut), émissions excédentaires abandonnées, SKPaint réutilisé. Réduction des animations coupe les effets.
- Ne pas activer `android:largeHeap`. Ne pas ajouter de flou plein écran, saveLayer ou framebuffer hors écran sans budget mesuré.
- Le budget du cache n'inclut pas toutes les allocations du codec, du compositeur, de MAUI ou du driver GPU. Mesurer le RSS natif, le pic de décodage et la répétition ouverture/fermeture sur un Android de faible mémoire.

## Overlays natifs

La fenêtre MAUI est un paramètre explicite. iOS : UIView dans la UIWindow du handler, interaction désactivée. Android : View dans le DecorView de l'Activity du handler, non cliquable et exclue de l'accessibilité. Pas d'autorisation SYSTEM_ALERT_WINDOW, aucune fenêtre système supplémentaire. Les effets sont décoratifs : ils ne doivent pas intercepter de geste ou annoncer des événements métier.

Le service se pilote uniquement sur le thread UI. L'overlay utilise les bounds de la fenêtre ; le jeu et ses commandes utilisent le layout/safe area de la page. Valider rotation, densité, barres système et navigation native sur appareil.

## Versions et .NET 11

`GameEngine.props` centralise .NET 10, MAUI 10.0.20 et SkiaSharp 3.119.1 pour les modules du moteur. Le Core existant garde ses versions pour ne pas changer ses autres consommateurs. Pour l'échéance de décembre demandée : installer SDK/workloads .NET 11 compatibles avec Xcode/JDK, mettre à jour le Core et les trois modules ensemble, puis l'application, et refaire tests + builds iOS/Android + mesure mémoire. Aucun package preview ni TargetFramework .NET 11 n'est activé dans cette version.

## Vérification

```sh
dotnet test MobileConcept.Core.GameEngine.Tests/MobileConcept.Core.GameEngine.Tests.csproj
dotnet build MobileConcept.Core.GameEngine.Maui/MobileConcept.Core.GameEngine.Maui.csproj -f net10.0-android
dotnet build MobileConcept.Core.GameEngine.Maui/MobileConcept.Core.GameEngine.Maui.csproj -f net10.0-ios
```

Mesurer en Release sur appareil, jamais en Debug : en Debug, les temps par image sont nettement plus élevés (plateau de Sokoban World : 14 ms en Debug, 8 ms en Release, mode processeur).

Pour des tests sous Linux, ajouter les native assets SkiaSharp adaptés au runner. La première application de référence est SokobanWorld/mobile. L'édition des niveaux, l'audio, la gestion automatique de pression mémoire et les solveurs de jeu ne sont pas implémentés dans ce socle.

Sources techniques : [SKCanvasView](https://learn.microsoft.com/dotnet/api/skiasharp.views.maui.controls.skcanvasview), [cycle de vie MAUI](https://learn.microsoft.com/dotnet/maui/fundamentals/app-lifecycle), [mémoire bitmap Android](https://developer.android.com/topic/performance/issues/bitmap-memory-usage), [Choreographer](https://developer.android.com/reference/android/view/Choreographer), [CADisplayLink](https://developer.apple.com/documentation/quartzcore/cadisplaylink).

## Jeu plein écran au niveau natif

`NativeGameLayer(window, contentView)` héberge la totalité du jeu (canvas et HUD MAUI) directement dans UIWindow/DecorView. Le propriétaire conserve le handle et le libère à la sortie de la page. Les particules IWindowEffects se placent ensuite au-dessus. La politique des barres système et les zones sûres des commandes appartiennent à l’application (Info.plist iOS, WindowInsetsController Android). La disposition native remesure le contenu à la rotation.

## Couche pleine résolution (`IOverlayScene`)

Mode processeur uniquement. `GameCanvasView` borne son raster (`MaxRasterDimension`, `MaxRasterPixels`) et laisse le compositeur l'agrandir : économique pour le décor, mais les sprites en ressortent flous. Une scène qui implémente `IOverlayScene` reçoit en plus `RenderOverlay(canvas, viewport)` sur un second `SKCanvasView` transparent, en pleine résolution, dessiné après `Render` avec le même viewport logique. N'y mettre que les éléments fins (héros, texte, pictogrammes) : le coût est celui d'un effacement plein écran plus les tracés. La couche est masquée quand la scène courante n'implémente pas l'interface.


## Rendu par la carte graphique (`GameCanvasView(gpu: true)`)

Même architecture sur Android et iOS : à chaque image de l'écran, la scène avance puis s'enregistre dans une `SKPicture` sur le fil d'interface ; une surface de la carte graphique en pleine résolution rejoue la dernière image. Surfaces : `SKGLView` (OpenGL, `GLTextureView`) sur Android, `MetalSurface` (`SKMetalView`, dessin à la demande) sur iOS, enregistrée par `UseMobileConceptGameEngine()`. Les règles et les gestes restent sur le fil d'interface même quand Android dessine sur son fil OpenGL. `Render` et `RenderOverlay` sont enregistrés dans la même image : les plafonds de raster et la couche séparée ne servent plus qu'au mode processeur, gardé en repli.

Choix : enregistrer la scène plutôt que la dessiner directement sur le fil OpenGL, pour que les règles, les gestes et les scènes existantes restent sur un seul fil ; Metal sur iOS dès maintenant, OpenGL y étant déclaré obsolète par Apple ; OpenGL sur Android (`SKGLView` de SkiaSharp MAUI), Vulkan n'étant pas nécessaire aux mesures actuelles. Les images enregistrées ne sont libérées que lorsque le fil de dessin ne les lit plus.

Mesures en Release (Sokoban World, mises à jour le 12 novembre 2026, pleine résolution) :

| Appareil, scène | Départ : processeur, minuteur | Actuel : carte graphique, boucle calée sur l'écran |
|---|---|---|
| Pixel 7a, carte | 30 à 56 i/s, image réduite 576×1280 | 90 i/s, 1080×2400 (OpenGL ou Vulkan) |
| Pixel 7a, plateau | 59 à 60 i/s irréguliers, 875×1944 | 89 à 90 i/s, 1080×2400 (OpenGL ou Vulkan) |
| iPhone 17, carte | 56 i/s irréguliers, image réduite 588×1280 | 120 i/s, 1206×2622 (Metal) |
| iPhone 17, plateau | 50 à 56 i/s irréguliers | 120 i/s, 1206×2622 (Metal) |
| Images à zéro ou deux pas | 6 à 19 % | 0 % |

| Coût d'une image (médiane) | Pixel 7a, OpenGL | Pixel 7a, Vulkan | iPhone 17, Metal |
|---|---|---|---|
| Fréquence de l'écran | 90 Hz (11,1 ms) | 90 Hz (11,1 ms) | 120 Hz (8,3 ms) |
| Enregistrement de la scène (fil d'interface) | 0,6 à 1,9 ms | 1 à 1,8 ms | 0,3 à 0,4 ms |
| Relecture par la carte graphique | 2,4 à 4,3 ms | 2,4 à 3,7 ms | 1 à 1,4 ms |
| Mode processeur, pour comparaison (image réduite, 60 Hz) | 8 à 18 ms | — | 1,2 à 3,5 ms |

Le Pixel n'atteint 90 Hz que si « Affichage fluide » est activé dans ses réglages (voir « 90 Hz sur Android ») ; sinon 60 i/s, avec les mêmes coûts. Vulkan est une option (voir « Vulkan sur Android »), OpenGL reste le défaut.

Première image sous Metal : environ 0,5 s, le temps que Metal prépare ses ressources (à couvrir par l'écran de lancement).

Mesure intégrée : `GameCanvasView.FrameDiagnostics = true` écrit une ligne `[FrameStats]` toutes les 5 s (cadence des images, intervalles, temps de dessin ; en mode carte graphique, temps d'enregistrement et de relecture). Lecture : `adb logcat | grep FrameStats` sur Android, `xcrun devicectl device process launch --console …` sur iOS. À n'activer que pour les mesures (Sokoban World la compile seulement avec `-p:FrameStats=true`).

Piège : après une modification du moteur, nettoyer `bin/obj` Release iOS de l'app et du moteur avant de recompiler ; une sortie AOT périmée plante au lancement sans pile managée.

## Vulkan sur Android (essai du 12 novembre 2026)

`GameCanvasView.AndroidGpu = AndroidGpuApi.Vulkan` (avant de créer la vue) remplace OpenGL par Vulkan sur Android si l'appareil déclare Vulkan 1.1, avec repli automatique sur OpenGL si l'initialisation ou le rendu échoue. Par défaut : OpenGL. `RenderingApi` indique l'API réellement utilisée.

Choix techniques :

1. **Aucun package tiers** : appels directs à `libvulkan.so` (le chargeur Android exporte le cœur et les extensions de surface et de swapchain) et à `libandroid.so` (`ANativeWindow_fromSurface`), dans `Vulkan/VulkanNative.cs`. SharpVk, non maintenu, est écarté.
2. **SkiaSharp 3.119.1 tel quel** : `GRContext.CreateVulkan` avec un `GRVkBackendContext` dont la fonction de chargement (`vkGetDeviceProcAddr`, puis `vkGetInstanceProcAddr`) est fournie en C#, comme le fait Avalonia (licence MIT, consulté pour la méthode).
3. **Vue** : `VulkanTextureView` (TextureView, comme le `SKGLView` de SkiaSharp, donc composée comme une vue ordinaire) et `VulkanSurface` + handler MAUI. Tout le travail Vulkan sur un fil de rendu dédié ; la scène reste enregistrée en `SKPicture` sur le fil d'interface, exactement comme avec OpenGL et Metal.
4. **Image par image** (`VulkanRenderer`) : acquisition d'une image de la swapchain avec attente CPU sur une barrière (SkiaSharp ne sait pas attendre un sémaphore), dessin Skia directement dans cette image, puis une barrière `COLOR_ATTACHMENT_OPTIMAL → PRESENT_SRC_KHR` soumise sur la même file que Skia et présentation qui attend sa fin. FIFO (synchronisé sur l'écran), 3 images, une image en vol.
5. **Image de la swapchain enveloppée comme texture** (`GRBackendTexture`) dès que l'usage `SAMPLED` est autorisé : en simple cible de rendu (`GRBackendRenderTarget`), Skia plante (pointeur nul dans `flush`) au premier mode de fusion qui relit la cible (`SoftLight` sur le plateau de Sokoban World).
6. **Cycle de vie** : destruction de la surface (application en arrière-plan) traitée de façon synchrone sur le fil de rendu avant de rendre la main à Android ; swapchain recréée au redimensionnement et sur `VK_ERROR_OUT_OF_DATE_KHR` / `VK_SUBOPTIMAL_KHR`.

Mesures en Release sur Pixel 7a (Sokoban World, 60 Hz, pleine résolution 1080×2400) :

| Scène | OpenGL | Vulkan |
|---|---|---|
| Carte, défilement long : images par seconde | 57 à 60 | 60 |
| Carte : relecture d'une image (médiane, p90) | 3,1 à 5 ms, p90 ≈ 5 à 6,5 ms | 3,1 à 3,4 ms, p90 ≈ 4,6 à 5 ms |
| Carte : plus long intervalle entre deux images | jusqu'à 66 ms | 33 ms |
| Plateau : images par seconde | 60 | 60 |
| Plateau : relecture d'une image (médiane) | 3,2 à 5,9 ms | 4,7 à 6,1 ms |
| Mémoire totale, 8 allers-retours carte ↔ jeu | ≈ 405 Mo (graphique ≈ 194 Mo), stable | ≈ 435 Mo (graphique ≈ 234 Mo), stable |
| Arrière-plan puis retour | — | correct (surface détruite et recréée) |

Conclusion de l'essai : Vulkan fonctionne et tient 60 i/s, avec une relecture de la carte un peu plus régulière ; pas de gain visible à 60 Hz sur ce téléphone, et environ 30 Mo de mémoire graphique en plus. OpenGL reste le défaut tant que Vulkan n'a pas été validé sur d'autres appareils Android (pilotes inégaux). Le chargeur .NET affiche au démarrage « Shared library 'libvulkan.so' not loaded, p/invoke may fail » : avertissement sans effet, la bibliothèque système est trouvée à l'appel.

## 90 Hz sur Android (12 novembre 2026)

Cause du plafond à 60 i/s mesuré jusque-là sur le Pixel 7a : le réglage utilisateur « Affichage fluide » était désactivé (vote système `PRIORITY_USER_SETTING_PEAK_REFRESH_RATE`, maximum 60 Hz), prioritaire sur toute demande d'une application. Une fois le réglage activé, le moteur demande la fréquence lui-même :

1. **Mode d'écran de la fenêtre** (`DisplayFrameSource`, au `Start`) : `WindowManager.LayoutParams.PreferredDisplayModeId` = mode le plus rapide de même résolution sans dépasser `MaximumFramesPerSecond` ; remis à 0 (choix du système) au `Stop`, pour la batterie hors du jeu.
2. **Catégorie de fréquence de la vue** (Android 15 et plus) : `View.RequestedFrameRate = RequestedFrameRateCategoryHigh` sur la surface de rendu, sinon le système ramène une vue « normale » à 60 Hz dès qu'on ne touche plus l'écran.
3. La boucle (Choreographer) et la swapchain Vulkan (FIFO) suivent ensuite la fréquence de l'écran sans autre changement.

Mesures en Release, Pixel 7a à 90 Hz (budget 11,1 ms par image), pleine résolution 1080×2400 :

| Scène | OpenGL | Vulkan |
|---|---|---|
| Carte au repos | 90 i/s ; relecture 3,2 à 3,3 ms | 90 i/s ; relecture 2,8 ms |
| Carte en défilement long | 89 à 90 i/s ; relecture 2,4 à 3,3 ms | 89 à 90 i/s ; relecture 2,4 à 2,8 ms |
| Plateau en jeu | 89 à 90 i/s ; relecture 3 à 4,3 ms | 89 à 90 i/s ; relecture 3,5 à 3,7 ms |
| Enregistrement de la scène (fil d'interface) | 0,6 à 1,9 ms | 1 à 1,8 ms |
| Images à zéro ou deux pas | 0 % | 0 % |
| Mémoire totale (plateau) | — | ≈ 456 Mo (graphique ≈ 244 Mo) |

Reste commun aux deux API : des arrêts ponctuels de 30 à 90 ms (construction d'une bande du décor de la carte, ramasse-miettes soupçonné), à traiter dans l'application.

## Ramasse-miettes et à-coups (12 novembre 2026)

Sur Android, une collecte de génération 0 du runtime Mono coûte environ 50 ms (mesuré sur Pixel 7a) : à 90 Hz, cela fait plusieurs images perdues. Règles pour les jeux :

- **Ne rien allouer par image** : réutiliser pinceaux, chemins, dégradés et tableaux ; pas de LINQ, de `ToString()` ni de tableaux temporaires dans `Update` et `Render`. Le moteur réutilise son `SKPictureRecorder` ; une `SKPicture` par image reste la seule allocation du mode carte graphique.
- **Agrandir la zone des nouveaux objets** dans l'application : `MONO_GC_PARAMS=nursery-size=32m` dans un fichier d'environnement Android (`<AndroidEnvironment Include="…" />`).
- **Collecter aux transitions** avec `GC.Collect(0)`, sous un écran de transition opaque, où la pause ne se voit pas (Sokoban World : au retour à la carte seulement ; l'ouverture d'un niveau doit rester instantanée).
- **Mesurer** : avec `FrameDiagnostics`, la ligne `[FrameStats]` donne les octets alloués par image sur le fil d'interface, et chaque à-coup produit une ligne `[FrameSpike]` (mise à jour, enregistrement, `FrameAdvanced`, reste hors moteur, passages du ramasse-miettes par génération).

Résultat dans Sokoban World : de 5,8 à 3,5 Kio alloués par image sur la carte, et des à-coups de 50 à 60 ms environ 6 fois par minute à un seul de 44 ms en 45 s de défilement (sans collecte), aucun en jeu sur le plateau.

## Transitions sans à-coup (13 novembre 2026)

Règles tirées de Sokoban World (mesures dans son dépôt : `documents/claude-instructions/tasks/2026-11-11-personnages-fluides.md`), et de l'analyse du code de Godot (`documents/claude-instructions/architecture/2026-11-12-godot-a-coups.md`).

1. **Préparer pendant les temps morts, pas au moment de l'affichage.** Le décor statique d'une scène coûteuse (cache plein écran, bandes de décor) se construit sur un fil de fond pendant ce que le joueur regarde déjà : déplacement du héros vers une étape, partie en cours pour la suivante, écran de chargement. Au premier rendu, la scène adopte le décor préparé s'il correspond exactement au cadrage, sinon elle construit le sien. Le cadrage et la construction doivent être des fonctions sans état de la scène, avec des pinceaux propres au fil de fond. Limiter le nombre de scènes préparées (mémoire) et libérer les plus anciennes sans bloquer le fil d'interface.
2. **Attendre des images réellement affichées.** `GameCanvasView.PresentedFrames` compte les images dessinées à l'écran (relecture par la carte graphique, ou rendu processeur) ; il diffère des tics de la boucle au démarrage, quand la surface de la carte graphique met plusieurs centaines de millisecondes à apparaître. Un écran de transition ne se retire qu'après N images réellement affichées.
3. **Masquer le reste par un écran de transition opaque** (carte d'étape, rideau) : le travail inévitable (changement de panneaux, collecte du ramasse-miettes, reconstruction) se fait pendant qu'il couvre tout l'écran, puis l'animation de sortie dévoile la nouvelle scène.
4. **Éliminer les coûts de première utilisation.** Sur Android, compiler l'application en Release avec `AndroidEnableProfiledAot=false` (précompilation complète) : sinon le premier usage d'un chemin de code (premier geste, première mise en page) est compilé à la volée et coûte 30 à 45 ms. Coût : APK plus gros (+11,6 Mo dans Sokoban World) et compilation plus longue.
5. **Animations qui tournent pendant un blocage** : seules les animations confiées au système (vecteur animé Android sur son fil de rendu, Core Animation sur iOS) continuent pendant que le fil d'interface est occupé ; une animation MAUI ou Skia se fige.
