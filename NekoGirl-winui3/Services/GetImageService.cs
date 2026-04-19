using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace NekoGirl_winui3.Services;

/// <summary>
/// 图片获取服务 - 从nekos.best API获取猫娘图片
/// </summary>
public class GetImageService : IDisposable
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly List<ImageData> _images = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isDisposed;

    public int ImageCount => _images.Count;

    static GetImageService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task GetNextSetAsync(int count = 5)
    {
        if (_isDisposed) return;

        await _semaphore.WaitAsync();
        try
        {
            await FetchMultipleImagesAsync(count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<BitmapImage?> GetImageAsync(int index)
    {
        if (_isDisposed) return null;

        await _semaphore.WaitAsync();
        try
        {
            if (index < 0 || index >= _images.Count)
            {
                Debug.WriteLine($"[跳过] 索引 {index} 无效");
                return null;
            }

            var imageData = _images[index];

            if (imageData.Bitmap != null)
            {
                return imageData.Bitmap;
            }

            try
            {
                Debug.WriteLine($"[加载] {imageData.Url}");

                byte[] imageBytes = await DownloadImageBytesAsync(imageData.Url);
                if (imageBytes.Length == 0)
                {
                    return null;
                }

                imageData.ImageBytes = imageBytes;
                var bitmap = await CreateOptimizedBitmapAsync(imageBytes);
                imageData.Bitmap = bitmap;

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[图片加载失败] {ex.Message}");
                return null;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async Task<BitmapImage> CreateOptimizedBitmapAsync(byte[] imageBytes)
    {
        var bitmap = new BitmapImage
        {
            DecodePixelWidth = 1920,
            DecodePixelType = DecodePixelType.Logical
        };

        using var stream = new MemoryStream(imageBytes);
        var randomAccessStream = stream.AsRandomAccessStream();
        randomAccessStream.Seek(0);
        await bitmap.SetSourceAsync(randomAccessStream);

        return bitmap;
    }

    public byte[]? GetImageBytes(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return null;
        return _images[index].ImageBytes;
    }

    public (string artistName, string artistLink) GetArtistInfo(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return ("", "");
        var data = _images[index];
        return (data.ArtistName, data.ArtistLink);
    }

    public bool IsImageCached(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return false;
        return _images[index].Bitmap != null;
    }

    public void ClearCache()
    {
        _semaphore.Wait();
        try
        {
            foreach (var image in _images)
            {
                image.Dispose();
            }
            _images.Clear();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task FetchMultipleImagesAsync(int count)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                string json = await HttpClient.GetStringAsync($"https://nekos.best/api/v2/neko?amount={count}", cts.Token);

                Debug.WriteLine($"[API响应] {json.Substring(0, Math.Min(200, json.Length))}...");

                using JsonDocument doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");

                foreach (var result in results.EnumerateArray())
                {
                    string? url = result.GetProperty("url").GetString();
                    string? name = result.GetProperty("artist_name").GetString();
                    string? link = result.GetProperty("artist_href").GetString();

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        Debug.WriteLine($"[成功] 获取URL: {url}，作者名:{name}");
                        _images.Add(new ImageData
                        {
                            Url = url,
                            ArtistName = string.IsNullOrEmpty(name) ? "未知画师" : name,
                            ArtistLink = link ?? ""
                        });
                    }
                }

                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Debug.WriteLine($"[403错误] 尝试 {attempt + 1}/3");
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[获取失败] {ex.Message}");
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }
        }
    }

    private async Task<byte[]> DownloadImageBytesAsync(string url, int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                return await HttpClient.GetByteArrayAsync(url, cts.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[下载失败] 尝试 {attempt + 1}/{maxRetries}: {ex.Message}");
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }
        }
        return [];
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        ClearCache();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class ImageData : IDisposable
{
    public string Url { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string ArtistLink { get; set; } = "";
    public byte[] ImageBytes { get; set; } = [];
    public BitmapImage? Bitmap { get; set; }

    public void Dispose()
    {
        Bitmap = null;
        ImageBytes = [];
    }
}
