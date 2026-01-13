import { useState, useEffect } from "react";
import axios from "axios";
import ArticleCard from "./components/ArticleCard";
import ArticleModal from "./components/ArticleModal";
import Filters from "./components/Filters";
import {Button} from "react-bootstrap/Button";
import { Pagination } from "antd";
import Switch from '@mui/material/Switch';
import { styled } from '@mui/material/styles';

const MaterialUISwitch = styled(Switch)(({ theme }) => ({
  width: 62,
  height: 34,
  padding: 7,
  '& .MuiSwitch-switchBase': {
    margin: 1,
    padding: 0,
    transform: 'translateX(6px)',
    '&.Mui-checked': {
      color: '#fff',
      transform: 'translateX(22px)',
      '& .MuiSwitch-thumb:before': {
        backgroundImage: `url('data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" height="20" width="20" viewBox="0 0 20 20"><path fill="${encodeURIComponent(
          '#fff',
        )}" d="M4.2 2.5l-.7 1.8-1.8.7 1.8.7.7 1.8.6-1.8L6.7 5l-1.9-.7-.6-1.8zm15 8.3a6.7 6.7 0 11-6.6-6.6 5.8 5.8 0 006.6 6.6z"/></svg>')`,
      },
      '& + .MuiSwitch-track': {
        opacity: 1,
        backgroundColor: theme.palette.mode === 'dark' ? '#8796A5' : '#aab4be',
      },
    },
  },
  '& .MuiSwitch-thumb': {
    backgroundColor: theme.palette.mode === 'dark' ? '#003892' : '#001e3c',
    width: 32,
    height: 32,
    '&::before': {
      content: "''",
      position: 'absolute',
      width: '100%',
      height: '100%',
      left: 0,
      top: 0,
      backgroundRepeat: 'no-repeat',
      backgroundPosition: 'center',
      backgroundImage: `url('data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" height="20" width="20" viewBox="0 0 20 20"><path fill="${encodeURIComponent(
        '#fff',
      )}" d="M9.305 1.667V3.75h1.389V1.667h-1.39zm-4.707 1.95l-.982.982L5.09 6.072l.982-.982-1.473-1.473zm10.802 0L13.927 5.09l.982.982 1.473-1.473-.982-.982zM10 5.139a4.872 4.872 0 00-4.862 4.86A4.872 4.872 0 0010 14.862 4.872 4.872 0 0014.86 10 4.872 4.872 0 0010 5.139zm0 1.389A3.462 3.462 0 0113.471 10a3.462 3.462 0 01-3.473 3.472A3.462 3.462 0 016.527 10 3.462 3.462 0 0110 6.528zM1.665 9.305v1.39h2.083v-1.39H1.666zm14.583 0v1.39h2.084v-1.39h-2.084zM5.09 13.928L3.616 15.4l.982.982 1.473-1.473-.982-.982zm9.82 0l-.982.982 1.473 1.473.982-.982-1.473-1.473zM9.305 16.25v2.083h1.389V16.25h-1.39z"/></svg>')`,
    },
  },
  '& .MuiSwitch-track': {
    opacity: 1,
    backgroundColor: theme.palette.mode === 'dark' ? '#8796A5' : '#aab4be',
    borderRadius: 20 / 2,
  },
}));



