#if ANDROID
using System.Runtime.InteropServices;
using System.Text;
using SkiaSharp;

namespace MobileConcept.Core.GameEngine.Maui.Vulkan;

/// <summary>
/// Rendu Skia sur une fenêtre Android par Vulkan. Toutes les méthodes s'appellent sur un seul fil (le fil de rendu de la vue).
/// <para>Chaque image : acquisition d'une image de la swapchain (attente CPU sur une barrière, SkiaSharp ne sachant pas attendre
/// un sémaphore), dessin Skia directement dans cette image, puis une barrière de disposition vers l'affichage, soumise sur la même
/// file que Skia (l'ordre de soumission garantit qu'elle passe après le dessin), et présentation qui attend la fin de cette barrière.
/// Une seule image en vol, ce qui suffit à quelques millisecondes de rendu par image.</para>
/// </summary>
internal sealed unsafe class VulkanRenderer : IDisposable
{
    private nint instance, physicalDevice, device, queue;
    private uint queueFamily, apiVersion;
    private GRVkBackendContext? backend;
    // Gardé vivant tant que le contexte Skia existe : Skia rappelle ce délégué pour charger les fonctions Vulkan.
    private GRVkGetProcedureAddressDelegate? getProc;
    private GRContext? context;
    private ulong surface, swapchain, acquireFence, frameFence, renderDone, commandPool;
    private nint commandBuffer;
    private ulong[] images = [];
    private int format;
    private uint usage, width, height;
    private nint window;
    private bool recreate;
    private static readonly string[] InstanceExtensions = ["VK_KHR_surface", "VK_KHR_android_surface"];
    private static readonly string[] DeviceExtensions = ["VK_KHR_swapchain"];

    public GRContext? Context => context;
    public bool HasSurface => swapchain != 0;

    /// <summary>Crée l'instance, le périphérique et le contexte Skia. Lève une exception si Vulkan est absent ou incomplet.</summary>
    public void Initialize()
    {
        CreateInstance();
        PickDevice();
        CreateDevice();
        CreateSkiaContext();
        CreateSync();
    }

    private void CreateInstance()
    {
        var names = Utf8(InstanceExtensions, out var handles);
        try
        {
            var appName = Marshal.StringToCoTaskMemUTF8("MobileConcept");
            try
            {
                var app = new Vk.ApplicationInfo { sType = 0, pApplicationName = appName, pEngineName = appName, apiVersion = Vk.ApiVersion11 };
                fixed (nint* ext = names)
                {
                    var info = new Vk.InstanceCreateInfo { sType = 1, pApplicationInfo = &app, enabledExtensionCount = (uint)names.Length, ppEnabledExtensionNames = ext };
                    Vk.Check(Vk.vkCreateInstance(&info, 0, out instance), "vkCreateInstance");
                }
            }
            finally { Marshal.FreeCoTaskMem(appName); }
        }
        finally { Free(handles); }
    }

