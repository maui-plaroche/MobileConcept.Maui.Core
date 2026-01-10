# MobileConcept.Maui.Core

Essential core library for .NET MAUI applications. Provides utilities, base classes, and extension methods for cross-platform mobile development on Android and iOS.

## Installation
```bash
dotnet add package MobileConcept.Maui.Core
```

Or via NuGet Package Manager:
```
Install-Package MobileConcept.Maui.Core
```

## Features

- **Image Helpers**: Resize and rotate images with EXIF orientation handling
- **Base Classes**: MVVM foundation classes
- **Extension Methods**: Common utilities for MAUI development
- **Platform Helpers**: Android and iOS specific utilities

## Usage

### Image Rotation and Resizing

The `ImageRotationHelper` class helps you handle images from the camera or photo library, automatically managing EXIF rotation and resizing:
```csharp
using MobileConcept.Maui.Core.Helpers;

private async Task MediaClickAsync(string mediaType)
{
    var options = new MediaPickerOptions { SelectionLimit = 1 };
    
    if (mediaType == "camera")
    {
        var result = await MediaPicker.CapturePhotoAsync(options);
        if (result is not null)
        {
            // Capture and process photo
            var stream = await result.OpenReadAsync();
            var resizedAndRotatedStream = ImageRotationHelper.ResizeProportionalWithExifRotation(
                stream, 
                maxWidth: 400, 
                maxHeight: 400
            );
            
            // Use the processed stream
            // ...
        }
    }
    else if (mediaType == "library")
    {
        var result = await MediaPicker.PickPhotosAsync(options);
        if (result.Count == 1)
        {
            // Select and process photo from library
            var stream = await result[0].OpenReadAsync();
            var resizedAndRotatedStream = ImageRotationHelper.ResizeProportionalWithExifRotation(
                stream, 
                maxWidth: 400, 
                maxHeight: 400
            );
            
            // Use the processed stream
            // ...
        }
    }
}
```

#### Why use ResizeProportionalWithExifRotation?

- **EXIF Rotation**: Automatically reads and applies the correct orientation from image metadata
- **Proportional Resize**: Maintains aspect ratio while fitting within specified dimensions
- **Memory Efficient**: Optimizes image size for mobile devices
- **Cross-Platform**: Works consistently on both Android and iOS

## Requirements

- .NET 9.0 or later
- .NET MAUI 9.0 or later

## Documentation

For more information, visit [Mobile Concept Blog](https://www.mobile-concept.com)

## License

MIT

## Support

For issues and questions, please visit our [GitHub repository](https://github.com/maui-plaroche/MobileConcept.Maui.Core)

---

Part of the **MobileConcept.Maui** suite of packages for .NET MAUI development.