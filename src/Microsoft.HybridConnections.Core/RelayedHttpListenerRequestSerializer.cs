﻿using Microsoft.Azure.Relay;
using Microsoft.HybridConnections.Core.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.HybridConnections.Core
{
    public static class RelayedHttpListenerRequestSerializer
    {
        /// <summary>
        /// Serialize RelayedHttpListenerRequest
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<string> SerializeAsync(RelayedHttpListenerRequest request)
        {
            var requestMessage = new RequestMessage
            {
                Content = await (new StreamContent(request.InputStream)).ReadAsByteArrayAsync(),
                HttpMethod = request.HttpMethod,
                RemoteEndPoint = request.RemoteEndPoint.Address.ToString(),
                Url = request.Url.AbsoluteUri,
                HybridConnectionScheme = request.Url.Scheme,
                HybridConnectionName = request.Url.Segments[1].Trim('/')
            };

            requestMessage.Headers = requestMessage.Headers ?? new List<KeyValuePair<string, IEnumerable<string>>>();
            foreach (var header in request.Headers.GetHeaders())
            {
                ((List<KeyValuePair<string, IEnumerable<string>>>)requestMessage.Headers)
                        .Add(new KeyValuePair<string, IEnumerable<string>>(header.Key, new List<string> { header.Value }));

            }

            return JsonConvert.SerializeObject(requestMessage);
        }

        /// <summary>
        /// Serialize HttpRequestMessage
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public static async Task<string> SerializeAsync(HttpRequestMessage request)
        {
            var requestMessage = new RequestMessage
            {
                Content = await request.Content.ReadAsByteArrayAsync(),
                HttpMethod = request.Method.Method,
                RemoteEndPoint = request.RequestUri.ToString(),
                Url = request.RequestUri.ToString()
            };

            // populate Headers
            foreach (var header in request.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    // Don't flow these headers here
                    continue;
                }
                requestMessage.Headers = requestMessage.Headers ?? new List<KeyValuePair<string, IEnumerable<string>>>();
                ((List<KeyValuePair<string, IEnumerable<string>>>)requestMessage.Headers)
                    .Add(new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));
            }

            return JsonConvert.SerializeObject(requestMessage);
        }

        /// <summary>
        /// Deserialize the JSON into the HttpRequestMessage
        /// </summary>
        /// <param name="jsonObject"></param>
        /// <returns></returns>
        public static HttpRequestMessage Deserialize(string jsonObject)
        {
            var serializedRequestMessage = JsonConvert.DeserializeObject<RequestMessage>(jsonObject);

            var requestMessage = new HttpRequestMessage();
            // Get message content
            requestMessage.Content = new ByteArrayContent(serializedRequestMessage.Content);

            // populate Headers
            foreach (var header in serializedRequestMessage.Headers)
            {
                if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    // Don't flow these headers here
                    continue;
                }
                requestMessage.Headers.Add(header.Key, header.Value);
            }

            requestMessage.Method = new HttpMethod(serializedRequestMessage.HttpMethod);
            requestMessage.RequestUri = string.IsNullOrEmpty(serializedRequestMessage.HybridConnectionScheme) ?
                    new Uri(serializedRequestMessage.Url, UriKind.RelativeOrAbsolute) :
                    GenerateUriFromSbUrl(serializedRequestMessage.Url, serializedRequestMessage.HybridConnectionScheme, serializedRequestMessage.HybridConnectionName);

            return requestMessage;

        }

        /// <summary>
        /// Validates the Json string
        /// </summary>
        /// <param name="strInput"></param>
        /// <returns></returns>
        private static bool IsValidJson(string strInput)
        {
            strInput = strInput.Trim();
            if ((!strInput.StartsWith("{") || !strInput.EndsWith("}")) && (!strInput.StartsWith("[") || !strInput.EndsWith("]")))
            {
                return false;
            }

            try
            {
                JToken.Parse(strInput);
                return true;
            }
            catch
            {
                return false;
            }
        }


        private static Uri GenerateUriFromSbUrl(string sbUrl, string scheme, string connectionName)
        {
            var httpUri = new Uri(sbUrl.Replace($"{scheme}://", "http://"));
            var relativePath = httpUri.GetComponents(UriComponents.PathAndQuery, UriFormat.Unescaped);
            relativePath = relativePath.Replace($"/{connectionName}/", string.Empty, StringComparison.OrdinalIgnoreCase);
            return new Uri(relativePath, UriKind.RelativeOrAbsolute);
        }
    }
}
