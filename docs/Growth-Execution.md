# Growth Execution (Channel Tests + Scale Gate)

## Channel tests (Weeks 3-6)

Monthly test budget cap: **NZD 500**

- Meta/Instagram: NZD 175
- Google Search: NZD 175
- Organic community loop (content + creative tooling): NZD 150

Primary conversion goal: `email-signup`

Weekly cadence:

1. Export last 7 days of signup and event data from `GET /api/growth/summary?days=7`.
2. Compare channel-level conversion and proxy CPS.
3. Pause low-converting audiences/keywords.
4. Move spend to top 1-2 performers.
5. Ship one creative/copy iteration per paid channel.

## Scale gate (Weeks 6-12)

Use:

`GET /api/growth/scale-gate?days=30&adSpendNzd=<monthly_spend>&minConversionRate=0.05&maxCostPerSignupNzd=5`

Decision rule:

- `scale` when conversion rate >= 5% and CPS <= NZD 5
- `reposition` when either threshold is missed

If `scale`:

- Increase budget by 20-30% week-over-week.

If `reposition`:

- Rewrite value proposition and landing CTA copy.
- Tighten audience targeting and negative keywords.
- Re-run 2-week experiment before increasing spend.
