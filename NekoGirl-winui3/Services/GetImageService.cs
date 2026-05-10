using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace NekoGirl_winui3.Services;

/// <summary>
/// 图片获取服务 - 从nekos.best API获取猫娘图片
/// </summary>
public class GetImageService : IDisposable
{
    private static readonly HttpClientHandler HttpClientHandler = new()
    {
        AllowAutoRedirect = true,
        UseCookies = true,
        CookieContainer = new CookieContainer(),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    };

    private static readonly HttpClient HttpClient = new(HttpClientHandler)
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static bool _sessionInitialized;
    private static readonly SemaphoreSlim _sessionLock = new(1, 1);

    private readonly List<ImageData> _images = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _isDisposed;

    public int ImageCount => _images.Count;

    static GetImageService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
        HttpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        HttpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
        HttpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
        HttpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        HttpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Microsoft Edge\";v=\"120\"");
        HttpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
        HttpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        HttpClient.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        HttpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    private static async Task EnsureSessionInitializedAsync()
    {
        if (_sessionInitialized) return;

        await _sessionLock.WaitAsync();
        try
        {
            if (_sessionInitialized) return;

            try
            {
                Debug.WriteLine("[初始化] 正在建立nekos.best会话...");

                using var request = new HttpRequestMessage(HttpMethod.Get, "https://nekos.best/");
                request.Headers.Add("Referer", "https://www.google.com/");
                request.Headers.Add("Origin", "https://nekos.best");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);

                Debug.WriteLine($"[初始化] 会话建立完成，状态码: {response.StatusCode}");

                var cookies = HttpClientHandler.CookieContainer.GetCookies(new Uri("https://nekos.best"));
                foreach (Cookie cookie in cookies)
                {
                    Debug.WriteLine($"[Cookie] {cookie.Name}={cookie.Value}");
                }

                _sessionInitialized = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[初始化警告] 会话建立失败: {ex.Message}，将尝试继续...");
                _sessionInitialized = true;
            }
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    public async Task GetNextSetAsync(int count = 5)
    {
        if (_isDisposed) return;

        await _semaphore.WaitAsync();
        try
        {
            await EnsureSessionInitializedAsync();
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
                    Debug.WriteLine($"[降级] HTTP下载失败，尝试直接从URL加载...");
                    return await LoadImageFromUrlDirectlyAsync(imageData.Url);
                }

                imageData.ImageBytes = imageBytes;
                var bitmap = await CreateOptimizedBitmapAsync(imageBytes);
                imageData.Bitmap = bitmap;

                return bitmap;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[图片加载失败] {ex.Message}");
                Debug.WriteLine($"[降级] 尝试直接从URL加载...");
                return await LoadImageFromUrlDirectlyAsync(imageData.Url);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static async Task<BitmapImage?> LoadImageFromUrlDirectlyAsync(string url)
    {
        try
        {
            var bitmap = new BitmapImage(new Uri(url));
            await Task.Delay(100);
            return bitmap;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[直接加载失败] {ex.Message}");
            return null;
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
                using var request = new HttpRequestMessage(HttpMethod.Get, $"https://nekos.best/api/v2/neko?amount={count}");
                request.Headers.Add("Referer", "https://nekos.best/");
                request.Headers.Add("Origin", "https://nekos.best");
                request.Headers.Add("Accept", "application/json, text/plain, */*");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);

                response.EnsureSuccessStatusCode();

                byte[] jsonBytes = await response.Content.ReadAsByteArrayAsync(cts.Token);
                
                Debug.WriteLine($"[响应大小] {jsonBytes.Length} 字节");
                Debug.WriteLine($"[前20字节] {BitConverter.ToString(jsonBytes.Take(Math.Min(20, jsonBytes.Length)).ToArray())}");
                
                string json = Encoding.UTF8.GetString(jsonBytes);

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
                Debug.WriteLine($"[API 403错误] 尝试 {attempt + 1}/3");
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));

                    await _sessionLock.WaitAsync();
                    try
                    {
                        _sessionInitialized = false;
                    }
                    finally
                    {
                        _sessionLock.Release();
                    }

                    await EnsureSessionInitializedAsync();
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
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
                request.Headers.Add("Referer", "https://nekos.best/");
                request.Headers.Add("Origin", "https://nekos.best");
                request.Headers.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
                request.Headers.Add("Sec-Fetch-Dest", "image");
                request.Headers.Add("Sec-Fetch-Mode", "no-cors");
                request.Headers.Add("Sec-Fetch-Site", "same-origin");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);

                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                Debug.WriteLine($"[图片下载403] 尝试 {attempt + 1}/{maxRetries}: {url}");

                if (attempt == 1)
                {
                    Debug.WriteLine("[重置会话] 尝试重新建立会话...");
                    await _sessionLock.WaitAsync();
                    try
                    {
                        _sessionInitialized = false;
                        HttpClientHandler.CookieContainer = new CookieContainer();
                    }
                    finally
                    {
                        _sessionLock.Release();
                    }
                    await EnsureSessionInitializedAsync();
                }

                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[下载超时] 尝试 {attempt + 1}/{maxRetries} (60秒超时)");
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
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
