function Filters({ filters, onFilterChange, sources, categories }) {
  return (
    <div className="filters">
      <div className="filter-row">
        <div className="filter-group">
          <label htmlFor="search">Search</label>
          <input
            type="text"
            id="search"
            placeholder="Search articles..."
            value={filters.search}
            onChange={(e) => onFilterChange('search', e.target.value)}
          />
        </div>

        <div className="filter-group">
          <label htmlFor="source">Source</label>
          <select
            id="source"
            value={filters.source}
            onChange={(e) => onFilterChange('source', e.target.value)}
          >
            <option value="">All Sources</option>
            {sources.map(source => (
              <option key={source} value={source}>{source}</option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="category">Category</label>
          <select
            id="category"
            value={filters.category}
            onChange={(e) => onFilterChange('category', e.target.value)}
          >
            <option value="">All Categories</option>
            {categories.map(category => (
              <option key={category} value={category}>{category}</option>
            ))}
          </select>
        </div>

        <div className="filter-group">
          <label htmlFor="sentiment">Sentiment</label>
          <select
            id="sentiment"
            value={filters.sentiment}
            onChange={(e) => onFilterChange('sentiment', e.target.value)}
          >
            <option value="">All Sentiments</option>
            <option value="positive">Positive</option>
            <option value="neutral">Neutral</option>
            <option value="negative">Negative</option>
          </select>
        </div>
      </div>
    </div>
  )
}

export default Filters
