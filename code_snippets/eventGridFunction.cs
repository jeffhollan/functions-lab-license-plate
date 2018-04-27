// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGridExtensionConfig?functionName={functionname}
  
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Text;
using Microsoft.Azure.EventGrid;
using System.Collections.Generic;
using System.Net.Http;
  
namespace FunctionApp1
{
    public static class Function1
    {
        private static string visionApiUrl = Environment.GetEnvironmentVariable("visionApiUrl");
        private static string visionApiKey = Environment.GetEnvironmentVariable("visionApiKey");
        private static string eventGridUrl = Environment.GetEnvironmentVariable("eventGridUrl");
        private static EventGridClient eventGridClient = new EventGridClient(new TopicCredentials(Environment.GetEnvironmentVariable("eventGridKey")));
        private static HttpClient client = new HttpClient();
  
        [FunctionName("Analyze")]
        public static async Task RunAsync(
            [EventGridTrigger]EventGridEvent eventGridEvent,
            [Blob("{data.url}", FileAccess.Read)] Stream plate,
            [Blob("{data.url}-result.txt", FileAccess.Write)] Stream outBlob,
            TraceWriter log)
        {
            log.Info(eventGridEvent.Data.ToString());
  
            // Cast event data to a Storage Blob Created Event
            StorageBlobCreatedEventData data = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
  
            log.Info($"About to analyze image: {data.Url}");
            // Try to analyze the text using Cognitive Services
            var result = await TryAnalyzeAsync(plate);
  
            log.Info($"Found text: {result.gotText} with text: {result.plate}");
            // If the text was parsed
            if(result.gotText)
            {
                log.Info("Writing results.txt into storage");
                new MemoryStream(Encoding.UTF8.GetBytes(result.plate)).CopyTo(outBlob);
            }
            // Else (unable to parse text)
            else
            {
                log.Info("Sending event to event grid");
                // Send an event to Azure Event Grid
                await EmitEventAsync(new EventGridEvent()
                {
                    Id = Guid.NewGuid().ToString(),
                    Subject = $"LicensePlate{eventGridEvent.Subject}",
                    EventType = "PlateNotRead",
                    EventTime = DateTime.Now,
                    Data = new { url = data.Url },
                    DataVersion = "1.0"
                });
  
                log.Info("Writing results.txt into storage");
                new MemoryStream(Encoding.UTF8.GetBytes("NOT FOUND - event sent")).CopyTo(outBlob);
            }
        }
  
        /// <summary>
        /// Try to analyze the image and look for the license plate. Will call cognitive services and look in a specific spot in result for text.
        /// If text isn't in the specific spot or no text returned will return false.
        /// </summary>
        /// <param name="image">Stream of the image to analyze</param>
        /// <returns>gotText: boolean if this got text or not -- plate: string for the plate text if retrieved</returns>
        private static async Task<(bool gotText, string plate)> TryAnalyzeAsync(Stream image)
        {
            // Send a request to Azure Cognitive Services
            var request = new HttpRequestMessage(HttpMethod.Post, visionApiUrl + "/ocr");
            request.Headers.Add("Ocp-Apim-Subscription-Key", visionApiKey);
            request.Content = new StreamContent(image);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            var response = await client.SendAsync(request);
  
            // If the call to cognitive worked
            if(response.IsSuccessStatusCode)
            {
                try
                {
                    // Try to pull out the detected plate
                    JObject resultObject = await response.Content.ReadAsAsync<JObject>();
                    string plate = (string)resultObject["regions"][0]["lines"][1]["words"][0]["text"];
                    return (gotText: true, plate: plate);
                }
                catch (Exception ex)
                {
                    // OCR result didn't have text where we expected
                    return (gotText: false, null);
                }
            }
            return (gotText: false, null);
        }
  
        /// <summary>
        /// Send an event to Azure Event Grid
        /// </summary>
        /// <param name="event">The event grid message to send</param>
        /// <returns></returns>
        private static async Task EmitEventAsync(EventGridEvent @event)
        {
            await eventGridClient.PublishEventsAsync(new Uri(eventGridUrl).Host, new List<EventGridEvent>() { @event });
        }
    }
}

