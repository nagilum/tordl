namespace TorDl.Extensions;

public static class StreamExtensions
{
    /// <summary>
    /// Copy stream content to another stream, with progress.
    /// </summary>
    /// <param name="source">Source stream.</param>
    /// <param name="destination">Destination stream.</param>
    /// <param name="bufferSize">Buffer size.</param>
    /// <param name="progress">Progress indicator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown if either source or destination stream cannot be read from or written too.</exception>
    public static async Task CopyToAsync(
        this Stream source,
        Stream destination,
        int bufferSize,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferSize);

        if (!source.CanRead)
        {
            throw new ArgumentException("Has to be readable", nameof(source));
        }

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Has to be writable", nameof(destination));
        }

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }
}