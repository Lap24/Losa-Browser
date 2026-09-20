// Simple adblock extension for Losa Browser.
// Requires: field `WebView2 view;` inside Browser class.
// This is still under development and may not work
// V1.0

using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;

void EnableAdBlock()
{
    if (view?.CoreWebView2 == null)
        return;

    var blockedHosts = new HashSet<string>
    {
        "ads.google.com",
        "pagead2.googlesyndication.com",
        "doubleclick.net",
        "adservice.google.com",
        "facebook.com",
        "connect.facebook.net",
        "static.doubleclick.net",
        "googletagservices.com",
        "googletagmanager.com",
        "adserver.snapads.com",
        "ads.yahoo.com"
    };

    view.CoreWebView2.WebResourceRequested += (sender, args) =>
    {
        try
        {
            var uri = new Uri(args.Request.Uri);
            var host = uri.Host.ToLowerInvariant();

            if (blockedHosts.Contains(host))
            {
                args.Response = view.CoreWebView2.Environment.CreateWebResourceResponse(
                    stream: null,
                    statusCode: 403,
                    reasonPhrase: "Blocked by Losa AdBlock",
                    headers: "Content-Type: text/plain"
                );
            }
        }
        catch
        {
            // Ignore malformed URLs
        }
    };
}
