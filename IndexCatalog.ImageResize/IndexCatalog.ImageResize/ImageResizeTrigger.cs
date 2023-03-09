using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using ImageMagick;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace IndexCatalog.ImageResize;

public class ImageResizeTrigger
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger _log;
    private const string UploadFolderName = "images";

    private static readonly List<Tuple<int, int>> ResolutionList = new ()
    {
        new (512,384)
    };

    public ImageResizeTrigger(ILoggerFactory log)
    {
        _log = log.CreateLogger<ImageResizeTrigger>();
        TryGetResolutionsFromConfig();
    }

    private static void TryGetResolutionsFromConfig()
    {
        var resolutions = Environment.GetEnvironmentVariable("ResizeResolutions");
        if (string.IsNullOrEmpty(resolutions)) return;
        
        ResolutionList.Clear();
        var resolutionsList = resolutions.Split(';');
        foreach (var resolution in resolutionsList)
        {
            var splittedResolution = resolution.Split(',');

            var couldParseWidth = Int32.TryParse(splittedResolution[0], out var width);
            var couldParseHeight = Int32.TryParse(splittedResolution[1], out var height);

            if (couldParseWidth && couldParseHeight)
            {
                ResolutionList.Add(new Tuple<int, int>(width, height));
            }
        }
    }

    [FunctionName("ImageResizeTrigger")]
    public async Task RunAsync(
        [BlobTrigger("images/{subfolder}/original-{name}")] Stream blob, 
        string name, 
        string subfolder,
        Binder binder)
    {
        _log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n in Folder:{subfolder} \n Size: {blob.Length} Bytes");

        var fileExtension = Path.GetExtension(name);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(name);

        //is valid guid as name
        if (Guid.TryParse(fileNameWithoutExtension, out _))
        {
            foreach (var resolution in ResolutionList)
            {
                blob.Position = 0;
                var (width, height) = resolution;
                var resizedImage = GetResizedImage(blob, width , height);
                
                if (resizedImage.Length == 0) continue;

                //upload image to storage with ending "[Guid]-[width]-[height].[extension]"
                var blobAttribute =
                    new BlobAttribute(
                        $"images/{subfolder}/{fileNameWithoutExtension}.{width}-{height}.{fileExtension}",
                        FileAccess.Write);

                await using var output = await binder.BindAsync<Stream>(blobAttribute);
                using var stream = new MemoryStream(resizedImage);
                await stream.CopyToAsync(output);
            }
        }
    }
    
    private byte[] GetResizedImage(Stream myBlob, int width, int height)
    {
        try
        {
            using var image = new MagickImage(myBlob);
            var size = new MagickGeometry(width, height)
            {
                IgnoreAspectRatio = false
            };

            image.Resize(size);
            return image.ToByteArray();
        }
        catch (Exception e)
        {
            _log.LogError("Error Resizing Image: " + e.Message);
        }

        return Array.Empty<byte>();
    }
}