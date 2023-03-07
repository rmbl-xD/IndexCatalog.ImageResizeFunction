using System;
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

    public ImageResizeTrigger(BlobServiceClient blobServiceClient, ILogger log)
    {
        _blobServiceClient = blobServiceClient;
        _log = log;
    }

    [FunctionName("ImageResizeTrigger")]
    public async Task RunAsync([BlobTrigger("images/{name}")] Stream blob, string name)
    {
        _log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {blob.Length} Bytes");
        
        //is valid guid as name
        //iterator image resize sizes from configuration
        //resize image 
        //upload image to storage with ending "[Guid]-[width]-[height].[extension]"


        var resizedImage = GetResizedImage(blob);
        await UploadImage(resizedImage);
    }
    
    private byte[] GetResizedImage(Stream myBlob)
    {
        try
        {
            using var image = new MagickImage(myBlob);
            var size = new MagickGeometry(100, 100)
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
     
    private async Task UploadImage(byte[] resizedImage)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient("images");
            var blobInstance = container.GetBlobClient("test1kleiner.png");
            using var stream = new MemoryStream(resizedImage);
            await blobInstance.UploadAsync(stream);
        }
        catch (Exception e)
        {
            _log.LogError("Error Uploading Image: " + e.Message);
        }
    }
}