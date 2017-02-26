﻿// <copyright file="RequestLogMiddleware.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;

namespace FubarDev.WebDavServer.AspNetCore.Logging
{
    /// <summary>
    /// The request log middleware
    /// </summary>
    public class RequestLogMiddleware
    {
        private static readonly Encoding _defaultEncoding = new UTF8Encoding(false);
        private readonly IEnumerable<MediaType> _supportedMediaTypes = new[]
        {
            "text/xml",
            "application/xml",
            "text/plain",
        }.Select(x => new MediaType(x)).ToList();

        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLogMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLogMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware</param>
        /// <param name="logger">The logger for this middleware</param>
        public RequestLogMiddleware(RequestDelegate next, ILogger<RequestLogMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invoked by ASP.NET core
        /// </summary>
        /// <param name="context">The HTTP context</param>
        /// <returns>The async task</returns>
        // ReSharper disable once ConsiderUsingAsyncSuffix
        public async Task Invoke(HttpContext context)
        {
            using (_logger.BeginScope("RequestInfo"))
            {
                var info = new List<string>()
                    {
                        $"{context.Request.Protocol} {context.Request.Method} {context.Request.GetDisplayUrl()}",
                    };

                try
                {
                    info.AddRange(context.Request.Headers.Select(x => $"{x.Key}: {x.Value}"));
                }
                catch
                {
                    // Ignore all exceptions
                }

                if (context.Request.Body != null && !string.IsNullOrEmpty(context.Request.ContentType))
                {
                    var contentType = new MediaType(context.Request.ContentType);
                    var isXml = _supportedMediaTypes.Any(x => contentType.IsSubsetOf(x));
                    if (isXml)
                    {
                        var encoding = _defaultEncoding;
                        if (contentType.Charset.HasValue)
                        {
                            encoding = Encoding.GetEncoding(contentType.Charset.Value);
                        }

                        var temp = new MemoryStream();
                        await context.Request.Body.CopyToAsync(temp, 65536).ConfigureAwait(false);

                        if (temp.Length != 0)
                        {
                            temp.Position = 0;

                            try
                            {
                                using (var reader = new StreamReader(temp, encoding, false, 1000, true))
                                {
                                    var doc = XDocument.Load(reader);
                                    info.Add($"Body: {doc}");
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(EventIds.Unspecified, ex, ex.Message);
                                temp.Position = 0;
                                using (var reader = new StreamReader(temp, encoding, false, 1000, true))
                                {
                                    var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                                    info.Add($"Body: {content}");
                                }
                            }

                            if (!context.Request.Body.CanSeek)
                            {
                                var oldStream = context.Request.Body;
                                context.Request.Body = temp;
                                oldStream.Dispose();
                            }

                            context.Request.Body.Position = 0;
                        }
                    }
                }

                _logger.LogInformation(string.Join("\r\n", info));
            }

            await _next(context).ConfigureAwait(false);
        }
    }
}
