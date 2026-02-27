import { useMemo, useState } from "react"

function buildPath(points, valueKey, width, height, maxValue) {
  if (!points?.length) return ""

  const safeMax = Math.max(1, maxValue)
  return points
    .map((point, index) => {
      const x = points.length === 1 ? width / 2 : (index / (points.length - 1)) * width
      const y = height - ((point[valueKey] || 0) / safeMax) * height
      return `${index === 0 ? "M" : "L"}${x.toFixed(2)} ${y.toFixed(2)}`
    })
    .join(" ")
}

function SentimentStats({ sentimentStats, sentimentTrends }) {
  const [period, setPeriod] = useState("24h")

  const points = useMemo(() => {
    if (period === "7d") {
      const last7 = sentimentTrends?.last7Days
      if (Array.isArray(last7) && last7.length > 0) {
        return last7
      }

      const last14 = sentimentTrends?.last14Days
      if (Array.isArray(last14) && last14.length > 0) {
        return last14.slice(-7)
      }

      return []
    }
    else if (period === "14d") {
      return sentimentTrends?.last14Days || []
    }
    return sentimentTrends?.last24Hours || []
  }, [period, sentimentTrends])

  const maxValue = useMemo(() => {
    if (!points.length) return 1
    return Math.max(
      1,
      ...points.map((point) => Math.max(point.positive || 0, point.neutral || 0, point.negative || 0))
    )
  }, [points])

  const periodTotals = useMemo(() => {
    if (!points.length) {
      const positive = sentimentStats?.positive || 0
      const neutral = sentimentStats?.neutral || 0
      const negative = sentimentStats?.negative || 0
      return {
        positive,
        neutral,
        negative,
        total: positive + neutral + negative,
      }
    }

    return points.reduce(
      (accumulator, point) => {
        const positive = point.positive || 0
        const neutral = point.neutral || 0
        const negative = point.negative || 0
        accumulator.positive += positive
        accumulator.neutral += neutral
        accumulator.negative += negative
        accumulator.total += positive + neutral + negative
        return accumulator
      },
      { positive: 0, neutral: 0, negative: 0, total: 0 }
    )
  }, [points, sentimentStats])

  const yTicks = useMemo(() => {
    const middle = Math.ceil(maxValue / 2)
    const ticks = [maxValue, middle, 0]
    return [...new Set(ticks)]
  }, [maxValue])

  const xTicks = useMemo(() => {
    if (!points.length) return []
    if (points.length === 1) {
      return [{ label: points[0].label || "", ratio: 0.5 }]
    }

    const middleIndex = Math.floor((points.length - 1) / 2)
    return [
      { label: points[0].label || "", ratio: 0 },
      { label: points[middleIndex].label || "", ratio: middleIndex / (points.length - 1) },
      { label: points[points.length - 1].label || "", ratio: 1 },
    ]
  }, [points])

  const xAxisLabel = period === "24h" ? "Time (hourly)" : "Date (daily)"

  const chartWidth = 500
  const chartHeight = 150
  const margins = { top: 8, right: 8, bottom: 28, left: 36 }
  const plotWidth = chartWidth - margins.left - margins.right
  const plotHeight = chartHeight - margins.top - margins.bottom

  const positivePath = buildPath(points, "positive", plotWidth, plotHeight, maxValue)
  const neutralPath = buildPath(points, "neutral", plotWidth, plotHeight, maxValue)
  const negativePath = buildPath(points, "negative", plotWidth, plotHeight, maxValue)

  return (
    <div className="sentiment-stats" aria-label="sentiment statistics and trends">
      <div className="sentiment-stats-top">
        <div className="sentiment-stat positive">
          <span className="sentiment-dot" aria-hidden="true"></span>
          <span className="sentiment-name">Positive</span>
          <span className="sentiment-count">{periodTotals.positive}</span>
        </div>
        <div className="sentiment-stat neutral">
          <span className="sentiment-dot" aria-hidden="true"></span>
          <span className="sentiment-name">Neutral</span>
          <span className="sentiment-count">{periodTotals.neutral}</span>
        </div>
        <div className="sentiment-stat negative">
          <span className="sentiment-dot" aria-hidden="true"></span>
          <span className="sentiment-name">Negative</span>
          <span className="sentiment-count">{periodTotals.negative}</span>
        </div>
        <div className="sentiment-stat total">
          <span className="sentiment-name">Total ({period.toUpperCase()})</span>
          <span className="sentiment-count">{periodTotals.total}</span>
        </div>
      </div>

      <div className="sentiment-trend-controls" role="tablist" aria-label="sentiment trend period">
        <button
          type="button"
          className={`trend-toggle ${period === "24h" ? "active" : ""}`}
          onClick={() => setPeriod("24h")} role="tab" aria-selected={period === "24h"}>
          24H
        </button>
        <button
          type="button"
          className={`trend-toggle ${period === "7d" ? "active" : ""}`}
          onClick={() => setPeriod("7d")} role="tab" aria-selected={period === "7d"}>
          7D
        </button>
        <button
          type="button"
          className={`trend-toggle ${period === "14d" ? "active" : ""}`}
          onClick={() => setPeriod("14d")} role="tab" aria-selected={period === "14d"}>
          14D
        </button>
      </div>

      <div className="sentiment-trend-chart" aria-label={`sentiment trend ${period}`}>
        <svg viewBox={`0 0 ${chartWidth} ${chartHeight}`} preserveAspectRatio="none" role="img">
          <line className="trend-axis" x1={margins.left} y1={margins.top + plotHeight} x2={margins.left + plotWidth} y2={margins.top + plotHeight} />
          <line className="trend-axis" x1={margins.left} y1={margins.top} x2={margins.left} y2={margins.top + plotHeight} />

          {yTicks.map((value) => {
            const y = margins.top + plotHeight - (value / Math.max(1, maxValue)) * plotHeight
            return (
              <g key={`y-${value}`}>
                <line className="trend-grid" x1={margins.left} y1={y} x2={margins.left + plotWidth} y2={y} />
                <text className="trend-tick-label" x={margins.left - 6} y={y + 3} textAnchor="end">{value}</text>
              </g>
            )
          })}

          {xTicks.map((tick, index) => {
            const x = margins.left + tick.ratio * plotWidth
            return (
              <text
                key={`x-${index}-${tick.label}`}
                className="trend-tick-label"
                x={x}
                y={margins.top + plotHeight + 14}
                textAnchor={index === 0 ? "start" : index === xTicks.length - 1 ? "end" : "middle"}
              >
                {tick.label}
              </text>
            )
          })}

          <text className="trend-axis-label" x={margins.left + plotWidth / 2} y={chartHeight - 2} textAnchor="middle">{xAxisLabel}</text>
          <text
            className="trend-axis-label"
            x={10}
            y={margins.top + plotHeight / 2}
            textAnchor="middle"
            transform={`rotate(-90 10 ${margins.top + plotHeight / 2})`}
          >
            Articles
          </text>

          <g transform={`translate(${margins.left}, ${margins.top})`}>
            <path className="trend-line positive" d={positivePath} />
            <path className="trend-line neutral" d={neutralPath} />
            <path className="trend-line negative" d={negativePath} />
          </g>
        </svg>
      </div>
    </div>
  )
}

export default SentimentStats
