using System;
using System.Collections.Generic;
using System.IO;
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
        new (1920, 1080),
        new (1024,768),
        new (800,600),
        new (512,384),
        new (384,216)
    };

    public ImageResizeTrigger(BlobServiceClient blobServiceClient, ILoggerFactory log)
    {
        _blobServiceClient = blobServiceClient;
        _log = log.CreateLogger<ImageResizeTrigger>();
    }

    [FunctionName("ImageResizeTrigger")]
    public async Task RunAsync([BlobTrigger("images/{subfolder}/original-{name}")] Stream blob, string name, string subfolder)
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

                //upload image to storage with ending "[Guid]-[width]-[height].[extension]"
                if (resizedImage.Length != 0)
                {
                    await UploadImage(resizedImage, subfolder ,$"{fileNameWithoutExtension}.{width}-{height}", fileExtension);
                }
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
     
    private async Task UploadImage(byte[] resizedImage, string subfolder, string name, string fileExtension)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(UploadFolderName);
            var blobInstance = container.GetBlobClient($"{subfolder}/{name}{fileExtension}");
           
            using var stream = new MemoryStream(resizedImage);
            var response = await blobInstance.UploadAsync(stream);
        }
        catch (Exception e)
        {
            _log.LogError("Error Uploading Image: " + e.Message);
        }
    }
}