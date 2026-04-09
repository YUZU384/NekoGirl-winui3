using Microsoft.UI.Xaml.Media.Imaging;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace NekoGirl_winui3.Services;

/// <summary>
/// GetImageService类 - 图片获取服务
/// 功能: 从nekos.best API获取猫娘图片数据，管理图片缓存
/// 实现: IDisposable接口，确保资源正确释放
/// </summary>
public class GetImageService : IDisposable
{
    // ============================================
    // 静态字段
    // ============================================
    
    /// <summary>
    /// 共享HttpClient实例
    /// 用途: 所有HTTP请求使用同一客户端，支持连接复用
    /// 超时设置: 30秒
    /// 静态构造: 设置User-Agent请求头
    /// </summary>
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    // ============================================
    // 实例字段
    // ============================================
    
    /// <summary>
    /// 图片数据列表
    /// 用途: 存储从API获取的图片元数据和缓存的位图
    /// 类型: List&lt;ImageData&gt;
    /// </summary>
    private readonly List<ImageData> _images = [];
    
    /// <summary>
    /// 信号量，用于线程同步
    /// 用途: 保护_images列表，防止并发访问冲突
    /// 初始计数: 1 (允许1个线程同时访问)
    /// </summary>
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    /// <summary>
    ///  disposed标志，防止重复释放资源
    /// </summary>
    private bool _isDisposed;

    // ============================================
    // 属性
    // ============================================
    
    /// <summary>
    /// 当前缓存的图片数量
    /// 用途: 供外部查询可用图片数量
    /// </summary>
    public int ImageCount => _images.Count;

    // ============================================
    // 静态构造函数
    // ============================================
    
    /// <summary>
    /// 静态构造函数 - 初始化HttpClient默认请求头
    /// 设置User-Agent模拟浏览器请求，避免被API拒绝
    /// </summary>
    static GetImageService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    // ============================================
    // 公共方法
    // ============================================
    
    /// <summary>
    /// 获取下一批图片
    /// 参数: count - 获取图片数量，默认5张
    /// 线程安全: 使用_semaphore保护
    /// 调用链: GetNextSetAsync -> FetchMultipleImagesAsync
    /// </summary>
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

