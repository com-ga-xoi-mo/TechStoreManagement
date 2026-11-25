<#
.SYNOPSIS
  Sends one SANDBOX bank notification to the VietQR webhook, signed with HMAC-SHA256.

.DESCRIPTION
  Stands in for a bank while there is no real provider. The signature is
  hex(HMAC-SHA256(secret, "<X-Timestamp>.<raw body>")), with the same secret as VietQr__WebhookSecret on the API.
  Pass the secret through -Secret or the VIETQR_WEBHOOK_SECRET environment variable; never commit it.

.EXAMPLE
  ./scripts/simulate-vietqr-webhook.ps1 -TransferCode TSABCDEFGH23 -Amount 11000000 -AccountNumber 0123456789
#>
param(
    [Parameter(Mandatory)] [string] $TransferCode,
    [Parameter(Mandatory)] [decimal] $Amount,
    [Parameter(Mandatory)] [string] $AccountNumber,
    [string] $TransactionId = ("FT" + [DateTime]::UtcNow.ToString("yyMMddHHmmss")),
    [string] $Secret = $env:VIETQR_WEBHOOK_SECRET,
    [string] $BaseUrl = "http://localhost:5176"
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Secret)) { throw "Set -Secret or VIETQR_WEBHOOK_SECRET (same value as VietQr__WebhookSecret)." }

$body = [ordered]@{
    transactionId   = $TransactionId
    amount          = $Amount
    transferContent = "CK $TransferCode"
    accountNumber   = $AccountNumber
    transferredAt   = [DateTime]::UtcNow.ToString("o")
} | ConvertTo-Json -Compress

$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
$hmac = New-Object System.Security.Cryptography.HMACSHA256 (, [Text.Encoding]::UTF8.GetBytes($Secret))
$signature = -join ($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$timestamp.$body")) | ForEach-Object { $_.ToString("x2") })

Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/payments/vietqr/webhook" -ContentType "application/json" `
    -Headers @{ "X-Timestamp" = $timestamp; "X-Signature" = $signature } -Body $body | ConvertTo-Json
