using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace StockTracker
{
    /// <summary>
    /// 网络请求辅助类 - 处理SSL连接和重试逻辑
    /// </summary>
    public static class NetworkHelper
    {
        /// <summary>
        /// 创建支持SSL的HttpClient
        /// </summary>
        public static HttpClient CreateSSLHttpClient(int timeoutSeconds = 30)
        {
            var handler = new HttpClientHandler();

            try
            {
                // 忽略SSL证书验证，避免因本地代理/防火墙/系统证书问题导致连接失败
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

                // 启用自动解压缩：服务端返回 gzip/deflate 时自动解压，避免读到乱码
                handler.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;

                handler.AllowAutoRedirect = true;
                handler.MaxAutomaticRedirections = 10;
            }
            catch (Exception ex)
            {
                Program.LogError("HttpClient SSL/解压配置失败", ex);
            }

            var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

            // 设置默认请求头
            try
            {
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Add("Accept", "application/json, */*");
                // 注意：不要手动设置 Accept-Encoding，AutomaticDecompression 会自动管理
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
            }
            catch (Exception ex)
            {
                Program.LogError("HttpClient请求头设置失败", ex);
            }

            return client;
        }

        /// <summary>
        /// 带重试的HTTP请求
        /// </summary>
        public static async Task<string> HttpPostWithRetryAsync(
            string url,
            string content,
            int maxRetries = 3,
            int timeoutSeconds = 30)
        {
            Exception? lastException = null;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    using var client = CreateSSLHttpClient(timeoutSeconds);
                    var contentObj = new StringContent(content, System.Text.Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(url, contentObj);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return responseString;
                    }
                    else
                    {
                        lastException = new HttpRequestException(
                            $"HTTP {response.StatusCode}: {responseString}");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    lastException = httpEx;

                    // 如果是SSL错误，等待后重试
                    if (httpEx.Message.Contains("SSL") || httpEx.Message.Contains("connection"))
                    {
                        if (retry < maxRetries - 1)
                        {
                            int waitTime = (retry + 1) * 2000; // 递增等待时间
                            await Task.Delay(waitTime);
                            continue;
                        }
                    }
                }
                catch (TaskCanceledException timeoutEx)
                {
                    lastException = timeoutEx;
                    // 超时重试
                    if (retry < maxRetries - 1)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break; // 其他错误不重试
                }
            }

            throw new HttpRequestException(
                $"HTTP请求失败，已重试{maxRetries}次。最后错误: {lastException?.Message}",
                lastException);
        }

        /// <summary>
        /// 带重试的HTTP GET请求
        /// </summary>
        public static async Task<string> HttpGetWithRetryAsync(
            string url,
            int maxRetries = 3,
            int timeoutSeconds = 30)
        {
            Exception? lastException = null;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    using var client = CreateSSLHttpClient(timeoutSeconds);

                    var response = await client.GetAsync(url);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        return responseString;
                    }
                    else
                    {
                        lastException = new HttpRequestException(
                            $"HTTP {response.StatusCode}: {responseString}");
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    lastException = httpEx;

                    if (httpEx.Message.Contains("SSL") || httpEx.Message.Contains("connection"))
                    {
                        if (retry < maxRetries - 1)
                        {
                            int waitTime = (retry + 1) * 2000;
                            await Task.Delay(waitTime);
                            continue;
                        }
                    }
                }
                catch (TaskCanceledException timeoutEx)
                {
                    lastException = timeoutEx;
                    if (retry < maxRetries - 1)
                    {
                        await Task.Delay(1000);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }

            throw new HttpRequestException(
                $"HTTP GET请求失败，已重试{maxRetries}次。最后错误: {lastException?.Message}",
                lastException);
        }

        /// <summary>
        /// 测试网络连接
        /// </summary>
        public static async Task<bool> TestNetworkConnectionAsync()
        {
            try
            {
                using var client = CreateSSLHttpClient(10);
                var response = await client.GetAsync("https://www.google.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 测试API连接
        /// </summary>
        public static async Task<bool> TestGeminiConnectionAsync(string apiKey)
        {
            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}";
                using var client = CreateSSLHttpClient(15);
                var response = await client.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}