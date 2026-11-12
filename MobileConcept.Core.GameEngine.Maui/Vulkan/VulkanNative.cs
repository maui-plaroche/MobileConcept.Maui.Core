#if ANDROID
using System.Runtime.InteropServices;

namespace MobileConcept.Core.GameEngine.Maui.Vulkan;

/// <summary>
/// Appels Vulkan strictement nécessaires au rendu Skia sur une fenêtre Android, liés directement à <c>libvulkan.so</c>
/// (le chargeur Android exporte le cœur 1.0/1.1 et les extensions de surface et de swapchain). Aucun package tiers.
/// Structures blittables, disposition C sur arm64 / x86_64 (handles non dispatchables sur 64 bits).
/// </summary>
internal static unsafe class Vk
{
    private const string Lib = "libvulkan.so";

    public const int Success = 0, NotReady = 1, Timeout = 2, Suboptimal = 1000001003, ErrorOutOfDate = -1000001004, ErrorSurfaceLost = -1000000000;
    public const uint ApiVersion11 = (1u << 22) | (1u << 12);
    public const uint QueueGraphics = 0x1;
    public const int FormatRgba8Unorm = 37, FormatBgra8Unorm = 44;
    public const uint UsageTransferSrc = 0x1, UsageTransferDst = 0x2, UsageSampled = 0x4, UsageColorAttachment = 0x10, UsageInputAttachment = 0x80;
    public const int LayoutUndefined = 0, LayoutColorAttachment = 2, LayoutPresentSrc = 1000001002;
    public const uint StageTopOfPipe = 0x1, StageColorAttachmentOutput = 0x400, StageBottomOfPipe = 0x2000;
    public const uint AccessColorAttachmentWrite = 0x100;
    public const uint CompositeOpaque = 0x1, CompositePremultiplied = 0x2, CompositeInherit = 0x8;
    public const uint TransformIdentity = 0x1;
    public const int PresentFifo = 2;
    public const uint FenceSignaled = 0x1;
    public const uint CommandPoolResetBuffer = 0x2;
    public const uint CommandBufferOneTimeSubmit = 0x1;
    public const uint AspectColor = 0x1;
    public const uint QueueFamilyIgnored = ~0u;

