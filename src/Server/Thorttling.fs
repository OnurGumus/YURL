module Throttling

open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open ThrottlingTroll

let private getClientIp (request: obj) : string =
    let proxyRequest = request :?> IIncomingHttpRequestProxy
    let httpContext = proxyRequest.Request.HttpContext
    let logger = httpContext.RequestServices.GetService<ILogger<obj>>() |> nonNull
    let headers = httpContext.Request.Headers

    // Try to get the real client IP from proxy headers first
    let ipAddress =
        if headers.ContainsKey("X-Forwarded-For") then
            let xForwardedFor = headers.["X-Forwarded-For"].ToString()
            logger.LogInformation("Using X-Forwarded-For header: {XForwardedFor}", xForwardedFor)
            // X-Forwarded-For can contain multiple IPs (client, proxy1, proxy2, etc.)
            // The first IP is typically the original client IP
            let firstIp = xForwardedFor.Split(',').[0].Trim()
            logger.LogInformation("Extracted client IP from X-Forwarded-For: {ClientIP}", firstIp)
            firstIp
        elif headers.ContainsKey("X-Real-IP") then
            let xRealIp = headers.["X-Real-IP"].ToString()
            logger.LogInformation("Using X-Real-IP header: {XRealIP}", xRealIp)
            xRealIp
        elif headers.ContainsKey("CF-Connecting-IP") then
            let cfConnectingIp = headers.["CF-Connecting-IP"].ToString()
            logger.LogInformation("Using CF-Connecting-IP header: {CFConnectingIP}", cfConnectingIp)
            cfConnectingIp
        else
            // Fall back to direct connection IP
            match httpContext.Connection.RemoteIpAddress with
            | null ->
                logger.LogWarning("No proxy headers found and RemoteIpAddress is null, using 'unknown_ip'")
                "unknown_ip"
            | addr ->
                let ip = addr.ToString()
                logger.LogInformation("No proxy headers found, using direct connection IP: {IpAddress}", ip)
                ip

    ipAddress

// Helper function to create rules for the slug API
let private createSlugApiRateLimitRule (limitMethod: ThrottlingTroll.RateLimitMethod) =
    ThrottlingTrollRule(
        UriPattern = "/api/slug", // Or "^/api/slug$" for exact regex match
        Method = "POST",
        LimitMethod = limitMethod,
        IdentityIdExtractor = getClientIp
    )

// Helper function to create rules for the abuse report API
let private createAbuseReportRateLimitRule (limitMethod: ThrottlingTroll.RateLimitMethod) =
    ThrottlingTrollRule(
        UriPattern = "/api/report-abuse",
        Method = "POST",
        LimitMethod = limitMethod,
        IdentityIdExtractor = getClientIp
    )

let config = ThrottlingTrollConfig()

do
    config.Rules <- [|
        createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 15, IntervalInSeconds = 60))
        createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 30, IntervalInSeconds = 3600))
        createSlugApiRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 60, IntervalInSeconds = 86400))
        // Abuse report rate limits - much stricter
        createAbuseReportRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 2, IntervalInSeconds = 300)) // 3 per 5 minutes
        createAbuseReportRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 3, IntervalInSeconds = 3600)) // 10 per hour
        createAbuseReportRateLimitRule (FixedWindowRateLimitMethod(PermitLimit = 5, IntervalInSeconds = 86400)) // 20 per day
    |]
