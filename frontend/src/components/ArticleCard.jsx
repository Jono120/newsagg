function ArticleCard({ article, onClick, onSignup }) {
  const sentimentLabel = (article.sentimentLabel || 'neutral').toLowerCase()
  const keyPhrases = (article.keyPhrases || []).slice(0, 3)

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
      {keyPhrases.length > 0 && (
        <div className="key-phrases">
          {keyPhrases.map((phrase, idx) => (
            <span key={idx} className="key-phrase-tag">{phrase}</span>
          ))}
        </div>
      )}
      <button
        type="button"
        className="signup-cta-button"
        onClick={(event) => {
          event.stopPropagation()
          onSignup?.('article-card')
        }}
      >
        Get NZ digest updates
      </button>
    </div>
  )
}

export default ArticleCard
