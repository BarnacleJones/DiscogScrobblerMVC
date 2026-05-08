"use strict";
(() => {
  // Scripts/shared/dataTableSearch.ts
  function applySearchFromQueryString(table, queryKey = "q") {
    const queryParams = new URLSearchParams(window.location.search);
    const searchQuery = queryParams.get(queryKey);
    if (searchQuery && searchQuery.trim()) {
      table.search(searchQuery.trim()).draw();
    }
  }
  function bindClearFilterButton(table, clearButtonSelector, queryKey = "q") {
    const clearButton = $(clearButtonSelector);
    const updateClearButtonState = () => {
      const hasActiveSearch = (table.search() ?? "").trim().length > 0;
      clearButton.prop("disabled", !hasActiveSearch);
    };
    clearButton.on("click", function() {
      table.search("").draw();
      const currentUrl = new URL(window.location.href);
      currentUrl.searchParams.delete(queryKey);
      window.history.replaceState({}, "", currentUrl.toString());
      updateClearButtonState();
    });
    table.on("search.dt", function() {
      updateClearButtonState();
    });
    updateClearButtonState();
  }

  // Scripts/shared/htmlEscape.ts
  function escapeHtml(value) {
    return String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
  }
  function escapeAttr(value) {
    return String(value).replace(/&/g, "&amp;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
  }

  // Scripts/shared/browseCountGrid.ts
  function formatNameHref(config) {
    return (_data, type, row) => {
      if (type === "sort" || type === "filter" || type === "type") return row.name ?? "";
      const name = (row.name ?? "").trim();
      if (!name) return "\u2014";
      const href = config.nameHref(row);
      return `<a href="${escapeAttr(href)}">${escapeHtml(name)}</a>`;
    };
  }
  function formatReleaseCount(_data, type) {
    if (type === "sort" || type === "filter" || type === "type") return _data ?? 0;
    return _data ?? 0;
  }
  function bindBrowseCountGrid(config) {
    const table = $(config.tableSelector).DataTable({
      ajax: {
        url: config.ajaxUrl,
        type: "GET",
        dataSrc: ""
      },
      columns: [
        { data: "name", render: formatNameHref(config), defaultContent: "" },
        { data: "releaseCount", type: "num", render: formatReleaseCount, searchable: false, className: "text-end" }
      ],
      order: [[0, "asc"]],
      pageLength: 25,
      language: {
        search: "",
        searchPlaceholder: config.searchPlaceholder,
        lengthMenu: "show _MENU_",
        info: "_START_\u2013_END_ of _TOTAL_",
        paginate: { previous: "\u2190", next: "\u2192" }
      },
      responsive: true
    });
    applySearchFromQueryString(table);
    bindClearFilterButton(table, config.clearButtonSelector);
  }

  // Scripts/genres.ts
  $(document).ready(function() {
    bindBrowseCountGrid({
      tableSelector: "#genresTable",
      ajaxUrl: "/Genres/GetGenres",
      clearButtonSelector: "#clearGenresFilterBtn",
      searchPlaceholder: "search genres\u2026",
      nameHref: (row) => `/collection/genre/${row.id}`
    });
  });
})();
