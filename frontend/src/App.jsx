import { useState, useEffect } from 'react'
import axios from 'axios'
import ArticleCard from './components/ArticleCard'
import ArticleModal from './components/ArticleModal'
import Filters from './components/Filters'

function App() {
  const [articles, setArticles] = useState([])
  const [filteredArticles, setFilteredArticles] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [selectedArticle, setSelectedArticle] = useState(null)
  const [stats, setStats] = useState(null)
  const [refreshing, setRefreshing] = useState(false)
  const [refreshMessage, setRefreshMessage] = useState(null)
  const [filters, setFilters] = useState({
    search: '',
    source: '',
    category: ''
  })
  const [currentPage, setCurrentPage] = useState(1)
  const pageSize = 9

  useEffect(() => {
    fetchArticles()
    fetchStatistics()
  }, [])

  useEffect(() => {
    applyFilters()
  }, [articles, filters])

  const fetchArticles = async () => {
    try {
      setLoading(true)
      const response = await axios.get('/api/articles')
      setArticles(response.data)
      setError(null)
    } catch (err) {
      setError('Failed to fetch articles. Make sure the backend is running.')
      console.error('Error fetching articles:', err)
    } finally {
      setLoading(false)
    }
  }

  const fetchStatistics = async () => {
    try {
      const response = await axios.get('/api/statistics')
      setStats(response.data)
    } catch (err) {
      console.error('Error fetching statistics:', err)
    }
  }

  const handleRefresh = async () => {
    setRefreshing(true)
    setRefreshMessage(null)
    try {
      // Trigger the scraper on the backend
      const response = await axios.post('/api/scraper/refresh')
      setRefreshMessage({
        type: 'info',
        text: 'Articles will update in a moment...'
      })
      
      // Wait a bit then reload articles
      setTimeout(async () => {
        await fetchArticles()
        await fetchStatistics()
        setCurrentPage(1)
        setRefreshMessage({
          type: 'success',
          text: 'Updated successfully!'
        })
        setTimeout(() => setRefreshMessage(null), 3000)
      }, 2000)
    } catch (err) {
      console.error('Error refreshing articles:', err)
      setRefreshMessage({
        type: 'error',
        text: 'Failed to refresh. Please try again later.'
      })
    } finally {
      setRefreshing(false)
    }
  }

  const applyFilters = () => {
    let filtered = [...articles]

    if (filters.search) {
      const searchLower = filters.search.toLowerCase()
      filtered = filtered.filter(article =>
        article.title.toLowerCase().includes(searchLower) ||
        article.description.toLowerCase().includes(searchLower)
      )
    }

    if (filters.source) {
      filtered = filtered.filter(article => article.source === filters.source)
    }

    if (filters.category) {
      filtered = filtered.filter(article => article.category === filters.category)
    }

    setFilteredArticles(filtered)
  }

  const handleFilterChange = (filterName, value) => {
    setFilters(prev => ({
      ...prev,
      [filterName]: value
    }))
    setCurrentPage(1)
  }

  const handleArticleClick = (article) => {
    setSelectedArticle(article)
  }

  const handleCloseModal = () => {
    setSelectedArticle(null)
  }

  const sources = [...new Set(articles.map(a => a.source))]
  const categories = [...new Set(articles.map(a => a.category))]

  const totalPages = Math.max(1, Math.ceil(filteredArticles.length / pageSize))
  const displayedArticles = filteredArticles.slice((currentPage - 1) * pageSize, currentPage * pageSize)

  return (
    <div className="app">
      <header>
        <div className="header-top">
          <div className="header-content">
            <h1>News Feed</h1>
            <p>Stay updated with the latest news from multiple sources</p>
          </div>
          <div className="header-stats">
            {stats && (
              <div className="stats-box">
                <div className="stat-item">
                  <span className="stat-value">{stats.totalArticles}</span>
                  <span className="stat-label">Articles</span>
                </div>
                <div className="stat-divider"></div>
                <div className="stat-item">
                  <span className="stat-value">{stats.sources?.length || 0}</span>
                  <span className="stat-label">Sources</span>
                </div>
              </div>
            )}
            <button 
              className="refresh-btn" 
              onClick={handleRefresh}
              disabled={loading || refreshing}
              title="Get Latest Articles"
            >
              {refreshing ? '⟳ Scraping...' : loading ? '⟳ Loading...' : '⟳ Refresh'}
            </button>
            {refreshMessage && (
              <div className={`refresh-message ${refreshMessage.type}`}>
                {refreshMessage.text}
              </div>
            )}
          </div>
        </div>
      </header>

      <div className="container">
        <Filters
          filters={filters}
          onFilterChange={handleFilterChange}
          sources={sources}
          categories={categories}
        />

        {loading && <div className="loading">Loading articles...</div>}
        
        {error && <div className="error">{error}</div>}

        {!loading && !error && filteredArticles.length === 0 && (
          <div className="no-articles">
            No articles found. Try adjusting your filters.
          </div>
        )}

        {!loading && !error && filteredArticles.length > 0 && (
          <>
            <div className="articles-grid">
              {displayedArticles.map(article => (
                <ArticleCard
                  key={article.id}
                  article={article}
                  onClick={() => handleArticleClick(article)}
                />
              ))}
            </div>
            <br></br>

            <div className="pagination">
              <button
                onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                disabled={currentPage === 1}
              >
                Previous
              </button>

              <span className="page-info">Page {currentPage} of {totalPages}</span>

              <button
                onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                disabled={currentPage === totalPages}
              >
                Next
              </button>
            </div>
          </>
        )}
      </div>

      {selectedArticle && (
        <ArticleModal
          article={selectedArticle}
          onClose={handleCloseModal}
        />
      )}
    </div>
  )
}

export default App
