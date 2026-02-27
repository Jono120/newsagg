function ArticleCard({ article, onClick }) {
  const sentimentLabel = (article.sentimentLabel || 'neutral').toLowerCase()

  const formatDate = (dateString) => {
    const date = new Date(dateString)
    return date.toLocaleDateString('en-NZ', {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    })
  }

  return (
    <div className="article-card" onClick={onClick}>
      <div className="source">{article.source}</div>
      <div className={`sentiment-badge ${sentimentLabel}`}>{sentimentLabel}</div>
      <div className="date">{formatDate(article.publishedDate)}</div>
      <h3>{article.title}</h3>
      <p className="description">
        {article.description.substring(0, 150)}
        {article.description.length > 150 ? '...' : ''}
      </p>
      <div className="category">{article.category}</div>
    </div>
  )
}

export default ArticleCard
