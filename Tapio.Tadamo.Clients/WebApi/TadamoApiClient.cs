using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Identity.Client;
using Newtonsoft.Json;

namespace Tapio.Tadamo.Clients.WebApi;

[SuppressMessage("Major Code Smell", "S1172:Unused method parameters should be removed", Justification = "Method needs parameters because its a partial implementation.")]
public partial class TadamoApiClient
{

    private readonly TadamoApiClientConfig _Config;
    private readonly IConfidentialClientApplication _App;

    public TadamoApiClient(HttpClient httpClient, TadamoApiClientConfig config)
    {
        _Config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-version", "v1");
        _App = ConfidentialClientApplicationBuilder.Create(_Config.TestAppClientId)
            .WithClientSecret(_Config.TestAppClientSecret)
            .WithAuthority(_Config.Authority)
            .Build();
        _baseUrl = _Config.BaseUrl;
        _settings = new Lazy<JsonSerializerSettings>(() => new JsonSerializerSettings());
    }

    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, string url)
    {
        InjectBearerToken(request);
    }

    partial void PrepareRequest(HttpClient client, HttpRequestMessage request, StringBuilder urlBuilder)
    {
        InjectBearerToken(request);
    }

    private void InjectBearerToken(HttpRequestMessage request)
    {
        var authenticationResult = _App.AcquireTokenForClient(new[] { $"{_Config.ResourceUrl}/.default" })
            .ExecuteAsync().GetAwaiter().GetResult();
        request.Headers.Authorization = new AuthenticationHeaderValue(
            scheme: "Bearer",
            parameter: authenticationResult.AccessToken);
    }

    /// <inheritdoc />
    public async Task<Stream> GetInstanceDataSchemaContentAsync(Guid typeId, string version, CancellationToken cancellationToken)
    {
        var schemaResult = await GetSystemTypeSchemasAsync(typeId, version, cancellationToken);
        var schemaUrl = schemaResult.Data.FirstOrDefault()?.InstanceDataSchemas.FirstOrDefault()?.Url;

        if (schemaUrl is null)
        {
            throw new TadamoApiException($"Instance data schema url for type '{typeId}' and version '{version}' was not found.", (int)HttpStatusCode.NotFound, null, null, null);
        }

        using var client = new HttpClient();

        var response = await client.GetAsync(schemaUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        return stream;
    }

    /// <inheritdoc />
    public async Task<Stream> GetMasterDataSchemaContentAsync(Guid typeId, string version, CancellationToken cancellationToken)
    {
        var schemaResult = await GetSystemTypeSchemasAsync(typeId, version, cancellationToken);
        var schemaUrl = schemaResult.Data.FirstOrDefault()?.InstanceDataSchemas.FirstOrDefault()?.Url;

        if (schemaUrl is null)
        {
            throw new TadamoApiException($"Instance data schema url for type '{typeId}' and version '{version}' was not found.", (int)HttpStatusCode.NotFound, null, null, null);
        }

        var response = await _httpClient.GetAsync(schemaUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();
        return stream;
    }

    /// <inheritdoc />
    public async Task<XmlDocument> GetEtmlInstanceByIdXmlAsync(Guid subscriptionId, Guid id, CancellationToken cancellationToken)
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/etml/subscriptions/{subscriptionId}/instancedata/{instanceId}");
        urlBuilder.Replace("{subscriptionId}", Uri.EscapeDataString(ConvertToString(subscriptionId, System.Globalization.CultureInfo.InvariantCulture)));
        urlBuilder.Replace("{instanceId}", Uri.EscapeDataString(ConvertToString(id, System.Globalization.CultureInfo.InvariantCulture)));

        var client = _httpClient;
        using var request = new HttpRequestMessage();

        request.Method = new HttpMethod("GET");
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/xml"));

        PrepareRequest(client, request, urlBuilder);

        var url = urlBuilder.ToString();
        request.RequestUri = new Uri(url, UriKind.RelativeOrAbsolute);

        PrepareRequest(client, request, url);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var headers = response.Headers.ToDictionary(h => h.Key, h => h.Value);

        foreach (var item in response.Content.Headers)
        {
            headers[item.Key] = item.Value;
        }

        var status = (int)response.StatusCode;
        if (status == 200)
        {
            var instanceAsXml = new XmlDocument();
            instanceAsXml.Load(await response.Content.ReadAsStreamAsync());
            return instanceAsXml;
        }

        var objectResponse = await ReadObjectResponseAsync<ErrorInformation>(response, headers, cancellationToken);
        if (objectResponse.Object == null)
        {
            throw new TadamoApiException("Response was null which was not expected.", status, objectResponse.Text, headers, null);
        }
        throw new TadamoApiException<ErrorInformation>("Error", status, objectResponse.Text, headers, objectResponse.Object, null);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateEtmlInstanceAsync(Guid subscriptionId, XmlDocument body, CancellationToken cancellationToken)
    {
        if (body == null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        var urlBuilder = new StringBuilder();
        urlBuilder.Append(BaseUrl != null ? BaseUrl.TrimEnd('/') : "").Append("/etml/subscriptions/{subscriptionId}/instancedata");
        urlBuilder.Replace("{subscriptionId}", Uri.EscapeDataString(ConvertToString(subscriptionId, System.Globalization.CultureInfo.InvariantCulture)));

        var client = _httpClient;
        using var request = new HttpRequestMessage();

        var content = new StringContent(body.OuterXml);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");
        request.Content = content;
        request.Method = new HttpMethod("POST");
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/xml"));

        PrepareRequest(client, request, urlBuilder);

        var url = urlBuilder.ToString();
        request.RequestUri = new Uri(url, UriKind.RelativeOrAbsolute);

        PrepareRequest(client, request, url);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            var headers = response.Headers.ToDictionary(h => h.Key, h => h.Value);
            if (response.Content is { Headers: { } })
            {
                foreach (var item in response.Content.Headers)
                    headers[item.Key] = item.Value;
            }

            var status = (int)response.StatusCode;
            switch (status)
            {
                case 201:
                {
                    var objectResponse = await ReadObjectResponseAsync<Guid>(response, headers, cancellationToken).ConfigureAwait(false);
                    return objectResponse.Object;
                }
                case 401:
                {
                    var responseText = response.Content == null
                        ? string.Empty
                        : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new TadamoApiException("Unauthorized", status, responseText, headers, null);
                }
                default:
                {
                    if (response is { Content: { } })
                    {
                        var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var doc = XDocument.Parse(responseText);
                        var message = doc.Root?.Element("Message")?.Value;

                        throw new TadamoApiException("Error", status, message, headers, null);
                    }

                    throw new TadamoApiException("Response was null which was not expected.", status, string.Empty, headers, null);
                }
            }
        }
        finally
        {
            response.Dispose();
        }
    }
}