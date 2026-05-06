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

  // Scripts/collection.ts
  function formatDate(iso) {
    if (!iso) return "\u2014";
    return new Date(iso).toLocaleDateString("en-NZ", {
      day: "numeric",
      month: "short",
      year: "numeric"
    });
  }
  function formatArtistLinks(data, type, row) {
    if (type === "sort" || type === "filter" || type === "type") return data;
    const artists = row.artists;
    if (!artists || artists.length === 0) return "\u2014";
    return artists.map((artist) => `<a href="/artist/${artist.id}">${escapeHtml(artist.name)}</a>`).join(", ");
  }
  function formatAlbumLink(data, type, row) {
    if (type === "sort" || type === "filter" || type === "type") return data;
    return `<a href="/release/${row.releaseId}">${escapeHtml(data)}</a>`;
  }
  function formatYearLink(data, type) {
    if (type === "sort" || type === "filter" || type === "type") return data == null ? "" : String(data);
    if (data == null) return "\u2014";
    const year = String(data);
    return `<a href="/Collection?q=${encodeURIComponent(year)}">${escapeHtml(year)}</a>`;
  }
  function formatCoverThumb(_data, type, row) {
    if (type === "sort" || type === "filter" || type === "type") return "";
    const coverUrl = row.coverUrl;
    if (coverUrl && String(coverUrl).trim()) {
      const safeCoverUrl = escapeAttr(String(coverUrl));
      return `<a href="/release/${row.releaseId}" class="collection-cover-link" tabindex="-1" aria-hidden="true"><img class="collection-cover-thumb" src="${safeCoverUrl}" alt="" loading="lazy" width="40" height="40" /></a>`;
    }
    return '<div class="collection-cover-placeholder" aria-hidden="true"></div>';
  }
  function expandedRowHtml(item) {
    const releaseUrl = `https://www.discogs.com/release/${item.releaseId}`;
    const discogsLinks = [
      `<a href="${escapeAttr(releaseUrl)}" target="_blank" rel="noopener noreferrer">Release</a>`
    ];
    if (item.discogsMasterId != null) {
      const masterUrl = `https://www.discogs.com/master/${item.discogsMasterId}`;
      discogsLinks.push(`<a href="${escapeAttr(masterUrl)}" target="_blank" rel="noopener noreferrer">Master</a>`);
    }
    return `
        <div class="child-content">
            <div>
                <div class="child-label">Date added</div>
                <div class="child-value">${formatDate(item.dateAdded)}</div>
            </div>
            <div>
                <div class="child-label">Discogs</div>
                <div class="child-value">${discogsLinks.join(' <span class="text-secondary">\xB7</span> ')}</div>
            </div>
            <div class="ms-auto">
                <button class="btn btn-primary btn-sm scrobble-btn"
                        data-release-id="${item.releaseId}"
                        data-artist="${escapeAttr(item.artistDisplay)}"
                        data-album="${escapeAttr(item.album)}">
                    Scrobble
                </button>
            </div>
        </div>`;
  }
  $(document).ready(function() {
    const collectionTable = $("#collectionTable").DataTable({
      ajax: {
        url: "/Collection/GetCollection",
        type: "GET",
        dataSrc: ""
      },
      columns: [
        { className: "dt-control", orderable: false, data: null, defaultContent: "" },
        {
          className: "collection-cover-cell",
          orderable: false,
          searchable: false,
          data: "coverUrl",
          render: formatCoverThumb
        },
        { data: "artistDisplay", render: formatArtistLinks },
        { data: "album", render: formatAlbumLink },
        { data: "year", render: formatYearLink }
      ],
      order: [[2, "asc"]],
      pageLength: 25,
      language: {
        search: "",
        searchPlaceholder: "search collection\u2026",
        lengthMenu: "show _MENU_",
        info: "_START_\u2013_END_ of _TOTAL_",
        paginate: { previous: "\u2190", next: "\u2192" }
      },
      responsive: true
    });
    applySearchFromQueryString(collectionTable);
    bindClearFilterButton(collectionTable, "#clearCollectionFilterBtn");
    $("#collectionTable tbody").on("click", "td.dt-control", function() {
      const clickedRow = $(this).closest("tr");
      const dataRow = collectionTable.row(clickedRow);
      if (dataRow.child.isShown()) {
        dataRow.child.hide();
        clickedRow.removeClass("dt-hasChild");
      } else {
        dataRow.child(expandedRowHtml(dataRow.data())).show();
        clickedRow.addClass("dt-hasChild");
        clickedRow.next().addClass("child-row");
      }
    });
    $("#collectionTable tbody").on("click", ".scrobble-btn", function() {
      const scrobbleButton = $(this);
      const releaseId = scrobbleButton.data("release-id");
      scrobbleButton.text("Scrobbling\u2026").prop("disabled", true);
      $.post("/Collection/Scrobble", { releaseId }).done(() => {
        scrobbleButton.text("\u2713 Scrobbled").addClass("btn-outline-secondary").removeClass("btn-primary");
      }).fail(() => {
        scrobbleButton.text("Failed").addClass("btn-outline-secondary").removeClass("btn-primary").prop("disabled", false);
      });
    });
  });
})();
