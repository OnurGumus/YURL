module AbuseReportHandler

open Microsoft.AspNetCore.Http
open Giraffe
open Microsoft.Extensions.Logging
open System
open System.IO

[<CLIMutable>]
type AbuseReport = {
    ReportUrl: string
    ReportReason: string
    ReportDescription: string
    ReportEvidence: string option
    ReportEmail: string option
    ReportUrgency: string
}

let private validateField fieldName (fieldValue: string) =
    let maxLength = 1024

    match fieldValue with
    | value when value.Length > maxLength -> Error $"{fieldName} exceeds maximum length of {maxLength} characters"
    | value -> Ok value

let private validateOptionalField fieldName (fieldValue: string option) =
    let maxLength = 1024

    match fieldValue with
    | None -> Ok None
    | Some value when value.Length > maxLength -> Error $"{fieldName} exceeds maximum length of {maxLength} characters"
    | Some value -> Ok(Some value)

let private getClientIp (ctx: HttpContext) =
    let headers = ctx.Request.Headers

    if headers.ContainsKey("X-Forwarded-For") then
        headers.["X-Forwarded-For"].ToString().Split(',').[0].Trim()
    elif headers.ContainsKey("X-Real-IP") then
        headers.["X-Real-IP"].ToString()
    else
        match ctx.Connection.RemoteIpAddress with
        | null -> "unknown"
        | addr -> addr.ToString()

let private isSuspiciousContent (description: string) (reason: string) =
    description.Contains("script>")
    || description.Contains("<iframe")
    || (description.Length < 10 && reason <> "other")
    || description.Contains(String.replicate 50 "a")

let private createReportContent (report: AbuseReport) (reportId: string) (timestamp: string) (clientIp: string) =
    let urgencyLabel =
        if report.ReportUrgency = "critical" then "🚨 CRITICAL"
        elif report.ReportUrgency = "high" then "⚠️ HIGH PRIORITY"
        else "📝 STANDARD"

    $"""ABUSE REPORT - {urgencyLabel}
Report ID: {reportId}
Timestamp: {timestamp}
Client IP: {clientIp}

REPORTED URL: {report.ReportUrl}
REASON: {report.ReportReason}
URGENCY: {report.ReportUrgency}

DESCRIPTION:
{report.ReportDescription}

ADDITIONAL EVIDENCE:
{report.ReportEvidence |> Option.defaultValue "None provided"}

REPORTER EMAIL:
{report.ReportEmail |> Option.defaultValue "Not provided"}

---
This report was automatically generated by yurl.ai abuse reporting system.
"""

let handler: HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            try
                let! report = ctx.BindModelAsync<AbuseReport>()
                let logger = ctx.GetLogger "AbuseReportHandler"

                // Validate all fields
                let validationResults = [
                    validateField "ReportUrl" report.ReportUrl
                    validateField "ReportReason" report.ReportReason
                    validateField "ReportDescription" report.ReportDescription
                    validateField "ReportUrgency" report.ReportUrgency
                ]

                let optionalValidationResults = [
                    validateOptionalField "ReportEvidence" report.ReportEvidence
                    validateOptionalField "ReportEmail" report.ReportEmail
                ]

                // Check if any validation failed
                let allErrors =
                    (validationResults
                     |> List.choose (function
                         | Error e -> Some e
                         | Ok _ -> None))
                    @ (optionalValidationResults
                       |> List.choose (function
                           | Error e -> Some e
                           | Ok _ -> None))

                match allErrors with
                | [] ->
                    // All validations passed - check for suspicious patterns
                    if isSuspiciousContent report.ReportDescription report.ReportReason then
                        logger.LogWarning(
                            "Suspicious abuse report blocked, IP: {ClientIP}",
                            ctx.Connection.RemoteIpAddress
                        )

                        return!
                            (setStatusCode 400
                             >=> json {|
                                 success = false
                                 error = "Report contains suspicious content"
                             |})
                                next
                                ctx
                    else
                        // Proceed with normal processing
                        let reportId = Guid.NewGuid().ToString()
                        let timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                        let clientIp = getClientIp ctx

                        let reportContent = createReportContent report reportId timestamp clientIp

                        let contentRootPath = ctx.GetWebHostEnvironment().ContentRootPath
                        let databaseDir = Path.Combine(contentRootPath, "Database")

                        if not (Directory.Exists(databaseDir)) then
                            Directory.CreateDirectory(databaseDir) |> ignore

                        let fileName = sprintf "%s_abuse_report.txt" reportId
                        let filePath = Path.Combine(databaseDir, fileName)
                        do! File.WriteAllTextAsync(filePath, reportContent)

                        logger.LogWarning(
                            "Abuse report received - ID: {ReportId}, URL: {ReportedUrl}, Reason: {Reason}, Urgency: {Urgency}",
                            reportId,
                            report.ReportUrl,
                            report.ReportReason,
                            report.ReportUrgency
                        )

                        return!
                            json
                                {|
                                    success = true
                                    reportId = reportId
                                |}
                                next
                                ctx

                | errors ->
                    let errorMsg = String.concat "; " errors

                    logger.LogWarning(
                        "Invalid abuse report submission: {Error}, IP: {ClientIP}",
                        errorMsg,
                        ctx.Connection.RemoteIpAddress
                    )

                    return! (setStatusCode 400 >=> json {| success = false; error = errorMsg |}) next ctx

            with ex ->
                let logger = ctx.GetLogger "AbuseReportHandler"
                logger.LogError(ex, "Failed to process abuse report")

                return!
                    (setStatusCode 500
                     >=> json {|
                         success = false
                         error = "Failed to submit report"
                     |})
                        next
                        ctx
        }
