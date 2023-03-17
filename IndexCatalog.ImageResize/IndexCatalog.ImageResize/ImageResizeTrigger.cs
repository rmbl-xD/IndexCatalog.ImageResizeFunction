using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace IndexCatalog.ImageResize;

public class ImageResizeTrigger
{
    private readonly ILogger _log;
    private const string UploadFolderName = "images";

    private static readonly List<Tuple<int, int>> ResolutionList = new () {};

    public ImageResizeTrigger(ILoggerFactory log)
    {
        _log = log.CreateLogger<ImageResizeTrigger>();
        TryGetResolutionsFromConfig();
    }

    private static void TryGetResolutionsFromConfig()
    {
        var resizeResolutions = Environment.GetEnvironmentVariable("ResizeResolutions");
        if (string.IsNullOrEmpty(resizeResolutions)) return;
        
        ResolutionList.Clear();
        var resolutionsList = resizeResolutions.Split(';');
        foreach (var resolution in resolutionsList)
        {
            var resolutions = resolution.Split(',');

            var couldParseWidth = int.TryParse(resolutions[0], out var width);
            var couldParseHeight = int.TryParse(resolutions[1], out var height);

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
        _log.LogInformation($"ImageResizeTrigger function processed blob\n Name: {name} \n in Folder: images/{subfolder} \n Size: {blob.Length} Bytes");
        
        if (ResolutionList.Count == 0)
        {
            _log.LogInformation($"Output resolution list is not set or empty. Please configure resolution list in appSettings. e.g. \"ResizeResolutions\": \"1920,1080;1024,768;800,600;512,384;384,216\"");
            return;
        }
        
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
                        $"{UploadFolderName}/{subfolder}/{fileNameWithoutExtension}.{width}-{height}.{fileExtension}",
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