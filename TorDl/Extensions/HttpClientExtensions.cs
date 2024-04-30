namespace TorDl.Extensions;

public static class HttpClientExtensions
{
    /// <summary>
    /// Trigger a download of a URL.
    /// </summary>
    /// <param name="client">HTTP client.</param>
    /// <param name="requestUri">URL to request.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="progress">Progress indicator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DownloadAsync(
        this HttpClient client,
        Uri requestUri,
        Stream destination,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            Program.Log.Information(
                "Response: {code} {description}",
                (int)response.StatusCode,
                response.StatusCode);
        }
        else
        {
            Program.Log.Error(
                "Response: {code} {description}",
                (int)response.StatusCode,
                response.StatusCode);

            return;
        }
        
        var contentLength = response.Content.Headers.ContentLength;

        await using var download = await response.Content.ReadAsStreamAsync(cancellationToken);
        
        if (progress == null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken);
            return;
        }

        var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
        
        await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken);
        progress.Report(1);
    }
}