    /// <summary>
    /// 获取指定索引的图片位图 (性能优化版本)
    /// 参数: index - 图片索引
    /// 返回: BitmapImage? - 位图对象，失败返回null
    /// 缓存逻辑: 若已缓存直接返回，否则下载并缓存
    /// 线程安全: 使用_semaphore保护
    /// 优化点:
    ///   - 限制解码尺寸避免内存溢出
    ///   - 使用高质量插值模式
    ///   - 异步解码避免阻塞UI线程
    /// </summary>
    public async Task<BitmapImage?> GetImageAsync(int index)
    {
        if (_isDisposed) return null;

        await _semaphore.WaitAsync();
        try
        {
            // 验证索引有效性
            if (index < 0 || index >= _images.Count)
            {
                Debug.WriteLine($"[跳过] 索引 {index} 无效");
                return null;
            }

            var imageData = _images[index];

            // 若已缓存位图，直接返回
            if (imageData.Bitmap != null)
            {
                return imageData.Bitmap;
            }

            try
            {
                Debug.WriteLine($"[加载] {imageData.Url}");

                // 下载图片字节数据
                byte[] imageBytes = await DownloadImageBytesAsync(imageData.Url);
                if (imageBytes.Length == 0)
                {
                    return null;
                }

                // 缓存字节数据
                imageData.ImageBytes = imageBytes;

                // 创建BitmapImage (性能优化版本)
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

    /// <summary>
    /// 创建优化的BitmapImage
    /// 参数: imageBytes - 图片字节数据
    /// 返回: BitmapImage - 优化后的位图
    /// 优化策略:
    ///   - DecodePixelWidth: 限制解码宽度为1920px，减少内存占用
    ///   - 保持原始宽高比
    ///   - 异步解码避免阻塞UI
    /// </summary>
    private static async Task<BitmapImage> CreateOptimizedBitmapAsync(byte[] imageBytes)
    {
        var bitmap = new BitmapImage
        {
            // 限制解码尺寸，避免大图片占用过多内存
            // 1920px宽度足够在窗口中清晰显示
            DecodePixelWidth = 1920,
            // 保持原始宽高比
            DecodePixelType = DecodePixelType.Logical
        };

        using var stream = new MemoryStream(imageBytes);
        var randomAccessStream = stream.AsRandomAccessStream();
        
        // 设置流位置到开头
        randomAccessStream.Seek(0);
        
        // 异步设置源，避免阻塞UI线程
        await bitmap.SetSourceAsync(randomAccessStream);
        
        return bitmap;
    }

    /// <summary>
    /// 获取指定索引图片的字节数据
    /// 参数: index - 图片索引
    /// 返回: byte[]? - 图片字节数组，无效返回null
    /// 用途: 保存图片时避免重新下载
    /// </summary>
    public byte[]? GetImageBytes(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return null;
        return _images[index].ImageBytes;
    }

    /// <summary>
    /// 获取指定索引图片的画师名称
    /// 参数: index - 图片索引
    /// 返回: string - 画师名称，无效返回"未知画师"
    /// </summary>
    public string GetImageArtist(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return "未知画师";
        return _images[index].ArtistName;
    }

    /// <summary>
    /// 获取指定索引图片的画师链接
    /// 参数: index - 图片索引
    /// 返回: string - 画师主页链接，无效返回空字符串
    /// </summary>
    public string GetArtistLink(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return "";
        return _images[index].ArtistLink;
    }

    /// <summary>
    /// 获取指定索引图片的URL
    /// 参数: index - 图片索引
    /// 返回: string? - 图片URL，无效返回null
    /// </summary>
    public string? GetImageUrl(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return null;
        return _images[index].Url;
    }

    /// <summary>
    /// 获取指定索引图片的画师信息
    /// 参数: index - 图片索引
    /// 返回: (artistName, artistLink) - 画师名称和链接元组
    /// </summary>
    public (string artistName, string artistLink) GetArtistInfo(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return ("", "");
        var data = _images[index];
        return (data.ArtistName, data.ArtistLink);
    }

    /// <summary>
    /// 检查指定索引的图片是否已缓存
    /// 参数: index - 图片索引
    /// 返回: bool - true表示已缓存，false表示未缓存
    /// </summary>
    public bool IsImageCached(int index)
    {
        if (_isDisposed || index < 0 || index >= _images.Count)
            return false;
        return _images[index].Bitmap != null;
    }

    /// <summary>
    /// 清空图片缓存
    /// 线程安全: 使用_semaphore保护
    /// 操作: 释放所有ImageData资源，清空列表
    /// </summary>
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

    // ============================================
    // 私有方法
    // ============================================
    
    /// <summary>
    /// 从API获取多张图片
    /// 参数: count - 获取数量
    /// 重试机制: 最多3次尝试，403错误等待1秒，其他错误等待500毫秒
    /// API端点: https://nekos.best/api/v2/neko?amount={count}
    /// </summary>
    private async Task FetchMultipleImagesAsync(int count)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // 创建20秒超时的取消令牌
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                // 调用nekos.best API获取多张图片
                string json = await HttpClient.GetStringAsync($"https://nekos.best/api/v2/neko?amount={count}", cts.Token);

                Debug.WriteLine($"[API响应] {json.Substring(0, Math.Min(200, json.Length))}...");

                // 解析JSON响应
                using JsonDocument doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");

                // 遍历结果，提取图片信息
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

                // 成功获取，跳出重试循环
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                // 403错误处理，等待后重试
                Debug.WriteLine($"[403错误] 尝试 {attempt + 1}/3");
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception ex)
            {
                // 其他错误处理
                Debug.WriteLine($"[获取失败] {ex.Message}");
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }
        }
    }

    /// <summary>
    /// 下载图片字节数据
    /// 参数: 
    ///   - url - 图片URL
    ///   - maxRetries - 最大重试次数，默认3次
    /// 返回: byte[] - 图片字节数组，失败返回空数组
    /// 超时: 每次请求20秒
    /// </summary>
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

    // ============================================
    // IDisposable实现
    // ============================================
    
    /// <summary>
    /// 释放资源
    /// 操作: 清空缓存，释放信号量，标记disposed状态
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        ClearCache();
        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// ImageData类 - 图片数据实体
/// 用途: 存储单张图片的元数据和缓存数据
/// 实现: IDisposable接口，释放位图引用
/// </summary>
public class ImageData : IDisposable
{
    /// <summary>
    /// 图片URL地址
    /// 来源: nekos.best API
    /// </summary>
    public string Url { get; set; } = "";
    
    /// <summary>
    /// 画师名称
    /// 默认值: "未知画师"
    /// </summary>
    public string ArtistName { get; set; } = "";
    
    /// <summary>
    /// 画师主页链接
    /// </summary>
    public string ArtistLink { get; set; } = "";
    
    /// <summary>
    /// 图片字节数据缓存
    /// 用途: 保存图片时直接使用，避免重新下载
    /// </summary>
    public byte[] ImageBytes { get; set; } = [];
    
    /// <summary>
    /// 位图对象缓存
    /// 用途: UI显示
    /// 生命周期: 延迟加载，首次显示时创建
    /// </summary>
    public BitmapImage? Bitmap { get; set; }

    /// <summary>
    /// 释放资源
    /// 操作: 清除位图引用，清空字节数组
    /// </summary>
    public void Dispose()
    {
        Bitmap = null;
        ImageBytes = [];
    }
}
