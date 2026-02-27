function ArticleModal({ article, onClose }) {
  const sentimentLabel = (article.sentimentLabel || 'neutral').toLowerCase()

  const formatDate = (dateString) => {
    const date = new Date(dateString)
    return date.toLocaleDateString('en-NZ', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
  }

  const formatConfidence = (confidence) => {
    const value = Number(confidence)
    if (!Number.isFinite(value)) {
      return 'N/A'
    }

    const normalized = value > 1 ? value : value * 100
    return `${Math.max(0, Math.min(100, normalized)).toFixed(1)}%`
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <button className="modal-close" onClick={onClose}>&times;</button>
        
        <div className="article-source">{article.source}</div>
        <div className="article-date">{formatDate(article.publishedDate)}</div>
        
        <h2>{article.title}</h2>
        
        <div className="article-description">{article.description}</div>
        
        <a 
          href={article.url} 
          target="_blank" 
          rel="noopener noreferrer" 
          className="article-link"
        >
          Read Full Article →
        </a>

        <div className="modal-sentiment-footer">
          <span className={`modal-sentiment-label ${sentimentLabel}`}>{sentimentLabel}</span>
          <span className="modal-sentiment-confidence">
            Sentiment confidence: {formatConfidence(article.sentimentConfidence)}
          </span>
        </div>
      </div>
    </div>
  )
}

export default ArticleModal
