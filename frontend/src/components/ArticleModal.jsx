function ArticleModal({ article, onClose }) {
  const formatDate = (dateString) => {
    const date = new Date(dateString)
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    })
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
      </div>
    </div>
  )
}

export default ArticleModal
