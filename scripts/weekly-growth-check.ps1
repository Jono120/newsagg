param(
    [string]$ApiBaseUrl = "https://newsagg-web.azurewebsites.net",
    [decimal]$AdSpendNzd = 125
)

$summaryUrl = "$ApiBaseUrl/api/growth/summary?days=7&adSpendNzd=$AdSpendNzd"
$scaleGateUrl = "$ApiBaseUrl/api/growth/scale-gate?days=30&adSpendNzd=$($AdSpendNzd * 4)&minConversionRate=0.05&maxCostPerSignupNzd=5"
$channelTestsUrl = "$ApiBaseUrl/api/growth/channel-tests"

Write-Host "Fetching weekly growth summary..."
$summary = Invoke-RestMethod -Method Get -Uri $summaryUrl

Write-Host "Fetching scale-gate decision..."
$decision = Invoke-RestMethod -Method Get -Uri $scaleGateUrl

Write-Host "Fetching channel test plan..."
$channels = Invoke-RestMethod -Method Get -Uri $channelTestsUrl

Write-Host ""
Write-Host "=== Weekly Growth Snapshot ==="
Write-Host "Visitors (7d): $($summary.visitors)"
Write-Host "Signups (7d): $($summary.signups)"
Write-Host ("Conversion Rate (7d): {0:P2}" -f $summary.conversionRate)
Write-Host "CPS (7d): NZD $($summary.costPerSignupNzd)"
Write-Host ""
Write-Host "=== Scale Gate Decision ==="
Write-Host "Decision: $($decision.decision)"
Write-Host "Meets conversion gate: $($decision.rationale.meetsConversion)"
Write-Host "Meets CPS gate: $($decision.rationale.meetsCps)"
Write-Host ""
Write-Host "=== Channel Test Budget ==="
$channels.channels | ForEach-Object {
    Write-Host "$($_.channel): NZD $($_.budgetNzd)"
}
