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

  // Scripts/tracks.ts
  function normalizePositionForSort(rawPosition) {
    const position = (rawPosition ?? "").trim().toUpperCase();
    const sideAndNumberMatch = /^([A-Z]+)\s*([0-9]+)$/.exec(position);
    if (!sideAndNumberMatch) return position;
    const side = sideAndNumberMatch[1];
    const trackNumber = Number.parseInt(sideAndNumberMatch[2], 10);
    const paddedTrackNumber = Number.isFinite(trackNumber) ? String(trackNumber).padStart(4, "0") : sideAndNumberMatch[2];
    return `${side}${paddedTrackNumber}`;
  }
  function formatPosition(data, type) {
    const position = (data ?? "").trim();
    if (type === "sort" || type === "type") return normalizePositionForSort(position);
    if (type === "filter") return "";
    if (!position) return "\u2014";
    return escapeHtml(position);
  }
  function formatTitle(data, type) {
    if (type === "sort" || type === "filter" || type === "type") return data;
    const title = (data ?? "").trim();
    if (!title) return "\u2014";
    const searchUrl = `/Tracks?q=${encodeURIComponent(title)}`;
    return `<a href="${escapeAttr(searchUrl)}">${escapeHtml(title)}</a>`;
  }
  function formatDuration(data, type) {
    const duration = (data ?? "").trim();
    if (type === "sort" || type === "filter" || type === "type") return duration;
    if (!duration) return "\u2014";
    const searchUrl = `/Tracks?q=${encodeURIComponent(duration)}`;
    return `<a href="${escapeAttr(searchUrl)}">${escapeHtml(duration)}</a>`;
  }
  function formatArtist(data, type) {
    if (type === "sort" || type === "filter" || type === "type") return data;
    const artist = (data ?? "").trim();
    if (!artist) return "\u2014";
    const searchUrl = `/Tracks?q=${encodeURIComponent(artist)}`;
    return `<a href="${escapeAttr(searchUrl)}">${escapeHtml(artist)}</a>`;
  }
  function expandedRowHtml(track) {
    const album = escapeHtml(track.album ?? "\u2014");
    const year = track.year == null ? "\u2014" : escapeHtml(String(track.year));
    const releaseUrl = `/release/${track.releaseId}`;
    return `
        <div class="child-content">
            <div>
                <div class="child-label">Album</div>
                <div class="child-value"><a href="${escapeAttr(releaseUrl)}">${album}</a></div>
            </div>
            <div class="ms-4">
                <div class="child-label">Year</div>
                <div class="child-value">${year}</div>
            </div>
        </div>`;
  }
  $(document).ready(function() {
    const tracksTable = $("#tracksTable").DataTable({
      ajax: {
        url: "/Tracks/GetTracks",
        type: "GET",
        dataSrc: ""
      },
      columns: [
        { className: "dt-control", orderable: false, data: null, defaultContent: "" },
        { data: "position", render: formatPosition, searchable: false },
        { data: "title", render: formatTitle },
        { data: "duration", render: formatDuration },
        { data: "artistDisplay", render: formatArtist }
      ],
      order: [[2, "asc"]],
      pageLength: 25,
      language: {
        search: "",
        searchPlaceholder: "search tracks\u2026",
        lengthMenu: "show _MENU_",
        info: "_START_\u2013_END_ of _TOTAL_",
        paginate: { previous: "\u2190", next: "\u2192" }
      },
      responsive: true
    });
    applySearchFromQueryString(tracksTable);
    bindClearFilterButton(tracksTable, "#clearTracksFilterBtn");
    $("#tracksTable tbody").on("click", "td.dt-control", function() {
      const clickedRow = $(this).closest("tr");
      const dataRow = tracksTable.row(clickedRow);
      if (dataRow.child.isShown()) {
        dataRow.child.hide();
        clickedRow.removeClass("dt-hasChild");
      } else {
        dataRow.child(expandedRowHtml(dataRow.data())).show();
        clickedRow.addClass("dt-hasChild");
        clickedRow.next().addClass("child-row");
      }
    });
  });
})();