    // ---------- structures ----------
    [StructLayout(LayoutKind.Sequential)] public struct ApplicationInfo { public int sType; public nint pNext; public nint pApplicationName; public uint applicationVersion; public nint pEngineName; public uint engineVersion; public uint apiVersion; }
    [StructLayout(LayoutKind.Sequential)] public struct InstanceCreateInfo { public int sType; public nint pNext; public uint flags; public ApplicationInfo* pApplicationInfo; public uint enabledLayerCount; public nint ppEnabledLayerNames; public uint enabledExtensionCount; public nint* ppEnabledExtensionNames; }
    [StructLayout(LayoutKind.Sequential)] public struct DeviceQueueCreateInfo { public int sType; public nint pNext; public uint flags; public uint queueFamilyIndex; public uint queueCount; public float* pQueuePriorities; }
    [StructLayout(LayoutKind.Sequential)] public struct DeviceCreateInfo { public int sType; public nint pNext; public uint flags; public uint queueCreateInfoCount; public DeviceQueueCreateInfo* pQueueCreateInfos; public uint enabledLayerCount; public nint ppEnabledLayerNames; public uint enabledExtensionCount; public nint* ppEnabledExtensionNames; public nint pEnabledFeatures; }
    [StructLayout(LayoutKind.Sequential)] public struct QueueFamilyProperties { public uint queueFlags; public uint queueCount; public uint timestampValidBits; public uint granularityWidth, granularityHeight, granularityDepth; }
    [StructLayout(LayoutKind.Sequential)] public struct AndroidSurfaceCreateInfo { public int sType; public nint pNext; public uint flags; public nint window; }
    [StructLayout(LayoutKind.Sequential)] public struct Extent2D { public uint width, height; }
    [StructLayout(LayoutKind.Sequential)] public struct SurfaceCapabilities { public uint minImageCount, maxImageCount; public Extent2D currentExtent, minImageExtent, maxImageExtent; public uint maxImageArrayLayers, supportedTransforms, currentTransform, supportedCompositeAlpha, supportedUsageFlags; }
    [StructLayout(LayoutKind.Sequential)] public struct SurfaceFormat { public int format, colorSpace; }
    [StructLayout(LayoutKind.Sequential)] public struct SwapchainCreateInfo { public int sType; public nint pNext; public uint flags; public ulong surface; public uint minImageCount; public int imageFormat; public int imageColorSpace; public Extent2D imageExtent; public uint imageArrayLayers; public uint imageUsage; public int imageSharingMode; public uint queueFamilyIndexCount; public uint* pQueueFamilyIndices; public uint preTransform; public uint compositeAlpha; public int presentMode; public uint clipped; public ulong oldSwapchain; }
    [StructLayout(LayoutKind.Sequential)] public struct CommandPoolCreateInfo { public int sType; public nint pNext; public uint flags; public uint queueFamilyIndex; }
    [StructLayout(LayoutKind.Sequential)] public struct CommandBufferAllocateInfo { public int sType; public nint pNext; public ulong commandPool; public int level; public uint commandBufferCount; }
    [StructLayout(LayoutKind.Sequential)] public struct CommandBufferBeginInfo { public int sType; public nint pNext; public uint flags; public nint pInheritanceInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct ImageSubresourceRange { public uint aspectMask, baseMipLevel, levelCount, baseArrayLayer, layerCount; }
    [StructLayout(LayoutKind.Sequential)] public struct ImageMemoryBarrier { public int sType; public nint pNext; public uint srcAccessMask, dstAccessMask; public int oldLayout, newLayout; public uint srcQueueFamilyIndex, dstQueueFamilyIndex; public ulong image; public ImageSubresourceRange subresourceRange; }
    [StructLayout(LayoutKind.Sequential)] public struct SubmitInfo { public int sType; public nint pNext; public uint waitSemaphoreCount; public ulong* pWaitSemaphores; public uint* pWaitDstStageMask; public uint commandBufferCount; public nint* pCommandBuffers; public uint signalSemaphoreCount; public ulong* pSignalSemaphores; }
    [StructLayout(LayoutKind.Sequential)] public struct PresentInfo { public int sType; public nint pNext; public uint waitSemaphoreCount; public ulong* pWaitSemaphores; public uint swapchainCount; public ulong* pSwapchains; public uint* pImageIndices; public int* pResults; }
    [StructLayout(LayoutKind.Sequential)] public struct FenceCreateInfo { public int sType; public nint pNext; public uint flags; }
    [StructLayout(LayoutKind.Sequential)] public struct SemaphoreCreateInfo { public int sType; public nint pNext; public uint flags; }

    // ---------- fonctions (cœur et extensions WSI exportées par le chargeur Android) ----------
    [DllImport(Lib)] public static extern int vkCreateInstance(InstanceCreateInfo* info, nint allocator, out nint instance);
    [DllImport(Lib)] public static extern void vkDestroyInstance(nint instance, nint allocator);
    [DllImport(Lib)] public static extern int vkEnumeratePhysicalDevices(nint instance, ref uint count, nint* devices);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceProperties(nint physicalDevice, byte* properties);
    [DllImport(Lib)] public static extern void vkGetPhysicalDeviceQueueFamilyProperties(nint physicalDevice, ref uint count, QueueFamilyProperties* properties);
    [DllImport(Lib)] public static extern int vkCreateDevice(nint physicalDevice, DeviceCreateInfo* info, nint allocator, out nint device);
    [DllImport(Lib)] public static extern void vkDestroyDevice(nint device, nint allocator);
    [DllImport(Lib)] public static extern void vkGetDeviceQueue(nint device, uint family, uint index, out nint queue);
    [DllImport(Lib)] public static extern int vkDeviceWaitIdle(nint device);
    [DllImport(Lib)] public static extern nint vkGetInstanceProcAddr(nint instance, byte* name);
    [DllImport(Lib)] public static extern nint vkGetDeviceProcAddr(nint device, byte* name);

    [DllImport(Lib)] public static extern int vkCreateAndroidSurfaceKHR(nint instance, AndroidSurfaceCreateInfo* info, nint allocator, out ulong surface);
    [DllImport(Lib)] public static extern void vkDestroySurfaceKHR(nint instance, ulong surface, nint allocator);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceSupportKHR(nint physicalDevice, uint family, ulong surface, out uint supported);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceCapabilitiesKHR(nint physicalDevice, ulong surface, out SurfaceCapabilities capabilities);
    [DllImport(Lib)] public static extern int vkGetPhysicalDeviceSurfaceFormatsKHR(nint physicalDevice, ulong surface, ref uint count, SurfaceFormat* formats);

    [DllImport(Lib)] public static extern int vkCreateSwapchainKHR(nint device, SwapchainCreateInfo* info, nint allocator, out ulong swapchain);
    [DllImport(Lib)] public static extern void vkDestroySwapchainKHR(nint device, ulong swapchain, nint allocator);
    [DllImport(Lib)] public static extern int vkGetSwapchainImagesKHR(nint device, ulong swapchain, ref uint count, ulong* images);
    [DllImport(Lib)] public static extern int vkAcquireNextImageKHR(nint device, ulong swapchain, ulong timeout, ulong semaphore, ulong fence, out uint index);
    [DllImport(Lib)] public static extern int vkQueuePresentKHR(nint queue, PresentInfo* info);

    [DllImport(Lib)] public static extern int vkCreateCommandPool(nint device, CommandPoolCreateInfo* info, nint allocator, out ulong pool);
    [DllImport(Lib)] public static extern void vkDestroyCommandPool(nint device, ulong pool, nint allocator);
    [DllImport(Lib)] public static extern int vkAllocateCommandBuffers(nint device, CommandBufferAllocateInfo* info, out nint buffer);
    [DllImport(Lib)] public static extern int vkBeginCommandBuffer(nint buffer, CommandBufferBeginInfo* info);
    [DllImport(Lib)] public static extern int vkEndCommandBuffer(nint buffer);
    [DllImport(Lib)] public static extern int vkResetCommandBuffer(nint buffer, uint flags);
    [DllImport(Lib)] public static extern void vkCmdPipelineBarrier(nint buffer, uint srcStage, uint dstStage, uint dependency, uint memoryBarrierCount, nint memoryBarriers, uint bufferBarrierCount, nint bufferBarriers, uint imageBarrierCount, ImageMemoryBarrier* imageBarriers);
    [DllImport(Lib)] public static extern int vkQueueSubmit(nint queue, uint count, SubmitInfo* submits, ulong fence);

    [DllImport(Lib)] public static extern int vkCreateFence(nint device, FenceCreateInfo* info, nint allocator, out ulong fence);
    [DllImport(Lib)] public static extern void vkDestroyFence(nint device, ulong fence, nint allocator);
    [DllImport(Lib)] public static extern int vkWaitForFences(nint device, uint count, ulong* fences, uint waitAll, ulong timeout);
    [DllImport(Lib)] public static extern int vkResetFences(nint device, uint count, ulong* fences);
    [DllImport(Lib)] public static extern int vkCreateSemaphore(nint device, SemaphoreCreateInfo* info, nint allocator, out ulong semaphore);
    [DllImport(Lib)] public static extern void vkDestroySemaphore(nint device, ulong semaphore, nint allocator);

    // ---------- fenêtre native Android ----------
    [DllImport("android")] public static extern nint ANativeWindow_fromSurface(nint env, nint surface);
    [DllImport("android")] public static extern void ANativeWindow_release(nint window);

    public static void Check(int result, string call)
    {
        if (result != Success) throw new VulkanException(call, result);
    }
}

/// <summary>Erreur Vulkan, avec le code renvoyé par l'appel.</summary>
public sealed class VulkanException(string call, int result) : Exception($"{call} a échoué ({result}).")
{
    public int Result { get; } = result;
}
#endif