    private void PickDevice()
    {
        uint count = 0;
        Vk.Check(Vk.vkEnumeratePhysicalDevices(instance, ref count, null), "vkEnumeratePhysicalDevices");
        if (count == 0) throw new VulkanException("vkEnumeratePhysicalDevices (aucun GPU)", -1);
        var devices = stackalloc nint[(int)count];
        Vk.Check(Vk.vkEnumeratePhysicalDevices(instance, ref count, devices), "vkEnumeratePhysicalDevices");
        physicalDevice = devices[0];
        // VkPhysicalDeviceProperties commence par apiVersion ; la structure complète fait moins de 1 Ko.
        var properties = stackalloc byte[2048];
        Vk.vkGetPhysicalDeviceProperties(physicalDevice, properties);
        apiVersion = Math.Min(*(uint*)properties, Vk.ApiVersion11);
        uint families = 0;
        Vk.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref families, null);
        var props = stackalloc Vk.QueueFamilyProperties[(int)families];
        Vk.vkGetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref families, props);
        for (uint i = 0; i < families; i++)
            if ((props[i].queueFlags & Vk.QueueGraphics) != 0) { queueFamily = i; return; }
        throw new VulkanException("vkGetPhysicalDeviceQueueFamilyProperties (aucune file graphique)", -1);
    }

    private void CreateDevice()
    {
        var names = Utf8(DeviceExtensions, out var handles);
        try
        {
            float priority = 1;
            var queueInfo = new Vk.DeviceQueueCreateInfo { sType = 2, queueFamilyIndex = queueFamily, queueCount = 1, pQueuePriorities = &priority };
            fixed (nint* ext = names)
            {
                var info = new Vk.DeviceCreateInfo { sType = 3, queueCreateInfoCount = 1, pQueueCreateInfos = &queueInfo, enabledExtensionCount = (uint)names.Length, ppEnabledExtensionNames = ext };
                Vk.Check(Vk.vkCreateDevice(physicalDevice, &info, 0, out device), "vkCreateDevice");
            }
            Vk.vkGetDeviceQueue(device, queueFamily, 0, out queue);
        }
        finally { Free(handles); }
    }

    private void CreateSkiaContext()
    {
        getProc = GetProcAddress;
        backend = new GRVkBackendContext
        {
            VkInstance = instance,
            VkPhysicalDevice = physicalDevice,
            VkDevice = device,
            VkQueue = queue,
            GraphicsQueueIndex = queueFamily,
            MaxAPIVersion = apiVersion,
            Extensions = GRVkExtensions.Create(getProc, instance, physicalDevice, InstanceExtensions, DeviceExtensions),
            GetProcedureAddress = getProc,
        };
        context = GRContext.CreateVulkan(backend) ?? throw new VulkanException("GRContext.CreateVulkan", -1);
    }

    private nint GetProcAddress(string name, nint inst, nint dev)
    {
        int length = Encoding.UTF8.GetByteCount(name);
        var bytes = stackalloc byte[length + 1];
        Encoding.UTF8.GetBytes(name, new Span<byte>(bytes, length));
        bytes[length] = 0;
        if (dev != 0) { var p = Vk.vkGetDeviceProcAddr(dev, bytes); if (p != 0) return p; }
        if (inst != 0) { var p = Vk.vkGetInstanceProcAddr(inst, bytes); if (p != 0) return p; }
        return Vk.vkGetInstanceProcAddr(0, bytes);
    }

    private void CreateSync()
    {
        var fenceInfo = new Vk.FenceCreateInfo { sType = 8 };
        Vk.Check(Vk.vkCreateFence(device, &fenceInfo, 0, out acquireFence), "vkCreateFence");
        fenceInfo.flags = Vk.FenceSignaled;
        Vk.Check(Vk.vkCreateFence(device, &fenceInfo, 0, out frameFence), "vkCreateFence");
        var semInfo = new Vk.SemaphoreCreateInfo { sType = 9 };
        Vk.Check(Vk.vkCreateSemaphore(device, &semInfo, 0, out renderDone), "vkCreateSemaphore");
        var poolInfo = new Vk.CommandPoolCreateInfo { sType = 39, flags = Vk.CommandPoolResetBuffer, queueFamilyIndex = queueFamily };
        Vk.Check(Vk.vkCreateCommandPool(device, &poolInfo, 0, out commandPool), "vkCreateCommandPool");
        var allocInfo = new Vk.CommandBufferAllocateInfo { sType = 40, commandPool = commandPool, level = 0, commandBufferCount = 1 };
        Vk.Check(Vk.vkAllocateCommandBuffers(device, &allocInfo, out commandBuffer), "vkAllocateCommandBuffers");
    }

    /// <summary>Attache la fenêtre Android (<c>ANativeWindow</c>, dont le renderer devient propriétaire) et crée la swapchain.</summary>
    public void AttachWindow(nint nativeWindow, int pixelWidth, int pixelHeight)
    {
        DetachWindow();
        window = nativeWindow;
        var info = new Vk.AndroidSurfaceCreateInfo { sType = 1000008000, window = window };
        Vk.Check(Vk.vkCreateAndroidSurfaceKHR(instance, &info, 0, out surface), "vkCreateAndroidSurfaceKHR");
        Vk.Check(Vk.vkGetPhysicalDeviceSurfaceSupportKHR(physicalDevice, queueFamily, surface, out var supported), "vkGetPhysicalDeviceSurfaceSupportKHR");
        if (supported == 0) throw new VulkanException("vkGetPhysicalDeviceSurfaceSupportKHR (présentation impossible)", -1);
        width = (uint)pixelWidth; height = (uint)pixelHeight;
        CreateSwapchain();
    }

    public void Resize(int pixelWidth, int pixelHeight)
    {
        if (surface == 0 || (pixelWidth == width && pixelHeight == height)) return;
        width = (uint)pixelWidth; height = (uint)pixelHeight;
        recreate = true;
    }

    private void CreateSwapchain()
    {
        Vk.Check(Vk.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(physicalDevice, surface, out var caps), "vkGetPhysicalDeviceSurfaceCapabilitiesKHR");
        uint formatCount = 0;
        Vk.Check(Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, ref formatCount, null), "vkGetPhysicalDeviceSurfaceFormatsKHR");
        var formats = stackalloc Vk.SurfaceFormat[(int)formatCount];
        Vk.Check(Vk.vkGetPhysicalDeviceSurfaceFormatsKHR(physicalDevice, surface, ref formatCount, formats), "vkGetPhysicalDeviceSurfaceFormatsKHR");
        var chosen = formats[0];
        for (int i = 0; i < formatCount; i++) if (formats[i].format == Vk.FormatRgba8Unorm) { chosen = formats[i]; break; }
        if (chosen.format != Vk.FormatRgba8Unorm)
            for (int i = 0; i < formatCount; i++) if (formats[i].format == Vk.FormatBgra8Unorm) { chosen = formats[i]; break; }
        if (chosen.format != Vk.FormatRgba8Unorm && chosen.format != Vk.FormatBgra8Unorm)
            throw new VulkanException("vkGetPhysicalDeviceSurfaceFormatsKHR (ni RGBA8 ni BGRA8)", -1);
        format = chosen.format;

        uint count = Math.Max(caps.minImageCount, 3);
        if (caps.maxImageCount > 0) count = Math.Min(count, caps.maxImageCount);
        var extent = caps.currentExtent.width != uint.MaxValue ? caps.currentExtent : new Vk.Extent2D { width = width, height = height };
        if (extent.width == 0 || extent.height == 0) return; // fenêtre réduite : rien à afficher
        usage = Vk.UsageColorAttachment | (caps.supportedUsageFlags & (Vk.UsageTransferSrc | Vk.UsageTransferDst | Vk.UsageSampled | Vk.UsageInputAttachment));
        uint alpha = (caps.supportedCompositeAlpha & Vk.CompositeInherit) != 0 ? Vk.CompositeInherit
            : (caps.supportedCompositeAlpha & Vk.CompositePremultiplied) != 0 ? Vk.CompositePremultiplied : Vk.CompositeOpaque;
        var old = swapchain;
        var info = new Vk.SwapchainCreateInfo
        {
            sType = 1000001000, surface = surface, minImageCount = count, imageFormat = format, imageColorSpace = chosen.colorSpace,
            imageExtent = extent, imageArrayLayers = 1, imageUsage = usage, imageSharingMode = 0,
            preTransform = (caps.supportedTransforms & Vk.TransformIdentity) != 0 ? Vk.TransformIdentity : caps.currentTransform,
            compositeAlpha = alpha, presentMode = Vk.PresentFifo, clipped = 1, oldSwapchain = old,
        };
        Vk.Check(Vk.vkCreateSwapchainKHR(device, &info, 0, out swapchain), "vkCreateSwapchainKHR");
        if (old != 0) Vk.vkDestroySwapchainKHR(device, old, 0);
        width = extent.width; height = extent.height;
        uint imageCount = 0;
        Vk.Check(Vk.vkGetSwapchainImagesKHR(device, swapchain, ref imageCount, null), "vkGetSwapchainImagesKHR");
        images = new ulong[imageCount];
        fixed (ulong* p = images) Vk.Check(Vk.vkGetSwapchainImagesKHR(device, swapchain, ref imageCount, p), "vkGetSwapchainImagesKHR");
        recreate = false;
    }

    private void RecreateSwapchain()
    {
        Vk.vkDeviceWaitIdle(device);
        CreateSwapchain();
    }

    /// <summary>Dessine une image : <paramref name="paint"/> reçoit la surface Skia et sa taille en pixels. Renvoie faux si rien n'a été affiché.</summary>
    public bool Render(Action<SKSurface, int, int> paint)
    {
        if (context is null || swapchain == 0) return false;
        if (recreate) { RecreateSwapchain(); if (swapchain == 0) return false; }
        ulong fence = frameFence;
        Vk.Check(Vk.vkWaitForFences(device, 1, &fence, 1, ulong.MaxValue), "vkWaitForFences");

        int acquired = Vk.vkAcquireNextImageKHR(device, swapchain, ulong.MaxValue, 0, acquireFence, out uint index);
        if (acquired == Vk.ErrorOutOfDate) { recreate = true; return false; }
        if (acquired != Vk.Success && acquired != Vk.Suboptimal) Vk.Check(acquired, "vkAcquireNextImageKHR");
        ulong acquire = acquireFence;
        Vk.Check(Vk.vkWaitForFences(device, 1, &acquire, 1, ulong.MaxValue), "vkWaitForFences");
        Vk.Check(Vk.vkResetFences(device, 1, &acquire), "vkResetFences");

        var imageInfo = new GRVkImageInfo
        {
            Image = images[index],
            ImageTiling = 0, // VK_IMAGE_TILING_OPTIMAL
            ImageLayout = Vk.LayoutUndefined, // contenu précédent inutile : l'image est entièrement redessinée
            Format = (uint)format,
            ImageUsageFlags = usage,
            SampleCount = 1,
            LevelCount = 1,
            CurrentQueueFamily = queueFamily,
            SharingMode = 0,
        };
        var colorType = format == Vk.FormatRgba8Unorm ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
        // Image enveloppée comme texture quand la swapchain l'autorise : les modes de fusion qui relisent la cible
        // (SoftLight, Multiply…) en ont besoin ; en simple cible de rendu, Skia plante au premier dessin de ce type.
        if ((usage & Vk.UsageSampled) != 0)
        {
            using var texture = new GRBackendTexture((int)width, (int)height, imageInfo);
            using var skSurface = SKSurface.Create(context, texture, GRSurfaceOrigin.TopLeft, 1, colorType)
                ?? throw new VulkanException("SKSurface.Create (texture Vulkan)", -1);
            paint(skSurface, (int)width, (int)height);
            context.Flush();
            context.Submit(false);
        }
        else
        {
            using var target = new GRBackendRenderTarget((int)width, (int)height, imageInfo);
            using var skSurface = SKSurface.Create(context, target, GRSurfaceOrigin.TopLeft, colorType)
                ?? throw new VulkanException("SKSurface.Create (Vulkan)", -1);
            paint(skSurface, (int)width, (int)height);
            context.Flush();
            context.Submit(false);
        }

        // Skia laisse l'image en COLOR_ATTACHMENT_OPTIMAL ; passage en disposition d'affichage, après le dessin (même file).
        var begin = new Vk.CommandBufferBeginInfo { sType = 42, flags = Vk.CommandBufferOneTimeSubmit };
        Vk.Check(Vk.vkResetCommandBuffer(commandBuffer, 0), "vkResetCommandBuffer");
        Vk.Check(Vk.vkBeginCommandBuffer(commandBuffer, &begin), "vkBeginCommandBuffer");
        var barrier = new Vk.ImageMemoryBarrier
        {
            sType = 45, srcAccessMask = Vk.AccessColorAttachmentWrite, dstAccessMask = 0,
            oldLayout = Vk.LayoutColorAttachment, newLayout = Vk.LayoutPresentSrc,
            srcQueueFamilyIndex = Vk.QueueFamilyIgnored, dstQueueFamilyIndex = Vk.QueueFamilyIgnored, image = images[index],
            subresourceRange = new Vk.ImageSubresourceRange { aspectMask = Vk.AspectColor, levelCount = 1, layerCount = 1 },
        };
        Vk.vkCmdPipelineBarrier(commandBuffer, Vk.StageColorAttachmentOutput, Vk.StageBottomOfPipe, 0, 0, 0, 0, 0, 1, &barrier);
        Vk.Check(Vk.vkEndCommandBuffer(commandBuffer), "vkEndCommandBuffer");
        Vk.Check(Vk.vkResetFences(device, 1, &fence), "vkResetFences");
        var buffer = commandBuffer; var done = renderDone;
        var submit = new Vk.SubmitInfo { sType = 4, commandBufferCount = 1, pCommandBuffers = &buffer, signalSemaphoreCount = 1, pSignalSemaphores = &done };
        Vk.Check(Vk.vkQueueSubmit(queue, 1, &submit, fence), "vkQueueSubmit");

        var chain = swapchain; int result = 0;
        var present = new Vk.PresentInfo { sType = 1000001001, waitSemaphoreCount = 1, pWaitSemaphores = &done, swapchainCount = 1, pSwapchains = &chain, pImageIndices = &index, pResults = &result };
        int presented = Vk.vkQueuePresentKHR(queue, &present);
        if (presented == Vk.ErrorOutOfDate || presented == Vk.Suboptimal) recreate = true;
        else Vk.Check(presented, "vkQueuePresentKHR");
        return true;
    }

    /// <summary>Libère la swapchain, la surface et la fenêtre (avant que la fenêtre Android ne disparaisse).</summary>
    public void DetachWindow()
    {
        if (device != 0) Vk.vkDeviceWaitIdle(device);
        if (swapchain != 0) { Vk.vkDestroySwapchainKHR(device, swapchain, 0); swapchain = 0; }
        if (surface != 0) { Vk.vkDestroySurfaceKHR(instance, surface, 0); surface = 0; }
        if (window != 0) { Vk.ANativeWindow_release(window); window = 0; }
        images = [];
    }

    public void Dispose()
    {
        DetachWindow();
        if (context is not null) { context.AbandonContext(true); context.Dispose(); context = null; }
        backend?.Dispose(); backend = null; getProc = null;
        if (device != 0)
        {
            if (commandPool != 0) Vk.vkDestroyCommandPool(device, commandPool, 0);
            if (acquireFence != 0) Vk.vkDestroyFence(device, acquireFence, 0);
            if (frameFence != 0) Vk.vkDestroyFence(device, frameFence, 0);
            if (renderDone != 0) Vk.vkDestroySemaphore(device, renderDone, 0);
            Vk.vkDestroyDevice(device, 0); device = 0;
        }
        if (instance != 0) { Vk.vkDestroyInstance(instance, 0); instance = 0; }
    }

    private static nint[] Utf8(string[] values, out nint[] handles)
    {
        handles = values.Select(v => Marshal.StringToCoTaskMemUTF8(v)).ToArray();
        return handles;
    }
    private static void Free(nint[] handles) { foreach (var h in handles) Marshal.FreeCoTaskMem(h); }
}
#endif
