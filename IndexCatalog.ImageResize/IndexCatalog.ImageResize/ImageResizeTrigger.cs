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

    public ImageResizeTrigger(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    [FunctionName("ImageResizeTrigger")]
    public async Task RunAsync([BlobTrigger("images/{name}")] Stream blob, string name, ILogger log)
    {
        log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {blob.Length} Bytes");
        
        var resizedImage = GetResizedImage(blob, log);
        await UploadImage(log, resizedImage);
    }
    
    private byte[] GetResizedImage(Stream myBlob, ILogger log)
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
            log.LogError("Error Resizing Image: " + e.Message);
        }

        return Array.Empty<byte>();
    }
     
    private async Task UploadImage(ILogger log, byte[] resizedImage)
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
            log.LogError("Error Uploading Image: " + e.Message);
        }
    }
}