function App() {
  const [articles, setArticles] = useState([]);
  const [filteredArticles, setFilteredArticles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedArticle, setSelectedArticle] = useState(null);
  const [stats, setStats] = useState(null);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshMessage, setRefreshMessage] = useState(null);
  const [filters, setFilters] = useState({
    search: "",
    source: "",
    category: "",
  });
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(12);

  useEffect(() => {
    fetchArticles();
    fetchStatistics();
  }, []);

  useEffect(() => {
    applyFilters();
  }, [articles, filters]);

  const fetchArticles = async () => {
    try {
      setLoading(true);
      const response = await axios.get("/api/articles");
      setArticles(response.data);
      setError(null);
    } catch (err) {
      setError("Failed to fetch articles. Make sure the backend is running.");
      console.error("Error fetching articles:", err);
    } finally {
      setLoading(false);
    }
  };

  const fetchStatistics = async () => {
    try {
      const response = await axios.get("/api/statistics");
      setStats(response.data);
    } catch (err) {
      console.error("Error fetching statistics:", err);
    }
  };

  const handleRefresh = async () => {
    setRefreshing(true);
    setRefreshMessage(null);
    try {
      // Trigger the scraper on the backend
      const response = await axios.post("/api/scraper/refresh");
      setRefreshMessage({
        type: "info",
        text: "Articles will update in a moment...",
      });

      // Wait a bit then reload articles
      setTimeout(async () => {
        await fetchArticles();
        await fetchStatistics();
        setCurrentPage(1);
        setRefreshMessage({
          type: "success",
          text: "Updated successfully!",
        });
        setTimeout(() => setRefreshMessage(null), 3000);
      }, 2000);
    } catch (err) {
      console.error("Error refreshing articles:", err);
      setRefreshMessage({
        type: "error",
        text: "Failed to refresh. Please try again later.",
      });
    } finally {
      setRefreshing(false);
    }
  };

  const applyFilters = () => {
    let filtered = [...articles];

    if (filters.search) {
      const searchLower = filters.search.toLowerCase();
      filtered = filtered.filter(
        (article) =>
          article.title.toLowerCase().includes(searchLower) ||
          article.description.toLowerCase().includes(searchLower)
      );
    }

    if (filters.source) {
      filtered = filtered.filter(
        (article) => article.source === filters.source
      );
    }

    if (filters.category) {
      filtered = filtered.filter(
        (article) => article.category === filters.category
      );
    }

    setFilteredArticles(filtered);
  };

  const handleFilterChange = (filterName, value) => {
    setFilters((prev) => ({
      ...prev,
      [filterName]: value,
    }));
    setCurrentPage(1);
  };

  const handleArticleClick = (article) => {
    setSelectedArticle(article);
  };

  const handleCloseModal = () => {
    setSelectedArticle(null);
  };

  const sources = [...new Set(articles.map((a) => a.source))];
  const categories = [...new Set(articles.map((a) => a.category))];

  const totalPages = Math.max(1, Math.ceil(filteredArticles.length / pageSize));
  const displayedArticles = filteredArticles.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  const onShowSizeChange = (current, newSize) => {
    setPageSize(newSize);
    setCurrentPage(1);
  };

  const [isDarkMode, setIsDarkMode] = useState(() => {
    try {
      const saved = localStorage.getItem("isDarkMode");
      if (saved !== null) return saved === "true";
    } catch (e) {}
    return (
      typeof window !== "undefined" &&
      window.matchMedia &&
      window.matchMedia("(prefers-color-scheme: dark)").matches
    );
  });

  useEffect(() => {
    try {
      if (isDarkMode) document.body.classList.add("dark");
      else document.body.classList.remove("dark");
      localStorage.setItem("isDarkMode", isDarkMode);
    } catch (e) {}
  }, [isDarkMode]);

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
                  <span className="stat-value">
                    {stats.sources?.length || 0}
                  </span>
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
              {refreshing
                ? "⟳ Scraping..."
                : loading
                ? "⟳ Loading..."
                : "⟳ Refresh"}
            </button>
            {refreshMessage && (
              <div className={`refresh-message ${refreshMessage.type}`}>
                {refreshMessage.text}
              </div>
            )}
            <MaterialUISwitch
              checked={isDarkMode}
              onChange={() => setIsDarkMode(!isDarkMode)}
              slotProps={{ input: { 'aria-label': 'dark mode toggle' } }}
            />
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
              {displayedArticles.map((article) => (
                <ArticleCard
                  key={article.id}
                  article={article}
                  onClick={() => handleArticleClick(article)}
                />
              ))}
            </div>
            <br></br>

            <div
              className="pagination d-flex justify-content-center align-items-center gap-3"
              transition="background-color 0.3s ease"
            >
              <Pagination
              size = "large"
                showSizeChanger
                pageSizeOptions={["12", "24", "36", "48", "60"]}
                onShowSizeChange={onShowSizeChange}
                onChange={(page, size) => {
                  setCurrentPage(page);
                  if (size && size !== pageSize) setPageSize(size);
                }}
                current={currentPage}
                pageSize={pageSize}
                total={filteredArticles.length}
              />
            </div>
          </>
        )}
      </div>

      <div className="spacer"></div>

      <div className="footer d-flex justify-content-center align-items-center">
        <p align="center">Something is afoot!</p>
      </div>

      {selectedArticle && (
        <ArticleModal article={selectedArticle} onClose={handleCloseModal} />
      )}
    </div>
  );
}

export default App;
