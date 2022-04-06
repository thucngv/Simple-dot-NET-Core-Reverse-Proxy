namespace ReverseProxy
{
    public class ReverseProxyMiddleware
    {
        private readonly HttpClient _httpClient;
        private readonly RequestDelegate _next;

        public ReverseProxyMiddleware(RequestDelegate next)
        {
            _httpClient = new HttpClient();
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var request = context.Request;
            var targetUri = new Uri($"{request.Scheme}://{request.Host}{request.Path}{(!string.IsNullOrWhiteSpace(request.QueryString.Value) ? "?" + request.QueryString.Value : string.Empty)}");

            if (targetUri != null)
            {
                var targetRequestMessage = CreateTargetMessage(context, targetUri);
                try
                {
                    using (var responseMessage = await _httpClient.SendAsync(targetRequestMessage, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted))
                    {
                        context.Response.StatusCode = (int)responseMessage.StatusCode;
                        //copy from target response headers
                        foreach (var header in responseMessage.Headers)
                            context.Response.Headers[header.Key] = header.Value.ToArray();

                        foreach (var header in responseMessage.Content.Headers)
                            context.Response.Headers[header.Key] = header.Value.ToArray();

                        context.Response.Headers.Remove("transfer-encoding");
                        await responseMessage.Content.CopyToAsync(context.Response.Body);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            await _next(context);
        }

        private HttpRequestMessage CreateTargetMessage(HttpContext context, Uri targetUri)
        {
            var requestMethod = context.Request.Method;
            var requestMessage = new HttpRequestMessage();

            //copy from original request content and headers
            if (!HttpMethods.IsGet(requestMethod) &&
              !HttpMethods.IsHead(requestMethod) &&
              !HttpMethods.IsDelete(requestMethod) &&
              !HttpMethods.IsTrace(requestMethod))
                requestMessage.Content = new StreamContent(context.Request.Body);

            foreach (var header in context.Request.Headers)
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());

            requestMessage.RequestUri = targetUri;
            requestMessage.Headers.Host = targetUri.Host;
            requestMessage.Method = GetMethod(requestMethod);

            return requestMessage;
        }

        private HttpMethod GetMethod(string method)
        {
            if (HttpMethods.IsDelete(method)) return HttpMethod.Delete;
            if (HttpMethods.IsGet(method)) return HttpMethod.Get;
            if (HttpMethods.IsHead(method)) return HttpMethod.Head;
            if (HttpMethods.IsOptions(method)) return HttpMethod.Options;
            if (HttpMethods.IsPost(method)) return HttpMethod.Post;
            if (HttpMethods.IsPut(method)) return HttpMethod.Put;
            if (HttpMethods.IsTrace(method)) return HttpMethod.Trace;
            return new HttpMethod(method);
        }
    }
}
